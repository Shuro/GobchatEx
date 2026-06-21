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

using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Module.Chat;
using Gobchat.Module.Hotkey;
using Gobchat.Module.Overlay;
using Gobchat.Module.UI;
using Gobchat.UI.Forms;
using Gobchat.UI.Web;

namespace Gobchat.Module.Misc
{
    public sealed class AppModuleSearchHotkey : IApplicationModule
    {
        private readonly JavascriptBuilder _jsBuilder = new JavascriptBuilder();

        private ConfigHotkeyUpdater _hkSearch;

        /// <summary>
        /// Requires: <see cref="IConfigManager"/> <br></br>
        /// Requires: <see cref="IHotkeyManager"/> <br></br>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// Requires: <see cref="IChatManager"/> <br></br>
        /// <br></br>
        /// Adds a hotkey that focuses the chat overlay and opens its search bar with the cursor
        /// in the search field, so the user can type immediately.
        /// </summary>
        public AppModuleSearchHotkey()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            var uiManager = container.Resolve<IUIManager>();

            _hkSearch = new ConfigHotkeyUpdater(
                "behaviour.hotkeys.search",
                Resources.Module_Misc_Hotkey_Search,
                container.Resolve<IConfigManager>(),
                "behaviour.hotkeys.search",
                container.Resolve<IHotkeyManager>(),
                container.Resolve<IChatManager>());

            _hkSearch.OnHotkey += () =>
            {
                if (uiManager.TryGetUIElement<OverlayForm>(AppModuleChatOverlay.OverlayUIId, out var overlay))
                    uiManager.UISynchronizer.RunSync(() =>
                    {
                        // Search only makes sense while the overlay is on screen. When it's hidden
                        // (logged out and not pinned) do nothing, rather than force it visible out of
                        // sync with the pin/login visibility model (AppModuleChatOverlay owns that).
                        // Use the tray/pin to show the overlay, then the search hotkey works normally.
                        if (!overlay.Visible)
                            return;

                        // Focus the overlay (and route OS keyboard to the WebView2) first, then tell the
                        // page to open search + focus the input. The click-through/lock state is untouched.
                        overlay.ActivateForInput();
                        var script = _jsBuilder.BuildCustomEventDispatcher(new FocusSearchWebEvent());
                        overlay.Browser.ExecuteScript(script);
                    });
            };
        }

        public void Dispose()
        {
            _hkSearch?.Dispose();
        }
    }
}
