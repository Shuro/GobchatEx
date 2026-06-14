/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
 * Copyright (C) 2026 Shuro
 *
 * This program is free software: you can redistribute it and/or modify it under
 * the terms of the GNU Affero General Public License as published by the Free
 * Software Foundation, version 3.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 *******************************************************************************/

using Gobchat.UI.Forms.Extension;
using Gobchat.UI.Forms.Helper;
using Gobchat.UI.Web;
using Microsoft.Web.WebView2.Core;
using NLog;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gobchat.UI.Forms
{
    /// <summary>
    /// The transparent, topmost, click-through overlay that hosts the chat UI.
    ///
    /// It hosts WebView2 in <b>composition hosting</b> mode (no child HWND) on a
    /// <c>WS_EX_NOREDIRECTIONBITMAP</c> window with a minimal DirectComposition tree, which is
    /// what reproduces the old CEF/OSR look: per-pixel-alpha chat floating over the game.
    /// Composition hosting delivers no input automatically, so the form forwards Win32 mouse
    /// messages to the WebView2 while interactive.
    ///
    /// Click-through is a whole-window <b>lock/unlock</b> toggle (see <see cref="SetClickThrough"/>):
    /// interactive (catches mouse, Ctrl-drag to move/resize) vs. passive
    /// (<c>WS_EX_TRANSPARENT | WS_EX_LAYERED</c> + opaque layered alpha, clicks fall through to
    /// the game). WebView2 has no per-pixel hit-testing, so this replaces the layered window's
    /// automatic alpha passthrough the CEF build relied on.
    /// </summary>
    public partial class OverlayForm : Form
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

        private const int WM_NCHITTEST = 0x0084;
        private const int HTCLIENT = 1;
        private const int HTCAPTION = 2;

        private readonly FormResizeHelper _formResizer;
        private readonly FormEnsureTopmostHelper _formEnsureTopmost;
        private readonly Web.JavascriptAndJsonBuilder _jsBuilder = new Web.JavascriptAndJsonBuilder();

        private CoreWebView2CompositionController _compositionController;

        // Held so the COM objects stay alive for the window's lifetime.
        private IDCompositionDevice _dcompDevice;
        private IDCompositionTarget _dcompTarget;
        private IDCompositionVisual _rootVisual;

        private bool _initStarted;
        private bool _clickThrough;
        private bool _ctrlActive;
        private bool _trackingMouse;

        public IManagedWebBrowser Browser { get; private set; }

        /// <summary>Whether the overlay is currently click-through (passive/locked).</summary>
        public bool IsClickThrough => _clickThrough;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                // NOREDIRECTIONBITMAP (at creation) is required for DirectComposition transparency.
                // LAYERED/TRANSPARENT are toggled at runtime for click-through, not set here.
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOREDIRECTIONBITMAP;
                return cp;
            }
        }

        public OverlayForm()
        {
            InitializeComponent();
            KeyPreview = true;

            _formResizer = new FormResizeHelper(this, 16);
            _formEnsureTopmost = new FormEnsureTopmostHelper(this, 1000);

            Browser = new ManagedWebBrowser();
            Browser.OnBrowserConsoleLog += (s, e) => logger.Info(() => $"Browser Console Log {e.Line} in {e.Source}\n=> {e.Message}");
            Browser.OnBrowserError += (s, e) => logger.Error(() => $"[{e.ErrorCode}] {e.ErrorText}");

            this.Resize += (sender, e) => Browser.Size = ClientSize;
            this.MinimumSize = new Size(200, 200);
        }

        protected override async void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (_initStarted)
                return;
            _initStarted = true;
            try
            {
                await InitializeAsync();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "WebView2 overlay initialization failed");
                MessageBox.Show(
                    "The chat overlay could not start its WebView2 browser.\n\n" + ex.Message,
                    "GobchatEx", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task InitializeAsync()
        {
            var environment = await WebViewManager.GetEnvironmentAsync().ConfigureAwait(true);
            _compositionController = await environment.CreateCoreWebView2CompositionControllerAsync(Handle).ConfigureAwait(true);

            // Transparent so the page's own alpha is preserved instead of a white fill.
            _compositionController.DefaultBackgroundColor = Color.Transparent;

            var hr = DComp.DCompositionCreateDevice(IntPtr.Zero, DComp.IID_IDCompositionDevice, out _dcompDevice);
            if (hr != 0)
                throw new COMException("DCompositionCreateDevice failed", hr);

            _dcompTarget = _dcompDevice.CreateTargetForHwnd(Handle, topmost: true);
            _rootVisual = _dcompDevice.CreateVisual();
            _dcompTarget.SetRoot(_rootVisual);
            _compositionController.RootVisualTarget = _rootVisual;
            _dcompDevice.Commit();

            _compositionController.Bounds = new Rectangle(Point.Empty, ClientSize);
            _compositionController.IsVisible = true;

            await ((ManagedWebBrowser)Browser).Attach(_compositionController).ConfigureAwait(true);
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            if (_compositionController != null)
                _compositionController.Bounds = new Rectangle(Point.Empty, ClientSize);
        }

        #region click-through (lock/unlock)

        public void ToggleClickThrough()
        {
            SetClickThrough(!_clickThrough);
        }

        /// <summary>
        /// Switches between interactive and passive (click-through). Passive needs
        /// <c>WS_EX_TRANSPARENT | WS_EX_LAYERED</c> plus an opaque layered alpha; transparent
        /// alone does not pass clicks through on a modern DWM desktop.
        /// </summary>
        public void SetClickThrough(bool enabled)
        {
            if (_clickThrough == enabled && IsHandleCreated)
                return;
            _clickThrough = enabled;
            if (!IsHandleCreated)
                return;

            var ex = NativeMethods.GetWindowLong(Handle, NativeMethods.GWL_EXSTYLE);
            ex &= ~(NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED);
            if (enabled)
                ex |= NativeMethods.WS_EX_TRANSPARENT | NativeMethods.WS_EX_LAYERED;
            NativeMethods.SetWindowLong(Handle, NativeMethods.GWL_EXSTYLE, ex);

            if (enabled)
                NativeMethods.SetLayeredWindowAttributes(Handle, 0, 255, NativeMethods.LWA_ALPHA);

            NativeMethods.SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOZORDER |
                NativeMethods.SWP_NOACTIVATE | NativeMethods.SWP_FRAMECHANGED);
        }

        #endregion click-through (lock/unlock)

        #region input

        protected override void WndProc(ref Message m)
        {
            // Passive/click-through: the window receives no input anyway, just pass through.
            if (!_clickThrough)
            {
                var ctrl = IsControlHeld();
                if (ctrl != _ctrlActive)
                {
                    _ctrlActive = ctrl;
                    DispatchOverlayState(isLocked: !ctrl);
                }

                // While Ctrl is held the whole window is a move/resize handle, so don't forward
                // mouse to the page; otherwise feed the (windowless) WebView2 its input.
                if (!ctrl && ForwardMouseMessage(ref m))
                    return;
            }

            base.WndProc(ref m);

            if (!_clickThrough && m.Msg == WM_NCHITTEST && IsControlHeld())
            {
                _formResizer.AllowToResize = true;
                if (_formResizer.ProcessFormMessage(ref m))
                    return; // on a resize border
                if (m.Result == (IntPtr)HTCLIENT)
                    m.Result = (IntPtr)HTCAPTION; // drag anywhere else
            }
        }

        // Translates Win32 mouse messages into CoreWebView2CompositionController.SendMouseInput
        // (validated in the Phase 0 spike). Returns true when the message was a mouse message and
        // has been consumed.
        private bool ForwardMouseMessage(ref Message m)
        {
            if (_compositionController == null)
                return false;

            CoreWebView2MouseEventKind kind;
            switch (m.Msg)
            {
                case 0x0200: kind = CoreWebView2MouseEventKind.Move; break;
                case 0x0201: kind = CoreWebView2MouseEventKind.LeftButtonDown; break;
                case 0x0202: kind = CoreWebView2MouseEventKind.LeftButtonUp; break;
                case 0x0203: kind = CoreWebView2MouseEventKind.LeftButtonDoubleClick; break;
                case 0x0204: kind = CoreWebView2MouseEventKind.RightButtonDown; break;
                case 0x0205: kind = CoreWebView2MouseEventKind.RightButtonUp; break;
                case 0x0206: kind = CoreWebView2MouseEventKind.RightButtonDoubleClick; break;
                case 0x0207: kind = CoreWebView2MouseEventKind.MiddleButtonDown; break;
                case 0x0208: kind = CoreWebView2MouseEventKind.MiddleButtonUp; break;
                case 0x0209: kind = CoreWebView2MouseEventKind.MiddleButtonDoubleClick; break;
                case 0x020A: kind = CoreWebView2MouseEventKind.Wheel; break;
                case 0x020E: kind = CoreWebView2MouseEventKind.HorizontalWheel; break;
                case 0x02A3: kind = CoreWebView2MouseEventKind.Leave; break;
                default: return false;
            }

            long w = m.WParam.ToInt64();
            long l = m.LParam.ToInt64();
            var keys = (CoreWebView2MouseEventVirtualKeys)((uint)w & 0x7F); // MK_* flags map 1:1
            uint mouseData = 0;
            Point pt;

            if (kind == CoreWebView2MouseEventKind.Wheel || kind == CoreWebView2MouseEventKind.HorizontalWheel)
            {
                mouseData = (uint)(short)((w >> 16) & 0xFFFF);                                  // wheel delta
                pt = PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF))); // screen -> client
            }
            else if (kind == CoreWebView2MouseEventKind.Leave)
            {
                _trackingMouse = false;
                pt = Point.Empty;
            }
            else
            {
                pt = new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)); // already client
                if (kind == CoreWebView2MouseEventKind.Move && !_trackingMouse)
                {
                    var tme = new NativeMethods.TRACKMOUSEEVENT
                    {
                        cbSize = Marshal.SizeOf<NativeMethods.TRACKMOUSEEVENT>(),
                        dwFlags = NativeMethods.TME_LEAVE,
                        hwndTrack = Handle,
                    };
                    NativeMethods.TrackMouseEvent(ref tme);
                    _trackingMouse = true;
                }
                if (kind == CoreWebView2MouseEventKind.LeftButtonDown ||
                    kind == CoreWebView2MouseEventKind.RightButtonDown ||
                    kind == CoreWebView2MouseEventKind.MiddleButtonDown)
                {
                    // Route keyboard to the page when the user clicks into it.
                    _compositionController.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
                }
            }

            _compositionController.SendMouseInput(kind, keys, mouseData, pt);
            return true;
        }

        private static bool IsControlHeld()
        {
            return (NativeMethods.GetKeyState((int)Keys.ControlKey) & NativeMethods.KEY_PRESSED) != 0;
        }

        private void DispatchOverlayState(bool isLocked)
        {
            try
            {
                var script = _jsBuilder.BuildCustomEventDispatcher(new Web.JavascriptEvents.OverlayStateUpdateEvent(isLocked));
                Browser.ExecuteScript(script);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to dispatch overlay state");
            }
        }

        #endregion input

        public void Reload()
        {
            Browser.Reload();
        }

        public void InvokeAsyncOnUI(Action<OverlayForm> action)
        {
            UIExtensions.InvokeAsyncOnUI(this, action);
        }

        public TOut InvokeSyncOnUI<TOut>(Func<OverlayForm, TOut> action)
        {
            return UIExtensions.InvokeSyncOnUI(this, action);
        }

        private void DisposeForm(bool disposing)
        {
            logger.Debug("Disposing overlay");

            _formEnsureTopmost?.Dispose();

            Browser?.Dispose();
            Browser = null;

            _compositionController = null;
            _rootVisual = null;
            _dcompTarget = null;
            _dcompDevice = null;
        }
    }
}
