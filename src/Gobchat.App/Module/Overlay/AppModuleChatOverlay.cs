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
using Gobchat.Module.Language;
using Gobchat.Core.Util;
using System.Collections.Generic;

namespace Gobchat.Module.Overlay
{
    public sealed class AppModuleChatOverlay : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public const string OverlayUIId = "Gobchat.ChatOverlayForm";

        private const string PinnedConfigKey = "behaviour.frame.chat.pinned";

        // App-global flag for the "hide the chat while FFXIV isn't focused" feature. Folded in here (it
        // used to live in the standalone AppModuleHideOnMinimize) so this module is the single owner of
        // _overlay.Visible - see the _visibilityLock note below.
        private const string FocusHideConfigKey = "behaviour.hideOnMinimize";

        private IConfigManager _configManager = null!; // set in Initialize, cleared in Dispose
        private IUIManager _manager = null!;
        private IMemoryReaderManager _memoryManager = null!;
        private IActorManager _actorManager = null!;
        private ILocaleManager _localeManager = null!;

        private OverlayForm _overlay = null!;
        private ToolStripMenuItem _pinMenuItem = null!;

        // Tray menu item texts come from .resx and are set once at creation; re-apply them when the
        // language changes (the .resx culture is swapped, but existing ToolStripMenuItems don't refresh).
        private readonly List<Action> _trayRelocalizers = new List<Action>();

        // Visibility is derived from these: the overlay shows once the page is ready and either it is
        // pinned or a character is logged in (connected to FFXIV with a current player), and - when the
        // focus-based auto-hide is enabled - only while FFXIV/GobchatEx is the foreground window. The
        // flags are written from several threads (connect worker, actor poll, config thread, window-focus
        // hook, UI), so every write and the combined read happen under _visibilityLock to avoid a stale
        // visibility decision. This module is the only writer of _overlay.Visible.
        private readonly object _visibilityLock = new object();
        private bool _pageReady;
        private bool _connected;
        private bool _loggedIn;
        private bool _pinned;
        private bool _hideWhenUnfocused;
        private bool _inForeground = true; // assume focused until the window observer says otherwise
        // True while the tray context menu is open. Showing the overlay then (the menu's own
        // SetForegroundWindow counts as a GobchatEx foreground flip) dismisses the dropdown, so we hold
        // the overlay hidden until the menu closes - otherwise the tray menu can't be opened at all.
        private bool _trayMenuOpen;


        /// <summary>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// Requires: <see cref="IConfigManager"/> <br></br>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// Requires: <see cref="IActorManager"/> <br></br>
        /// Requires: <see cref="ILocaleManager"/> <br></br>
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
            _localeManager = container.Resolve<ILocaleManager>();

            _pinned = _configManager.GetProperty(PinnedConfigKey, false);
            _hideWhenUnfocused = _configManager.GetProperty(FocusHideConfigKey, false);
            _connected = _memoryManager.ConnectionState == ConnectionState.Connected;
            _loggedIn = _actorManager.GetActivePlayerName() != null;

            var synchronizer = _manager.UISynchronizer;
            synchronizer.RunSync(() => InitializeUI());

            _memoryManager.OnConnectionStateChanged += Memory_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged += Actor_OnCurrentPlayerChanged;
            _configManager.AddPropertyChangeListener(PinnedConfigKey, true, false, OnEvent_ConfigManager_PinnedChange);

            // Feed the foreground state through ComputeShouldShow (instead of writing _overlay.Visible
            // directly, as the old AppModuleHideOnMinimize did) so a focus change can't race the pin/login
            // decision. initialize:true applies the stored hide flag now.
            _memoryManager.OnWindowFocusChanged += Memory_OnWindowFocusChanged;
            _configManager.AddPropertyChangeListener(FocusHideConfigKey, true, true, OnEvent_ConfigManager_FocusHideChange);

            // Observe the game's foreground state unconditionally - not only while the auto-hide feature is
            // on - so the overlay can re-assert its topmost z-order whenever FFXIV returns to the foreground
            // (see Memory_OnWindowFocusChanged). Without a foreground signal an alt-tab back into the game
            // leaves its freshly-activated window stacked above our never-activated overlay, hiding the
            // chat. The hook is a single SetWinEventHook, and the hide feature still gates purely on
            // _hideWhenUnfocused in ComputeShouldShow, so observing always is free of side effects there.
            // SetWinEventHook ties its hook to the calling thread's pump, so start it on the UI thread.
            _manager.UISynchronizer.RunSync(() => _memoryManager.ObserveGameWindow = true);

            // Keep the tray menu labels in sync with the active language. Subscribing also fires once
            // immediately (ILocaleManager contract), which harmlessly re-applies the current language.
            _localeManager.OnLocaleChange += OnEvent_LocaleManager_LocaleChange;
        }

        private void InitializeUI()
        {
            _overlay = _manager.CreateUIElement(OverlayUIId, () => new OverlayForm());
            _overlay.Show(); //initializes all properties
            _overlay.Visible = false;

            _configManager.AddPropertyChangeListener("behaviour.frame.chat", true, true, OnEvent_ConfigManager_PositionChange);

            // Position/size are persisted only when the overlay is pinned back in place — not continuously
            // while dragging — so a move/resize doesn't churn the config or re-sync the whole UI mid-gesture.
            _overlay.LockStateChanged += OnEvent_Overlay_LockChanged;

            _overlay.Browser.OnBrowserLoadPageDone += (s, e) =>
            {
                // The page is ready; visibility is now driven by pin + login state (see
                // ApplyChatVisibility): the overlay shows once pinned, or once a character is logged in.
                //
                // Re-seed _connected/_loggedIn from the authoritative source here. They are first read in
                // Initialize, but the memory/actor workers flip connected -> player-detected during the
                // same startup window (the CHARMAP signature often only becomes readable just as this
                // module initializes), so the initial reads can latch a stale "false" and the single
                // login event can fire before this module subscribes - leaving the overlay hidden until
                // pinned even though a character is logged in. Page-load-done happens well after the
                // workers settle and after our subscriptions, so this is a safe point to resync.
                bool shouldShow;
                lock (_visibilityLock)
                {
                    _pageReady = true;
                    _connected = _memoryManager.ConnectionState == ConnectionState.Connected;
                    _loggedIn = _actorManager.GetActivePlayerName() != null;
                    shouldShow = ComputeShouldShow();
                }
                ApplyChatVisibility(shouldShow);
            };

            // When the settings window closes, Windows may hand focus to the desktop rather than back to
            // the game; with focus-based auto-hide active that would leave the overlay hidden. Bring FFXIV
            // back to the foreground so the overlay stays visible (see OnEvent_Browser_SettingsWindowClosed).
            _overlay.Browser.SettingsWindowClosed += OnEvent_Browser_SettingsWindowClosed;

            if (_manager.TryGetUIElement<INotifyIconManager>(AppModuleNotifyIcon.NotifyIconManagerId, out var trayIcon))
            {
                // Keep the overlay from popping up over the tray context menu while it is open (see
                // _trayMenuOpen). Both events run on the UI thread.
                trayIcon.OnMenuOpen += (s, e) => SetTrayMenuOpen(true);
                trayIcon.OnMenuClose += (s, e) => SetTrayMenuOpen(false);

                //trayIcon.Icon = Gobchat.Resource.GobTrayIconOff;

                // A single left-click on the tray icon opens settings (its default action) - the overlay's
                // own cog is unreachable while click-through, so the tray is the reliable entry point.
                // (Pin stays available via the right-click menu and the overlay toolbar.)
                trayIcon.OnIconClick += (s, e) => OpenSettings();

                // Opens the settings dialog without needing to click the overlay's cog (which is
                // unreachable while the overlay is click-through). Shares OpenSettings with the icon click.
                // Listed first so the primary action sits at the top of the menu.
                var menuItemSettings = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_OpenSettings);
                menuItemSettings.Click += (s, e) => OpenSettings();
                trayIcon.AddMenu("overlay.settings", menuItemSettings);

                _pinMenuItem = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_Pin)
                {
                    Checked = _pinned,
                    CheckOnClick = false,
                };
                _pinMenuItem.Click += (s, e) => TogglePinned();
                trayIcon.AddMenu("overlay.pin", _pinMenuItem);

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

                // Re-read the (culture-dependent) .resx labels on language change. The DevTool item below
                // is intentionally not registered (it's an untranslated developer aid).
                _trayRelocalizers.Add(() => _pinMenuItem.Text = Resources.Module_NotifyIcon_UI_Pin);
                _trayRelocalizers.Add(() => menuItemSettings.Text = Resources.Module_NotifyIcon_UI_OpenSettings);
                _trayRelocalizers.Add(() => menuItemClickThrough.Text = Resources.Module_NotifyIcon_UI_ClickThrough);
                _trayRelocalizers.Add(() => menuItemReload.Text = Resources.Module_NotifyIcon_UI_Reload);
                _trayRelocalizers.Add(() => menuItemFrameReset.Text = Resources.Module_NotifyIcon_UI_Reset);

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

        // Hands focus back to the game after the settings window closes, so the focus-based auto-hide
        // doesn't leave the overlay hidden because focus fell through to the desktop. Gated on the hide
        // feature itself (_hideWhenUnfocused) - not on ObserveGameWindow, which now runs unconditionally
        // for the topmost re-assert - so closing settings doesn't yank focus to the game when auto-hide
        // isn't in use. Raised on the UI thread (settings FormClosed), so it's safe to call straight through.
        private void OnEvent_Browser_SettingsWindowClosed(object? sender, EventArgs e)
        {
            bool hideWhenUnfocused;
            lock (_visibilityLock)
                hideWhenUnfocused = _hideWhenUnfocused;
            if (hideWhenUnfocused)
                _memoryManager.FocusGameWindow();
        }

        // The pin was toggled in the overlay. On pin (locked), persist the new position + size; if the
        // user parked it (mostly) off-screen, restore the last valid frame instead. The event is raised
        // on the UI thread (from the pin's bridge call), so the overlay can be read directly.
        private void OnEvent_Overlay_LockChanged(object? sender, bool locked)
        {
            if (!locked || _overlay == null)
                return;

            if (IsFrameOnScreens(_overlay.DesktopBounds))
            {
                var location = _overlay.Location;
                var size = _overlay.Size;
                _configManager.SetProperty("behaviour.frame.chat.position.x", location.X);
                _configManager.SetProperty("behaviour.frame.chat.position.y", location.Y);
                _configManager.SetProperty("behaviour.frame.chat.size.width", size.Width);
                _configManager.SetProperty("behaviour.frame.chat.size.height", size.Height);
                _configManager.DispatchChangeEvents();
            }
            else
            {
                UpdateFormPositionOnUIThread();
            }
        }

        private void Memory_OnConnectionStateChanged(object? sender, ConnectionEventArgs e)
        {
            bool shouldShow;
            lock (_visibilityLock)
            {
                _connected = e.State == ConnectionState.Connected;
                shouldShow = ComputeShouldShow();
            }
            ApplyChatVisibility(shouldShow);
        }

        private void Actor_OnCurrentPlayerChanged(object? sender, CurrentPlayerChangedEventArgs e)
        {
            bool shouldShow;
            lock (_visibilityLock)
            {
                _loggedIn = e.CurrentPlayerName != null;
                shouldShow = ComputeShouldShow();
            }
            ApplyChatVisibility(shouldShow);
        }

        private void OnEvent_ConfigManager_PinnedChange(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            var pinned = _configManager.GetProperty(PinnedConfigKey, false);
            bool shouldShow;
            lock (_visibilityLock)
            {
                _pinned = pinned;
                shouldShow = ComputeShouldShow();
            }
            _manager.UISynchronizer.RunSync(() =>
            {
                if (_pinMenuItem != null)
                    _pinMenuItem.Checked = pinned;
            });
            ApplyChatVisibility(shouldShow);
        }

        // Toggles the persisted pin flag. The config change drives both the visibility recompute
        // (via the listener) and the JS toolbar button (via AppModuleConfigToUI's config sync).
        private void TogglePinned()
        {
            var pinned = !_configManager.GetProperty(PinnedConfigKey, false);
            _configManager.SetProperty(PinnedConfigKey, pinned);
            _configManager.DispatchChangeEvents();
        }

        // The focus-based auto-hide was toggled (or applied at startup). This only flips the visibility
        // mask now - the foreground observer runs unconditionally (started in Initialize for the topmost
        // re-assert), so _inForeground is always current and we just recompute against it. Turning the
        // feature OFF recomputes too, so a pinned/logged-in overlay that an earlier focus-loss had hidden
        // comes back (the old standalone module never re-showed it, leaving the overlay stuck hidden).
        private void OnEvent_ConfigManager_FocusHideChange(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            var hideWhenUnfocused = _configManager.GetProperty(FocusHideConfigKey, false);
            bool shouldShow;
            lock (_visibilityLock)
            {
                _hideWhenUnfocused = hideWhenUnfocused;
                shouldShow = ComputeShouldShow();
            }
            ApplyChatVisibility(shouldShow);
        }

        // The tray context menu opened (true) or closed (false). While open we force the overlay hidden so
        // it can't appear over - and dismiss - the dropdown; on close we recompute against the real
        // pin/login/focus state. Raised on the UI thread.
        private void SetTrayMenuOpen(bool open)
        {
            bool shouldShow;
            lock (_visibilityLock)
            {
                _trayMenuOpen = open;
                shouldShow = ComputeShouldShow();
            }
            ApplyChatVisibility(shouldShow);
        }

        // FFXIV (or GobchatEx) gained/lost foreground focus. Only meaningful while the auto-hide feature is
        // on, but recorded either way and applied through ComputeShouldShow so focus can never override the
        // pin/login decision out-of-band the way the old direct _overlay.Visible write did.
        private void Memory_OnWindowFocusChanged(object? sender, Gobchat.Memory.WindowFocusChangedEventArgs e)
        {
            bool shouldShow;
            lock (_visibilityLock)
            {
                _inForeground = e.IsInForeground;
                shouldShow = ComputeShouldShow();
            }

            // When FFXIV (or a GobchatEx window) comes back to the foreground it activates above our overlay
            // - both are topmost, and within the topmost band the most-recently-activated window wins - so
            // after an alt-tab the chat ends up buried behind the game. Re-assert our topmost position
            // (without activating, so the game keeps focus and click-through is untouched) to lift it back
            // on top. Runs on every focus-gain regardless of the auto-hide setting; that's why the observer
            // is started unconditionally in Initialize.
            if (e.IsInForeground)
                _overlay?.InvokeAsyncOnUI(o => o.ReassertTopmost());

            ApplyChatVisibility(shouldShow);
        }

        // Opens the settings dialog by driving the page's own openGobConfig. Shared by the tray icon click
        // (its default action) and the "Open settings" context-menu item.
        private void OpenSettings()
        {
            _manager.UISynchronizer.RunSync(() =>
            {
                logger.Info("Tray: open settings requested, invoking window.openGobConfig");
                _overlay.Browser.ExecuteScript("if (window.openGobConfig) { window.openGobConfig(); } else { console.error('openGobConfig is not defined on window'); }");
            });
        }

        // Must be called while holding _visibilityLock. The page still loads even while hidden so it can
        // back the settings dialog as its window.opener.
        private bool ComputeShouldShow()
        {
            // The focus mask only bites when the auto-hide feature is on; with it off, _inForeground stays
            // true so (!_hideWhenUnfocused || _inForeground) is always true and the term drops out.
            // _trayMenuOpen forces hidden regardless, so the overlay never covers the open tray menu.
            return _pageReady
                && !_trayMenuOpen
                && (_pinned || (_connected && _loggedIn))
                && (!_hideWhenUnfocused || _inForeground);
        }

        // Applies the precomputed visibility on the UI thread without blocking the caller. RunAsync
        // (not RunSync) avoids deadlocking when a connect-worker state change drives this while the UI
        // thread is busy - it reinforces the Phase B connect-worker deadlock fix.
        private void ApplyChatVisibility(bool shouldShow)
        {
            if (_overlay == null)
                return;

            _manager.UISynchronizer.RunAsync(() =>
            {
                if (_overlay != null && _overlay.Visible != shouldShow)
                {
                    _overlay.Visible = shouldShow;
                    // Logged at Info so the visibility decision is actually visible in gobchatex_debug.log
                    // (the contributing signals - login/logout, focus changes - are Debug-only). The flag
                    // reads here are for diagnostics only, so they run without the lock.
                    logger.Info(() => $"Chat overlay {(shouldShow ? "shown" : "hidden")} " +
                        $"(pinned={_pinned}, connected={_connected}, loggedIn={_loggedIn}, " +
                        $"hideWhenUnfocused={_hideWhenUnfocused}, inForeground={_inForeground})");
                }
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
            // A zero/negative-size frame can't be "on screen" and would divide by zero below; treat it
            // as off-screen so callers fall back to the default/last-valid position.
            if (frameArea.Width <= 0 || frameArea.Height <= 0)
                return false;

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

        // Re-applies the active language to the tray menu labels. Raised on language change (and once on
        // subscribe); marshalled to the UI thread since it touches ToolStripMenuItems.
        private void OnEvent_LocaleManager_LocaleChange(object? sender, LocaleEventArgs e)
        {
            _manager?.UISynchronizer.RunSync(() =>
            {
                foreach (var relocalize in _trayRelocalizers)
                    relocalize();
            });
        }

        public void Dispose()
        {
            // Guard the unsubscribe (mirrors AppModuleNotifyIcon): if Initialize threw before
            // _localeManager was resolved, Dispose must not NRE and mask the original failure.
            if (_localeManager != null)
                _localeManager.OnLocaleChange -= OnEvent_LocaleManager_LocaleChange;
            _memoryManager.OnConnectionStateChanged -= Memory_OnConnectionStateChanged;
            _memoryManager.OnWindowFocusChanged -= Memory_OnWindowFocusChanged;
            _actorManager.OnCurrentPlayerChanged -= Actor_OnCurrentPlayerChanged;
            _configManager.RemovePropertyChangeListener(OnEvent_ConfigManager_PinnedChange);
            _configManager.RemovePropertyChangeListener(OnEvent_ConfigManager_FocusHideChange);
            _configManager.RemovePropertyChangeListener(OnEvent_ConfigManager_PositionChange);
            _overlay.LockStateChanged -= OnEvent_Overlay_LockChanged;
            _overlay.Browser.SettingsWindowClosed -= OnEvent_Browser_SettingsWindowClosed;

            // Safety net: persist the final frame on clean shutdown (the normal save happens on pin).
            var chatLocation = _overlay.Location;
            _configManager.SetProperty("behaviour.frame.chat.position.x", chatLocation.X);
            _configManager.SetProperty("behaviour.frame.chat.position.y", chatLocation.Y);

            var chatSize = _overlay.Size;
            _configManager.SetProperty("behaviour.frame.chat.size.width", chatSize.Width);
            _configManager.SetProperty("behaviour.frame.chat.size.height", chatSize.Height);

            _manager.UISynchronizer.RunSync(() => _overlay.Close());

            _manager.DisposeUIElement(OverlayUIId);

            _trayRelocalizers.Clear();

            _manager = null!;
            _overlay = null!;
            _configManager = null!;
            _memoryManager = null!;
            _actorManager = null!;
            _localeManager = null!;
            _pinMenuItem = null!;
        }
    }
}