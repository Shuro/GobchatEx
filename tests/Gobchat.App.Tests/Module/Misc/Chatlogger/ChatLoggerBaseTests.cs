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

            public static string SanitizeFolder(string name) => SanitizeForFolderName(name);
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

        // Full paths of every log file under the temp dir, including character subfolders.
        private string[] AllLogFiles() => Directory.Exists(_tempDir)
            ? Directory.GetFiles(_tempDir, "chatlog_*.log", SearchOption.AllDirectories)
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

        [Theory]
        [InlineData("J'ohn Gobchat", "John Gobchat")]
        [InlineData("Y'shtola Rhul", "Yshtola Rhul")]
        [InlineData("  Spaced   Name  ", "Spaced Name")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void SanitizeForFolderName_KeepsSpacesAndDropsApostrophes(string input, string expected)
        {
            // WHY: a folder name must stay readable (keep the space between first/last name) yet be
            // path-safe (drop apostrophes), e.g. "log\John Gobchat\".
            Assert.Equal(expected, TestLogger.SanitizeFolder(input));
        }

        [Fact]
        public void Logging_WithCharacterFolders_WritesIntoPerCharacterSubfolder()
        {
            // WHY: with the toggle on, each character's session lands in its own subfolder.
            var logger = NewActiveLogger();
            logger.SetLogFolder(_tempDir);
            logger.SetUseCharacterFolders(true);
            logger.SetCurrentCharacter("Alice");

            logger.Log(SayMessage());
            logger.Flush();

            var file = Assert.Single(AllLogFiles());
            Assert.Equal(Path.Combine(_tempDir, "Alice"), Path.GetDirectoryName(file));
        }

        [Fact]
        public void TogglingCharacterFolders_WhenMoveFails_KeepsLoggingToTheOldFile()
        {
            // CFG-3: if the relocation move fails (here: destination already exists), the toggle must not
            // throw and the session must keep its existing file handle instead of losing it - otherwise a
            // later Flush would crash or silently write nowhere while the user believes files moved.
            var logger = NewActiveLogger();
            logger.SetLogFolder(_tempDir);
            logger.SetCurrentCharacter("Alice");

            logger.Log(SayMessage());
            logger.Flush();
            var rootFile = Assert.Single(AllLogFiles());
            var fileName = Path.GetFileName(rootFile);

            // Pre-create the move target so File.Move throws (the 2-arg overload fails on an existing dest).
            var aliceFolder = Path.Combine(_tempDir, "Alice");
            Directory.CreateDirectory(aliceFolder);
            var blocker = Path.Combine(aliceFolder, fileName);
            File.WriteAllText(blocker, "pre-existing");

            // The toggle must swallow the failed move and leave the active file untouched.
            var ex = Record.Exception(() => logger.SetUseCharacterFolders(true));
            Assert.Null(ex);
            Assert.True(File.Exists(rootFile));               // old handle preserved
            Assert.Equal("pre-existing", File.ReadAllText(blocker)); // move did not occur

            // Logging continues on the old file without throwing.
            logger.Log(SayMessage());
            var flushEx = Record.Exception(() => logger.Flush());
            Assert.Null(flushEx);
        }

        [Fact]
        public void TogglingCharacterFolders_WhileActive_MovesCurrentFile()
        {
            // WHY: switching the setting mid-session must move the open file so the session stays in one
            // continuous file instead of splitting across the root folder and the subfolder.
            var logger = NewActiveLogger();
            logger.SetLogFolder(_tempDir);
            logger.SetCurrentCharacter("Alice");

            logger.Log(SayMessage());
            logger.Flush();
            var rootFile = Assert.Single(AllLogFiles());
            Assert.Equal(_tempDir, Path.GetDirectoryName(rootFile));
            var fileName = Path.GetFileName(rootFile);

            // Turn folders on -> the open file moves into the Alice subfolder (same file name).
            logger.SetUseCharacterFolders(true);
            var movedFile = Assert.Single(AllLogFiles());
            Assert.Equal(Path.Combine(_tempDir, "Alice"), Path.GetDirectoryName(movedFile));
            Assert.Equal(fileName, Path.GetFileName(movedFile));

            // Turn folders off -> it moves back to the root.
            logger.SetUseCharacterFolders(false);
            var backFile = Assert.Single(AllLogFiles());
            Assert.Equal(_tempDir, Path.GetDirectoryName(backFile));
            Assert.Equal(fileName, Path.GetFileName(backFile));
        }
    }
}
