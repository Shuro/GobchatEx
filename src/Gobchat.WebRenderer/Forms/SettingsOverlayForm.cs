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
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gobchat.UI.Forms
{
    /// <summary>
    /// The settings dialog host: a borderless, ctrl-draggable window (topmost only while active, see
    /// <see cref="OnActivated"/>) that reads as a sleek floating panel (the config dialog fills it with
    /// an opaque background). Created from
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

        // DWM window attributes (Windows 11+). On Windows 10 the call returns a non-zero HRESULT and
        // is simply ignored — the window stays square with no border, no crash.
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWCP_ROUND = 2;
        // COLORREF (0x00BBGGRR) for the design --border grey (#2c303a), matching the in-page dividers.
        private const int SettingsBorderColor = 0x003A302C;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        // Windows-managed maximize (Win+Up / Aero Snap to the top edge / the taskbar's right-click menu)
        // on a borderless window defaults to covering the whole monitor — taskbar included — because
        // there's no caption to make Windows honour the work area. WM_GETMINMAXINFO lets us cap the
        // maximized size/position to the working area instead, so it behaves like a normal window.
        private const int WM_GETMINMAXINFO = 0x0024;

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public Point ptReserved;
            public Point ptMaxSize;
            public Point ptMaxPosition;
            public Point ptMinTrackSize;
            public Point ptMaxTrackSize;
        }

        // How long after the page starts loading the window reveals itself even if the page never
        // signalled ready (a JS error before revealSettings) — so it can't stay invisible and locked.
        private const int RevealWatchdogMs = 4000;

        private readonly CoreWebView2Environment _environment;
        private readonly Func<string, string?>? _resourceResolver;
        private readonly Action<Rectangle>? _framePersister;
        private readonly Func<Rectangle?>? _frameProvider;
        private readonly Timer _persistTimer;
        private readonly Timer _revealWatchdog;

        private CoreWebView2Controller? _controller;
        private bool _revealed;

        public CoreWebView2? CoreWebView2 { get; private set; }

        public SettingsOverlayForm(CoreWebView2Environment environment, Func<string, string?>? resourceResolver, Action<Rectangle>? framePersister, Func<Rectangle?>? frameProvider)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _resourceResolver = resourceResolver;
            _framePersister = framePersister;
            _frameProvider = frameProvider;

            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            // A normal taskbar window (so the title-bar minimize has a restore affordance) that does not
            // float above everything; it is only raised topmost while active (see OnActivated/OnDeactivate)
            // so it sits above the WS_EX_TOPMOST chat overlay without permanently covering other apps.
            ShowInTaskbar = true;
            ShowIcon = false;
            // ShowIcon=false only hides the (absent) caption icon; the taskbar button still uses Form.Icon
            // and would otherwise fall back to the generic WinForms icon. Use the exe's own embedded icon
            // (GobIcon) so the taskbar matches the app icon.
            try { Icon = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath); }
            catch (Exception) { /* keep default icon if extraction fails */ }
            TopMost = false;
            // Dark fill matching the dialog gradient, so there is no white flash before the page paints.
            BackColor = Color.FromArgb(0x31, 0x31, 0x31);
            Text = "GobchatEx";
            ClientSize = new Size(1200, 880);
            MinimumSize = new Size(480, 360);

            // Persist the window frame app-globally (via the App's callback) a short while after the user
            // stops moving/resizing, so the next open restores it — debounced like the chat overlay. The
            // authoritative save is on close (OnFormClosing); this only captures mid-session changes.
            _persistTimer = new Timer { Interval = 500 };
            _persistTimer.Tick += PersistTimer_Tick;
            LocationChanged += OnFrameChanged;
            SizeChanged += OnFrameChanged;

            // The window starts hidden (never Show()n at construction) and is revealed only once the
            // config page has finished building/theming (it calls GobchatAPI.revealSettings), so the
            // user never sees an empty dark frame. This watchdog is the safety net: if that signal
            // never arrives, reveal anyway after RevealWatchdogMs and log it.
            _revealWatchdog = new Timer { Interval = RevealWatchdogMs };
            _revealWatchdog.Tick += RevealWatchdog_Tick;
        }

        private void OnFrameChanged(object? sender, EventArgs e)
        {
            // Ignore the initial layout (before the page is up); only persist real user moves.
            if (_framePersister == null || CoreWebView2 == null)
                return;
            _persistTimer.Stop();
            _persistTimer.Start();
        }

        private void PersistTimer_Tick(object? sender, EventArgs e)
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

        // Title-bar window controls, driven from config.ts through the overlay bridge (the settings
        // page shares window.opener's GobchatAPI). The window is a normal taskbar window, so a minimize
        // has a restore affordance.
        public void MinimizeToTaskbar()
        {
            WindowState = FormWindowState.Minimized;
        }

        // GobchatEx's chat overlay is WS_EX_TOPMOST, so a normal (non-topmost) settings window is always
        // partially covered by it wherever they overlap. Raise the settings window topmost while it is the
        // active window so it sits above the overlay the user is configuring; drop back when it loses focus
        // so it can still go behind other applications, preserving the normal-window design.
        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (!TopMost)
                TopMost = true;
        }

        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (TopMost)
                TopMost = false;
        }

        // Reveal-when-ready: shows + activates the window the first time it's called (idempotent).
        // Driven by the page's revealSettings bridge call once the config UI has rendered, with the
        // watchdog timer (and a failed-navigation fallback) as safety nets.
        public void RevealNow()
        {
            _revealWatchdog?.Stop();
            if (_revealed || IsDisposed)
                return;
            _revealed = true;
            if (!Visible)
                Show();
            // The WebView2 controller was created while the window was hidden (IsVisible=false), so it
            // isn't compositing onto the now-shown HWND — a grey box. Turn it on and reapply Bounds to
            // force it to paint the already-built page.
            if (_controller != null)
            {
                _controller.IsVisible = true;
                _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
            }
            Activate();
        }

        // Second cog click: bring the already-open settings window to the foreground — reveal it if
        // still hidden, restore it if minimized, then activate + raise.
        public void FocusNow()
        {
            RevealNow();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void RevealWatchdog_Tick(object? sender, EventArgs e)
        {
            if (_revealed)
                return;
            logger.Warn("Settings window reveal watchdog fired — the page never signalled ready; revealing anyway");
            RevealNow();
        }

        private static bool IsOnAnyScreen(Rectangle frame)
        {
            foreach (var screen in Screen.AllScreens)
                if (screen.WorkingArea.IntersectsWith(frame))
                    return true;
            return false;
        }

        // Restore the last app-global placement. The frame is clamped to the working area of the screen
        // it mostly sits on, so a placement saved on a now-disconnected or rearranged monitor can never
        // open off-screen. With no saved state the constructor's centered default stands.
        private void ApplySavedPlacement()
        {
            try
            {
                var saved = _frameProvider?.Invoke();
                if (saved == null)
                    return;
                var frame = ClampToScreen(saved.Value);
                StartPosition = FormStartPosition.Manual;
                if (frame.Width > 0 && frame.Height > 0)
                    ClientSize = new Size(frame.Width, frame.Height);
                Location = frame.Location;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to restore settings window placement");
            }
        }

        // Fit a frame inside the working area of the screen it overlaps most (Screen.FromRectangle picks
        // the largest-overlap or nearest screen): cap the size to that screen, then nudge the position so
        // the whole window is visible.
        private static Rectangle ClampToScreen(Rectangle frame)
        {
            var area = Screen.FromRectangle(frame).WorkingArea;
            var width = Math.Min(frame.Width > 0 ? frame.Width : area.Width, area.Width);
            var height = Math.Min(frame.Height > 0 ? frame.Height : area.Height, area.Height);
            var x = Math.Max(area.Left, Math.Min(frame.X, area.Right - width));
            var y = Math.Max(area.Top, Math.Min(frame.Y, area.Bottom - height));
            return new Rectangle(x, y, width, height);
        }

        // Save the current frame app-globally. Uses RestoreBounds when minimized/maximized so we record
        // the normal-state frame, not the off-screen/maximized one.
        private void PersistCurrentFrame()
        {
            try
            {
                var frame = WindowState == FormWindowState.Normal
                    ? new Rectangle(Location, ClientSize)
                    : RestoreBounds;
                if (frame.Width > 0 && frame.Height > 0)
                    _framePersister?.Invoke(frame);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to persist settings window frame on close");
            }
        }

        // Round the borderless window's corners and give it a subtle grey outline so it reads as a
        // floating panel instead of a hard rectangle. Windows 11 only (silently ignored on Win10).
        private void ApplyRoundedChrome()
        {
            try
            {
                int corner = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
                int border = SettingsBorderColor;
                DwmSetWindowAttribute(Handle, DWMWA_BORDER_COLOR, ref border, sizeof(int));
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to apply rounded window chrome");
            }
        }

        public async Task InitializeAsync()
        {
            // Force the handle so the controller can bind to it.
            _ = Handle;
            ApplyRoundedChrome();

            // Restore the last app-global placement (clamped on-screen) before the controller binds, so
            // the window opens where the user left it regardless of which profile is active.
            ApplySavedPlacement();

            // Arm the reveal safety net before anything that can throw, so even a failure during WebView2
            // setup can't leave the (hidden) window invisible and locked — it reveals after the watchdog.
            _revealWatchdog.Start();

            _controller = await _environment.CreateCoreWebView2ControllerAsync(Handle).ConfigureAwait(true);
            CoreWebView2 = _controller.CoreWebView2;
            _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
            _controller.DefaultBackgroundColor = BackColor;
            // The window starts hidden; keep the controller's rendering off until RevealNow shows the
            // window and flips it back on (a controller left visible on a hidden HWND renders grey once
            // the window appears). The page still loads and runs its scripts while not rendering.
            _controller.IsVisible = false;

            var settings = CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            // Turn off the browser-level accelerator keys (Ctrl+F find, Ctrl+P print, Ctrl+R/F5 reload,
            // Ctrl+/- zoom, ...). Without this the settings window pops WebView2's built-in find bar on
            // Ctrl+F. The chat overlay already disables these (see ManagedWebBrowser).
            settings.AreBrowserAcceleratorKeysEnabled = false;
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
            CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            this.Resize += OnFormResize;
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Secondary fallback: on a *failed* navigation the page can't call revealSettings, so show
            // the (otherwise invisible) window immediately instead of waiting out the watchdog. A
            // successful load is left for the page's own revealSettings call, so there's no flash of a
            // half-built page.
            if (!e.IsSuccess)
                RevealNow();
        }

        private void OnFormResize(object? sender, EventArgs e)
        {
            if (_controller != null)
                _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
        }

        private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            ManagedWebBrowser.ServeResource(_environment, _resourceResolver, e);
        }

        private void OnWindowCloseRequested(object? sender, object e)
        {
            // Raised by window.close() in the page (the dialog's save/exit/cancel buttons).
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke((Action)Close);
        }

        // Constrain a maximized borderless window to the working area of the monitor it's on, so it
        // leaves the taskbar visible like a regular window instead of going edge-to-edge fullscreen.
        // ptMaxPosition/ptMaxSize are relative to the monitor the window maximizes onto.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_GETMINMAXINFO)
            {
                var screen = Screen.FromHandle(Handle);
                var work = screen.WorkingArea;
                var bounds = screen.Bounds;
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(m.LParam);
                mmi.ptMaxPosition = new Point(work.Left - bounds.Left, work.Top - bounds.Top);
                mmi.ptMaxSize = new Point(work.Width, work.Height);
                Marshal.StructureToPtr(mmi, m.LParam, false);
            }
            base.WndProc(ref m);
        }

        // Every closure (Save / Cancel / title-bar X all route window.close() -> Close()) saves the
        // current placement, so the next open restores it.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _persistTimer?.Stop();
            PersistCurrentFrame();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    _persistTimer?.Stop();
                    _persistTimer?.Dispose();
                    _revealWatchdog?.Stop();
                    _revealWatchdog?.Dispose();
                    if (CoreWebView2 != null)
                    {
                        CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                        CoreWebView2.WindowCloseRequested -= OnWindowCloseRequested;
                        CoreWebView2.NavigationCompleted -= OnNavigationCompleted;
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
