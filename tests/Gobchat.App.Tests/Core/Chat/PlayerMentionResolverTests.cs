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
using System.Linq;
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// Player mentions turn the logged-in character's name (and optional extra words) into trigger words.
    /// WHY this matters: a FFXIV first name can be a common English word ("Sun", "Bell"), so each name
    /// part must be independently toggleable — always matching every part would mis-highlight ordinary RP.
    /// The resolver also feeds the same whole-word finder, so it must hand over clean, de-duplicated words.
    /// </summary>
    public sealed class PlayerMentionResolverTests
    {
        // Convenience wrapper: the three "partial / Miqo'te" switches default off so the existing
        // whole-word cases stay readable; the dedicated tests below opt them in explicitly.
        private static PlayerMentionWords Resolve(
            string fullName,
            bool matchFullName,
            bool matchFirstName,
            bool matchLastName,
            System.Collections.Generic.IEnumerable<string> custom,
            bool partialFirst = false,
            bool partialLast = false,
            bool miqote = false)
        {
            return PlayerMentionResolver.ResolveWords(
                fullName, matchFullName, matchFirstName, matchLastName,
                partialFirst, partialLast, miqote, custom);
        }

        [Fact]
        public void ResolveWords_AllParts_ReturnsFullFirstAndLast()
        {
            var words = Resolve("Max Mustermiqo'te", true, true, true, null);

            Assert.Equal(new[] { "Max Mustermiqo'te", "Max", "Mustermiqo'te" }, words.WholeWords);
            Assert.Empty(words.PartialWords);
        }

        [Fact]
        public void ResolveWords_OnlyLastName_OmitsFullAndFirst()
        {
            // A user who only wants the surname highlighted (e.g. first name is a common word) must not
            // get the full name or forename smuggled back in.
            var words = Resolve("Sun Seeker", false, false, true, null);

            Assert.Equal(new[] { "Seeker" }, words.WholeWords);
        }

        [Fact]
        public void ResolveWords_MergesCustomWords()
        {
            var words = Resolve("Max Mustermiqo'te", false, true, false, new[] { "boss", "captain" });

            Assert.Equal(new[] { "Max", "boss", "captain" }, words.WholeWords);
        }

        [Fact]
        public void ResolveWords_DeduplicatesCaseInsensitively_KeepingFirstCasing()
        {
            // The custom list repeating a name part (in another case) must not produce a duplicate regex;
            // the finder already matches case-insensitively, so duplicates are pure noise.
            var words = Resolve("Max Mustermiqo'te", false, true, false, new[] { "MAX", "  max  " });

            Assert.Equal(new[] { "Max" }, words.WholeWords);
        }

        [Fact]
        public void ResolveWords_SingleTokenName_DoesNotDuplicate()
        {
            // A one-word name means full == first == last; it must collapse to a single trigger word.
            var words = Resolve("Cloud", true, true, true, null);

            Assert.Equal(new[] { "Cloud" }, words.WholeWords);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveWords_BlankName_YieldsOnlyCustomWords(string? name)
        {
            var words = Resolve(name, true, true, true, new[] { "ally" });

            Assert.Equal(new[] { "ally" }, words.WholeWords);
        }

        [Fact]
        public void ResolveWords_NoPartsAndNoCustom_IsEmpty()
        {
            var words = Resolve("Max Mustermiqo'te", false, false, false, Array.Empty<string>());

            Assert.Empty(words.WholeWords);
            Assert.Empty(words.PartialWords);
        }

        // --- Partial matching --------------------------------------------------------------------

        [Fact]
        public void ResolveWords_PartialFirstName_GoesToPartialNotWhole()
        {
            // Partial first name must be matched as a substring, so it belongs in the partial list — and
            // must NOT also sit in the whole-word list (that would be redundant and risk double-marking).
            var words = Resolve("John Doe", false, false, false, null, partialFirst: true);

            Assert.Equal(new[] { "John" }, words.PartialWords);
            Assert.Empty(words.WholeWords);
        }

        [Fact]
        public void ResolveWords_PartialLastName_GoesToPartial()
        {
            var words = Resolve("Some Gobchat", false, false, false, null, partialLast: true);

            Assert.Equal(new[] { "Gobchat" }, words.PartialWords);
            Assert.Empty(words.WholeWords);
        }

        [Fact]
        public void ResolveWords_PartialFirst_WinsOverWholeFirst()
        {
            // With both the whole and partial first-name switches on, the forename must resolve to the
            // partial list only — a substring match already covers the whole word.
            var words = Resolve("John Doe", false, true, false, null, partialFirst: true);

            Assert.Equal(new[] { "John" }, words.PartialWords);
            Assert.DoesNotContain("John", words.WholeWords);
        }

        // --- Miqo'te mode ------------------------------------------------------------------------

        [Theory]
        [InlineData("A'nabelle Surana", "nabelle")] // tribe prefix is the short part -> keep the longer tail
        [InlineData("Kiht'to Surana", "Kiht")]      // the longer part is before the apostrophe
        [InlineData("Y'shtola Rhul", "shtola")]
        public void ResolveWords_Miqote_AddsLongestApostropheSegment(string name, string expected)
        {
            var words = Resolve(name, false, false, false, null, miqote: true);

            Assert.Contains(expected, words.WholeWords);
        }

        [Fact]
        public void ResolveWords_Miqote_NoApostrophe_AddsNothing()
        {
            // The forename has no apostrophe, so Miqo'te mode must contribute no extra word.
            var words = Resolve("John Doe", false, false, false, null, miqote: true);

            Assert.Empty(words.WholeWords);
            Assert.Empty(words.PartialWords);
        }

        [Fact]
        public void ResolveWords_Miqote_AlongsideFirstName_KeepsBoth()
        {
            // Matching the whole forename and the Miqo'te short name are independent; both whole words show up.
            var words = Resolve("A'nabelle Surana", false, true, false, null, miqote: true);

            Assert.Equal(new[] { "A'nabelle", "nabelle" }, words.WholeWords);
        }

        // --- Fuzzy candidates --------------------------------------------------------------------

        [Fact]
        public void FuzzyCandidates_IncludePartialNamesAsWholeWords()
        {
            // Regression guard: turning on a partial switch must NOT drop that name from fuzzy. The
            // partially-matched forename is still a fuzzy candidate, alongside the whole-word surname.
            var words = Resolve("John Doe", false, false, true, null, partialFirst: true);

            var fuzzy = PlayerMentionResolver.FuzzyCandidates(words);

            Assert.Contains("John", fuzzy);
            Assert.Contains("Doe", fuzzy);
        }

        [Fact]
        public void FuzzyCandidates_DeduplicateAcrossLists()
        {
            // A single-token name landing in both lists (full name whole + partial first) must yield one
            // fuzzy candidate, not a duplicate.
            var words = Resolve("Cloud", true, false, false, null, partialFirst: true);

            Assert.Equal(new[] { "Cloud" }, PlayerMentionResolver.FuzzyCandidates(words));
        }

        [Fact]
        public void FuzzyCandidates_NoWords_IsEmpty()
        {
            var words = Resolve("John Doe", false, false, false, null);

            Assert.Empty(PlayerMentionResolver.FuzzyCandidates(words));
        }
    }
}
