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
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// FFXIV prefixes a player-channel sender with their friend-list group symbol (★●▲♦♥♠♣). The builder
    /// must peel that symbol off into <see cref="ChatMessageSource.FfGroup"/> so it never reaches the
    /// displayed/normalized <see cref="ChatMessageSource.CharacterName"/>.
    ///
    /// WHY this matters: <c>CharacterName</c> is the single source of truth every downstream consumer keys
    /// on — the right-click "Add Player to Custom Group" menu stores it as a trigger, and both the C#
    /// (<see cref="ChatMessageTriggerGroupSetter"/>) and JS (Chat.ts ChatGroupControl) matchers search it.
    /// If the symbol leaked into <c>CharacterName</c>, a player added to a custom group from a marked line
    /// would be stored as "♥jane …" and never re-match a future glyph-free line. These tests lock that
    /// invariant so it can't silently regress.
    /// </summary>
    public sealed class ChatMessageBuilderSourceMarkerTests
    {
        // The friend-group symbols are literal Unicode (FFXIVUnicodes.GroupUnicodes), indexed 0..6.
        private static readonly string Star = char.ConvertFromUtf32(0x2605);  // ★ = GroupUnicodes[0]
        private static readonly string Heart = char.ConvertFromUtf32(0x2665); // ♥ = GroupUnicodes[4]

        [Theory]
        [InlineData(ChatChannel.Say)]
        [InlineData(ChatChannel.Party)]
        public void BuildChatMessage_PlayerChannelHeartGlyph_StripsGlyphIntoFfGroup(ChatChannel channel)
        {
            var builder = new ChatMessageBuilder();

            var message = builder.BuildChatMessage(DateTime.Now, channel, $"{Heart}Jane Ffxivingway", "hello");

            Assert.Equal(4, message.Source.FfGroup);
            Assert.Equal("Jane Ffxivingway", message.Source.CharacterName);
        }

        [Fact]
        public void BuildChatMessage_StarGlyph_MapsToGroupIndexZero()
        {
            // Index 0 is the tell-tale case: FfGroup defaults to -1, so a stored 0 proves the symbol was
            // actually parsed (not left unset) while still being stripped from the name.
            var builder = new ChatMessageBuilder();

            var message = builder.BuildChatMessage(DateTime.Now, ChatChannel.Say, $"{Star}Max Mustermiqote", "hi");

            Assert.Equal(0, message.Source.FfGroup);
            Assert.Equal("Max Mustermiqote", message.Source.CharacterName);
        }

        [Fact]
        public void BuildChatMessage_NoGlyph_LeavesNameIntactAndFfGroupUnset()
        {
            // The strip must be driven by the leading symbol, not applied unconditionally: a plain name keeps
            // every character and FfGroup stays at its -1 "none" sentinel.
            var builder = new ChatMessageBuilder();

            var message = builder.BuildChatMessage(DateTime.Now, ChatChannel.Say, "Jane Ffxivingway", "hello");

            Assert.Equal(-1, message.Source.FfGroup);
            Assert.Equal("Jane Ffxivingway", message.Source.CharacterName);
        }
    }
}
