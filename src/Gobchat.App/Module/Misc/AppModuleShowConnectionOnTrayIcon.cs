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
using Gobchat.Module.NotifyIcon;
using System;
using Gobchat.Core.UI;
using Gobchat.Module.MemoryReader;
using Gobchat.Module.Overlay;
using Gobchat.UI.Forms;

namespace Gobchat.Module.Misc
{
    // Drives the tray icon's "G" through three states from connection + chat-overlay visibility:
    //   disconnected            -> GobTrayIconOff    (black G)
    //   connected + chat shown  -> GobTrayIconOn     (gold G)
    //   connected + chat hidden -> GobTrayIconHidden (black G, gold outline)
    // Visibility tracks the overlay's actual Visible state, so focus-based auto-hide
    // (AppModuleChatOverlay's ComputeShouldShow) correctly flips the icon to the hidden state too.
    public sealed class AppModuleShowConnectionOnTrayIcon : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IDIContext _container = null!;
        private IMemoryReaderManager _memoryReader = null!;
        private IUIManager _uiManager = null!;
        private OverlayForm _overlay = null!;

        // Written from the connection worker (connected) and the UI thread (overlayVisible); the combined
        // read in UpdateIcon happens under the same lock so the chosen icon can't latch a stale mix.
        private readonly object _lock = new object();
        private bool _connected;
        private bool _overlayVisible;

        /// <summary>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// <br></br>
        /// Reads UI element: <see cref="OverlayForm"/> (chat overlay visibility) <br></br>
        /// Adds to UI element: <see cref="INotifyIconManager"/> <br></br>
        /// </summary>
        public AppModuleShowConnectionOnTrayIcon()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _memoryReader = _container.Resolve<IMemoryReaderManager>();
            _uiManager = _container.Resolve<IUIManager>();

            _connected = _memoryReader.ConnectionState == ConnectionState.Connected;

            // The chat overlay module is initialized earlier (it creates the overlay synchronously), so the
            // UI element exists by now. Seed from its current visibility - login can happen during startup,
            // before this module subscribes - then keep in sync via VisibleChanged.
            if (_uiManager.TryGetUIElement(AppModuleChatOverlay.OverlayUIId, out OverlayForm overlay))
            {
                _overlay = overlay;
                _uiManager.UISynchronizer.RunSync(() => _overlayVisible = _overlay.Visible);
                _overlay.VisibleChanged += Overlay_OnVisibleChanged;
            }
            else
            {
                logger.Warn("Chat overlay not found; tray icon will not reflect chat visibility.");
            }

            _memoryReader.OnConnectionStateChanged += MemoryReader_OnConnectionState;

            UpdateIcon();
        }

        public void Dispose()
        {
            // Guard the unsubscribes: if Initialize threw before these were set, Dispose must not NRE and
            // mask the original failure.
            if (_memoryReader != null)
                _memoryReader.OnConnectionStateChanged -= MemoryReader_OnConnectionState;
            if (_overlay != null)
                _overlay.VisibleChanged -= Overlay_OnVisibleChanged;

            _container = null!;
            _memoryReader = null!;
            _uiManager = null!;
            _overlay = null!;
        }

        private void MemoryReader_OnConnectionState(object? sender, ConnectionEventArgs e)
        {
            lock (_lock)
                _connected = e.State == ConnectionState.Connected;
            UpdateIcon();
        }

        // Raised on the UI thread when the overlay is shown/hidden (pin, login, or focus auto-hide).
        private void Overlay_OnVisibleChanged(object? sender, EventArgs e)
        {
            lock (_lock)
                _overlayVisible = _overlay != null && _overlay.Visible;
            UpdateIcon();
        }

        private void UpdateIcon()
        {
            // Capture the manager into a local: a connection change can race Dispose (which nulls
            // _uiManager) - e.g. FFXIV closing while the app shuts down fires this on the worker thread.
            // The posted lambda must not read the field, or it would NRE on the UI thread (an unhandled
            // throw there crashes the process, since RunAsync has no exception handler).
            var uiManager = _uiManager;
            if (uiManager == null)
                return;

            System.Drawing.Icon icon;
            lock (_lock)
                icon = !_connected
                    ? Resources.GobTrayIconOff
                    : (_overlayVisible ? Resources.GobTrayIconOn : Resources.GobTrayIconHidden);

            // Funnel every set through the UI thread - state changes arrive on both a worker thread
            // (connection) and the UI thread (visibility). Once the UIManager is disposed its map is
            // cleared, so TryGetUIElement just returns false here instead of throwing.
            uiManager.UISynchronizer.RunAsync(() =>
            {
                if (uiManager.TryGetUIElement<INotifyIconManager>(AppModuleNotifyIcon.NotifyIconManagerId, out var trayIcon))
                    trayIcon.Icon = icon;
            });
        }
    }
}
