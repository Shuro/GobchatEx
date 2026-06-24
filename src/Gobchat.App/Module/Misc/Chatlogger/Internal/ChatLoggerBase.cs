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
using System.IO;
using System.Globalization;
using System.Text;
using Gobchat.Core.Chat;
using Gobchat.Core.Util.Extension;
using Gobchat.Core.Util.Extension.Queue;


namespace Gobchat.Module.Misc.Chatlogger.Internal
{
    public abstract class ChatLoggerBase : IChatLogger
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly Queue<string> _pendingMessages = new Queue<string>();

        protected readonly string _loggerId;
        protected readonly object _synchronizationLock = new object();
        
        private ChatChannel[] _logChannels = Array.Empty<ChatChannel>();
        private bool _hasNonInternalMessage;
        private string? _characterName; // currently logged-in character; null = logged out -> paused

        protected string? FileHandle { get; private set; }

        public IEnumerable<ChatChannel> LogChannels
        {
            get => _logChannels.ToArray();
            set => _logChannels = value.ToArrayOrEmpty();
        }

        public bool Active { get; set; }

        public string LogFolder { get; private set; } = null!; // set via SetLogFolder before any write

        public bool UseCharacterFolders { get; private set; }

        public ChatLoggerBase(string loggerId)
        {
            _loggerId = loggerId ?? throw new ArgumentNullException(nameof(loggerId));
        }

        abstract protected string FormatMessage(ChatMessage msg);

        virtual protected void OnFileChange() { }

        public void SetLogFolder(string folder)
        {
            if (folder == null || folder.Length == 0)
                throw new ArgumentNullException(nameof(folder));

            if (folder.Equals(LogFolder))
                return;

            lock (_synchronizationLock)
            {
                if (FileHandle != null)
                    Flush();

                FileHandle = null;
                LogFolder = folder;
            }
        }

        public void Log(ChatMessage message)
        {
            // Only log while a character is logged in (_characterName != null) and logging is enabled.
            if (Active && _characterName != null && _logChannels.Contains(message.Channel))
            {
                lock (_synchronizationLock)
                {
                    _pendingMessages.Enqueue(FormatMessage(message));
                    _hasNonInternalMessage = true;
                }
            }
        }

        /// <summary>
        /// Sets the currently logged-in character (<c>null</c> = logged out). On any change - login,
        /// logout, or character switch - the current file is finalized and the next message starts a
        /// fresh one, so each character session gets its own log file.
        /// </summary>
        public void SetCurrentCharacter(string? characterName)
        {
            var normalized = string.IsNullOrWhiteSpace(characterName) ? null : characterName.Trim();

            lock (_synchronizationLock)
            {
                if (string.Equals(_characterName, normalized, StringComparison.Ordinal))
                    return;

                Flush();              // finalize the previous character's pending messages
                FileHandle = null;    // next write opens a new file
                _characterName = normalized;
            }
        }

        /// <summary>
        /// Reduces a character name to a filename-safe token: letters/digits kept, whitespace and
        /// hyphens become a single '-', everything else (apostrophes, punctuation) dropped. E.g.
        /// "J'ohn Gobchat" -> "John-Gobchat".
        /// </summary>
        protected static string SanitizeForFileName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var sb = new StringBuilder(name.Length);
            foreach (var ch in name.Trim())
            {
                char toAppend;
                if (char.IsLetterOrDigit(ch))
                    toAppend = ch;
                else if (ch == '-' || char.IsWhiteSpace(ch))
                    toAppend = '-';
                else
                    continue; // drop apostrophes, punctuation, invalid path chars

                if (toAppend == '-' && (sb.Length == 0 || sb[sb.Length - 1] == '-'))
                    continue; // collapse runs, no leading hyphen
                sb.Append(toAppend);
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == '-')
                sb.Length--; // no trailing hyphen
            return sb.ToString();
        }

        /// <summary>
        /// Like <see cref="SanitizeForFileName"/> but keeps spaces, so the result reads as a folder name:
        /// letters/digits and single spaces are kept, runs of whitespace collapse to one space, and
        /// everything else (apostrophes, punctuation, invalid path chars) is dropped. E.g.
        /// "J'ohn Gobchat" -> "John Gobchat".
        /// </summary>
        protected static string SanitizeForFolderName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var sb = new StringBuilder(name.Length);
            foreach (var ch in name.Trim())
            {
                char toAppend;
                if (char.IsLetterOrDigit(ch))
                    toAppend = ch;
                else if (char.IsWhiteSpace(ch))
                    toAppend = ' ';
                else
                    continue; // drop apostrophes, punctuation, invalid path chars

                if (toAppend == ' ' && (sb.Length == 0 || sb[sb.Length - 1] == ' '))
                    continue; // collapse runs, no leading space
                sb.Append(toAppend);
            }

            while (sb.Length > 0 && sb[sb.Length - 1] == ' ')
                sb.Length--; // no trailing space
            return sb.ToString();
        }

        /// <summary>
        /// Toggles whether each character's logs are written into their own subfolder beneath
        /// <see cref="LogFolder"/>. When switched while a file is already open, that file is moved
        /// into/out of the subfolder so the active session stays in one continuous file.
        /// </summary>
        public void SetUseCharacterFolders(bool useCharacterFolders)
        {
            lock (_synchronizationLock)
            {
                if (UseCharacterFolders == useCharacterFolders)
                    return;

                UseCharacterFolders = useCharacterFolders;
                RelocateCurrentFile();
            }
        }

        /// <summary>The folder the next/current file belongs in: a per-character subfolder when
        /// character folders are on and a character is logged in, otherwise <see cref="LogFolder"/>.</summary>
        private string ResolveTargetFolder()
        {
            if (UseCharacterFolders)
            {
                var folder = SanitizeForFolderName(_characterName);
                if (folder.Length > 0)
                    return Path.Combine(LogFolder, folder);
            }
            return LogFolder;
        }

        /// <summary>
        /// Moves the currently open log file to match the current <see cref="UseCharacterFolders"/>
        /// setting. No-op when no file is open yet (the next file is created in the right place anyway).
        /// On a failed move the old <see cref="FileHandle"/> is kept so logging continues uninterrupted.
        /// </summary>
        private void RelocateCurrentFile()
        {
            if (FileHandle == null)
                return;

            Flush(); // write any pending lines to the old path before moving it

            var targetFolder = ResolveTargetFolder();
            var newPath = Path.Combine(targetFolder, Path.GetFileName(FileHandle));
            if (string.Equals(newPath, FileHandle, StringComparison.OrdinalIgnoreCase))
                return;

            // CFG-3: a failed move (destination exists, permission, cross-volume) must not abort the toggle
            // or escape mid-Flush. Keep the existing handle so the session keeps writing to the old file.
            try
            {
                Directory.CreateDirectory(targetFolder);
                File.Move(FileHandle, newPath);
                FileHandle = newPath;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not relocate chat log file from {0} to {1}", FileHandle, newPath);
            }
        }

        private void CreateNewFile()
        {
            var targetFolder = ResolveTargetFolder();
            Directory.CreateDirectory(targetFolder);
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm", CultureInfo.InvariantCulture);
            var character = SanitizeForFileName(_characterName);
            var fileName = string.IsNullOrEmpty(character)
                ? $"chatlog_{timestamp}.log"
                : $"chatlog_{timestamp}_{character}.log";
            FileHandle = Path.Combine(targetFolder, fileName);
            WriteMessageToFile($"Chatlogger Id: {_loggerId}");
            OnFileChange();
        }

        public void Flush()
        {
            if (_pendingMessages.Count < 1)
                return;

            lock (_synchronizationLock)
            {
                if (FileHandle == null)
                {
                    if (!_hasNonInternalMessage)
                        return; //only create a new file if there is at least one non internal message!
                    CreateNewFile();
                }

                WriteMessagesToFile(_pendingMessages.DequeueAll());
                _hasNonInternalMessage = false;
            }
        }

        protected void LogMessage(string message)
        {
            lock (_synchronizationLock)
                _pendingMessages.Enqueue(message);
        }

        protected void WriteMessageToFile(string message)
        {
            if (message != null && message.Length > 0)
                WriteMessagesToFile(new string[] { message });
        }

        protected void WriteMessagesToFile(IEnumerable<string> messages)
        {
            if (messages != null)
                File.AppendAllLines(FileHandle!, messages, System.Text.Encoding.UTF8);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool v)
        {
            Flush();
        }
    }
}