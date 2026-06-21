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
using System.Linq;
using System.Threading;

namespace Gobchat.Module.MemoryReader.Internal
{
    /// <summary>
    /// A fake <see cref="IMemoryReaderManager"/> for the <c>--dry-run</c> developer mode: it never
    /// attaches to FFXIV / Sharlayan. Instead it simulates a connected game so the whole downstream
    /// pipeline (actors, distance/range-filter fade, login/greeter transitions) can be exercised without
    /// the game. It starts <see cref="ConnectionState.Searching"/>, then a one-shot timer flips it to
    /// <see cref="ConnectionState.Connected"/> (the requested greeter "flash"). The synthetic actor
    /// roster and current player are driven by hand through <see cref="IDryRunController"/> (the Debug
    /// settings page). Chat is injected elsewhere via <c>IChatManager</c>, not through this manager.
    /// </summary>
    internal sealed class DryRunMemoryManager : IMemoryReaderManager, IDryRunController, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // The Searching -> Connected delay. It must outlast the system-overlay page load so the greeter
        // is visibly shown (Searching) and then hidden (Connected) - the requested "flash".
        private const int ConnectDelayMs = 1500;

        // Guards all mutable state below (roster, current player, connection state writes via the helper).
        private readonly object _lock = new object();

        // Roster keyed by the upper-invariant name; the value preserves the display name + distance.
        private readonly Dictionary<string, DryRunCharacter> _roster = new Dictionary<string, DryRunCharacter>();

        // The logged-in character, or null when "logged out".
        private string _currentPlayer;

        private volatile ConnectionState _connectionState = ConnectionState.Searching;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0069:Disposable fields should be disposed", Justification = "Disposed in Dispose")]
        private Timer _connectTimer;

        public DryRunMemoryManager()
        {
            logger.Info("Dry-run mode: using a fake memory manager (no FFXIV attach)");
            // One-shot: fire once after the delay, then never again (Timeout.Infinite period).
            _connectTimer = new Timer(_ => SetConnectionState(ConnectionState.Connected), null, ConnectDelayMs, Timeout.Infinite);
        }

        // No-op if unchanged, else set and raise the event (mirrors FFXIVMemoryManager.SetConnectionState).
        private void SetConnectionState(ConnectionState state)
        {
            if (_connectionState == state)
                return;
            _connectionState = state;
            OnConnectionStateChanged?.Invoke(this, new ConnectionEventArgs(state));
        }

        #region IMemoryReaderManager

        public ConnectionState ConnectionState => _connectionState;

        public bool IsConnected => _connectionState == ConnectionState.Connected;

        public bool ChatLogAvailable => IsConnected;

        public bool PlayerCharactersAvailable => IsConnected;

        public bool IsBlockedByElevation => false;

        public int ConnectedProcessId => -1;

        public bool ObserveGameWindow { get; set; }

        // No game process in dry-run mode; nothing to focus.
        public void FocusGameWindow() { }

        // Never raised: the dry-run overlay is not driven by game-window focus.
        public event EventHandler<WindowFocusChangedEventArgs> OnWindowFocusChanged;

        public event EventHandler<ConnectionEventArgs> OnConnectionStateChanged;

        public List<int> GetProcessIds() => new List<int>();

        public void ConnectTo(int processId)
        {
            // No real process to attach to in dry-run.
        }

        public List<PlayerCharacter> GetPlayerCharacters()
        {
            lock (_lock)
            {
                // Project the placed roster to PlayerCharacters. Name/DistanceToPlayer/Flag have internal
                // setters; the InternalsVisibleTo("GobchatEx") on Gobchat.Memory makes them reachable here.
                return _roster.Values
                    .Select(c => new PlayerCharacter
                    {
                        Name = c.Name,
                        DistanceToPlayer = c.Distance,
                        Flag = PlayerCharacter.UpdateFlag.New,
                    })
                    .ToList();
            }
        }

        public CurrentPlayer GetCurrentPlayer()
        {
            lock (_lock)
            {
                return _currentPlayer == null ? null : new CurrentPlayer { Name = _currentPlayer };
            }
        }

        // Chat is injected via IChatManager (the dry-run send path), not read from here.
        public List<ChatlogItem> GetNewestChatlog() => new List<ChatlogItem>();

        #endregion IMemoryReaderManager

        #region IDryRunController

        public string CurrentPlayer
        {
            get
            {
                lock (_lock)
                {
                    return _currentPlayer;
                }
            }
        }

        public void Connect(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            lock (_lock)
            {
                _currentPlayer = name.Trim();
            }
        }

        public void Disconnect()
        {
            lock (_lock)
            {
                _currentPlayer = null;
            }
        }

        public void AddCharacter(string name, float distance)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            var trimmed = name.Trim();
            var clamped = distance < 0 ? 0 : distance;
            lock (_lock)
            {
                _roster[trimmed.ToUpperInvariant()] = new DryRunCharacter(trimmed, clamped);
            }
        }

        public void RemoveCharacter(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            lock (_lock)
            {
                _roster.Remove(name.Trim().ToUpperInvariant());
            }
        }

        public IReadOnlyList<DryRunCharacter> GetRoster()
        {
            lock (_lock)
            {
                return _roster.Values
                    .OrderBy(c => c.Distance)
                    .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        #endregion IDryRunController

        public void Dispose()
        {
            _connectTimer?.Dispose();
            _connectTimer = null;
        }
    }
}
