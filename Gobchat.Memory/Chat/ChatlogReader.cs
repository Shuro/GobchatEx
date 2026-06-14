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
using Sharlayan.Models.ReadResults;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gobchat.Memory.Chat
{
    internal sealed class ChatlogReader
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ProcessConnector _connector;

        private int _previousArrayIndex = 0;
        private int _previousOffset = 0;
        private bool _chatlogException = false;

        public ChatlogReader(ProcessConnector connector)
        {
            _connector = connector;
        }

        public bool ChatLogAvailable => _connector.ActiveHandler?.Reader.CanGetChatLog() == true;

        private void Reset()
        {
            logger.Info("Reseting ChatLogReader array index");
            _previousArrayIndex = 0;
            _previousOffset = 0;
        }

        public List<Sharlayan.Core.ChatLogItem> Query()
        {
            var handler = _connector.ActiveHandler;
            if (handler == null)
                return new List<Sharlayan.Core.ChatLogItem>();

            _chatlogException = false;

            // Upstream no longer exposes a dedicated ChatLogReaderException, so any exception raised
            // by the handler while reading the chat log resets the read cursors.
            handler.OnException += ResetChatlogProcessorOnException;
            ChatLogResult readResult;
            try
            {
                readResult = handler.Reader.GetChatLog(_previousArrayIndex, _previousOffset);
            }
            finally
            {
                handler.OnException -= ResetChatlogProcessorOnException;
            }

            if (_chatlogException || readResult == null)
            {
                Reset();
            }
            else
            {
                _previousArrayIndex = readResult.PreviousArrayIndex;
                _previousOffset = readResult.PreviousOffset;
            }

            return readResult?.ChatLogItems?.ToList() ?? new List<Sharlayan.Core.ChatLogItem>();
        }

        private void ResetChatlogProcessorOnException(object sender, Logger log, Exception ex)
        {
            _chatlogException = true;
        }
    }
}
