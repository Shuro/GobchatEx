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

using Gobchat.Core.Runtime;
using Gobchat.Core.UI;
using Gobchat.Module.Actor;
using Gobchat.Module.MemoryReader;
using Gobchat.Module.UI;
using Gobchat.UI.Forms;
using Gobchat.UI.Web;
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace Gobchat.Module.Overlay
{
    /// <summary>
    /// Hosts the "system" overlay: a transparent, topmost window on the primary monitor that shows the
    /// greeter splash (until FFXIV is attached) and brief login/logout notifications. It owns its own
    /// <see cref="OverlayForm"/> + browser and is driven by pushing <see cref="ConnectionStateWebEvent"/>s
    /// to its page. While the greeter is visible the window shrinks to a centered, click-capturing region
    /// (so the greeter's close button works); otherwise it is fullscreen + click-through (toasts only).
    /// The only JS&#8594;C# surface bound here is the minimal <see cref="SystemOverlayBrowserAPI"/> (the
    /// greeter's quit button); the page is otherwise GobchatAPI-/config-free.
    ///
    /// Requires: <see cref="IUIManager"/> <br></br>
    /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
    /// Requires: <see cref="IActorManager"/> <br></br>
    /// <br></br>
    /// Installs UI element: <see cref="OverlayForm"/> <br></br>
    /// </summary>
    public sealed class AppModuleSystemOverlay : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public const string SystemOverlayUIId = "Gobchat.SystemOverlayForm";
        private const string SystemUrl = "https://gobchat.localhost/system.html";

        // While the greeter splash is visible the overlay must stop being click-through so its close (X)
        // button can be clicked. Click-through is whole-window (no per-pixel passthrough), so rather than
        // capturing the whole primary monitor we shrink to a centered window just large enough for the
        // splash; clicks outside it still fall through to the game/taskbar. The size is FIXED (logical
        // CSS pixels, converted to physical via the form DPI) — not a fraction of the screen — so the
        // capture region stays this small on any monitor instead of ballooning on large/ultrawide displays.
        // Generous enough to hold the centered splash (incl. its longest localized status line + shadow).
        private const int GreeterWindowCssWidth = 600;
        private const int GreeterWindowCssHeight = 400;

        // In toast (fullscreen) mode the overlay is topmost and click-through. A topmost window whose
        // bounds EXACTLY cover a monitor is the trigger for the Windows shell's fullscreen-app detection,
        // which auto-hides the taskbar for the whole session — so the taskbar never reappears when the
        // user alt-tabs from FFXIV to another app. Leaving one uncovered pixel row at the bottom (where
        // the taskbar normally lives) defeats that heuristic while staying visually fullscreen; toast
        // content is centered/top, so the 1px loss is invisible.
        private const int FullscreenBottomInset = 1;

        private readonly JavascriptBuilder _jsBuilder = new JavascriptBuilder();

        private IUIManager _manager = null!; // set in Initialize, cleared in Dispose
        private IMemoryReaderManager _memoryManager = null!;
        private IActorManager _actorManager = null!;
        private OverlayForm _overlay = null!;
        private string _uiRoot = null!;

        public AppModuleSystemOverlay()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _manager = container.Resolve<IUIManager>();
            _memoryManager = container.Resolve<IMemoryReaderManager>();
            _actorManager = container.Resolve<IActorManager>();
            _uiRoot = Path.GetFullPath(Path.Combine(GobchatContext.ResourceLocation, "ui"));

            _manager.UISynchronizer.RunSync(InitializeUI);

            _memoryManager.OnConnectionStateChanged += Memory_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged += Actor_OnCurrentPlayerChanged;
        }

        private void InitializeUI()
        {
            _overlay = _manager.CreateUIElement(SystemOverlayUIId, () => new OverlayForm());

            _overlay.Browser.ResourceRootFolder = _uiRoot;
            _overlay.Browser.ResourceResolver = p => UiResourceResolver.Resolve(_uiRoot, p);

            // The bridge core is injected per browser; bind only the minimal shutdown API so the greeter's
            // close (X) button can quit the app. Queued before navigation (see ManagedWebBrowser.Attach).
            _overlay.Browser.BindBrowserAPI(new SystemOverlayBrowserAPI(), true);

            // Push the current state once the page is ready (state changes may predate the load).
            _overlay.Browser.OnBrowserLoadPageDone += Browser_OnBrowserLoadPageDone;
            _overlay.Browser.OnBrowserInitialized += Browser_OnBrowserInitialized;

            _overlay.Show();
            var bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            // Same 1px bottom sliver as ApplyOverlayMode's toast branch: without it this initial
            // monitor-covering topmost window trips the shell's fullscreen detection and hides the
            // taskbar during the brief gap before the first PushConnectionState resizes the overlay.
            _overlay.Bounds = new Rectangle(
                bounds.X, bounds.Y, bounds.Width, bounds.Height - FullscreenBottomInset);
            // Start fullscreen + passive; the first PushConnectionState switches to the constrained,
            // click-capturing greeter window if the splash is visible (see ApplyOverlayMode).
            _overlay.SetClickThrough(true);
        }

        private void Browser_OnBrowserInitialized(object? sender, BrowserInitializedEventArgs e)
        {
            _overlay.Browser.Load(SystemUrl);
        }

        private void Browser_OnBrowserLoadPageDone(object? sender, BrowserLoadPageEventArgs e)
        {
            PushConnectionState();
        }

        private void Memory_OnConnectionStateChanged(object? sender, ConnectionEventArgs e) => PushConnectionState();

        private void Actor_OnCurrentPlayerChanged(object? sender, CurrentPlayerChangedEventArgs e) => PushConnectionState();

        private void PushConnectionState()
        {
            if (_overlay == null)
                return;
            try
            {
                var state = (int)_memoryManager.ConnectionState;
                var greeterText = GreeterTextForState(state);
                // Resolve all user-facing strings here: the system overlay page is intentionally
                // GobchatAPI-/config-free, so the backend pushes localized text (and {0}-templates the
                // page fills with the character name) instead of the page hardcoding English.
                var evt = new ConnectionStateWebEvent(
                    state,
                    _actorManager.GetActivePlayerName(),
                    greeterText,
                    Loc("system.notify.login"),
                    Loc("system.notify.logout"),
                    Loc("system.notify.switch"),
                    Loc("system.greeter.close"));
                var script = _jsBuilder.BuildCustomEventDispatcher(evt);
                // Switch the window's size/click-through and push the event together on the UI thread: the
                // overlay must become click-capturing (constrained, centered) while the greeter is up so
                // its close button works, then return to fullscreen + passive once connected.
                _overlay.InvokeAsyncOnUI(o =>
                {
                    ApplyOverlayMode(greeterText != null);
                    o.Browser.ExecuteScript(script);
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to push connection state to the system overlay");
            }
        }

        // Switches the overlay between the constrained, click-capturing greeter window (centered, so the
        // close button is reachable) and the fullscreen, click-through window used for toasts. Greeter and
        // toasts never show together (toasts only fire while connected, i.e. the greeter is hidden), so
        // resizing per-mode is safe. Must run on the UI thread (window handle ops).
        private void ApplyOverlayMode(bool greeterVisible)
        {
            var screen = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            if (greeterVisible)
            {
                // Logical px -> physical: the WebView2 page rasterizes at the window's DPI scale, so the
                // splash's CSS size grows with DPI; scale the capture window the same way (DeviceDpi/96).
                var scale = _overlay.DeviceDpi / 96.0;
                var width = Math.Min(screen.Width, (int)(GreeterWindowCssWidth * scale));
                var height = Math.Min(screen.Height, (int)(GreeterWindowCssHeight * scale));
                var x = screen.X + (screen.Width - width) / 2;
                var y = screen.Y + (screen.Height - height) / 2;
                _overlay.Bounds = new Rectangle(x, y, width, height);
                _overlay.SetClickThrough(false);
            }
            else
            {
                _overlay.SetClickThrough(true);
                // Cover the monitor minus a 1px bottom sliver so the shell doesn't treat this topmost
                // window as a fullscreen app and hide the taskbar (see FullscreenBottomInset).
                _overlay.Bounds = new Rectangle(
                    screen.X, screen.Y, screen.Width, screen.Height - FullscreenBottomInset);
            }
        }

        // Localized greeter line for the connection state, or null when connected (greeter hides).
        private static string? GreeterTextForState(int state)
        {
            switch (state)
            {
                case (int)ConnectionState.Connected: return null;
                case (int)ConnectionState.NotFound: return Loc("system.greeter.notfound");
                case (int)ConnectionState.Searching: return Loc("system.greeter.searching");
                case (int)ConnectionState.NoAccess: return Loc("system.greeter.noaccess");
                case (int)ConnectionState.OutdatedSignatures: return Loc("system.greeter.outdated");
                default: return Loc("system.greeter.starting"); // NotInitialized / unknown
            }
        }

        private static string Loc(string key) => WebUIResources.ResourceManager.GetString(key, WebUIResources.Culture) ?? key;

        public void Dispose()
        {
            if (_manager == null)
                return; // never initialized (Initialize threw before _manager was set)

            _memoryManager.OnConnectionStateChanged -= Memory_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged -= Actor_OnCurrentPlayerChanged;

            if (_overlay != null)
            {
                _overlay.Browser.OnBrowserInitialized -= Browser_OnBrowserInitialized;
                _overlay.Browser.OnBrowserLoadPageDone -= Browser_OnBrowserLoadPageDone;
                _manager.UISynchronizer.RunSync(() => _overlay.Close());
                _manager.DisposeUIElement(SystemOverlayUIId);
            }

            _manager = null!;
            _memoryManager = null!;
            _actorManager = null!;
            _overlay = null!;
        }
    }
}
