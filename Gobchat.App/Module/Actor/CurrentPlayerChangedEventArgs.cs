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

namespace Gobchat.Module.Actor
{
    /// <summary>
    /// Raised when the locally logged-in character changes. The three cases are distinguished by
    /// which name is null: login (<see cref="PreviousPlayerName"/> null), logout
    /// (<see cref="CurrentPlayerName"/> null), or a character switch (both non-null and different).
    /// </summary>
    public sealed class CurrentPlayerChangedEventArgs : EventArgs
    {
        /// <summary>The character logged in just before this change, or <c>null</c> if none.</summary>
        public string PreviousPlayerName { get; }

        /// <summary>The character now logged in, or <c>null</c> if logged out.</summary>
        public string CurrentPlayerName { get; }

        public CurrentPlayerChangedEventArgs(string previousPlayerName, string currentPlayerName)
        {
            PreviousPlayerName = previousPlayerName;
            CurrentPlayerName = currentPlayerName;
        }
    }
}
