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

namespace Gobchat.Memory.Chat
{
    [Serializable]
    public class ChatBuildException : Exception
    {
        public byte[] ChatData { get; }
        public string Text { get; }

        public ChatBuildException(Exception innerException, byte[] chatData, string text)
            : base(string.Empty, innerException)
        {
            ChatData = chatData;
            Text = text;
            // Chain the payload into Exception.Data so a structured log sink can capture/replay the line.
            Data["chatData"] = chatData != null ? BitConverter.ToString(chatData) : null;
            Data["line"] = text;
        }

        
    }
}