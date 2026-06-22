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

using Gobchat.Memory;
using Gobchat.Memory.Actor;
using Gobchat.Memory.Chat;
using System;
using System.Collections.Generic;

namespace Gobchat.Module.MemoryReader
{
    internal interface IMemoryReaderManager
    {
        ConnectionState ConnectionState { get; }

        bool IsConnected { get; }

        void ConnectTo(int processId);

        int ConnectedProcessId { get; }

        List<int> GetProcessIds();

        bool ChatLogAvailable { get; }

        bool PlayerCharactersAvailable { get; }

        /// <summary>
        /// True if an FFXIV process is running but cannot be read because it is more elevated than we are.
        /// </summary>
        bool IsBlockedByElevation { get; }

        bool ObserveGameWindow { get; set; }

        /// <summary>
        /// Brings the connected FFXIV window to the foreground, e.g. to hand focus back to the game
        /// after a GobchatEx window closes so the focus-based auto-hide keeps the overlay visible.
        /// No-op when not connected.
        /// </summary>
        void FocusGameWindow();

        event EventHandler<WindowFocusChangedEventArgs>? OnWindowFocusChanged;

        event EventHandler<ConnectionEventArgs>? OnConnectionStateChanged;

        List<PlayerCharacter> GetPlayerCharacters();

        /// <summary>
        /// The locally logged-in character, or <c>null</c> when disconnected or at the title /
        /// character-select screen.
        /// </summary>
        CurrentPlayer? GetCurrentPlayer();

        List<ChatlogItem> GetNewestChatlog();
    }

    public enum ConnectionState
    {
        NotInitialized,
        Connected,
        NotFound,
        Searching,

        /// <summary>
        /// An FFXIV process is running but its memory cannot be read because it is more elevated
        /// than GobchatEx (e.g. FFXIV was started as administrator). Restarting GobchatEx as
        /// administrator resolves it. Appended last on purpose to keep the existing ordinals
        /// (the JS bridge reads these as plain numbers).
        /// </summary>
        NoAccess,

        /// <summary>
        /// Attached to an FFXIV process, but the memory signatures Gobchat needs were not found -
        /// the downloaded signature data is outdated for this game build, so chat/actor data cannot
        /// be read. Reported instead of <see cref="Connected"/> so the UI doesn't claim a working
        /// connection. Appended last on purpose to keep the existing ordinals.
        /// </summary>
        OutdatedSignatures
    }

    public sealed class ConnectionEventArgs : EventArgs
    {
        public ConnectionState State { get; }

        public bool IsConnected { get => State == ConnectionState.Connected; }

        public ConnectionEventArgs(ConnectionState state)
        {
            State = state;
        }
    }
}