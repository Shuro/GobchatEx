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
using Gobchat.Core.Util;
using Gobchat.Core.Util.Extension;

namespace Gobchat.Core.Chat
{
    public sealed class ChatMessageBuilder
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly ChatChannel[] PlayerChannels = {
            ChatChannel.Say, ChatChannel.Emote, ChatChannel.Yell, ChatChannel.Shout, ChatChannel.TellSend, ChatChannel.TellRecieve, ChatChannel.Party, ChatChannel.Guild, ChatChannel.Alliance,
            ChatChannel.AnimatedEmote,
            ChatChannel.CrossWorldLinkShell_1, ChatChannel.CrossWorldLinkShell_2, ChatChannel.CrossWorldLinkShell_3, ChatChannel.CrossWorldLinkShell_4,
            ChatChannel.CrossWorldLinkShell_5, ChatChannel.CrossWorldLinkShell_6, ChatChannel.CrossWorldLinkShell_7, ChatChannel.CrossWorldLinkShell_8,
            ChatChannel.LinkShell_1, ChatChannel.LinkShell_2, ChatChannel.LinkShell_3, ChatChannel.LinkShell_4,
            ChatChannel.LinkShell_5, ChatChannel.LinkShell_6, ChatChannel.LinkShell_7, ChatChannel.LinkShell_8,
        };

        private static readonly int[] GroupUnicodes = FFXIVUnicodes.GroupUnicodes.Select(e => e.Value).ToArray();
        private static readonly int[] PartyUnicodes = FFXIVUnicodes.PartyUnicodes.Select(e => e.Value).ToArray();
        private static readonly int[] RaidUnicodes = FFXIVUnicodes.RaidUnicodes.Select(e => e.Value).ToArray();

        // CHT-3: config-change callbacks write these fields/sub-objects from the config and actor threads
        // while the chat worker reads them in BuildChatMessage/FormatChatMessage. Without a barrier those
        // writes race the reads (stale/half-applied pipeline config). A single mutual-exclusion lock guards
        // every read and write here: config writes are rare (user changing settings), so the worker only
        // ever blocks for the brief moment a setting lands, and each formatted message sees a consistent
        // snapshot. The bool/float scalars route through ChatManagerConfig's other helpers, not this type.
        private readonly object _configLock = new object();

        private ChatChannel[] _formatChannels = Array.Empty<ChatChannel>();
        private ChatChannel[] _mentionChannels = Array.Empty<ChatChannel>();

        private readonly ChatMessageSegmentFormatter _formatter = new ChatMessageSegmentFormatter();
        private readonly ChatMessageMentionFinder _mentionFinder = new ChatMessageMentionFinder();

        // CHT-3: scalar flags are written by config callbacks and read by the worker - volatile makes the
        // write visible without paying for the lock (a one-tick staleness on a single bool is harmless).
        private volatile bool _detectEmoteInSayChannel;
        private volatile bool _detectEmoteInPartyChannel;
        private volatile bool _excludeUserMention;

        public bool DetectEmoteInSayChannel
        {
            get => _detectEmoteInSayChannel;
            set => _detectEmoteInSayChannel = value;
        }

        public bool DetectEmoteInPartyChannel
        {
            get => _detectEmoteInPartyChannel;
            set => _detectEmoteInPartyChannel = value;
        }

        public bool ExcludeUserMention
        {
            get => _excludeUserMention;
            set => _excludeUserMention = value;
        }

        public ChatChannel[] FormatChannels
        {
            get { lock (_configLock) return _formatChannels.ToArray(); }
            set { lock (_configLock) _formatChannels = value.ToArrayOrEmpty(); }
        }

        public FormatConfig[] Formats
        {
            get { lock (_configLock) return _formatter.Formats.ToArray(); }
            set { lock (_configLock) _formatter.Formats = value.ToArrayOrEmpty(); }
        }

        public ChatChannel[] MentionChannels
        {
            get { lock (_configLock) return _mentionChannels.ToArray(); }
            set { lock (_configLock) _mentionChannels = value.ToArrayOrEmpty(); }
        }

        public string[] Mentions
        {
            get { lock (_configLock) return _mentionFinder.Mentions.ToArray(); }
            set { lock (_configLock) _mentionFinder.Mentions = value; }
        }

        public string[] PartialMentions
        {
            get { lock (_configLock) return _mentionFinder.PartialMentions.ToArray(); }
            set { lock (_configLock) _mentionFinder.PartialMentions = value; }
        }

        public string[] FuzzyMentions
        {
            get { lock (_configLock) return _mentionFinder.FuzzyMentions.ToArray(); }
            set { lock (_configLock) _mentionFinder.FuzzyMentions = value; }
        }

        public FuzzyMatchLevel FuzzyMentionLevel
        {
            get { lock (_configLock) return _mentionFinder.FuzzyLevel; }
            set { lock (_configLock) _mentionFinder.FuzzyLevel = value; }
        }

        public ChatMessageBuilder()
        {
            _mentionFinder.MessageSegmentType = MessageSegmentType.Mention;
        }

        public ChatMessage BuildChatMessage(DateTime time, ChatChannel channel, string source, string message)
        {
            var chatMessage = new ChatMessage()
            {
                Timestamp = time,
                Channel = channel
            };

            chatMessage.Content.Add(new ChatMessageSegment(MessageSegmentType.Undefined, message));
            SetMessageSource(chatMessage, source);

            return chatMessage;
        }

        private void SetMessageSource(ChatMessage chatMessage, string source)
        {
            chatMessage.Source = new ChatMessageSource(source)
            {
                IsAPlayer = PlayerChannels.Contains(chatMessage.Channel)
            };

            if (source != null && source.Length > 0 && chatMessage.Source.IsAPlayer)
            {
                var readIdx = 0;
                int GetUnicodeIndex(int[] unicodes)
                {
                    var cp = (int)source[readIdx];
                    if (0xD800 <= cp && cp <= 0xDFFF)    //surrogate pair
                        cp = ((cp - 0xD800) << 10) + (source[readIdx + 1] - 0xDC00) + 0x10000;
                    return Array.IndexOf(unicodes, cp);
                }

                int lookupIdx;
                if (ChatChannel.Party == chatMessage.Channel)
                { // check for party number
                    lookupIdx = GetUnicodeIndex(PartyUnicodes);
                    if (lookupIdx >= 0)
                    {
                        chatMessage.Source.Party = lookupIdx;
                        // chatMessage.Source.Prefix = (chatMessage.Source.Prefix ?? "") + $"[{lookupIdx + 1}]"; //part of html now
                        readIdx += 1; //party unicodes should be of size 1
                    }
                }
                else if (ChatChannel.Alliance == chatMessage.Channel)
                { // check for alliance letter
                    lookupIdx = GetUnicodeIndex(RaidUnicodes);
                    if (lookupIdx >= 0)
                    {
                        chatMessage.Source.Alliance = lookupIdx;
                        // chatMessage.Source.Prefix = (chatMessage.Source.Prefix ?? "") + $"[{char.ConvertFromUtf32(lookupIdx + 'A')}]";
                        readIdx += 1; //raid unicodes should be of size 1
                    }
                }

                //check if source starts with a player assigned group letter
                lookupIdx = GetUnicodeIndex(GroupUnicodes);
                if (lookupIdx >= 0)
                {
                    chatMessage.Source.FfGroup = lookupIdx;
                    // chatMessage.Source.Prefix = (chatMessage.Source.Prefix ?? "") + FFXIVUnicodes.GroupUnicodes[lookupIdx].Symbol;
                    readIdx += 1;
                }

                chatMessage.Source.CharacterName = chatMessage.Source.Original.Substring(readIdx);

                if (chatMessage.Channel == ChatChannel.AnimatedEmote && chatMessage.Content.Count > 0)
                { // these are special
                    var serverStart = chatMessage.Source.CharacterName.Length + 1;
                    var msg = chatMessage.Content[0];

                    if (msg.Text.Length > serverStart && msg.Text[serverStart] == '[')
                    {
                        var serverEnd = msg.Text.IndexOf(']', serverStart);
                        if(serverEnd > 0)
                        {
                            var server = msg.Text.Substring(serverStart, serverEnd - serverStart + 1);
                            chatMessage.Source.CharacterName += " " + server;
                        }
                    }
                }
            }
        }

        public void FormatChatMessage(ChatMessage chatMessage)
        {
            // CHT-3: hold the lock for the whole format pass so the pipeline config (_formatChannels,
            // _mentionChannels, _formatter, _mentionFinder) cannot be swapped mid-message by a config write.
            lock (_configLock)
            {
                FormatChatMessageLocked(chatMessage);
            }
        }

        private void FormatChatMessageLocked(ChatMessage chatMessage)
        {
            if (_formatChannels.Contains(chatMessage.Channel))
            {
                _formatter.Format(chatMessage);
                // Autodetect emote: when a message carries marked direct speech, everything else is
                // flagged emote. Enabled per channel — Say and/or Party.
                var detectEmote =
                    (DetectEmoteInSayChannel && chatMessage.Channel == ChatChannel.Say) ||
                    (DetectEmoteInPartyChannel && chatMessage.Channel == ChatChannel.Party);
                if (detectEmote)
                {
                    var containsSay = chatMessage.Content.Any(e => e.Type == MessageSegmentType.Say);
                    if (containsSay)
                        SetUndefinedTo(chatMessage, MessageSegmentType.Emote);
                }
            }

            var channelHasMentionsEnabled = _mentionChannels.Contains(chatMessage.Channel);

            var messageByUser = chatMessage.Source.IsUser;
            var doNotScanUserMessage = ExcludeUserMention;

            var scanForMentions = !(messageByUser && doNotScanUserMessage) && channelHasMentionsEnabled;
            if (scanForMentions)
            {
                _mentionFinder.MarkMentions(chatMessage);
            }
            else
            {
                logger.Debug(() =>
                {                    
                    var msg = "Ignore mentions in message, because of: ";
                    var reasons = new List<string>();

                    if (!channelHasMentionsEnabled)
                        reasons.Add("channel has mentions disabled");

                    if (messageByUser && doNotScanUserMessage)
                        reasons.Add("message is from user and should not be checked");

                    return msg + string.Join(", ", reasons);
                });
            }

            SetDefaultTypes(chatMessage);
        }

        private void SetDefaultTypes(ChatMessage chatMessage)
        {
            switch (chatMessage.Channel)
            {
                case ChatChannel.Say:
                    SetUndefinedTo(chatMessage, MessageSegmentType.Say);
                    break;

                case ChatChannel.Emote:
                    SetUndefinedTo(chatMessage, MessageSegmentType.Emote);
                    break;
            }
        }

        private static void SetUndefinedTo(ChatMessage chatMessage, MessageSegmentType newType)
        {
            foreach (var message in chatMessage.Content)
                if (message.Type == MessageSegmentType.Undefined)
                    message.Type = newType;
        }
    }
}