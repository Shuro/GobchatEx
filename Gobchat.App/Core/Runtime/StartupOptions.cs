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
        /// Debug/developer mode (the <c>--settings</c> flag): keep the chat overlay hidden and open
        /// the settings dialog automatically, for quick access to the config UI while developing.
        /// </summary>
        public bool SettingsOnly { get; }

        public StartupOptions(bool settingsOnly)
        {
            SettingsOnly = settingsOnly;
        }
    }
}
