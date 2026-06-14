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

using Gobchat.App.Tests.Fakes;
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// The range filter fades chat by sender distance. WHY this matters: this is the exact mapping a
    /// roleplayer sees as messages dim with distance — full opacity within the fade-out radius, a
    /// linear ramp to the cut-off, then hidden. The "unknown sender stays fully visible" rule
    /// (distance 0) is a deliberate, documented behaviour (docs/range-filter-todo.md) and is pinned here.
    /// </summary>
    public sealed class ChatMessageActorDataSetterTests
    {
        private static ChatMessage Message(ChatChannel channel = ChatChannel.Say, string character = "Alice", bool isPlayer = true)
        {
            return new ChatMessage
            {
                Channel = channel,
                Source = new ChatMessageSource("orig") { CharacterName = character, IsAPlayer = isPlayer },
            };
        }

        private static ChatMessageActorDataSetter Setter(FakeActorManager actorManager, float cutOff = 24, float fadeOut = 16)
        {
            return new ChatMessageActorDataSetter(actorManager)
            {
                SetVisibility = true,
                CutOffDistance = cutOff,
                FadeOutDistance = fadeOut,
                CutOffChannels = new[] { ChatChannel.Say },
            };
        }

        [Theory]
        [InlineData(10f, 100)] // inside fade-out radius -> fully visible
        [InlineData(16f, 100)] // exactly at fade-out -> fully visible
        [InlineData(20f, 50)]  // midway between fade-out (16) and cut-off (24) -> 50%
        [InlineData(24f, 0)]   // exactly at cut-off -> hidden
        [InlineData(30f, 0)]   // beyond cut-off -> hidden
        public void SetActorData_FadesVisibilityByDistance(float distance, int expectedVisibility)
        {
            var actors = new FakeActorManager { DistanceProvider = _ => distance };
            var message = Message();

            Setter(actors).SetActorData(message);

            Assert.Equal(expectedVisibility, message.Source.Visibility);
        }

        [Fact]
        public void SetActorData_UnknownSender_StaysFullyVisible()
        {
            // Distance 0 means "not in the tracked nearby-PC realm"; such senders are left fully visible.
            var actors = new FakeActorManager { DistanceProvider = _ => 0f };
            var message = Message();

            Setter(actors).SetActorData(message);

            Assert.Equal(100, message.Source.Visibility);
        }

        [Fact]
        public void SetActorData_ChannelNotRangeFiltered_IsNotFaded()
        {
            var actors = new FakeActorManager { DistanceProvider = _ => 999f };
            var message = Message(channel: ChatChannel.Party); // not in CutOffChannels

            Setter(actors).SetActorData(message);

            Assert.Equal(100, message.Source.Visibility);
        }

        [Fact]
        public void SetActorData_VisibilityDisabled_DoesNotFade()
        {
            var actors = new FakeActorManager { DistanceProvider = _ => 999f };
            var setter = Setter(actors);
            setter.SetVisibility = false;
            var message = Message();

            setter.SetActorData(message);

            Assert.Equal(100, message.Source.Visibility);
        }

        [Fact]
        public void SetActorData_FadeOutNotBelowCutOff_StaysFullyVisible()
        {
            // Degenerate config (fade-out >= cut-off): avoid divide-by-zero / inversion, stay visible.
            var actors = new FakeActorManager { DistanceProvider = _ => 16f };
            var message = Message();

            Setter(actors, cutOff: 16, fadeOut: 16).SetActorData(message);

            Assert.Equal(100, message.Source.Visibility);
        }

        [Fact]
        public void SetActorData_NotAvailable_LeavesMessageUntouched()
        {
            var actors = new FakeActorManager { IsAvailable = false, ActivePlayerName = "Alice", DistanceProvider = _ => 999f };
            var message = Message();

            Setter(actors).SetActorData(message);

            Assert.Equal(100, message.Source.Visibility);
            Assert.False(message.Source.IsUser);
        }

        [Theory]
        [InlineData("Alice", true)]
        [InlineData("alice", true)]  // case-insensitive
        [InlineData("Bob", false)]
        public void SetActorData_MarksOwnMessagesAsUser(string activePlayer, bool expectedIsUser)
        {
            var actors = new FakeActorManager { ActivePlayerName = activePlayer };
            var message = Message(character: "Alice");

            new ChatMessageActorDataSetter(actors).SetActorData(message);

            Assert.Equal(expectedIsUser, message.Source.IsUser);
        }
    }
}
