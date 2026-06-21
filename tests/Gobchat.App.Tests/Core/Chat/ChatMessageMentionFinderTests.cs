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
using System.Text;
using Gobchat.Core.Chat;
using Gobchat.Core.Util;
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

        private static ChatMessageMentionFinder FuzzyFinder(FuzzyMatchLevel level, params string[] words)
        {
            return new ChatMessageMentionFinder
            {
                FuzzyMentions = words,
                FuzzyLevel = level,
                MessageSegmentType = MessageSegmentType.Mention,
            };
        }

        // Render ASCII letters as Mathematical Sans-Serif Bold (the "𝗙𝗟𝗨𝗫 instead of FLUX" case).
        private static string ToMathBold(string ascii)
        {
            var sb = new StringBuilder();
            foreach (var c in ascii)
            {
                if (c >= 'A' && c <= 'Z') sb.Append(char.ConvertFromUtf32(0x1D5D4 + (c - 'A')));
                else if (c >= 'a' && c <= 'z') sb.Append(char.ConvertFromUtf32(0x1D5EE + (c - 'a')));
                else sb.Append(c);
            }
            return sb.ToString();
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

        // --- Fuzzy matching --------------------------------------------------------------------------

        [Theory]
        [InlineData("Daria")]   // substitution
        [InlineData("Dharya")]  // insertion
        [InlineData("Daryah")]  // append
        public void MarkMentions_Fuzzy_MatchesTypoOfName(string typo)
        {
            var message = MessageWith($"hi {typo} there");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Darya").MarkMentions(message);

            Assert.True(message.ContainsMentions);
        }

        [Theory]
        [InlineData("Khitto")]   // dropped apostrophe
        [InlineData("Kiht'to")]  // transposition
        [InlineData("Khit'o")]   // dropped letter
        public void MarkMentions_Fuzzy_MatchesApostropheNameTypos(string typo)
        {
            // FFXIV names with apostrophes are the headline fuzzy case; the apostrophe must not break it.
            var message = MessageWith($"hey {typo}!");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Khit'to").MarkMentions(message);

            Assert.True(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_Fuzzy_StillMatchesTheExactName()
        {
            var message = MessageWith("Darya waves");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Darya").MarkMentions(message);

            Assert.True(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_Fuzzy_ShortNameStaysExactOnly()
        {
            // "Ana" is below the length guard, so the near word "and" must NOT be treated as a mention —
            // this is the guard that keeps short names from drowning RP in false positives.
            var message = MessageWith("and then");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Ana").MarkMentions(message);

            Assert.False(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_Fuzzy_Balanced_RejectsTwoEditMiss()
        {
            // "Maria" is 2 edits from "Darya"; Balanced only grants 1 for a 5-letter name.
            var message = MessageWith("Maria waves");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Darya").MarkMentions(message);

            Assert.False(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_Fuzzy_PreservesSurroundingText()
        {
            var message = MessageWith("hi Daria there");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Darya").MarkMentions(message);

            var mention = Assert.Single(message.Content, s => s.Type == MessageSegmentType.Mention);
            Assert.Equal("Daria", mention.Text);
            Assert.Equal("hi Daria there", string.Concat(message.Content.Select(s => s.Text)));
        }

        [Fact]
        public void MarkMentions_NoFuzzyWords_LeavesTypoUnmatched()
        {
            // Exact-only (fuzzy off): a typo'd name must not mention.
            var message = MessageWith("Daria waves");

            Finder("Darya").MarkMentions(message);

            Assert.False(message.ContainsMentions);
        }

        [Fact]
        public void MarkMentions_ExactAndFuzzy_MarksBothWithoutDoubleProcessing()
        {
            // The real wiring: a player word is in BOTH lists. The exact hit stays exact (the fuzzy pass
            // skips the segment already typed as a mention) and the typo is added on top.
            var message = MessageWith("Darya and Daria");
            var finder = new ChatMessageMentionFinder
            {
                Mentions = new[] { "Darya" },
                FuzzyMentions = new[] { "Darya" },
                FuzzyLevel = FuzzyMatchLevel.Balanced,
                MessageSegmentType = MessageSegmentType.Mention,
            };

            finder.MarkMentions(message);

            var mentions = message.Content
                .Where(s => s.Type == MessageSegmentType.Mention)
                .Select(s => s.Text)
                .ToArray();
            Assert.Equal(new[] { "Darya", "Daria" }, mentions);
            Assert.Equal("Darya and Daria", string.Concat(message.Content.Select(s => s.Text)));
        }

        // --- Decorative code points (Mathematical Sans-Serif Bold) -----------------------------------

        [Fact]
        public void MarkMentions_MathBold_ExactMatchKeepsOriginalGlyphs()
        {
            // The match is found on an NFKC-folded copy, but the highlighted segment must keep the
            // ORIGINAL decorative glyphs (display stays original) — and the line must re-stitch exactly.
            var decoratedName = ToMathBold("Alice");
            var message = MessageWith($"hi {decoratedName} there");

            Finder("Alice").MarkMentions(message);

            Assert.True(message.ContainsMentions);
            var mention = Assert.Single(message.Content, s => s.Type == MessageSegmentType.Mention);
            Assert.Equal(decoratedName, mention.Text);
            Assert.Equal($"hi {decoratedName} there", string.Concat(message.Content.Select(s => s.Text)));
        }

        [Fact]
        public void MarkMentions_Fuzzy_MatchesMathBoldName()
        {
            // Fuzzy compares on a folded copy too, so a name typed in math-bold still reaches its rule.
            var decoratedName = ToMathBold("Darya");
            var message = MessageWith($"hi {decoratedName} there");

            FuzzyFinder(FuzzyMatchLevel.Balanced, "Darya").MarkMentions(message);

            Assert.True(message.ContainsMentions);
            var mention = Assert.Single(message.Content, s => s.Type == MessageSegmentType.Mention);
            Assert.Equal(decoratedName, mention.Text);
        }
    }
}
