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
    public interface IActorManager
    {
        bool IsAvailable { get; }

        /// <summary>The locally logged-in character's name, or <c>null</c> when logged out.</summary>
        string GetActivePlayerName();

        int GetPlayerCount();

        float GetDistanceToPlayerWithName(string name);

        string[] GetPlayersInArea();

        /// <summary>
        /// Raised when the locally logged-in character changes (login, logout, or switch). Fired on
        /// the actor update worker thread.
        /// </summary>
        event EventHandler<CurrentPlayerChangedEventArgs> OnCurrentPlayerChanged;
    }
}