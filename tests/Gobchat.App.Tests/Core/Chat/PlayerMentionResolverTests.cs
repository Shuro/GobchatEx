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
        [Fact]
        public void ResolveWords_AllParts_ReturnsFullFirstAndLast()
        {
            var words = PlayerMentionResolver.ResolveWords("Max Mustermiqo'te", true, true, true, null);

            Assert.Equal(new[] { "Max Mustermiqo'te", "Max", "Mustermiqo'te" }, words);
        }

        [Fact]
        public void ResolveWords_OnlyLastName_OmitsFullAndFirst()
        {
            // A user who only wants the surname highlighted (e.g. first name is a common word) must not
            // get the full name or forename smuggled back in.
            var words = PlayerMentionResolver.ResolveWords("Sun Seeker", false, false, true, null);

            Assert.Equal(new[] { "Seeker" }, words);
        }

        [Fact]
        public void ResolveWords_MergesCustomWords()
        {
            var words = PlayerMentionResolver.ResolveWords("Max Mustermiqo'te", false, true, false, new[] { "boss", "captain" });

            Assert.Equal(new[] { "Max", "boss", "captain" }, words);
        }

        [Fact]
        public void ResolveWords_DeduplicatesCaseInsensitively_KeepingFirstCasing()
        {
            // The custom list repeating a name part (in another case) must not produce a duplicate regex;
            // the finder already matches case-insensitively, so duplicates are pure noise.
            var words = PlayerMentionResolver.ResolveWords("Max Mustermiqo'te", false, true, false, new[] { "MAX", "  max  " });

            Assert.Equal(new[] { "Max" }, words);
        }

        [Fact]
        public void ResolveWords_SingleTokenName_DoesNotDuplicate()
        {
            // A one-word name means full == first == last; it must collapse to a single trigger word.
            var words = PlayerMentionResolver.ResolveWords("Cloud", true, true, true, null);

            Assert.Equal(new[] { "Cloud" }, words);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ResolveWords_BlankName_YieldsOnlyCustomWords(string? name)
        {
            var words = PlayerMentionResolver.ResolveWords(name, true, true, true, new[] { "ally" });

            Assert.Equal(new[] { "ally" }, words);
        }

        [Fact]
        public void ResolveWords_NoPartsAndNoCustom_IsEmpty()
        {
            var words = PlayerMentionResolver.ResolveWords("Max Mustermiqo'te", false, false, false, Array.Empty<string>());

            Assert.Empty(words);
        }
    }
}
