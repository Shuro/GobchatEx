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

using Gobchat.UI.Forms;
using Gobchat.Core.Runtime;
using Gobchat.Core.Config;
using System.Windows.Forms;
using Gobchat.Core.UI;
using System;
using Gobchat.Module.NotifyIcon;
using Gobchat.Module.MemoryReader;
using Gobchat.Module.Actor;
using Gobchat.Core.Util;

namespace Gobchat.Module.Overlay
{
    public sealed class AppModuleChatOverlay : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public const string OverlayUIId = "Gobchat.ChatOverlayForm";

        private const string PinnedConfigKey = "behaviour.frame.chat.pinned";

        private IConfigManager _configManager;
        private IUIManager _manager;
        private IMemoryReaderManager _memoryManager;
        private IActorManager _actorManager;
        private bool _settingsOnly;

        private OverlayForm _overlay;
        private ToolStripMenuItem _pinMenuItem;

        // Visibility is derived from these: the overlay shows once the page is ready and either it is
        // pinned or a character is logged in (connected to FFXIV with a current player).
        private bool _pageReady;
        private bool _connected;
        private bool _loggedIn;
        private bool _pinned;

        private DelayedCallback _moveCallback;
        private DelayedCallback _resizeCallback;

        /// <summary>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// Requires: <see cref="IConfigManager"/> <br></br>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// Requires: <see cref="IActorManager"/> <br></br>
        /// <br></br>
        /// Adds to UI element: <see cref="INotifyIconManager"/> <br></br>
        /// Installs UI element: <see cref="OverlayForm"/> <br></br>
        /// </summary>
        public AppModuleChatOverlay()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _manager = container.Resolve<IUIManager>();
            _configManager = container.Resolve<IConfigManager>();
            _memoryManager = container.Resolve<IMemoryReaderManager>();
            _actorManager = container.Resolve<IActorManager>();
            _settingsOnly = container.Resolve<StartupOptions>().SettingsOnly;

            _pinned = _configManager.GetProperty(PinnedConfigKey, false);
            _connected = _memoryManager.ConnectionState == ConnectionState.Connected;
            _loggedIn = _actorManager.GetActivePlayerName() != null;

            var synchronizer = _manager.UISynchronizer;
            synchronizer.RunSync(() => InitializeUI());

            _memoryManager.OnConnectionStateChanged += Memory_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged += Actor_OnCurrentPlayerChanged;
            _configManager.AddPropertyChangeListener(PinnedConfigKey, true, false, OnEvent_ConfigManager_PinnedChange);
        }

        private void InitializeUI()
        {
            _overlay = _manager.CreateUIElement(OverlayUIId, () => new OverlayForm());
            _overlay.Show(); //initializes all properties
            _overlay.Visible = false;

            _configManager.AddPropertyChangeListener("behaviour.frame.chat", true, true, OnEvent_ConfigManager_PositionChange);

            _moveCallback = new DelayedCallback(TimeSpan.FromSeconds(1), () =>
            {
                var location = _overlay.Location;
                if (IsFrameOnScreens(_overlay.DesktopBounds))
                {
                    _configManager.SetProperty("behaviour.frame.chat.position.x", location.X);
                    _configManager.SetProperty("behaviour.frame.chat.position.y", location.Y);
                    _configManager.DispatchChangeEvents();
                }
                else // restore last location and size from config
                {
                    UpdateFormPosition();
                }
            });

            _resizeCallback = new DelayedCallback(TimeSpan.FromSeconds(1), () =>
            {
                var size = _overlay.Size;
                if (IsFrameOnScreens(_overlay.DesktopBounds))
                {
                    _configManager.SetProperty("behaviour.frame.chat.size.width", size.Width);
                    _configManager.SetProperty("behaviour.frame.chat.size.height", size.Height);
                    _configManager.DispatchChangeEvents();
                }
                else // restore last location and size from config
                {
                    UpdateFormPosition();
                }
            });

            _overlay.Move += (s, e) => _moveCallback.Call();
            _overlay.SizeChanged += (s, e) => _resizeCallback.Call();

            _overlay.Browser.OnBrowserLoadPageDone += (s, e) =>
            {
                // The page is ready; visibility is now driven by pin + login state (see
                // UpdateChatVisibility). In settings-only debug mode the overlay stays hidden.
                _pageReady = true;
                UpdateChatVisibility();
            };

            if (_manager.TryGetUIElement<INotifyIconManager>(AppModuleNotifyIcon.NotifyIconManagerId, out var trayIcon))
            {
                //trayIcon.Icon = Gobchat.Resource.GobTrayIconOff;

                // The overlay auto-shows while logged in; the only manual control is the pin (force it
                // visible even when logged out), shared with the toolbar pin button via config.
                trayIcon.OnIconClick += (s, e) => TogglePinned();

                _pinMenuItem = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_Pin)
                {
                    Checked = _pinned,
                    CheckOnClick = false,
                };
                _pinMenuItem.Click += (s, e) => TogglePinned();
                trayIcon.AddMenu("overlay.pin", _pinMenuItem);

                // Opens the settings dialog without needing to click the overlay's cog (which is
                // unreachable while the overlay is click-through). Drives the page's own openGobConfig.
                var menuItemSettings = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_OpenSettings);
                menuItemSettings.Click += (s, e) => _manager.UISynchronizer.RunSync(() =>
                {
                    logger.Info("Tray: open settings requested, invoking window.openGobConfig");
                    _overlay.Browser.ExecuteScript("if (window.openGobConfig) { window.openGobConfig(); } else { console.error('openGobConfig is not defined on window'); }");
                });
                trayIcon.AddMenu("overlay.settings", menuItemSettings);

                // Lock/unlock toggle: WebView2 composition hosting has no per-pixel hit-testing, so
                // click-through is a whole-window switch (see OverlayForm.SetClickThrough).
                var menuItemClickThrough = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_ClickThrough)
                {
                    Checked = _overlay.IsClickThrough,
                    CheckOnClick = false,
                };
                menuItemClickThrough.Click += (s, e) => _manager.UISynchronizer.RunSync(() =>
                {
                    _overlay.ToggleClickThrough();
                    menuItemClickThrough.Checked = _overlay.IsClickThrough;
                });
                trayIcon.AddMenu("overlay.clickthrough", menuItemClickThrough);

                var menuItemReload = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_Reload);
                menuItemReload.Click += (s, e) => _overlay.Reload();
                trayIcon.AddMenu("overlay.reload", menuItemReload);

                var menuItemFrameReset = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_Reset);
                menuItemFrameReset.Click += (s, e) => ResetFrameToDefaultLocation();
                trayIcon.AddMenu("overlay.reset", menuItemFrameReset);

#if DEBUG
                var menuItemDevTool = new ToolStripMenuItem("DevTool");
                menuItemDevTool.Click += (s, e) => _overlay.Browser.ShowDevTools();
                trayIcon.AddMenuToGroup("debug", "overlay.devtool", menuItemDevTool);
#endif
            }

            _overlay.Visible = false;
        }

        private void OnEvent_ConfigManager_PositionChange(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            UpdateFormPosition();
        }

        private void Memory_OnConnectionStateChanged(object sender, ConnectionEventArgs e)
        {
            _connected = e.State == ConnectionState.Connected;
            UpdateChatVisibility();
        }

        private void Actor_OnCurrentPlayerChanged(object sender, CurrentPlayerChangedEventArgs e)
        {
            _loggedIn = e.CurrentPlayerName != null;
            UpdateChatVisibility();
        }

        private void OnEvent_ConfigManager_PinnedChange(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            _pinned = _configManager.GetProperty(PinnedConfigKey, false);
            _manager.UISynchronizer.RunSync(() =>
            {
                if (_pinMenuItem != null)
                    _pinMenuItem.Checked = _pinned;
            });
            UpdateChatVisibility();
        }

        // Toggles the persisted pin flag. The config change drives both the visibility recompute
        // (via the listener) and the JS toolbar button (via AppModuleConfigToUI's config sync).
        private void TogglePinned()
        {
            var pinned = !_configManager.GetProperty(PinnedConfigKey, false);
            _configManager.SetProperty(PinnedConfigKey, pinned);
            _configManager.DispatchChangeEvents();
        }

        private void UpdateChatVisibility()
        {
            if (_overlay == null)
                return;

            // Settings-only debug mode keeps the chat overlay hidden (the page still loads so it can
            // back the settings dialog as its window.opener).
            var shouldShow = !_settingsOnly && _pageReady && (_pinned || (_connected && _loggedIn));

            _manager.UISynchronizer.RunSync(() =>
            {
                if (_overlay != null && _overlay.Visible != shouldShow)
                    _overlay.Visible = shouldShow;
            });
        }

        private void UpdateFormPosition()
        {
            _manager.UISynchronizer.RunSync(UpdateFormPositionOnUIThread);
        }

        private void UpdateFormPositionOnUIThread()
        {
            try
            {
                var posX = _configManager.GetProperty<long>("behaviour.frame.chat.position.x");
                var posY = _configManager.GetProperty<long>("behaviour.frame.chat.position.y");
                var width = _configManager.GetProperty<long>("behaviour.frame.chat.size.width");
                var height = _configManager.GetProperty<long>("behaviour.frame.chat.size.height");

                var location = new System.Drawing.Point((int)posX, (int)posY);
                var size = new System.Drawing.Size((int)width, (int)height);

                if (!IsFrameOnScreens(new System.Drawing.Rectangle(location, size)))
                { // location and size invalid, fallback to default location
                    logger.Info("Overlay off screen, reseting position and size");
                    ResetFrameToDefaultLocation();
                    return;
                }

                if (!location.Equals(_overlay.Location))
                    _overlay.Location = location;

                if (!size.Equals(_overlay.Size))
                    _overlay.Size = size;
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
            }
        }

        private bool IsFrameOnScreens(System.Drawing.Rectangle frameArea, float minCoverage = 0.2f)
        {
            var coveredPixels = 0;
            foreach (var screen in Screen.AllScreens)
            {
                var screenArea = screen.WorkingArea;
                if (screenArea.IntersectsWith(frameArea))
                {
                    var intersection = new System.Drawing.Rectangle(frameArea.Location, frameArea.Size);
                    intersection.Intersect(screenArea);
                    coveredPixels += intersection.Width * intersection.Height;
                }
            }
            var coverage = coveredPixels / (frameArea.Width * frameArea.Height * 1f);
            return coverage >= minCoverage;
        }

        private void ResetFrameToDefaultLocation()
        {
            _configManager.DeleteProperty("behaviour.frame.chat.position");
            _configManager.DeleteProperty("behaviour.frame.chat.size");
            _configManager.DispatchChangeEvents();
        }

        public void Dispose()
        {
            _memoryManager.OnConnectionStateChanged -= Memory_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged -= Actor_OnCurrentPlayerChanged;
            _configManager.RemovePropertyChangeListener(OnEvent_ConfigManager_PinnedChange);
            _configManager.RemovePropertyChangeListener(OnEvent_ConfigManager_PositionChange);

            var chatLocation = _overlay.Location;
            _configManager.SetProperty("behaviour.frame.chat.position.x", chatLocation.X);
            _configManager.SetProperty("behaviour.frame.chat.position.y", chatLocation.Y);

            var chatSize = _overlay.Size;
            _configManager.SetProperty("behaviour.frame.chat.size.width", chatSize.Width);
            _configManager.SetProperty("behaviour.frame.chat.size.height", chatSize.Height);

            _manager.UISynchronizer.RunSync(() => _overlay.Close());

            _manager.DisposeUIElement(OverlayUIId);
            _moveCallback.Dispose();
            _resizeCallback.Dispose();

            _manager = null;
            _overlay = null;
            _configManager = null;
            _memoryManager = null;
            _actorManager = null;
            _pinMenuItem = null;
            _moveCallback = null;
            _resizeCallback = null;
        }
    }
}