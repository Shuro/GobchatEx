/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
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

        event EventHandler<WindowFocusChangedEventArgs> OnWindowFocusChanged;

        event EventHandler<ConnectionEventArgs> OnConnectionStateChanged;

        List<PlayerCharacter> GetPlayerCharacters();

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
        NoAccess
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