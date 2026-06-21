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

namespace Gobchat.Core.Runtime
{
    /// <summary>
    /// Immutable startup options derived from the command line, registered in the application
    /// <see cref="DIContext"/> so modules can adapt their behavior on start up.
    /// </summary>
    public sealed class StartupOptions
    {
        /// <summary>
        /// Dry-run developer mode (the <c>--dry-run</c> flag): do not attach to FFXIV / Sharlayan at
        /// all. A fake memory manager simulates a connected game (the greeter flashes, then chat
        /// follows normal pin/login visibility) and the settings dialog auto-opens on the Debug page,
        /// whose Dry Run section can inject characters, positions, logins, and chat messages by hand.
        /// </summary>
        public bool DryRun { get; }

        public StartupOptions(bool dryRun)
        {
            DryRun = dryRun;
        }
    }
}
