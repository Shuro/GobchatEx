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
using System.Text.RegularExpressions;
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// Overlap merge in ReplaceTypeByText (CHT-1). WHY this matters: two trigger patterns whose matches
    /// overlap (e.g. "Ali" and "Alice") must collapse into a single highlighted span covering the union.
    /// The pre-fix code copied the (Start,End) value tuple and mutated the copy, so the widened end never
    /// reached the list and the highlight stopped short ("Ali" highlighted, "ce" left plain).
    /// </summary>
    public sealed class ReplaceTypeByTextTests
    {
        private static ReplaceTypeByText ReplacerFor(params string[] patterns)
        {
            var replacer = new ReplaceTypeByText { SegmentType = MessageSegmentType.Mention };
            foreach (var pattern in patterns)
                replacer.Pattern.Add(new Regex(pattern));
            return replacer;
        }

        private static SegmentMarker Run(ReplaceTypeByText replacer, string text)
        {
            var marker = new SegmentMarker();
            replacer.Segment(marker, MessageSegmentType.Undefined, text);
            marker.Finish();
            return marker;
        }

        [Fact]
        public void Segment_MergesOverlappingMatches_IntoOneSpanCoveringTheUnion()
        {
            // Arrange: "Ali" (0..3) overlaps "Alice" (0..5) in "Alice".
            var replacer = ReplacerFor("Ali", "Alice");

            // Act
            var marker = Run(replacer, "Alice");

            // Assert: exactly one Mention span, covering the full union 0..5 (pre-fix it ended at 3).
            var mention = Assert.Single(marker.Marks.Where(m => m.Type == MessageSegmentType.Mention));
            Assert.Equal(0, mention.Start);
            Assert.Equal(5, mention.End);
        }

        [Fact]
        public void Segment_KeepsDisjointMatches_AsSeparateSpans()
        {
            // Arrange: non-overlapping triggers must remain two distinct spans.
            var replacer = ReplacerFor("Alice", "Bob");

            // Act
            var marker = Run(replacer, "Alice and Bob");

            // Assert
            var mentions = marker.Marks.Where(m => m.Type == MessageSegmentType.Mention).ToList();
            Assert.Equal(2, mentions.Count);
            Assert.Equal((0, 5), (mentions[0].Start, mentions[0].End));
            Assert.Equal((10, 13), (mentions[1].Start, mentions[1].End));
        }
    }
}
