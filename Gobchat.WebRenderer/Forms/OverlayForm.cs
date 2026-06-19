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
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_CAPTURECHANGED = 0x0215;
        private const int HTCLIENT = 1;

        // While unlocked, this thin border (px) of each edge hit-tests as a resize zone; the bottom-right
        // corner uses the larger size so it is easy to grab for a two-axis resize.
        private const int ResizeEdge = 6;
        private const int ResizeCorner = 16;

        // Cap the live resize (~45 fps). Each step is an async cross-process WebView2 re-raster; firing
        // one per mouse move floods that pipeline (worst when enlarging, which reallocates the surface),
        // so the visible size backlogs behind the cursor and then snaps. Coalescing to the latest cursor
        // size at a steady rate lets the browser keep up; EndResize applies the exact final size. Lower
        // this toward 16 (~60 fps) for smoother tracking, or raise it if a hard fast pull ever backlogs.
        private const int ResizeThrottleMs = 22;

        private readonly FormEnsureTopmostHelper _formEnsureTopmost;
        private readonly Web.JavascriptBuilder _jsBuilder = new Web.JavascriptBuilder();

        private CoreWebView2CompositionController _compositionController;

        // The most recent handle-constructed Cursor we assigned. WinForms never frees the HCURSOR
        // copy such a Cursor owns, so we keep a reference and dispose it when it is replaced.
        private Cursor _ownedCursor;

        // Held so the COM objects stay alive for the window's lifetime.
        private IDCompositionDevice _dcompDevice;
        private IDCompositionTarget _dcompTarget;
        private IDCompositionVisual _rootVisual;

        private bool _initStarted;
        private bool _clickThrough;
        private bool _unlocked;
        private bool _trackingMouse;

        // Custom (non-modal) resize state; see WndProc. We deliberately do NOT use the OS resize loop
        // (SendMessage WM_NCLBUTTONDOWN), because that blocks the message pump and freezes the WebView2
        // composition until release. Tracking it ourselves keeps the content live during the drag.
        private bool _resizing;
        private int _resizeEdge;
        private Point _resizeStartCursor;
        private Rectangle _resizeStartBounds;
        private int _lastResizeApplyTick;

        public IManagedWebBrowser Browser { get; private set; }

        /// <summary>Raised (with the new locked state) whenever the overlay is pinned/unpinned via the pin.</summary>
        public event EventHandler<bool> LockStateChanged;

        /// <summary>Whether the overlay is currently click-through (passive/locked).</summary>
        public bool IsClickThrough => _clickThrough;

        /// <summary>Whether the overlay is unlocked for moving/resizing (driven by the toolbar pin).</summary>
        public bool IsUnlocked => _unlocked;

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

            _formEnsureTopmost = new FormEnsureTopmostHelper(this, 1000);

            Browser = new ManagedWebBrowser();
            Browser.OnBrowserConsoleLog += (s, e) => logger.Info(() => $"Browser Console Log {e.Line} in {e.Source}\n=> {e.Message}");
            Browser.OnBrowserError += (s, e) => logger.Error(() => $"[{e.ErrorCode}] {e.ErrorText}");

            // Resize is centralized in OnClientSizeChanged (which also commits the DComp device so the
            // new size is presented); no separate Resize→Browser.Size path, to avoid an uncommitted write.
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

            // Composition hosting makes the HOST window own the mouse cursor: WebView2 only computes the
            // cursor the page wants (CSS pointer/text/grab/…) and raises CursorChanged. Without this the
            // form keeps its default arrow and every page cursor is dead. (Resize-edge cursors still come
            // from the OS via WM_NCHITTEST, since those points hit-test as non-client.)
            _compositionController.CursorChanged += OnCompositionCursorChanged;

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

            // Attach re-applies the browser wrapper's own initial size, so set the real client size once
            // more and commit to present it. (Resize is otherwise centralized in OnClientSizeChanged.)
            _compositionController.Bounds = new Rectangle(Point.Empty, ClientSize);
            _dcompDevice.Commit();
        }

        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            if (_compositionController != null)
            {
                _compositionController.Bounds = new Rectangle(Point.Empty, ClientSize);
                // WebView2 renders into our DComp root visual, so a bounds change isn't presented until
                // our device commits. Without this the surface only catches up on the next incidental
                // commit — the "resize, freeze, then jump" lag. Commit now so the content tracks live.
                _dcompDevice?.Commit();
            }
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

        #region lock / move (toolbar pin)

        /// <summary>
        /// Unlocked = the overlay can be moved (toolbar/grip drag, see <see cref="BeginWindowDrag"/>)
        /// and resized (edge hit zones in <see cref="WndProc"/>); the page shows its accent ring +
        /// grip + resize ticks. Locked = frozen but still interactive (toolbar stays clickable). This
        /// is the toolbar pin's job; it is separate from <see cref="SetClickThrough"/> (full passive).
        /// </summary>
        public void SetUnlocked(bool unlocked)
        {
            _unlocked = unlocked;

            // You can't grab the window while clicks fall through to the game, so unlocking forces the
            // overlay interactive.
            if (unlocked && _clickThrough)
                SetClickThrough(false);

            DispatchOverlayState(isLocked: !_unlocked);
            LockStateChanged?.Invoke(this, !_unlocked);
        }

        public void ToggleLock()
        {
            SetUnlocked(!_unlocked);
        }

        /// <summary>
        /// Hands an in-progress drag off to the OS move loop. Called by the page on mousedown over the
        /// toolbar background/grip (not the action icons), so the buttons stay clickable.
        /// </summary>
        public void BeginWindowDrag()
        {
            if (!_unlocked || !IsHandleCreated)
                return;
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessage(Handle, NativeMethods.WM_NCLBUTTONDOWN, (IntPtr)NativeMethods.HTCAPTION, IntPtr.Zero);
        }

        // Which resize edge (HT* code) the given client point falls in while unlocked, or 0 for none.
        // The bottom-right corner wins (two-axis) so it stays a reliable grab target.
        private int ResizeHitTest(Point client)
        {
            int w = ClientSize.Width;
            int h = ClientSize.Height;

            if (client.X >= w - ResizeCorner && client.Y >= h - ResizeCorner)
                return NativeMethods.HTBOTTOMRIGHT;
            if (client.Y < ResizeEdge)
                return NativeMethods.HTTOP;
            if (client.Y >= h - ResizeEdge)
                return NativeMethods.HTBOTTOM;
            if (client.X < ResizeEdge)
                return NativeMethods.HTLEFT;
            if (client.X >= w - ResizeEdge)
                return NativeMethods.HTRIGHT;
            return 0;
        }

        private void StartResize(int edge)
        {
            _resizing = true;
            _resizeEdge = edge;
            _resizeStartCursor = Cursor.Position;
            _resizeStartBounds = Bounds;
            Capture = true;
        }

        // Resizes by setting Bounds directly per mouse move (no modal loop), so the WebView2 stays live.
        private void PerformResize()
        {
            var cursor = Cursor.Position;
            int dx = cursor.X - _resizeStartCursor.X;
            int dy = cursor.Y - _resizeStartCursor.Y;

            var b = _resizeStartBounds;
            int left = b.Left, top = b.Top, right = b.Right, bottom = b.Bottom;

            if (_resizeEdge == NativeMethods.HTLEFT)
                left = b.Left + dx;
            else if (_resizeEdge == NativeMethods.HTRIGHT || _resizeEdge == NativeMethods.HTBOTTOMRIGHT)
                right = b.Right + dx;

            if (_resizeEdge == NativeMethods.HTTOP)
                top = b.Top + dy;
            else if (_resizeEdge == NativeMethods.HTBOTTOM || _resizeEdge == NativeMethods.HTBOTTOMRIGHT)
                bottom = b.Bottom + dy;

            int minW = MinimumSize.Width, minH = MinimumSize.Height;
            if (right - left < minW)
            {
                if (_resizeEdge == NativeMethods.HTLEFT) left = right - minW; else right = left + minW;
            }
            if (bottom - top < minH)
            {
                if (_resizeEdge == NativeMethods.HTTOP) top = bottom - minH; else bottom = top + minH;
            }

            Bounds = new Rectangle(left, top, right - left, bottom - top);
        }

        private void EndResize()
        {
            // The last throttled move was likely skipped; apply the exact final cursor size before stopping.
            PerformResize();
            _resizing = false;
            Capture = false;
        }

        #endregion lock / move (toolbar pin)

        #region input

        protected override void WndProc(ref Message m)
        {
            // WndProc is invoked by the OS (a native->managed callback). A managed exception escaping
            // here can't unwind through the native frame, so the CLR fail-fasts the whole process with
            // STATUS_FATAL_USER_CALLBACK_EXCEPTION (0xC000041D) - observed when the overlay is dragged
            // mostly off-screen. Guard every custom handler so a transient fault degrades to default
            // message handling instead of killing the app.
            try
            {
                // Custom (non-modal) resize: drive Bounds directly off the mouse so the WebView2 keeps
                // re-compositing live (the OS resize loop would block the pump and freeze the content).
                if (_resizing)
                {
                    if (m.Msg == WM_MOUSEMOVE)
                    {
                        // Throttle to ~30 fps; each apply uses the live cursor, so skipped moves just coalesce.
                        int now = Environment.TickCount;
                        if (now - _lastResizeApplyTick >= ResizeThrottleMs)
                        {
                            _lastResizeApplyTick = now;
                            PerformResize();
                        }
                        return;
                    }
                    if (m.Msg == WM_LBUTTONUP) { EndResize(); return; }
                    if (m.Msg == WM_CAPTURECHANGED) { EndResize(); } // capture lost (Alt-Tab etc.) → stop
                }

                // A press on an (unlocked) edge/corner starts the resize. The OS routed it to WM_NCLBUTTONDOWN
                // because ResizeHitTest marked that pixel non-client below.
                if (!_clickThrough && _unlocked && !_resizing
                    && m.Msg == NativeMethods.WM_NCLBUTTONDOWN && IsResizeCode((int)(long)m.WParam))
                {
                    StartResize((int)(long)m.WParam);
                    return;
                }

                // Interactive: feed the (windowless) WebView2 its input so the page (buttons, tabs, and the
                // toolbar drag handler) works. Passive/click-through receives no input anyway.
                if (!_clickThrough && !_resizing && ForwardMouseMessage(ref m))
                    return;
            }
            catch (Exception ex)
            {
                // Fall through to base.WndProc so the window keeps responding.
                logger.Warn(ex, "Overlay WndProc pre-dispatch handler failed");
            }

            base.WndProc(ref m);

            try
            {
                // While unlocked, mark the thin edge/corner zones as resize areas, so the OS shows the
                // resize cursor on hover and sends the press to WM_NCLBUTTONDOWN above. Moving stays
                // page-driven (the toolbar/grip), so the rest of the client area is left clickable.
                if (!_clickThrough && _unlocked && m.Msg == WM_NCHITTEST && m.Result == (IntPtr)HTCLIENT)
                {
                    var code = ResizeHitTest(PointToClient(Cursor.Position));
                    if (code != 0)
                        m.Result = (IntPtr)code;
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Overlay WndProc post-dispatch hit-test failed");
            }
        }

        private static bool IsResizeCode(int code)
        {
            return code == NativeMethods.HTLEFT || code == NativeMethods.HTRIGHT
                || code == NativeMethods.HTTOP || code == NativeMethods.HTBOTTOM
                || code == NativeMethods.HTBOTTOMRIGHT;
        }

        // Forwards Win32 mouse messages to the (windowless) WebView2; see CompositionMouseInput.
        private bool ForwardMouseMessage(ref Message m)
        {
            return CompositionMouseInput.ForwardMouseMessage(_compositionController, this, ref m, ref _trackingMouse);
        }

        // Applies the cursor the page requested (via WebView2's HCURSOR) to the host form. WinForms only
        // honours Cursor for client-area WM_SETCURSOR, so the resize edges (non-client hit codes) keep
        // their OS cursors. A zero handle (briefly possible) falls back to the default arrow.
        private void OnCompositionCursorChanged(object sender, object e)
        {
            try
            {
                var handle = _compositionController?.Cursor ?? IntPtr.Zero;
                // Assign the new cursor first, then dispose the one it replaced — never dispose the
                // shared Cursors.Default. A handle-built Cursor owns a copied HCURSOR that WinForms
                // won't free, so without this every page cursor change leaks a GDI handle.
                var previous = _ownedCursor;
                if (handle == IntPtr.Zero)
                {
                    Cursor = Cursors.Default;
                    _ownedCursor = null;
                }
                else
                {
                    var cursor = new Cursor(handle);
                    Cursor = cursor;
                    _ownedCursor = cursor;
                }
                previous?.Dispose();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to apply the WebView2 cursor");
            }
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

            if (_compositionController != null)
                _compositionController.CursorChanged -= OnCompositionCursorChanged;
            _compositionController = null;

            // Active COM calls and managed Cursor disposal only on deterministic Dispose (UI thread).
            // On the finalizer path (disposing == false) the RCWs may be on the wrong apartment or
            // already finalized, so we just drop the references and let their own finalizers release.
            if (disposing)
            {
                _ownedCursor?.Dispose();

                // Tear the composition tree down explicitly (mirrors the setup in InitializeAsync) and
                // release the RCWs now, on the UI thread, child -> parent. Otherwise the native objects
                // are only freed when the finalizer runs, possibly after the HWND is already destroyed.
                try
                {
                    _dcompTarget?.SetRoot(null);
                    _dcompDevice?.Commit();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to detach the DirectComposition root visual");
                }

                if (_rootVisual != null)
                    Marshal.FinalReleaseComObject(_rootVisual);
                if (_dcompTarget != null)
                    Marshal.FinalReleaseComObject(_dcompTarget);
                if (_dcompDevice != null)
                    Marshal.FinalReleaseComObject(_dcompDevice);
            }

            _ownedCursor = null;
            _rootVisual = null;
            _dcompTarget = null;
            _dcompDevice = null;
        }
    }
}
