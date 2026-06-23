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

using Gobchat.Core.UI;
using Xunit;

namespace Gobchat.App.Tests.Core.UI
{
    /// <summary>
    /// The update dialog shows release notes in a plain WinForms TextBox, which renders neither markdown
    /// nor lone-LF line breaks. WHY this matters: the notes are authored as Keep-a-Changelog markdown
    /// (LF line endings, '###'/'**'/'-' markers), and without conversion they collapsed into one run-on
    /// paragraph full of literal markers. These tests pin that each structural element ends up on its own
    /// CRLF line with its markdown stripped.
    /// </summary>
    public class PatchNotesFormatterTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \n  ")]
        public void FormatForDisplay_ReturnsEmpty_ForNullOrBlank(string? input)
        {
            Assert.Equal(string.Empty, PatchNotesFormatter.FormatForDisplay(input));
        }

        [Fact]
        public void FormatForDisplay_NormalizesLoneLfToCrlf_SoTheTextBoxBreaksLines()
        {
            // The whole point: a TextBox only honours CRLF, so lone-LF input must come back CRLF-delimited.
            var result = PatchNotesFormatter.FormatForDisplay("first line\nsecond line");

            Assert.Equal("first line\r\nsecond line", result);
            Assert.DoesNotContain("\n\n", result); // no bare LF left behind
        }

        [Fact]
        public void FormatForDisplay_StripsHeadingMarkersAndVersionBrackets()
        {
            var result = PatchNotesFormatter.FormatForDisplay("## [2.0.0] - 2026.06.13\n### Added");

            var lines = result.Split("\r\n");
            Assert.Equal("2.0.0 - 2026.06.13", lines[0]);
            Assert.Contains("Added", result);
            Assert.DoesNotContain("#", result);
            Assert.DoesNotContain("[", result);
        }

        [Fact]
        public void FormatForDisplay_TurnsListItemsIntoBullets()
        {
            var result = PatchNotesFormatter.FormatForDisplay("- one\n- two");

            Assert.Equal("  • one\r\n  • two", result);
        }

        [Fact]
        public void FormatForDisplay_StripsInlineEmphasisCodeAndLinkUrls()
        {
            var result = PatchNotesFormatter.FormatForDisplay(
                "New **FFXIV Modern** theme; see *Settings*, the `resources\\` folder and [Keep a Changelog](https://keepachangelog.com).");

            Assert.Equal(
                "New FFXIV Modern theme; see Settings, the resources\\ folder and Keep a Changelog.",
                result);
            Assert.DoesNotContain("*", result);
            Assert.DoesNotContain("`", result);
            Assert.DoesNotContain("https://", result);
        }

        [Fact]
        public void FormatForDisplay_SeparatesSectionsWithABlankLine()
        {
            // A heading after content gets a blank line before it so categories don't butt against bullets.
            var result = PatchNotesFormatter.FormatForDisplay("### Added\n- a feature\n### Fixed\n- a bug");

            Assert.Equal("Added\r\n  • a feature\r\n\r\nFixed\r\n  • a bug", result);
        }
    }
}
