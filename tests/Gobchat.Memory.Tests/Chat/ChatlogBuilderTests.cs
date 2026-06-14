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
using System.Linq;
using System.Text;
using Gobchat.Memory.Chat;
using Gobchat.Memory.Chat.Token;
using Xunit;

namespace Gobchat.Memory.Tests.Chat
{
    /// <summary>
    /// Verifies the FFXIV chat-line byte protocol parser. WHY this matters: the tokenizer is the
    /// boundary that turns raw game-memory bytes into the segments the whole chat pipeline renders;
    /// a regression here silently corrupts every message. The first 8 bytes of a line are a header
    /// the parser must skip, and control sequences begin with 0x02 followed by a type byte.
    /// </summary>
    public sealed class ChatlogBuilderTests
    {
        // The tokenizer reads payload starting at offset 8, so every fixture is prefixed with an
        // 8-byte header (its content is irrelevant and never inspected).
        private static Sharlayan.Core.ChatLogItem Line(string code, params byte[] payload)
        {
            var bytes = new byte[8].Concat(payload).ToArray();
            return new Sharlayan.Core.ChatLogItem
            {
                Code = code,
                TimeStamp = new DateTime(2026, 1, 1),
                Bytes = bytes,
            };
        }

        private static byte[] Utf8(string s) => Encoding.UTF8.GetBytes(s);

        [Fact]
        public void Process_PlainText_YieldsSingleTextToken()
        {
            var result = new ChatlogBuilder().Process(Line("000E", Utf8("Hello")));

            Assert.NotNull(result);
            Assert.Equal(0x000E, result!.Channel);
            var token = Assert.IsType<TextToken>(Assert.Single(result.Tokens));
            Assert.Equal("Hello", token.GetText());
        }

        [Theory]
        [InlineData("000E", 14)]
        [InlineData("0048", 72)]
        [InlineData("000A", 10)]
        public void Process_ParsesChannelAsHex(string code, int expectedChannel)
        {
            var result = new ChatlogBuilder().Process(Line(code, Utf8("x")));

            Assert.NotNull(result);
            Assert.Equal(expectedChannel, result!.Channel);
        }

        [Fact]
        public void Process_AutotranslateToken_IsParsedBetweenText()
        {
            // "Hi " + 0x02 2E <len=2> 05 03 + "!"  ->  text, autotranslate(05 03), text
            var payload = Utf8("Hi ").Concat(new byte[] { 0x02, 0x2E, 0x02, 0x05, 0x03 }).Concat(Utf8("!")).ToArray();

            var tokens = new ChatlogBuilder().Process(Line("000E", payload))!.Tokens;

            Assert.Equal(3, tokens.Count);
            Assert.Equal("Hi ", Assert.IsType<TextToken>(tokens[0]).GetText());
            var auto = Assert.IsType<AutotranslateToken>(tokens[1]);
            Assert.Equal(new byte[] { 0x05, 0x03 }, auto.Code);
            Assert.Equal("!", Assert.IsType<TextToken>(tokens[2]).GetText());
        }

        [Fact]
        public void Process_ServerDelimiterToken_IsParsed()
        {
            var payload = Utf8("A").Concat(new byte[] { 0x02, 0x12, 0x02, 0x59, 0x03 }).Concat(Utf8("B")).ToArray();

            var tokens = new ChatlogBuilder().Process(Line("000E", payload))!.Tokens;

            Assert.Equal(3, tokens.Count);
            Assert.Equal("A", Assert.IsType<TextToken>(tokens[0]).GetText());
            Assert.IsType<ServerDelimiterToken>(tokens[1]);
            Assert.Equal("B", Assert.IsType<TextToken>(tokens[2]).GetText());
        }

        [Fact]
        public void Process_LinkToken_SplitsTypeAndValueOnDelimiter()
        {
            // 0x02 27 <len=5> 01 01 FF 42 03  ->  type bytes before 0xFF, value bytes after
            var payload = new byte[] { 0x02, 0x27, 0x05, 0x01, 0x01, 0xFF, 0x42, 0x03 };

            var token = Assert.IsType<LinkToken>(Assert.Single(new ChatlogBuilder().Process(Line("000E", payload))!.Tokens));

            Assert.Equal("0227", token.Trigger);
            Assert.Equal("0101", token.LinkType);
            Assert.Equal("4203", token.LinkValue);
        }

        [Fact]
        public void Process_LinkToken_WithoutDelimiter_IsUnknownLinkToken()
        {
            // 0x02 27 <len=3> 01 02 03 (no 0xFF) -> UnknownLinkToken with the whole payload as type
            var payload = new byte[] { 0x02, 0x27, 0x03, 0x01, 0x02, 0x03 };

            var token = Assert.IsType<UnknownLinkToken>(Assert.Single(new ChatlogBuilder().Process(Line("000E", payload))!.Tokens));

            Assert.Equal("0227", token.Trigger);
            Assert.Equal("010203", token.LinkType);
        }

        [Fact]
        public void Process_UnrecognizedControlByte_IsUnknownToken()
        {
            // 0x02 49 <len=2> AB 03  ->  default branch keeps trigger + raw packed data
            var payload = new byte[] { 0x02, 0x49, 0x02, 0xAB, 0x03 };

            var token = Assert.IsType<UnknownToken>(Assert.Single(new ChatlogBuilder().Process(Line("000E", payload))!.Tokens));

            Assert.Equal("0249", token.Trigger);
            Assert.Equal(new byte[] { 0xAB, 0x03 }, token.Code);
        }

        [Fact]
        public void Process_0x01Sequence_IsSkippedWithoutMergingSurroundingText()
        {
            // 0x0201XX is documented as "not a control character"; it must not emit a token,
            // and must not glue the text on either side together.
            var payload = Utf8("X").Concat(new byte[] { 0x02, 0x01, 0x99 }).Concat(Utf8("Y")).ToArray();

            var tokens = new ChatlogBuilder().Process(Line("000E", payload))!.Tokens;

            Assert.Collection(tokens,
                t => Assert.Equal("X", Assert.IsType<TextToken>(t).GetText()),
                t => Assert.Equal("Y", Assert.IsType<TextToken>(t).GetText()));
        }

        [Fact]
        public void Process_NullItem_ReturnsNull()
        {
            Assert.Null(new ChatlogBuilder().Process(null!));
        }

        [Fact]
        public void Process_NonHexCode_ReturnsNull()
        {
            Assert.Null(new ChatlogBuilder().Process(Line("ZZZ", Utf8("x"))));
        }

        [Fact]
        public void Process_MalformedTrailingControlByte_ThrowsChatBuildException()
        {
            // A lone 0x02 at the end means the parser reads past the buffer for its type byte.
            Assert.Throws<ChatBuildException>(() => new ChatlogBuilder().Process(Line("000E", 0x02)));
        }

        [Fact]
        public void Process_DebugMode_AppendsRawPayloadTextToken()
        {
            var line = Line("000E", Utf8("Hi"));
            var withoutDebug = new ChatlogBuilder().Process(line)!.Tokens.Count;

            var debugTokens = new ChatlogBuilder { DebugMode = true }.Process(line)!.Tokens;

            Assert.Equal(withoutDebug + 1, debugTokens.Count);
            Assert.Equal("Hi", Assert.IsType<TextToken>(debugTokens[^1]).GetText());
        }
    }
}
