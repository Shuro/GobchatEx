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
        string? GetActivePlayerName();

        int GetPlayerCount();

        float GetDistanceToPlayerWithName(string name);

        string[] GetPlayersInArea();

        /// <summary>
        /// Marks that something (the settings range-filter preview / Debug nearby panel) needs nearby
        /// player positions right now. While this stays "alive" (renewed by a heartbeat) the actor worker
        /// scans positions even if no chat tab has the range filter enabled, so the preview is never
        /// starved. It self-expires shortly after the last call, so an abrupt settings-window close just
        /// lets it lapse rather than scanning forever.
        /// </summary>
        void TouchPreviewKeepalive();

        /// <summary>True while a recent <see cref="TouchPreviewKeepalive"/> has not yet expired.</summary>
        bool PreviewKeepaliveActive { get; }

        /// <summary>
        /// Raised when the locally logged-in character changes (login, logout, or switch). Fired on
        /// the actor update worker thread.
        /// </summary>
        event EventHandler<CurrentPlayerChangedEventArgs>? OnCurrentPlayerChanged;
    }
}