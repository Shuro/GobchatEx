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
    /// Mentions highlight (and optionally sound) when a configured trigger word appears in a message.
    /// WHY this matters: it must match whole words case-insensitively and split the surrounding text so
    /// only the trigger is styled — over-matching (substrings) or under-matching would mis-highlight RP.
    /// </summary>
    public sealed class ChatMessageMentionFinderTests
    {
        private static ChatMessage MessageWith(string text)
        {
            var message = new ChatMessage();
            message.Content.Add(new ChatMessageSegment(MessageSegmentType.Say, text));
            return message;
        }

        private static ChatMessageMentionFinder Finder(params string[] mentions)
        {
            return new ChatMessageMentionFinder
            {
                Mentions = mentions,
                MessageSegmentType = MessageSegmentType.Mention,
            };
        }

        [Fact]
        public void MarkMentions_SplitsTriggerIntoMentionSegment()
        {
            var message = MessageWith("hi Alice there");

            Finder("Alice").MarkMentions(message);

            Assert.True(message.ContainsMentions);
            var mention = Assert.Single(message.Content, s => s.Type == MessageSegmentType.Mention);
            Assert.Equal("Alice", mention.Text);
            // surrounding text is preserved and re-stitches to the original
            Assert.Equal("hi Alice there", string.Concat(message.Content.Select(s => s.Text)));
        }

        [Fact]
        public void MarkMentions_IsCaseInsensitive()
        {
            var message = MessageWith("HEY alice!");

            Finder("Alice").MarkMentions(message);

            Assert.True(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_OnlyMatchesWholeWords()
        {
            // "Ali" must not match inside "Alice" (word boundary required).
            var message = MessageWith("Alice waves");

            Finder("Ali").MarkMentions(message);

            Assert.False(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_NoMentionsConfigured_IsNoOp()
        {
            var message = MessageWith("Alice waves");

            Finder().MarkMentions(message);

            Assert.False(message.ContainsMentions);
            var segment = Assert.Single(message.Content);
            Assert.Equal("Alice waves", segment.Text);
        }
    }
}
