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

using System.Collections.Generic;
using Gobchat.Memory.Actor;
using Sharlayan.Core;
using Xunit;
using ActorType = Sharlayan.Core.Enums.Actor.Type;

namespace Gobchat.Memory.Tests.Actor
{
    /// <summary>
    /// Verifies the actor-to-PlayerCharacter projection (the unit-testable core of the memory
    /// reader). WHY this matters: the range filter fades chat by this distance, and FFXIV reports
    /// distance center-to-hitbox-edge, so the reader must subtract the target's hitbox radius —
    /// getting this wrong shifts every fade threshold. The live read needs the game and stays
    /// out of scope; only the pure projection is exercised here with hand-built Sharlayan actors.
    /// </summary>
    public sealed class PlayerLocationMemoryReaderTests
    {
        // ProcessConnector is only used by the live-read entry point, not by Process(...).
        private static PlayerLocationMemoryReader NewReader() => new PlayerLocationMemoryReader(null!);

        private static ActorItem Actor(uint id, string name, ActorType type, double x, double y, double z, float hitbox = 0.5f)
        {
            var coordinate = new Coordinate(0, 0, 0);
            coordinate.X = x;
            coordinate.Y = y;
            coordinate.Z = z;
            return new ActorItem
            {
                ID = id,
                Name = name,
                UUID = "uuid-" + id,
                Type = type,
                Coordinate = coordinate,
                HitBoxRadius = hitbox,
            };
        }

        private static Coordinate At(double x, double y, double z)
        {
            var c = new Coordinate(0, 0, 0);
            c.X = x; c.Y = y; c.Z = z;
            return c;
        }

        [Fact]
        public void Process_ComputesEdgeDistanceAndCopiesIdentity()
        {
            // self at origin, PC at (3,4,0) -> 3D center distance 5; minus 0.50 hitbox = 4.50.
            var results = new List<PlayerCharacter>();
            NewReader().Process(
                new[] { Actor(42, "Vtorak Azora", ActorType.PC, 3, 4, 0) },
                PlayerCharacter.UpdateFlag.Update,
                At(0, 0, 0),
                results);

            var pc = Assert.Single(results);
            Assert.Equal("Vtorak Azora", pc.Name);
            Assert.Equal(42u, pc.Id);
            Assert.Equal("uuid-42", pc.UId);
            Assert.Equal(PlayerCharacter.UpdateFlag.Update, pc.Flag);
            Assert.Equal(4.5, pc.DistanceToPlayer, 2);
        }

        [Theory]
        // 3D center distance (with hitbox 0.50) -> the in-game value FFXIV shows (FINDINGS.md snapshot).
        [InlineData(0.77, 0.27)]
        [InlineData(3.18, 2.68)]
        [InlineData(5.00, 4.50)]
        [InlineData(7.38, 6.88)]
        public void Process_EdgeDistance_MatchesInGameValue(double center3D, double expectedInGame)
        {
            var results = new List<PlayerCharacter>();
            NewReader().Process(
                new[] { Actor(1, "PC", ActorType.PC, center3D, 0, 0) },
                PlayerCharacter.UpdateFlag.Update,
                At(0, 0, 0),
                results);

            Assert.Equal(expectedInGame, Assert.Single(results).DistanceToPlayer, 2);
        }

        [Fact]
        public void Process_OverlappingHitbox_ClampsDistanceToZero()
        {
            // Closer than the hitbox radius would give a negative edge distance; it must clamp to 0.
            var results = new List<PlayerCharacter>();
            NewReader().Process(
                new[] { Actor(1, "PC", ActorType.PC, 0.3, 0, 0) },
                PlayerCharacter.UpdateFlag.Update,
                At(0, 0, 0),
                results);

            Assert.Equal(0f, Assert.Single(results).DistanceToPlayer);
        }

        [Fact]
        public void Process_NoActivePlayerPosition_LeavesDistanceZero()
        {
            var results = new List<PlayerCharacter>();
            NewReader().Process(
                new[] { Actor(1, "PC", ActorType.PC, 3, 4, 0) },
                PlayerCharacter.UpdateFlag.New,
                mainActorPosition: null,
                results);

            Assert.Equal(0f, Assert.Single(results).DistanceToPlayer);
        }

        [Fact]
        public void Process_SkipsNonPlayerAndInvalidActors()
        {
            var npc = Actor(2, "Some NPC", ActorType.NPC, 1, 1, 0);
            var invalid = new ActorItem { Type = ActorType.PC }; // no ID/Name -> IsValid == false
            var results = new List<PlayerCharacter>();

            NewReader().Process(
                new[] { Actor(1, "Real PC", ActorType.PC, 1, 0, 0), npc, invalid },
                PlayerCharacter.UpdateFlag.Update,
                At(0, 0, 0),
                results);

            Assert.Equal("Real PC", Assert.Single(results).Name);
        }
    }
}
