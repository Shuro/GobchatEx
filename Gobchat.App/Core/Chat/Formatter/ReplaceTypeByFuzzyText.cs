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
using System.Text.RegularExpressions;
using Gobchat.Core.Util;

namespace Gobchat.Core.Chat
{
    /// <summary>
    /// The fuzzy counterpart to <see cref="ReplaceTypeByText"/>: instead of whole-word regex matches it
    /// scans each word-token of a segment and marks it when its edit distance to one of the configured
    /// player words is within the budget the active <see cref="FuzzyMatchLevel"/> grants for that word's
    /// length (see <see cref="StringSimilarity.MaxDistanceFor"/>). This is what catches typo'd names —
    /// "Daria"/"Dharya" for "Darya", "Khitto"/"Kiht'to"/"Khit'o" for "Khit'to".
    /// </summary>
    internal sealed class ReplaceTypeByFuzzyText : IReplacer
    {
        // Letters, combining marks, and apostrophes (straight + curly) make up a name token; FFXIV names
        // contain apostrophes ("Khit'to"), so they must stay part of the token rather than split it.
        private static readonly Regex TokenRegex = new Regex(@"[\p{L}\p{M}'’]+", RegexOptions.Compiled);

        // Pre-normalized (apostrophe-folded, lowercased) words to compare against.
        private string[] _words = Array.Empty<string>();

        public MessageSegmentType SegmentType { get; set; } = MessageSegmentType.Undefined;

        public FuzzyMatchLevel Level { get; set; } = FuzzyMatchLevel.Conservative;

        public IEnumerable<string> Words
        {
            get => _words;
            set => _words = (value ?? Enumerable.Empty<string>())
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .Select(Normalize)
                .ToArray();
        }

        public bool StartReplace(ChatMessage message)
        {
            return _words.Length > 0;
        }

        public void EndReplace()
        {
        }

        public void Segment(SegmentMarker marker, MessageSegmentType currentType, string text)
        {
            // The exact pass runs first and isolates exact mentions into their own segments; don't
            // re-scan those (a player word would only fuzzy-match itself again — wasted work).
            if (currentType == SegmentType)
                return;

            // Word-tokens are disjoint and already left-to-right, so no sort/merge is needed.
            var matches = new List<(int Start, int End)>();
            foreach (Match token in TokenRegex.Matches(text))
            {
                if (IsFuzzyMatch(Normalize(token.Value)))
                    matches.Add((token.Index, token.Index + token.Length));
            }

            if (matches.Count == 0)
                return; // no marks -> FormatSegments keeps the original segment unchanged

            marker.NewMark(currentType, 0);
            foreach (var (start, end) in matches)
            {
                marker.Mark.End = start;
                marker.NewMark(SegmentType, start, end);
                marker.NewMark(currentType, end);
            }
            marker.Mark.End = text.Length;
        }

        private bool IsFuzzyMatch(string token)
        {
            var tokenLength = token.Length;
            foreach (var word in _words)
            {
                var budget = StringSimilarity.MaxDistanceFor(Level, word.Length);
                if (budget < 0)
                    continue; // word too short to fuzzy-match safely
                if (Math.Abs(tokenLength - word.Length) > budget)
                    continue; // length gap alone already exceeds the budget
                if (StringSimilarity.OsaDistance(token, word) <= budget)
                    return true;
            }
            return false;
        }

        // Fold the curly apostrophe onto the straight one and lowercase, so matching is
        // case- and apostrophe-style-insensitive on both the words and the message tokens.
        private static string Normalize(string value)
        {
            return value.Replace('’', '\'').ToLowerInvariant();
        }
    }
}
