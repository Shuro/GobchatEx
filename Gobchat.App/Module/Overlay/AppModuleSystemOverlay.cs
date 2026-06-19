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
using System.IO;
using System.Windows.Forms;

namespace Gobchat.Module.Overlay
{
    /// <summary>
    /// Hosts the "system" overlay: a fullscreen, transparent, always-click-through window on the
    /// primary monitor that shows the greeter splash (until FFXIV is attached) and brief login/logout
    /// notifications. It owns its own <see cref="OverlayForm"/> + browser and is driven entirely by
    /// pushing <see cref="ConnectionStateWebEvent"/>s to its page (no GobchatAPI binding needed).
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

        private readonly JavascriptBuilder _jsBuilder = new JavascriptBuilder();

        private IUIManager _manager;
        private IMemoryReaderManager _memoryManager;
        private IActorManager _actorManager;
        private OverlayForm _overlay;
        private string _uiRoot;

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

            // Push the current state once the page is ready (state changes may predate the load).
            _overlay.Browser.OnBrowserLoadPageDone += Browser_OnBrowserLoadPageDone;
            _overlay.Browser.OnBrowserInitialized += Browser_OnBrowserInitialized;

            _overlay.Show();
            var bounds = Screen.PrimaryScreen?.Bounds ?? Screen.AllScreens[0].Bounds;
            _overlay.Bounds = bounds;
            // Always passive: the splash/toasts never take input, so clicks fall through to the game.
            _overlay.SetClickThrough(true);
        }

        private void Browser_OnBrowserInitialized(object sender, BrowserInitializedEventArgs e)
        {
            _overlay.Browser.Load(SystemUrl);
        }

        private void Browser_OnBrowserLoadPageDone(object sender, BrowserLoadPageEventArgs e)
        {
            PushConnectionState();
        }

        private void Memory_OnConnectionStateChanged(object sender, ConnectionEventArgs e) => PushConnectionState();

        private void Actor_OnCurrentPlayerChanged(object sender, CurrentPlayerChangedEventArgs e) => PushConnectionState();

        private void PushConnectionState()
        {
            if (_overlay == null)
                return;
            try
            {
                var state = (int)_memoryManager.ConnectionState;
                // Resolve all user-facing strings here: the system overlay page is intentionally
                // GobchatAPI-/config-free, so the backend pushes localized text (and {0}-templates the
                // page fills with the character name) instead of the page hardcoding English.
                var evt = new ConnectionStateWebEvent(
                    state,
                    _actorManager.GetActivePlayerName(),
                    GreeterTextForState(state),
                    Loc("system.notify.login"),
                    Loc("system.notify.logout"),
                    Loc("system.notify.switch"));
                var script = _jsBuilder.BuildCustomEventDispatcher(evt);
                _overlay.InvokeAsyncOnUI(o => o.Browser.ExecuteScript(script));
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to push connection state to the system overlay");
            }
        }

        // Localized greeter line for the connection state, or null when connected (greeter hides).
        private static string GreeterTextForState(int state)
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

            _manager = null;
            _memoryManager = null;
            _actorManager = null;
            _overlay = null;
        }
    }
}
