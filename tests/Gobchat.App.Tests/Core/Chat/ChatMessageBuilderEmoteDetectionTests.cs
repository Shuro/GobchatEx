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

using System.Linq;
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// Emote autodetection: in a channel the user enabled it for, once a message carries marked direct
    /// speech (a Say segment), every still-Undefined segment is reclassified as Emote. WHY this matters:
    /// it lets RP where only the spoken words are quoted have the rest styled as emote automatically — and
    /// the per-channel toggles (Say and Party) must stay independent and never bleed into other channels.
    /// </summary>
    public sealed class ChatMessageBuilderEmoteDetectionTests
    {
        private static ChatMessage MessageWith(ChatChannel channel, params ChatMessageSegment[] segments)
        {
            var message = new ChatMessage
            {
                Channel = channel,
                Source = new ChatMessageSource("Tester") { IsUser = false },
            };
            foreach (var segment in segments)
                message.Content.Add(segment);
            return message;
        }

        private static ChatMessageSegment Say(string text) => new ChatMessageSegment(MessageSegmentType.Say, text);
        private static ChatMessageSegment Undefined(string text) => new ChatMessageSegment(MessageSegmentType.Undefined, text);

        [Fact]
        public void Party_WhenEnabledAndSayPresent_ReclassifiesUndefinedAsEmote()
        {
            var builder = new ChatMessageBuilder
            {
                FormatChannels = new[] { ChatChannel.Party },
                DetectEmoteInPartyChannel = true,
            };
            var message = MessageWith(ChatChannel.Party, Say("\"hi\""), Undefined(" waves"));

            builder.FormatChatMessage(message);

            Assert.Equal(MessageSegmentType.Emote, message.Content.Single(s => s.Text == " waves").Type);
        }

        [Fact]
        public void Party_WhenDisabled_LeavesUndefinedUntouched()
        {
            // Party isn't a SetDefaultTypes channel, so with detection off the Undefined segment must stay
            // exactly Undefined — proving the reclassification is driven by the flag, not something else.
            var builder = new ChatMessageBuilder
            {
                FormatChannels = new[] { ChatChannel.Party },
                DetectEmoteInPartyChannel = false,
            };
            var message = MessageWith(ChatChannel.Party, Say("\"hi\""), Undefined(" waves"));

            builder.FormatChatMessage(message);

            Assert.Equal(MessageSegmentType.Undefined, message.Content.Single(s => s.Text == " waves").Type);
        }

        [Fact]
        public void Party_WhenEnabledButNoSayPresent_LeavesUndefinedUntouched()
        {
            // No marked direct speech -> nothing to anchor the emote conversion to.
            var builder = new ChatMessageBuilder
            {
                FormatChannels = new[] { ChatChannel.Party },
                DetectEmoteInPartyChannel = true,
            };
            var message = MessageWith(ChatChannel.Party, Undefined("just text"));

            builder.FormatChatMessage(message);

            Assert.Equal(MessageSegmentType.Undefined, message.Content.Single(s => s.Text == "just text").Type);
        }

        [Fact]
        public void Say_DetectionStillWorks_Independently()
        {
            // The Say path must keep working after the Party flag was added alongside it.
            var builder = new ChatMessageBuilder
            {
                FormatChannels = new[] { ChatChannel.Say },
                DetectEmoteInSayChannel = true,
            };
            var message = MessageWith(ChatChannel.Say, Say("\"hi\""), Undefined(" waves"));

            builder.FormatChatMessage(message);

            Assert.Equal(MessageSegmentType.Emote, message.Content.Single(s => s.Text == " waves").Type);
        }

        [Fact]
        public void Party_FlagDoesNotReclassifyTheSayChannel()
        {
            // Party detection on, Say detection off, message in Say: the Party flag must not turn the
            // Say channel's text into emote (it must not match a channel it wasn't enabled for).
            var builder = new ChatMessageBuilder
            {
                FormatChannels = new[] { ChatChannel.Say, ChatChannel.Party },
                DetectEmoteInPartyChannel = true,
                DetectEmoteInSayChannel = false,
            };
            var message = MessageWith(ChatChannel.Say, Say("\"hi\""), Undefined(" waves"));

            builder.FormatChatMessage(message);

            Assert.NotEqual(MessageSegmentType.Emote, message.Content.Single(s => s.Text == " waves").Type);
        }
    }
}
