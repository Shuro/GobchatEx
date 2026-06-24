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
using System.IO;
using Gobchat.Core.Util;
using Xunit;

namespace Gobchat.App.Tests.Core.Util
{
    /// <summary>
    /// Guards <see cref="PathSecurityUtil"/>, the containment helper the C#↔JS bridge uses to keep
    /// page-supplied paths from escaping their allowed root (traversal, sibling-prefix, absolute escape).
    /// Windows-only semantics (case-insensitive), matching the app's target framework.
    /// </summary>
    public sealed class PathSecurityUtilTests
    {
        private static readonly string Root =
            Path.GetFullPath(Path.Combine(Path.GetTempPath(), "gobchat-pathsec-root"));

        [Fact]
        public void IsContainedIn_TrueForFileDirectlyUnderRoot()
        {
            Assert.True(PathSecurityUtil.IsContainedIn(Root, Path.Combine(Root, "file.txt")));
        }

        [Fact]
        public void IsContainedIn_TrueForNestedFile()
        {
            Assert.True(PathSecurityUtil.IsContainedIn(Root, Path.Combine(Root, "a", "b", "file.txt")));
        }

        [Fact]
        public void IsContainedIn_TrueForRootItself()
        {
            Assert.True(PathSecurityUtil.IsContainedIn(Root, Root));
        }

        [Fact]
        public void IsContainedIn_FalseForParentEscape()
        {
            Assert.False(PathSecurityUtil.IsContainedIn(Root, Path.Combine(Root, "..", "secret.txt")));
        }

        [Fact]
        public void IsContainedIn_FalseForSiblingWithSharedPrefix()
        {
            // "<root>Sibling" shares the textual prefix of "<root>" but is NOT inside it.
            var sibling = Root + "Sibling" + Path.DirectorySeparatorChar + "x.txt";
            Assert.False(PathSecurityUtil.IsContainedIn(Root, sibling));
        }

        [Fact]
        public void IsContainedIn_IgnoresCase()
        {
            var upper = Path.Combine(Root.ToUpperInvariant(), "file.txt");
            Assert.True(PathSecurityUtil.IsContainedIn(Root, upper));
        }

        [Fact]
        public void ResolveWithin_RootsRelativePathUnderRoot()
        {
            var resolved = PathSecurityUtil.ResolveWithin(Root, "ui/styles/styles.json");
            Assert.Equal(Path.Combine(Root, "ui", "styles", "styles.json"), resolved);
        }

        [Fact]
        public void ResolveWithin_ThrowsForRelativeTraversalEscape()
        {
            Assert.Throws<UnauthorizedAccessException>(
                () => PathSecurityUtil.ResolveWithin(Root, ".." + Path.DirectorySeparatorChar + "passwd"));
        }

        [Fact]
        public void ResolveWithin_ThrowsForAbsolutePathOutsideRoot()
        {
            var outside = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "elsewhere", "x.txt"));
            Assert.Throws<UnauthorizedAccessException>(() => PathSecurityUtil.ResolveWithin(Root, outside));
        }
    }
}
