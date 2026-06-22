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

using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// The dry-run "inject scenario" tool replays GobchatEx's own written chatlogs back into the chat
    /// pipeline. WHY this matters: the parser is fed real sample logs that deliberately mix two header
    /// formats, system/status channels, and one malformed line - a replay must map every genuine channel
    /// name to <see cref="ChatChannel"/>, hand the raw sender back verbatim (so the pipeline can recover
    /// the party glyph/world), and silently drop noise instead of throwing mid-replay.
    /// </summary>
    public sealed class ChatlogReplayParserTests
    {
        // Party-position glyphs that prefix a sender in the logs. Built from code points so this source
        // file stays pure ASCII (no source-encoding dependency for the byte-exact sender assertions).
        private static readonly string Heart = char.ConvertFromUtf32(0x2665); // U+2665 BLACK HEART SUIT
        private static readonly string Star = char.ConvertFromUtf32(0x2605);  // U+2605 BLACK STAR

        [Fact]
        public void Parse_MapsCclv1PlayerLine_AndKeepsSenderVerbatim()
        {
            // Arrange - a Party line whose sender carries the party-position glyph + cross-world tag.
            var line = $"Party [2026-02-14 19:35:21+01:00] {Heart}Jane Ffxivingway [Moogle]: alright valentione's day dungeon run, who forgot their food buff";

            // Act
            var result = ChatlogReplayParser.Parse(new[] { line });

            // Assert - the sender is the raw Source.Original (glyph + world preserved), message intact.
            var entry = Assert.Single(result);
            Assert.Equal(ChatChannel.Party, entry.Channel);
            Assert.Equal($"{Heart}Jane Ffxivingway [Moogle]", entry.Source);
            Assert.Equal("alright valentione's day dungeon run, who forgot their food buff", entry.Message);
        }

        [Fact]
        public void Parse_SkipsHeaderLines()
        {
            // Both CCLv1 header lines and the FCLv1 id line are metadata, not chat - they must vanish.
            var lines = new[]
            {
                "Chatlogger Id: CCLv1",
                "Chatlogger format:{channel} [{date} {time-full}] {sender}: {message}",
                "Chatlogger Id: FCLv1",
            };

            Assert.Empty(ChatlogReplayParser.Parse(lines));
        }

        [Fact]
        public void Parse_SkipsMalformedLine_WithoutThrowing()
        {
            // A deliberately corrupted paste from the sample logs: bad input cannot abort a replay.
            var line = "hQ7#@!corrupted clipboard paste 0x00 - this is not a log line at all";

            Assert.Empty(ChatlogReplayParser.Parse(new[] { line }));
        }

        [Fact]
        public void Parse_HandlesEmptySender_OnRandomLine()
        {
            // /random output is logged with no sender; the channel still maps and the message survives,
            // including its internal double space and [847] brackets.
            var line = "Random [2026-01-09 20:33:27+01:00] : Random! You roll  [847] (out of 999).";

            var entry = Assert.Single(ChatlogReplayParser.Parse(new[] { line }));
            Assert.Equal(ChatChannel.Random, entry.Channel);
            Assert.Equal("", entry.Source);
            Assert.Equal("Random! You roll  [847] (out of 999).", entry.Message);
        }

        [Fact]
        public void Parse_MapsTellChannels()
        {
            var lines = new[]
            {
                $"TellRecieve [2026-02-14 19:43:12+01:00] {Heart}Jane Ffxivingway [Moogle]: psst. plan B?",
                $"TellSend [2026-02-14 19:43:51+01:00] {Heart}Jane Ffxivingway [Moogle]: plan A holds.",
            };

            var result = ChatlogReplayParser.Parse(lines);
            Assert.Equal(2, result.Count);
            Assert.Equal(ChatChannel.TellRecieve, result[0].Channel);
            Assert.Equal(ChatChannel.TellSend, result[1].Channel);
        }

        [Fact]
        public void Parse_KeepsBracketedWorldInsideAnimatedEmoteMessage()
        {
            // Only the leading [timestamp] is consumed, so the [World] tag inside the message survives.
            var line = "AnimatedEmote [2026-01-09 20:48:36+01:00] Lorem Ipsunade: Lorem Ipsunade [Twintania] waves to Max Mustermiqote [Shiva].";

            var entry = Assert.Single(ChatlogReplayParser.Parse(new[] { line }));
            Assert.Equal(ChatChannel.AnimatedEmote, entry.Channel);
            Assert.Equal("Lorem Ipsunade", entry.Source);
            Assert.Equal("Lorem Ipsunade [Twintania] waves to Max Mustermiqote [Shiva].", entry.Message);
        }

        [Fact]
        public void Parse_KeepsSystemLines_ButDropsOnlyNoise()
        {
            // System/status channels (GobchatInfo) are intentionally replayed; only the header and the
            // malformed line are dropped. Proves the filter is "skip non-chat", not "skip non-player".
            var lines = new[]
            {
                "Chatlogger Id: CCLv1",
                "GobchatInfo [2026-02-14 19:32:08+01:00] Gobchat: FFXIV detected.",
                "hQ7#@!corrupted clipboard paste",
                $"Say [2026-01-09 20:09:17+01:00] {Star}Max Mustermiqote [Shiva]: \"There you are!\"",
            };

            var result = ChatlogReplayParser.Parse(lines);
            Assert.Equal(2, result.Count);
            Assert.Equal(ChatChannel.GobchatInfo, result[0].Channel);
            Assert.Equal("Gobchat", result[0].Source);
            Assert.Equal(ChatChannel.Say, result[1].Channel);
            Assert.Equal($"{Star}Max Mustermiqote [Shiva]", result[1].Source);
        }
    }
}
