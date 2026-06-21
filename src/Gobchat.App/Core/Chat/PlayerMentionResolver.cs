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
using System.Collections.Generic;
using System.Linq;

namespace Gobchat.Core.Chat
{
    /// <summary>
    /// Turns a logged-in character's name and per-character settings into the flat list of words
    /// that should be treated as mentions. FFXIV names are always "Forename Surname", so the
    /// forename is the first whitespace-separated token and the surname the last.
    ///
    /// The result feeds the same <see cref="ChatMessageMentionFinder"/> as the global trigger words,
    /// so each word is later matched whole-word and case-insensitively. Words are returned trimmed
    /// and de-duplicated (case-insensitively), preserving the first occurrence's casing.
    /// </summary>
    public static class PlayerMentionResolver
    {
        public static IReadOnlyList<string> ResolveWords(
            string fullName,
            bool matchFullName,
            bool matchFirstName,
            bool matchLastName,
            IEnumerable<string> customMentions)
        {
            var result = new List<string>();

            void Add(string word)
            {
                if (string.IsNullOrWhiteSpace(word))
                    return;
                var trimmed = word.Trim();
                if (trimmed.Length == 0)
                    return;
                if (!result.Any(w => string.Equals(w, trimmed, StringComparison.OrdinalIgnoreCase)))
                    result.Add(trimmed);
            }

            var name = fullName?.Trim() ?? string.Empty;
            if (name.Length > 0)
            {
                var parts = name.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (matchFullName)
                    Add(name);
                if (matchFirstName && parts.Length > 0)
                    Add(parts[0]);
                if (matchLastName && parts.Length > 0)
                    Add(parts[parts.Length - 1]);
            }

            if (customMentions != null)
                foreach (var custom in customMentions)
                    Add(custom);

            return result;
        }
    }
}
