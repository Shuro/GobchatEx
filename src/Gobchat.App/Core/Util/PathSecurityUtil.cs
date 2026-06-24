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

namespace Gobchat.Core.Util
{
    /// <summary>
    /// Helpers for keeping file-system access inside an allowed root directory. Used by the C#↔JS
    /// bridge and the chat logger so a path supplied by the (untrusted) web page cannot escape into
    /// arbitrary locations via <c>..</c> traversal or an absolute path. Comparisons are
    /// case-insensitive (the app is Windows-only).
    /// </summary>
    public static class PathSecurityUtil
    {
        /// <summary>
        /// True when <paramref name="candidate"/> is <paramref name="root"/> itself or a path nested
        /// underneath it, after canonicalising both with <see cref="Path.GetFullPath(string)"/>.
        /// A shared textual prefix (e.g. <c>root</c> vs <c>rootSibling</c>) is NOT containment.
        /// </summary>
        public static bool IsContainedIn(string root, string candidate)
        {
            if (string.IsNullOrEmpty(root))
                throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrEmpty(candidate))
                throw new ArgumentNullException(nameof(candidate));

            var normalizedRoot = Path.GetFullPath(root)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullCandidate = Path.GetFullPath(candidate);

            if (string.Equals(fullCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return true;

            var rootWithSeparator = normalizedRoot + Path.DirectorySeparatorChar;
            return fullCandidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves <paramref name="pathMaybeRelative"/> against <paramref name="root"/> (relative
        /// paths are combined with the root; absolute paths are used as given) and returns the
        /// canonical full path, but only if the result stays within the root. Throws
        /// <see cref="UnauthorizedAccessException"/> when the path would escape the root.
        /// </summary>
        public static string ResolveWithin(string root, string pathMaybeRelative)
        {
            if (string.IsNullOrEmpty(root))
                throw new ArgumentNullException(nameof(root));
            if (string.IsNullOrEmpty(pathMaybeRelative))
                throw new ArgumentNullException(nameof(pathMaybeRelative));

            var combined = Path.IsPathRooted(pathMaybeRelative)
                ? pathMaybeRelative
                : Path.Combine(root, pathMaybeRelative);

            var fullPath = Path.GetFullPath(combined);
            if (!IsContainedIn(root, fullPath))
                throw new UnauthorizedAccessException(
                    $"Path '{pathMaybeRelative}' resolves outside the allowed directory.");

            return fullPath;
        }
    }
}
