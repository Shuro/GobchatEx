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

        [Fact]
        public void Segment_MergesOverlapping_RegardlessOfPatternOrder()
        {
            // The longer pattern is listed first, so both matches share Start 0 and the sort order between
            // them is unspecified. The merge must collapse them to the union either way (Math.Max on End).
            var replacer = ReplacerFor("Alice", "Ali");

            var marker = Run(replacer, "Alice");

            var mention = Assert.Single(marker.Marks.Where(m => m.Type == MessageSegmentType.Mention));
            Assert.Equal((0, 5), (mention.Start, mention.End));
        }

        [Fact]
        public void Segment_MergesChainOfThreeOverlaps_IntoOneSpan()
        {
            // (0,2) (1,3) (2,4) chain together: each merge widens the running span so the next match still
            // overlaps it. A non-transitive merge would leave a gap; this pins the iterative widening.
            var replacer = ReplacerFor("aa", "ab", "bb");

            var marker = Run(replacer, "aabb");

            var mention = Assert.Single(marker.Marks.Where(m => m.Type == MessageSegmentType.Mention));
            Assert.Equal((0, 4), (mention.Start, mention.End));
        }

        [Fact]
        public void Segment_MergesAdjacentTouchingMatches_IntoOneSpan()
        {
            // "ab" ends where "cd" begins (current.Start == previous.End). The merge guard is `<=`, so two
            // back-to-back highlights collapse into one continuous span rather than rendering as a seam.
            var replacer = ReplacerFor("ab", "cd");

            var marker = Run(replacer, "abcd");

            var mention = Assert.Single(marker.Marks.Where(m => m.Type == MessageSegmentType.Mention));
            Assert.Equal((0, 4), (mention.Start, mention.End));
        }

        [Fact]
        public void Segment_FullyContainedMatch_DoesNotSplitTheEnclosingSpan()
        {
            // "cd" sits entirely inside "abcde". The merge must keep the wider span (0,5) and never shrink
            // it to the inner match's End (Math.Max), nor emit a second span.
            var replacer = ReplacerFor("abcde", "cd");

            var marker = Run(replacer, "abcde");

            var mention = Assert.Single(marker.Marks.Where(m => m.Type == MessageSegmentType.Mention));
            Assert.Equal((0, 5), (mention.Start, mention.End));
        }
    }
}
