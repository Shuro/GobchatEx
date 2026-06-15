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

using Gobchat.UI.Forms.Helper;
using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gobchat.UI.Forms
{
    /// <summary>
    /// The settings dialog host: a borderless, topmost, ctrl-draggable window that reads as a sleek
    /// floating panel (the config dialog fills it with an opaque background). Created from
    /// <see cref="ManagedWebBrowser"/>'s NewWindowRequested handler, so the page keeps its
    /// <c>window.opener</c> sharing (GobchatAPI, gobConfig, Gobchat) and the existing config
    /// TypeScript runs unchanged.
    ///
    /// It uses <b>windowed</b> WebView2 hosting (a real child HWND) on purpose: the config UI relies
    /// on native popups (the <c>&lt;select&gt;</c> profile/channel drop-downs), which composition
    /// hosting — like the chat overlay's — does not render correctly. Ctrl-drag to move is done with
    /// WebView2's non-client-region support (<see cref="CoreWebView2Settings.IsNonClientRegionSupportEnabled"/>):
    /// while Ctrl is held, config.html turns a full-window layer into an <c>app-region: drag</c>
    /// region, and WebView2 moves the host window natively (the host-side SendMessage drag trick is
    /// unreliable because the windowed WebView2 child owns the mouse).
    /// </summary>
    internal sealed class SettingsOverlayForm : Form
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private const int WS_EX_TOOLWINDOW = 0x00000080;

        private readonly CoreWebView2Environment _environment;
        private readonly Func<string, string> _resourceResolver;
        private readonly Action<Rectangle> _framePersister;
        private readonly FormEnsureTopmostHelper _formEnsureTopmost;
        private readonly Timer _persistTimer;

        private CoreWebView2Controller _controller;

        public CoreWebView2 CoreWebView2 { get; private set; }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOOLWINDOW; // keep it out of the taskbar / alt-tab like the overlay
                return cp;
            }
        }

        public SettingsOverlayForm(CoreWebView2Environment environment, Func<string, string> resourceResolver, Action<Rectangle> framePersister)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _resourceResolver = resourceResolver;
            _framePersister = framePersister;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = false;
            ShowIcon = false;
            TopMost = true;
            // Dark fill matching the dialog gradient, so there is no white flash before the page paints.
            BackColor = Color.FromArgb(0x31, 0x31, 0x31);
            Text = "GobchatEx";
            ClientSize = new Size(1040, 600);
            MinimumSize = new Size(480, 360);

            _formEnsureTopmost = new FormEnsureTopmostHelper(this, 1000);

            // Persist the window frame to config (via the App's callback) a short while after the user
            // stops ctrl-dragging, so the next open restores it — debounced like the chat overlay.
            // TODO: not working at runtime yet — the settings window position is still not
            // saved/restored; needs investigation (does LocationChanged fire for app-region drags? is
            // the frame written/read back correctly via window.open features?).
            _persistTimer = new Timer { Interval = 500 };
            _persistTimer.Tick += PersistTimer_Tick;
            LocationChanged += OnFrameChanged;
            SizeChanged += OnFrameChanged;
        }

        private void OnFrameChanged(object sender, EventArgs e)
        {
            // Ignore the initial layout (before the page is up); only persist real user moves.
            if (_framePersister == null || CoreWebView2 == null)
                return;
            _persistTimer.Stop();
            _persistTimer.Start();
        }

        private void PersistTimer_Tick(object sender, EventArgs e)
        {
            _persistTimer.Stop();
            try
            {
                _framePersister?.Invoke(new Rectangle(Location, ClientSize));
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to persist settings window frame");
            }
        }

        public void ApplyWindowFeatures(CoreWebView2WindowFeatures features)
        {
            if (features == null)
                return;
            if (features.HasSize && features.Width > 0 && features.Height > 0)
                ClientSize = new Size((int)features.Width, (int)features.Height);
            if (features.HasPosition)
            {
                var location = new Point((int)features.Left, (int)features.Top);
                if (IsOnAnyScreen(new Rectangle(location, Size)))
                {
                    StartPosition = FormStartPosition.Manual;
                    Location = location;
                }
            }
        }

        private static bool IsOnAnyScreen(Rectangle frame)
        {
            foreach (var screen in Screen.AllScreens)
                if (screen.WorkingArea.IntersectsWith(frame))
                    return true;
            return false;
        }

        public async Task InitializeAsync()
        {
            // Force the handle so the controller can bind to it.
            _ = Handle;

            _controller = await _environment.CreateCoreWebView2ControllerAsync(Handle).ConfigureAwait(true);
            CoreWebView2 = _controller.CoreWebView2;
            _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
            _controller.DefaultBackgroundColor = BackColor;

            var settings = CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
            // Lets config.html's ctrl-held "app-region: drag" layer move the window natively.
            settings.IsNonClientRegionSupportEnabled = true;
#if DEBUG
            settings.AreDevToolsEnabled = true;
#else
            settings.AreDevToolsEnabled = false;
#endif

            // Resources are served through WebResourceRequested + the shared resolver (no virtual-host
            // folder mapping; see ManagedWebBrowser.Attach). config.html uses window.opener for
            // GobchatAPI/console, so no bridge is registered here.
            CoreWebView2.AddWebResourceRequestedFilter("https://" + ManagedWebBrowser.VirtualHost + "/*", CoreWebView2WebResourceContext.All);
            CoreWebView2.WebResourceRequested += OnWebResourceRequested;
            CoreWebView2.WindowCloseRequested += OnWindowCloseRequested;

            this.Resize += OnFormResize;
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            if (_controller != null)
                _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            ManagedWebBrowser.ServeResource(_environment, _resourceResolver, e);
        }

        private void OnWindowCloseRequested(object sender, object e)
        {
            // Raised by window.close() in the page (the dialog's save/exit/cancel buttons).
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke((Action)Close);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _persistTimer?.Stop();
                    _persistTimer?.Dispose();
                    _formEnsureTopmost?.Dispose();
                    if (CoreWebView2 != null)
                    {
                        CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                        CoreWebView2.WindowCloseRequested -= OnWindowCloseRequested;
                    }
                    _controller?.Close();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Error while disposing settings overlay");
                }
                _controller = null;
                CoreWebView2 = null;
            }
            base.Dispose(disposing);
        }
    }
}
