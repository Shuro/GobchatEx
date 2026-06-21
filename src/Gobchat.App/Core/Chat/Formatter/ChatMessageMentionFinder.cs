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
        private readonly ReplaceTypeByText _replacer = new ReplaceTypeByText();
        private readonly ReplaceTypeByFuzzyText _fuzzyReplacer = new ReplaceTypeByFuzzyText();

        public IEnumerable<string> Mentions
        {
            get => _mentions;
            set
            {
                _mentions = value.ToArrayOrEmpty();
                var pattern = _mentions.Select(t => new Regex($@"\b{Regex.Escape(t)}\b", RegexOptions.IgnoreCase));
                _replacer.Pattern.Clear();
                _replacer.Pattern.AddRange(pattern);
            }
        }

        /// <summary>
        /// The (player) words that should additionally be matched fuzzily. They are normally also part of
        /// <see cref="Mentions"/>, so exact hits stay exact; this only adds near-miss (typo) matches.
        /// </summary>
        public IEnumerable<string> FuzzyMentions
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