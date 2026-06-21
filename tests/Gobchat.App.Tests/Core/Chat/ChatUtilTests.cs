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

using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// FFXIV cross-world names arrive as "Name[Server]". The actor realm and range filter key on the
    /// bare name (via <see cref="ChatUtil.StripServerName"/>), so stripping the server suffix must be exact.
    /// </summary>
    public sealed class ChatUtilTests
    {
        [Theory]
        [InlineData("Vtorak Azora[Gilgamesh]", "Vtorak Azora")]
        [InlineData("Vtorak Azora [Gilgamesh]", "Vtorak Azora")]
        [InlineData("Vtorak Azora", "Vtorak Azora")]
        public void StripServerName_RemovesServerSuffix(string input, string expected)
        {
            Assert.Equal(expected, ChatUtil.StripServerName(input));
        }

        [Theory]
        [InlineData("Vtorak Azora[Gilgamesh]", "Vtorak Azora", "Gilgamesh")]
        [InlineData("Vtorak Azora [Gilgamesh]", "Vtorak Azora", "Gilgamesh")]
        [InlineData("Vtorak Azora[ Gilgamesh ]", "Vtorak Azora", "Gilgamesh")]
        public void SplitCharacterName_SeparatesNameAndServer(string input, string expectedName, string expectedServer)
        {
            var (name, server) = ChatUtil.SplitCharacterName(input);

            Assert.Equal(expectedName, name);
            Assert.Equal(expectedServer, server);
        }

        [Fact]
        public void SplitCharacterName_WithoutServer_ReturnsNullServer()
        {
            var (name, server) = ChatUtil.SplitCharacterName("Vtorak Azora");

            Assert.Equal("Vtorak Azora", name);
            Assert.Null(server);
        }

        // FFXIV stores pasted "fancy" letters as Private Use Area glyphs (the boxed 'A' at U+E071,
        // contiguous with ASCII), which no font can render. They must fold back to plain ASCII so the
        // overlay shows readable text instead of tofu. Built from ints so no PUA literals live in source.
        private static string ToBoxed(string ascii)
        {
            var chars = ascii.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (chars[i] >= '0' && chars[i] <= 'Z')
                    chars[i] = (char)(chars[i] + 0xE030); // inverse of MapBoxedGlyphsToAscii
            return new string(chars);
        }

        [Fact]
        public void MapBoxedGlyphsToAscii_FoldsBoxedTextToAscii()
        {
            // The real-world case: "FLUX | RAINBOW FEVER" typed in styled letters and read back from memory.
            var input = ToBoxed("FLUX") + " | " + ToBoxed("RAINBOW FEVER");

            Assert.Equal("FLUX | RAINBOW FEVER", ChatUtil.MapBoxedGlyphsToAscii(input));
        }

        [Fact]
        public void MapBoxedGlyphsToAscii_PinsBlockBoundaries()
        {
            // Anchored on FFXIVUnicodes.Raid_A/B/C (U+E071..E073 = boxed A/B/C) and the block ends.
            Assert.Equal("ABC", ChatUtil.MapBoxedGlyphsToAscii(new string(new[] { (char)0xE071, (char)0xE072, (char)0xE073 })));
            Assert.Equal("0", ChatUtil.MapBoxedGlyphsToAscii(((char)0xE060).ToString())); // block start -> '0'
            Assert.Equal("Z", ChatUtil.MapBoxedGlyphsToAscii(((char)0xE08A).ToString())); // block end   -> 'Z'
        }

        [Fact]
        public void MapBoxedGlyphsToAscii_LeavesPlainAndOtherPuaUntouched()
        {
            Assert.Equal("Hello 123", ChatUtil.MapBoxedGlyphsToAscii("Hello 123")); // plain text: no-op
            // Party-number PUA (U+E091, FFXIVUnicodes.Party_2) sits outside the boxed block and must stay.
            var partyMarker = ((char)0xE091).ToString();
            Assert.Equal(partyMarker, ChatUtil.MapBoxedGlyphsToAscii(partyMarker));
        }
    }
}
