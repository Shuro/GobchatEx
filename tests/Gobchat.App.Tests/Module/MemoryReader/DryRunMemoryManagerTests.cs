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
using Gobchat.Module.MemoryReader;
using Gobchat.Module.MemoryReader.Internal;
using System;
using System.Linq;
using System.Threading;
using Xunit;

namespace Gobchat.App.Tests.Module.MemoryReader
{
    /// <summary>
    /// The dry-run fake stands in for the FFXIV memory manager so the real actor/login/range pipeline
    /// runs with no game. WHY these behaviours matter: the Debug page re-adds a character to *reposition*
    /// it (so keying is case-insensitive and re-place must overwrite distance), the range-filter fade
    /// reads the projected distances (so negatives must clamp and the sort must be nearest-first), and the
    /// greeter "flash" depends on the fake starting <see cref="ConnectionState.Searching"/> and then
    /// auto-connecting once - without that, the downstream pipeline would never see a connected game.
    /// </summary>
    public sealed class DryRunMemoryManagerTests
    {
        // --- roster (IDryRunController) -------------------------------------------------------------

        [Fact]
        public void AddCharacter_PlacesCharacter_InRosterAndProjection()
        {
            using var mgr = new DryRunMemoryManager();

            mgr.AddCharacter("Alice", 5f);

            var roster = mgr.GetRoster();
            Assert.Single(roster);
            Assert.Equal("Alice", roster[0].Name);
            Assert.Equal(5f, roster[0].Distance);

            // Projected to the actor pipeline as a "New" sighting carrying the same name + distance.
            var actors = mgr.GetPlayerCharacters();
            Assert.Single(actors);
            Assert.Equal("Alice", actors[0].Name);
            Assert.Equal(5f, actors[0].DistanceToPlayer);
            Assert.Equal(PlayerCharacter.UpdateFlag.New, actors[0].Flag);
        }

        [Fact]
        public void AddCharacter_IsCaseInsensitive_RePlaceOverwritesNameAndDistance()
        {
            // WHY: "Alice" and "alice" are the same person; re-adding repositions rather than duplicates.
            using var mgr = new DryRunMemoryManager();

            mgr.AddCharacter("Alice", 5f);
            mgr.AddCharacter("alice", 12f);

            var roster = mgr.GetRoster();
            Assert.Single(roster);
            Assert.Equal("alice", roster[0].Name);   // latest display name wins
            Assert.Equal(12f, roster[0].Distance);    // latest distance wins
        }

        [Fact]
        public void AddCharacter_ClampsNegativeDistanceToZero()
        {
            // WHY: distance feeds the range-filter fade; a negative would be a nonsensical input.
            using var mgr = new DryRunMemoryManager();

            mgr.AddCharacter("Alice", -3f);

            Assert.Equal(0f, mgr.GetRoster().Single().Distance);
        }

        [Fact]
        public void AddCharacter_TrimsName()
        {
            using var mgr = new DryRunMemoryManager();

            mgr.AddCharacter("  Alice  ", 5f);

            Assert.Equal("Alice", mgr.GetRoster().Single().Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddCharacter_IgnoresNullOrWhitespaceName(string name)
        {
            using var mgr = new DryRunMemoryManager();

            mgr.AddCharacter(name, 5f);

            Assert.Empty(mgr.GetRoster());
        }

        [Fact]
        public void RemoveCharacter_IsCaseInsensitive()
        {
            using var mgr = new DryRunMemoryManager();
            mgr.AddCharacter("Alice", 5f);

            mgr.RemoveCharacter("ALICE");

            Assert.Empty(mgr.GetRoster());
            Assert.Empty(mgr.GetPlayerCharacters());
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RemoveCharacter_IgnoresNullOrWhitespaceName(string name)
        {
            using var mgr = new DryRunMemoryManager();
            mgr.AddCharacter("Alice", 5f);

            mgr.RemoveCharacter(name);

            Assert.Single(mgr.GetRoster());
        }

        [Fact]
        public void GetRoster_SortsByDistanceThenNameIgnoringCase()
        {
            using var mgr = new DryRunMemoryManager();
            mgr.AddCharacter("Carol", 10f);
            mgr.AddCharacter("bob", 5f);
            mgr.AddCharacter("Alice", 5f);

            var names = mgr.GetRoster().Select(c => c.Name).ToArray();

            // Nearest first; ties broken by name (ordinal-ignore-case, so "Alice" before "bob").
            Assert.Equal(new[] { "Alice", "bob", "Carol" }, names);
        }

        // --- current player (IDryRunController + GetCurrentPlayer) ----------------------------------

        [Fact]
        public void Initially_NoCurrentPlayer()
        {
            using var mgr = new DryRunMemoryManager();

            Assert.Null(mgr.CurrentPlayer);
            Assert.Null(mgr.GetCurrentPlayer());
        }

        [Fact]
        public void Connect_SetsTrimmedCurrentPlayer()
        {
            using var mgr = new DryRunMemoryManager();

            mgr.Connect("  Hero  ");

            Assert.Equal("Hero", mgr.CurrentPlayer);
            Assert.Equal("Hero", mgr.GetCurrentPlayer().Name);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Connect_IgnoresNullOrWhitespace(string name)
        {
            using var mgr = new DryRunMemoryManager();

            mgr.Connect(name);

            Assert.Null(mgr.CurrentPlayer);
            Assert.Null(mgr.GetCurrentPlayer());
        }

        [Fact]
        public void Disconnect_ClearsCurrentPlayer()
        {
            using var mgr = new DryRunMemoryManager();
            mgr.Connect("Hero");

            mgr.Disconnect();

            Assert.Null(mgr.CurrentPlayer);
            Assert.Null(mgr.GetCurrentPlayer());
        }

        // --- connection contract (IMemoryReaderManager) ---------------------------------------------

        [Fact]
        public void Initially_Searching_AndNotAvailable()
        {
            // WHY: the greeter is shown while Searching; the chat overlay/actor reads stay gated until
            // the fake "connects", so all availability flags must start false.
            using var mgr = new DryRunMemoryManager();

            Assert.Equal(ConnectionState.Searching, mgr.ConnectionState);
            Assert.False(mgr.IsConnected);
            Assert.False(mgr.ChatLogAvailable);
            Assert.False(mgr.PlayerCharactersAvailable);
        }

        [Fact]
        public void FixedContractValues_MatchAFakeWithNoRealProcessOrChat()
        {
            using var mgr = new DryRunMemoryManager();

            Assert.False(mgr.IsBlockedByElevation);
            Assert.Equal(-1, mgr.ConnectedProcessId);
            Assert.Empty(mgr.GetProcessIds());
            Assert.Empty(mgr.GetNewestChatlog());
        }

        [Fact]
        public void AutoConnects_Once_RaisingConnectedAndFlippingAvailability()
        {
            // WHY: the whole point of the fake is to drive a connected game without FFXIV - the one-shot
            // timer is the "greeter flash" (Searching -> Connected). The downstream pipeline only starts
            // reading actors/chat once this fires, so it must happen and be observable exactly once.
            using var mgr = new DryRunMemoryManager();
            var connected = new ManualResetEventSlim(false);
            var raisedCount = 0;
            ConnectionEventArgs captured = null;
            mgr.OnConnectionStateChanged += (_, e) =>
            {
                Interlocked.Increment(ref raisedCount);
                captured = e;
                if (e.State == ConnectionState.Connected)
                    connected.Set();
            };

            // Generous timeout: the internal delay is well under a second, this only guards CI jitter.
            Assert.True(connected.Wait(TimeSpan.FromSeconds(5)), "Dry-run manager never auto-connected");

            Assert.Equal(ConnectionState.Connected, captured.State);
            Assert.True(captured.IsConnected);
            Assert.Equal(1, raisedCount);

            Assert.True(mgr.IsConnected);
            Assert.True(mgr.ChatLogAvailable);
            Assert.True(mgr.PlayerCharactersAvailable);
        }
    }
}
