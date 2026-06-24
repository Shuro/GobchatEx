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

using NLog;
using System;
using System.Collections.Generic;

namespace Gobchat.Memory.Chat
{
    internal sealed class ChatlogMemoryReader
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Chat.ChatlogReader _reader;
        private readonly Chat.ChatlogBuilder _builder = new Chat.ChatlogBuilder();

        public ChatlogMemoryReader(ProcessConnector connector)
        {
            _reader = new Chat.ChatlogReader(connector);
        }

        public bool ChatLogAvailable => _reader.ChatLogAvailable;

        public List<Chat.ChatlogItem> GetNewestChatlog()
        {
            var rawLogs = _reader.Query();
            var result = new List<Chat.ChatlogItem>();

            foreach (var rawLog in rawLogs)
            {
                try
                {
                    var chatLogItem = _builder.Process(rawLog);
                    if (chatLogItem != null)
                        result.Add(chatLogItem);
                }
                catch (Chat.ChatBuildException e)
                {
                    //TODO handle this
                    // The hex of the raw bytes is still 1:1 recoverable to verbatim player chat, so keep it
                    // at Debug only - the release log ships at Info+ and is routinely attached to bug reports.
                    // The failure itself stays visible at Error (without the recoverable payload).
                    logger.Error(() => "Error in processing chat item");
                    logger.Debug(() => $"Chat item failed ({e.ChatData?.Length ?? 0} bytes): {BitConverter.ToString(e.ChatData ?? Array.Empty<byte>())}");
                    logger.Error(e);
                }
            }

            // MEM-3: no timestamp dedup here. ChatlogReader.Query already advances Sharlayan's position
            // cursor (_previousArrayIndex/_previousOffset) and returns only lines not seen before, so this
            // list is already deduplicated. The old filter set _lastTimestamp = max - 5s and dropped any
            // later line whose (second-resolution) timestamp tied a previous batch's - silently losing
            // bursts of same-second messages in busy chat (raids). Removing it also drops the stale-state
            // problem on reconnect (MEM-11). A rare cursor reset re-reads the buffer (possible duplicates),
            // which matches first-connect behaviour and is preferable to dropping live messages.
            return result;
        }
    }
}