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

using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System;
using Gobchat.Core.Util;
using Gobchat.Core.Util.Extension;

namespace Gobchat.Core.Chat
{
    public sealed class ChatMessageMentionFinder
    {
        private string[] _mentions = Array.Empty<string>();
        private string[] _partialMentions = Array.Empty<string>();
        private readonly ReplaceTypeByText _replacer = new ReplaceTypeByText();
        private readonly ReplaceTypeByFuzzyText _fuzzyReplacer = new ReplaceTypeByFuzzyText();

        // CHT-10: read-only view over the backing array; the setter still copies on write.
        public IReadOnlyList<string> Mentions
        {
            get => _mentions;
            set
            {
                _mentions = value.ToArrayOrEmpty();
                RebuildPatterns();
            }
        }

        /// <summary>
        /// Words matched as case-insensitive substrings instead of whole words (the opt-in "partial
        /// first/last name" switches), so e.g. "John" also hits "Johntastic". Only the matched portion
        /// is split out as a mention segment.
        /// </summary>
        public IReadOnlyList<string> PartialMentions
        {
            get => _partialMentions;
            set
            {
                _partialMentions = value.ToArrayOrEmpty();
                RebuildPatterns();
            }
        }

        private void RebuildPatterns()
        {
            _replacer.Pattern.Clear();
            // Whole-word: bounded so "Ali" doesn't match inside "Alice".
            _replacer.Pattern.AddRange(_mentions
                .Where(t => t.Length > 0)
                .Select(t => new Regex($@"\b{Regex.Escape(t)}\b", RegexOptions.IgnoreCase)));
            // Partial: unbounded substring, matching (and highlighting) only the occurrence itself. An
            // empty term would compile to a regex that matches at every position (marking the whole
            // line), so empties are filtered out defensively even though the resolver never emits one.
            _replacer.Pattern.AddRange(_partialMentions
                .Where(t => t.Length > 0)
                .Select(t => new Regex(Regex.Escape(t), RegexOptions.IgnoreCase)));
        }

        /// <summary>
        /// The (player) words that should additionally be matched fuzzily. They are normally also part of
        /// <see cref="Mentions"/>, so exact hits stay exact; this only adds near-miss (typo) matches.
        /// </summary>
        public IReadOnlyList<string> FuzzyMentions
        {
            get => _fuzzyReplacer.Words;
            set => _fuzzyReplacer.Words = value;
        }

        public FuzzyMatchLevel FuzzyLevel
        {
            get => _fuzzyReplacer.Level;
            set => _fuzzyReplacer.Level = value;
        }

        public MessageSegmentType MessageSegmentType
        {
            get => _replacer.SegmentType;
            set
            {
                _replacer.SegmentType = value;
                _fuzzyReplacer.SegmentType = value;
            }
        }

        public void MarkMentions(ChatMessage message)
        {
            // Exact pass first, then fuzzy. Each is a no-op when its word list is empty (StartReplace),
            // and the fuzzy pass skips segments the exact pass already marked as mentions.
            message.FormatSegments(_replacer);
            message.FormatSegments(_fuzzyReplacer);
        }
    }
}