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
        /// <summary><see cref="MemoryReader.ConnectionState"/> ordinal: 0 none, 1 connected, 2 not found, 3 searching, 4 no access.</summary>
        public int state;

        /// <summary>The logged-in character's name, or <c>null</c> when logged out.</summary>
        public string player;

        public ConnectionStateWebEvent(int state, string player) : base("ConnectionStateEvent")
        {
            this.state = state;
            this.player = player;
        }
    }
}
