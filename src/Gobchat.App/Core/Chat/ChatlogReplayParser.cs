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
using System.Text.RegularExpressions;

namespace Gobchat.Core.Chat
{
    /// <summary>A single chat line recovered from a written chatlog, ready to be replayed.</summary>
    public sealed record ReplayLine(ChatChannel Channel, string Source, string Message);

    /// <summary>
    /// Parses GobchatEx's own written chatlog (the <c>CCLv1</c>/<c>FCLv1</c> formats) back into
    /// <see cref="ReplayLine"/>s for the <c>--dry-run</c> "inject scenario" developer tool. It is the
    /// inverse of <c>CustomChatLogger</c>'s default <c>{channel} [{date} {time-full}] {sender}: {message}</c>
    /// template: the <c>{sender}</c> token is the raw <c>ChatMessage.Source.Original</c>, so a parsed line
    /// can be fed straight back into <c>IChatManager.EnqueueMessage</c> and the chat pipeline reconstructs
    /// everything else (party glyph, character name, world) exactly as a live capture would.
    /// <para>
    /// Robust by design: header lines (<c>Chatlogger Id:</c> / <c>Chatlogger format:</c>), blank lines,
    /// malformed paste garbage, and lines whose channel token isn't a known <see cref="ChatChannel"/> are
    /// all skipped rather than throwing - a sample log deliberately contains such noise.
    /// </para>
    /// </summary>
    public static class ChatlogReplayParser
    {
        // {channel} [{date} {time}] {sender}: {message}
        //  - channel: a single word (every ChatChannel name is one word)
        //  - the leading [...] is the timestamp; only that one is consumed, so [World] tags inside the
        //    message (e.g. AnimatedEmote "... [Twintania] waves to ...") survive untouched
        //  - sender is lazy so it stops at the first ": " separator; it may be empty (Random/Error lines)
        private static readonly Regex LinePattern = new Regex(
            @"^(?<channel>\w+) \[[^\]]*\] (?<sender>.*?): (?<message>.*)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// Parses <paramref name="lines"/> into the chat lines that can be replayed, in order. Lines that
        /// don't match the chatlog line shape or carry an unknown channel are dropped silently.
        /// </summary>
        public static IReadOnlyList<ReplayLine> Parse(IEnumerable<string> lines)
        {
            if (lines == null)
                throw new ArgumentNullException(nameof(lines));

            var result = new List<ReplayLine>();
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                    continue;

                var match = LinePattern.Match(line);
                if (!match.Success)
                    continue;

                if (!Enum.TryParse<ChatChannel>(match.Groups["channel"].Value, ignoreCase: true, out var channel)
                    || !Enum.IsDefined(typeof(ChatChannel), channel))
                    continue;

                result.Add(new ReplayLine(channel, match.Groups["sender"].Value, match.Groups["message"].Value));
            }
            return result;
        }
    }
}
