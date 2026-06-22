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

using System;

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Pushed to the chat overlay to run a genuinely UI-side <c>/e gc</c> command whose effect lives in the
    /// page (info/error on-off, config open). C# owns detecting and dispatching every <c>gc</c> command;
    /// these few are forwarded here instead of executed, and the page maps the command name to the matching
    /// action. The <c>command</c> field serializes (camel-cased) to <c>event.detail.command</c>.
    /// </summary>
    public sealed class ExecuteUiCommandWebEvent : Gobchat.UI.Web.JavascriptEvents.JSEvent
    {
        public string command;

        public ExecuteUiCommandWebEvent(string command) : base("ExecuteUiCommandEvent")
        {
            this.command = command ?? throw new ArgumentNullException(nameof(command));
        }
    }
}
