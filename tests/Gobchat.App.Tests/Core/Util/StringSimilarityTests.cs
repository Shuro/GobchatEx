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

using Gobchat.Core.Util;
using Xunit;

namespace Gobchat.App.Tests.Core.Util
{
    /// <summary>
    /// Backs fuzzy player-mention matching. WHY this matters: the distance must count an adjacent letter
    /// swap as a single edit (so "Kiht'to" still reaches "Khit'to"), and the per-length budget is the only
    /// guard between "catch the user's typos" and "highlight ordinary words" — both the transposition rule
    /// and the tier thresholds are load-bearing, so they are pinned here.
    /// </summary>
    public sealed class StringSimilarityTests
    {
        [Theory]
        [InlineData("Darya", "Darya", 0)]
        [InlineData("Darya", "Daria", 1)]    // substitution y->i
        [InlineData("Darya", "Dharya", 1)]   // insertion of h
        [InlineData("Darya", "Daryah", 1)]   // append
        [InlineData("Khit'to", "Khitto", 1)] // deletion of the apostrophe
        [InlineData("Khit'to", "Khit'o", 1)] // deletion of a t
        [InlineData("Khit'to", "Kiht'to", 1)] // transposition hi -> ih
        [InlineData("Darya", "Maria", 2)]    // two substitutions
        [InlineData("abc", "", 3)]
        [InlineData("", "abc", 3)]
        public void OsaDistance_CountsEditsAsExpected(string a, string b, int expected)
        {
            Assert.Equal(expected, StringSimilarity.OsaDistance(a, b));
        }

        [Fact]
        public void OsaDistance_TreatsAdjacentSwapAsOneEdit()
        {
            // The whole reason OSA is used over plain Levenshtein (which would score this 2).
            Assert.Equal(1, StringSimilarity.OsaDistance("ab", "ba"));
        }

        [Fact]
        public void OsaDistance_IsSymmetric()
        {
            Assert.Equal(
                StringSimilarity.OsaDistance("Khit'to", "Kiht'to"),
                StringSimilarity.OsaDistance("Kiht'to", "Khit'to"));
        }

        [Theory]
        // Conservative: exact-only below 5, then 1 edit at any length.
        [InlineData(FuzzyMatchLevel.Conservative, 4, -1)]
        [InlineData(FuzzyMatchLevel.Conservative, 5, 1)]
        [InlineData(FuzzyMatchLevel.Conservative, 12, 1)]
        // Balanced (default): exact-only below 5, 1 edit for 5-7, 2 edits at 8+.
        [InlineData(FuzzyMatchLevel.Balanced, 4, -1)]
        [InlineData(FuzzyMatchLevel.Balanced, 5, 1)]
        [InlineData(FuzzyMatchLevel.Balanced, 7, 1)]
        [InlineData(FuzzyMatchLevel.Balanced, 8, 2)]
        // Aggressive: exact-only below 4, 1 edit for 4-5, 2 edits at 6+.
        [InlineData(FuzzyMatchLevel.Aggressive, 3, -1)]
        [InlineData(FuzzyMatchLevel.Aggressive, 4, 1)]
        [InlineData(FuzzyMatchLevel.Aggressive, 5, 1)]
        [InlineData(FuzzyMatchLevel.Aggressive, 6, 2)]
        public void MaxDistanceFor_FollowsTierTable(FuzzyMatchLevel level, int wordLength, int expected)
        {
            Assert.Equal(expected, StringSimilarity.MaxDistanceFor(level, wordLength));
        }

        [Theory]
        [InlineData("conservative", FuzzyMatchLevel.Conservative)]
        [InlineData("balanced", FuzzyMatchLevel.Balanced)]
        [InlineData("aggressive", FuzzyMatchLevel.Aggressive)]
        [InlineData("AGGRESSIVE", FuzzyMatchLevel.Aggressive)]
        [InlineData("  balanced ", FuzzyMatchLevel.Balanced)]
        [InlineData("nonsense", FuzzyMatchLevel.Conservative)]
        [InlineData(null, FuzzyMatchLevel.Conservative)]
        [InlineData("", FuzzyMatchLevel.Conservative)]
        public void ParseLevel_DefaultsToConservative(string? value, FuzzyMatchLevel expected)
        {
            Assert.Equal(expected, StringSimilarity.ParseLevel(value!));
        }
    }
}
