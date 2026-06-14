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
using Gobchat.Core.Runtime;
using Xunit;

namespace Gobchat.App.Tests.Core.Runtime
{
    /// <summary>
    /// Version parsing/ordering drives the auto-updater's "is there a newer release?" decision, so
    /// the ordering rules — especially that a pre-release sorts *below* the same final version — must
    /// hold, otherwise the updater could offer a downgrade or skip an upgrade.
    /// </summary>
    public sealed class GobVersionTests
    {
        [Theory]
        [InlineData("1.2.3", 1u, 2u, 3u, 0u)]
        [InlineData("v2.0.0", 2u, 0u, 0u, 0u)]
        [InlineData("1", 1u, 0u, 0u, 0u)]
        [InlineData("1.2", 1u, 2u, 0u, 0u)]
        [InlineData("1.2.3-4", 1u, 2u, 3u, 4u)]
        [InlineData("1.2.3-beta.5", 1u, 2u, 3u, 5u)]
        public void TryParse_ValidVersions(string text, uint major, uint minor, uint patch, uint pre)
        {
            Assert.True(GobVersion.TryParse(text, out var v));
            Assert.Equal((major, minor, patch, pre), (v.Major, v.Minor, v.Patch, v.PreRelease));
            Assert.Equal(pre != 0, v.IsPreRelease);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("1.2.3.4")]
        [InlineData("-1")]
        public void TryParse_InvalidVersions_ReturnFalse(string text)
        {
            Assert.False(GobVersion.TryParse(text, out _));
        }

        [Fact]
        public void Constructor_FromInvalidString_Throws()
        {
            Assert.Throws<ArgumentException>(() => new GobVersion("not-a-version"));
        }

        [Fact]
        public void Ordering_ByMajorMinorPatch()
        {
            Assert.True(new GobVersion(1, 2, 3) < new GobVersion(1, 2, 4));
            Assert.True(new GobVersion(1, 9, 9) < new GobVersion(2, 0, 0));
            Assert.True(new GobVersion(2, 0, 0) > new GobVersion(1, 9, 9));
        }

        [Fact]
        public void Ordering_PreReleaseSortsBelowFinalRelease()
        {
            var preRelease = new GobVersion(1, 2, 3, 1);
            var finalRelease = new GobVersion(1, 2, 3, 0);

            Assert.True(preRelease < finalRelease);
            Assert.True(finalRelease > preRelease);
            Assert.NotEqual(preRelease, finalRelease);
        }

        [Fact]
        public void Ordering_BetweenPreReleases()
        {
            Assert.True(new GobVersion(1, 2, 3, 1) < new GobVersion(1, 2, 3, 2));
        }

        [Fact]
        public void Equality_AndHashCode_AreConsistent()
        {
            var a = new GobVersion(1, 2, 3, 4);
            var b = new GobVersion(1, 2, 3, 4);

            Assert.Equal(a, b);
            Assert.True(a == b);
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
            Assert.Equal(0, a.CompareTo(b));
        }

        [Theory]
        [InlineData(1u, 2u, 3u, 0u, "1.2.3")]
        [InlineData(1u, 2u, 3u, 4u, "1.2.3-4")]
        public void ToString_FormatsPreReleaseSuffix(uint major, uint minor, uint patch, uint pre, string expected)
        {
            Assert.Equal(expected, new GobVersion(major, minor, patch, pre).ToString());
        }
    }
}
