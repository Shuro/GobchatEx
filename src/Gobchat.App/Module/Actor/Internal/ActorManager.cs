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
using System.Collections.Generic;
using System.Linq;
using Gobchat.Core.Chat;
using Gobchat.Memory.Actor;

namespace Gobchat.Module.Actor.Internal
{
    internal sealed class ActorManager : IActorManager
    {
        private sealed class Data
        {
            public DateTime LastUpdateTime;
            public PlayerCharacter Actor;
        }

        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // Consecutive empty current-player reads before a logout is declared, so a single transient
        // miss between two valid reads of the same character does not flap login/logout.
        private const int LogoutDebounceThreshold = 2;

        private readonly Dictionary<string, Data> _realm = new Dictionary<string, Data>();
        private readonly Queue<Data> _pendingUpdates = new Queue<Data>();

        private string _currentPlayerName; // null = logged out
        private int _logoutDebounce;

        public bool IsAvailable { get; internal set; }
        public TimeSpan OutdatedTimelimit { get; set; } = TimeSpan.FromSeconds(3);

        public event EventHandler<CurrentPlayerChangedEventArgs> OnCurrentPlayerChanged;

        public int GetPlayerCount()
        {
            lock (_realm)
            {
                return _realm.Count;
            }
        }

        public string GetActivePlayerName()
        {
            lock (_realm)
            {
                return _currentPlayerName;
            }
        }

        /// <summary>
        /// Feeds the reliable current player (from <c>GetCurrentPlayer</c>) each poll. Diffs it against
        /// the last known character and raises <see cref="OnCurrentPlayerChanged"/> on login, logout
        /// (after a short debounce), or switch. Pass <c>null</c> when disconnected / at title screen.
        /// </summary>
        public void SetCurrentPlayer(CurrentPlayer player)
        {
            var newName = player?.Name;
            CurrentPlayerChangedEventArgs change = null;

            lock (_realm)
            {
                if (newName == null)
                {
                    if (_currentPlayerName != null && ++_logoutDebounce >= LogoutDebounceThreshold)
                    {
                        change = new CurrentPlayerChangedEventArgs(_currentPlayerName, null);
                        _currentPlayerName = null;
                        _logoutDebounce = 0;
                    }
                }
                else
                {
                    _logoutDebounce = 0;
                    if (!string.Equals(_currentPlayerName, newName, StringComparison.Ordinal))
                    {
                        change = new CurrentPlayerChangedEventArgs(_currentPlayerName, newName);
                        _currentPlayerName = newName;
                    }
                }
            }

            if (change != null)
            {
                logger.Debug($"Current player changed: '{change.PreviousPlayerName}' -> '{change.CurrentPlayerName}'");
                OnCurrentPlayerChanged?.Invoke(this, change);
            }
        }

        public string[] GetPlayersInArea()
        {
            lock (_realm)
            {
                return _realm.Values.Select(data => data.Actor.Name).ToArray();
            }
        }

        public float GetDistanceToPlayerWithName(string name)
        {
            if (name == null)
                return 0;

            name = ChatUtil.StripServerName(name);

            lock (_realm)
            {
                if (_realm.TryGetValue(name.ToUpperInvariant(), out var storedData))
                    return storedData.Actor.DistanceToPlayer;
                return 0;
            }
        }

        internal void AddUpdate(IEnumerable<PlayerCharacter> actors)
        {
            var updateTime = DateTime.Now;

            var updates = actors
                .Where(e => e.Flag != PlayerCharacter.UpdateFlag.Remove)
                .Select(e => new Data() { LastUpdateTime = updateTime, Actor = e });

            lock (_realm)
            {
                foreach (var update in updates)
                    _pendingUpdates.Enqueue(update);
            }
        }

        internal void UpdateManager()
        {
            lock (_realm)
            {
                _realm.Clear();

                foreach (var newData in _pendingUpdates)
                {
                    var key = newData.Actor.Name.ToUpperInvariant(); // names can have a different capitalization than seen in the chat window!

                    if (_realm.TryGetValue(key, out var oldData))
                    {
                        //if (newData.LastUpdateTime > oldData.LastUpdateTime)
                        if (newData.Actor.DistanceToPlayer < oldData.Actor.DistanceToPlayer)
                            _realm[key] = newData;
                    }
                    else
                    {
                        _realm[key] = newData;
                    }
                }

                _pendingUpdates.Clear();
            }
        }
    }
}