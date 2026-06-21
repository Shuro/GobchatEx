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

using System.Text;
using Gobchat.Core.Util;
using Xunit;

namespace Gobchat.App.Tests.Core.Util
{
    /// <summary>
    /// NFKC normalization used for chat matching only. WHY this matters: a rule written in plain ASCII
    /// must still fire on a message typed in decorative code points (e.g. Mathematical Sans-Serif Bold
    /// "𝗙𝗟𝗨𝗫"), yet the displayed text stays original — so the index map must translate a match found on
    /// the folded copy back to the exact original substring, and the all-ASCII hot path must stay free.
    /// </summary>
    public sealed class UnicodeNormalizerTests
    {
        // Map ASCII letters to their Mathematical Sans-Serif Bold code points (each a surrogate pair),
        // the headline real-world case ("𝗙𝗟𝗨𝗫" instead of "FLUX").
        private static string ToMathBold(string ascii)
        {
            var sb = new StringBuilder();
            foreach (var c in ascii)
            {
                if (c >= 'A' && c <= 'Z')
                    sb.Append(char.ConvertFromUtf32(0x1D5D4 + (c - 'A')));
                else if (c >= 'a' && c <= 'z')
                    sb.Append(char.ConvertFromUtf32(0x1D5EE + (c - 'a')));
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }

        [Fact]
        public void NormalizeWithMap_AlreadyAscii_ReturnsIdentityWithNullMap()
        {
            // The common case: a plain message must not allocate a map and must be returned unchanged,
            // so the matcher behaves exactly as before for everyone not using decorative code points.
            var (text, map) = UnicodeNormalizer.NormalizeWithMap("hi Alice there");

            Assert.Equal("hi Alice there", text);
            Assert.Null(map);
        }

        [Fact]
        public void NormalizeWithMap_MathBold_FoldsToAsciiAndMapsBackToSurrogatePairs()
        {
            var original = ToMathBold("Alice"); // 5 code points, 10 UTF-16 units

            var (text, map) = UnicodeNormalizer.NormalizeWithMap(original);

            Assert.Equal("Alice", text);
            // Each folded char maps back to the START of its original surrogate pair; the final sentinel
            // is the original length, so a match's end index maps cleanly.
            Assert.Equal(new[] { 0, 2, 4, 6, 8, 10 }, map);
            Assert.Equal(original.Length, map[map.Length - 1]);
        }

        [Fact]
        public void NormalizeWithMap_MapTranslatesMatchSpanToOriginalSubstring()
        {
            // Prove the contract the mention highlighter relies on: a span found on the folded text maps
            // back to the *original* (still-decorative) substring, not the ASCII one.
            var prefix = "hi ";
            var name = ToMathBold("Alice");
            var original = prefix + name + " there";

            var (text, map) = UnicodeNormalizer.NormalizeWithMap(original);

            var start = text.IndexOf("Alice");
            var end = start + "Alice".Length;
            var origStart = map[start];
            var origEnd = map[end];

            Assert.Equal(name, original.Substring(origStart, origEnd - origStart));
        }

        [Fact]
        public void Normalize_MathBold_FoldsToAscii()
        {
            Assert.Equal("FLUX", UnicodeNormalizer.Normalize(ToMathBold("FLUX")));
        }

        [Theory]
        [InlineData("FLUX")]
        [InlineData("")]
        [InlineData("Khit'to")]
        public void Normalize_AlreadyPlain_ReturnsUnchanged(string value)
        {
            Assert.Equal(value, UnicodeNormalizer.Normalize(value));
        }

        [Fact]
        public void NormalizeWithMap_UnpairedSurrogate_DoesNotThrow()
        {
            // Chat text comes from raw game memory and can contain a lone surrogate; normalization must
            // degrade gracefully (no exception) rather than drop the whole message.
            var loneHighSurrogate = "\uD83D"; // high surrogate with no following low surrogate

            var exception = Record.Exception(() => UnicodeNormalizer.NormalizeWithMap(loneHighSurrogate));

            Assert.Null(exception);
        }
    }
}
