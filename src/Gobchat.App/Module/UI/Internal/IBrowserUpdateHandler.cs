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

using System.Threading.Tasks;

namespace Gobchat.Module.UI
{
    public interface IBrowserUpdateHandler
    {
        /// <summary>
        /// Runs the on-demand update check/flow (the same one the startup check uses) off the UI thread and
        /// returns a page-facing outcome code (the <c>UpdateOutcome</c> name, e.g. "UpToDate",
        /// "OpenedReleasePage", "Failed", "Busy"). For an installer build with an update this drives the
        /// confirm → download → apply-and-restart flow.
        /// </summary>
        Task<string> CheckForUpdates();
    }
}
