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
    /// Pushed to the system overlay to show a one-off notification toast (used by the Debug settings page).
    /// </summary>
    public sealed class ShowNotificationWebEvent : Gobchat.UI.Web.JavascriptEvents.JSEvent
    {
        /// <summary>The text to display in the toast.</summary>
        public string message;

        public ShowNotificationWebEvent(string message) : base("ShowNotificationEvent")
        {
            this.message = message;
        }
    }
}
