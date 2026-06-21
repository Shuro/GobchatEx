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

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Pushed to the overlay whenever the FFXIV connection state or the logged-in character changes,
    /// so it can show a "starting / waiting" banner and hide it once connected and logged in.
    /// </summary>
    public sealed class ConnectionStateWebEvent : Gobchat.UI.Web.JavascriptEvents.JSEvent
    {
        /// <summary><see cref="MemoryReader.ConnectionState"/> ordinal: 0 none, 1 connected, 2 not found, 3 searching, 4 no access, 5 outdated signatures.</summary>
        public int state;

        /// <summary>The logged-in character's name, or <c>null</c> when logged out.</summary>
        public string player;

        /// <summary>
        /// Localized greeter line for the current state, or <c>null</c> when the greeter should hide
        /// (connected). Pushed from the backend because the system overlay is intentionally
        /// GobchatAPI-/config-free and cannot localize on its own.
        /// </summary>
        public string greeterText;

        /// <summary>Localized login toast template; <c>{0}</c> is the character name.</summary>
        public string notifyLogin;

        /// <summary>Localized logout toast template; <c>{0}</c> is the character name.</summary>
        public string notifyLogout;

        /// <summary>Localized character-switch toast template; <c>{0}</c> is the new character name.</summary>
        public string notifySwitch;

        // The greeter/notify strings are only consumed by the system overlay; the chat-overlay pusher
        // (AppModuleMemoryToUI) sends just state+player and leaves them null.
        public ConnectionStateWebEvent(int state, string player, string greeterText = null,
            string notifyLogin = null, string notifyLogout = null, string notifySwitch = null) : base("ConnectionStateEvent")
        {
            this.state = state;
            this.player = player;
            this.greeterText = greeterText;
            this.notifyLogin = notifyLogin;
            this.notifyLogout = notifyLogout;
            this.notifySwitch = notifySwitch;
        }
    }
}
