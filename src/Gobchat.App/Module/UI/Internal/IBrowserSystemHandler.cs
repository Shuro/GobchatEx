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
    public interface IBrowserSystemHandler
    {
        /// <summary>Shows a transient notification toast on the system overlay (no-op when it isn't present).</summary>
        Task ShowNotification(string message);

        /// <summary>Toggles the greeter splash on the system overlay (Debug page preview; no-op when absent).</summary>
        Task ToggleGreeter();
    }
}
