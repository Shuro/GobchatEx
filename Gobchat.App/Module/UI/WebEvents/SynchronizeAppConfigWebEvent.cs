/*******************************************************************************
 * Copyright (C) 2020 MarbleBag
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
    /// Signals the page that an application-global setting changed (theme, language, …). The page
    /// re-reads the app settings via <c>GobchatAPI.getAppSettingsAsJson()</c>. Mirrors
    /// <see cref="SynchronizeConfigWebEvent"/>, but for the separate app-settings store.
    /// </summary>
    public sealed class SynchronizeAppConfigWebEvent : Gobchat.UI.Web.JavascriptEvents.JSEvent
    {
        public SynchronizeAppConfigWebEvent() : base("SynchronizeAppConfigEvent")
        {
        }
    }
}
