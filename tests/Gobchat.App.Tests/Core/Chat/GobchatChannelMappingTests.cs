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
    /// Maps FFXIV client channel codes to Gobchat's logical channels. WHY this matters: every incoming
    /// message is routed by this map, several client codes collapse into one logical channel (NPC, random),
    /// and unmapped codes must fall back to None rather than throw mid-stream.
    /// </summary>
    public sealed class GobchatChannelMappingTests
    {
        [Theory]
        [InlineData(FFXIVChatChannel.SAY, ChatChannel.Say)]
        [InlineData(FFXIVChatChannel.EMOTE, ChatChannel.Emote)]
        [InlineData(FFXIVChatChannel.TELL_SEND, ChatChannel.TellSend)]
        [InlineData(FFXIVChatChannel.NPC_TALK, ChatChannel.NPC_Dialog)]
        [InlineData(FFXIVChatChannel.NPC_DIALOGUE, ChatChannel.NPC_Dialog)] // multiple client codes -> one logical channel
        [InlineData(FFXIVChatChannel.RANDOM_SELF, ChatChannel.Random)]
        [InlineData(FFXIVChatChannel.RANDOM_OTHER, ChatChannel.Random)]
        public void GetChannel_FromClientCode_MapsToLogicalChannel(FFXIVChatChannel client, ChatChannel expected)
        {
            Assert.Equal(expected, GobchatChannelMapping.GetChannel(client).ChatChannel);
        }

        [Fact]
        public void GetChannel_UnknownClientCode_FallsBackToNone()
        {
            var unknown = (FFXIVChatChannel)0x7FFF;

            Assert.Equal(ChatChannel.None, GobchatChannelMapping.GetChannel(unknown).ChatChannel);
        }

        [Fact]
        public void GetChannel_InternalName_IsKebabCasedLowercase()
        {
            Assert.Equal("crossworldlinkshell-1", GobchatChannelMapping.GetChannel(ChatChannel.CrossWorldLinkShell_1).InternalName);
        }

        [Fact]
        public void EveryChatChannel_IsMapped()
        {
            // The static initializer throws if any ChatChannel lacks a mapping; assert the count to lock that in.
            var expectedCount = Enum.GetValues(typeof(ChatChannel)).Length;

            Assert.Equal(expectedCount, GobchatChannelMapping.GetAllChannels().Count);
        }
    }
}
