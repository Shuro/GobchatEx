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
using System.Linq;
using Gobchat.Core.Chat;
using Gobchat.Module.Misc.Chatlogger.Internal;
using Xunit;

namespace Gobchat.App.Tests.Module.Misc.Chatlogger
{
    /// <summary>
    /// The chat logger writes one file per character session. WHY this matters: a roleplayer expects a
    /// fresh log when they log in or switch characters, no logging while logged out, and a clean
    /// filename even for names with apostrophes/spaces (FFXIV names like "Y'shtola Rhul").
    /// </summary>
    public sealed class ChatLoggerBaseTests : IDisposable
    {
        private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "gobchat-chatlog-test-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            try { if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, true); } catch { /* best effort */ }
        }

        /// <summary>Minimal concrete logger that records a constant line and exposes the name sanitizer.</summary>
        private sealed class TestLogger : ChatLoggerBase
        {
            public TestLogger() : base("TEST") { }

            protected override string FormatMessage(ChatMessage msg) => "line";

            public static string Sanitize(string name) => SanitizeForFileName(name);
        }

        private static ChatMessage SayMessage() => new ChatMessage { Channel = ChatChannel.Say };

        private TestLogger NewActiveLogger()
        {
            return new TestLogger
            {
                Active = true,
                LogChannels = new[] { ChatChannel.Say },
            };
        }

        private string[] LogFiles() => Directory.Exists(_tempDir)
            ? Directory.GetFiles(_tempDir, "chatlog_*.log").Select(Path.GetFileName).ToArray()
            : Array.Empty<string>();

        [Theory]
        [InlineData("J'ohn Gobchat", "John-Gobchat")]
        [InlineData("Y'shtola Rhul", "Yshtola-Rhul")]
        [InlineData("  Spaced   Name  ", "Spaced-Name")]
        [InlineData("Foo-Bar", "Foo-Bar")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void SanitizeForFileName_DropsApostrophesAndCollapsesWhitespace(string input, string expected)
        {
            Assert.Equal(expected, TestLogger.Sanitize(input));
        }

        [Fact]
        public void Logging_StartsNewFilePerCharacter_AndPausesWhenLoggedOut()
        {
            var logger = NewActiveLogger();
            logger.SetLogFolder(_tempDir);

            // Logged out -> nothing is written even though logging is "active".
            logger.Log(SayMessage());
            logger.Flush();
            Assert.Empty(LogFiles());

            // Login as Alice -> a file tagged with her name.
            logger.SetCurrentCharacter("Alice");
            logger.Log(SayMessage());
            logger.Flush();

            // Switch to Bob -> a second, separate file.
            logger.SetCurrentCharacter("Bob");
            logger.Log(SayMessage());
            logger.Flush();

            // Logout -> writing pauses again, no new file.
            logger.SetCurrentCharacter(null);
            logger.Log(SayMessage());
            logger.Flush();

            var files = LogFiles();
            Assert.Equal(2, files.Length);
            Assert.Contains(files, f => f.EndsWith("_Alice.log", StringComparison.Ordinal));
            Assert.Contains(files, f => f.EndsWith("_Bob.log", StringComparison.Ordinal));
        }

        [Fact]
        public void Logging_WhenInactive_WritesNothing()
        {
            // WHY: the "create logs" setting must still win even while a character is logged in.
            var logger = NewActiveLogger();
            logger.Active = false;
            logger.SetLogFolder(_tempDir);
            logger.SetCurrentCharacter("Alice");

            logger.Log(SayMessage());
            logger.Flush();

            Assert.Empty(LogFiles());
        }
    }
}
