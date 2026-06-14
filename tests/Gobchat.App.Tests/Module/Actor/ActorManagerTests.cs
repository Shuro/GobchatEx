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

using Gobchat.Memory.Actor;
using Gobchat.Module.Actor.Internal;
using Xunit;

namespace Gobchat.App.Tests.Module.Actor
{
    /// <summary>
    /// The actor manager maintains the "name -> nearest player" realm the range filter queries.
    /// WHY this matters: the same character can appear multiple times per memory poll, names vary in
    /// case and carry a server suffix in chat, and the filter must use the *closest* sighting — so
    /// dedup-by-closest and case/server-insensitive lookup are the behaviours that keep fades correct.
    /// </summary>
    public sealed class ActorManagerTests
    {
        private static PlayerCharacter Pc(string name, float distance,
            PlayerCharacter.UpdateFlag flag = PlayerCharacter.UpdateFlag.Update, bool isUser = false, uint id = 0)
        {
            return new PlayerCharacter
            {
                Name = name,
                DistanceToPlayer = distance,
                Flag = flag,
                IsUser = isUser,
                Id = id,
            };
        }

        private static ActorManager ManagerWith(params PlayerCharacter[] actors)
        {
            var manager = new ActorManager();
            manager.AddUpdate(actors);
            manager.UpdateManager();
            return manager;
        }

        [Fact]
        public void GetDistanceToPlayerWithName_ReturnsStoredDistance()
        {
            var manager = ManagerWith(Pc("Alice", 5f));

            Assert.Equal(5f, manager.GetDistanceToPlayerWithName("Alice"));
            Assert.Equal(1, manager.GetPlayerCount());
        }

        [Fact]
        public void UpdateManager_DeduplicatesByName_KeepingClosest()
        {
            var manager = ManagerWith(Pc("Alice", 10f), Pc("Alice", 3f));

            Assert.Equal(1, manager.GetPlayerCount());
            Assert.Equal(3f, manager.GetDistanceToPlayerWithName("Alice"));
        }

        [Fact]
        public void GetDistanceToPlayerWithName_IgnoresCaseAndServerSuffix()
        {
            var manager = ManagerWith(Pc("Alice", 7f));

            Assert.Equal(7f, manager.GetDistanceToPlayerWithName("alice[Gilgamesh]"));
        }

        [Fact]
        public void GetDistanceToPlayerWithName_UnknownOrNull_ReturnsZero()
        {
            var manager = ManagerWith(Pc("Alice", 7f));

            Assert.Equal(0f, manager.GetDistanceToPlayerWithName("Bob"));
            Assert.Equal(0f, manager.GetDistanceToPlayerWithName(null!));
        }

        [Fact]
        public void AddUpdate_ExcludesRemovedActors()
        {
            var manager = ManagerWith(
                Pc("Alice", 5f),
                Pc("Ghost", 2f, flag: PlayerCharacter.UpdateFlag.Remove));

            Assert.Equal(1, manager.GetPlayerCount());
            Assert.Equal(0f, manager.GetDistanceToPlayerWithName("Ghost"));
        }

        [Fact]
        public void UpdateManager_TracksActivePlayer()
        {
            var manager = ManagerWith(
                Pc("Hero", 0f, isUser: true, id: 42),
                Pc("Alice", 5f));

            Assert.Equal("Hero", manager.GetActivePlayerName());
        }

        [Fact]
        public void GetPlayersInArea_ReturnsTrackedNames()
        {
            var manager = ManagerWith(Pc("Alice", 5f), Pc("Bob", 6f));

            var names = manager.GetPlayersInArea();
            Assert.Equal(2, names.Length);
            Assert.Contains("Alice", names);
            Assert.Contains("Bob", names);
        }
    }
}
