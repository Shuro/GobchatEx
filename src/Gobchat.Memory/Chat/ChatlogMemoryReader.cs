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
using System.Linq;

namespace Gobchat.Memory.Chat
{
    internal sealed class ChatlogMemoryReader
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly Chat.ChatlogReader _reader;
        private readonly Chat.ChatlogBuilder _builder = new Chat.ChatlogBuilder();

        private static readonly TimeSpan TimestampEpsilon = TimeSpan.FromSeconds(5);
        private DateTime _lastTimestamp = default;
        private bool _timestampRead = false;

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
                    // Log the raw bytes (hex) rather than the decoded line: the decoded text is
                    // verbatim player chat and must not land in the release log.
                    logger.Error(() => "Error in processing chat item");
                    logger.Error(() => $"Chat item failed ({e.ChatData?.Length ?? 0} bytes): {BitConverter.ToString(e.ChatData ?? Array.Empty<byte>())}");
                    logger.Error(e);
                }
            }

            if (result.Count > 0)
            {
                if (_timestampRead)
                {
                    var unfilteredCount = result.Count;
                    result = result.Where(e => _lastTimestamp < e.TimeStamp).ToList();
                    if (logger.IsDebugEnabled && unfilteredCount > 0 && unfilteredCount != result.Count)
                        logger.Debug($"Removed {unfilteredCount - result.Count} messages due to expired timestamps");
                }
                else
                    _timestampRead = true;

                _lastTimestamp = result.Select(e => e.TimeStamp).Max().Subtract(TimestampEpsilon);
            }

            return result;
        }
    }
}