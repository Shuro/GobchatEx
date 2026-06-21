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

using Gobchat.Core.Util;
using Gobchat.Core.Util.Extension;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Gobchat.Core.Chat
{
    public sealed class ChatMessageTriggerGroupSetter
    {
        private TriggerGroup[] _groups;

        public TriggerGroup[] Groups
        {
            get => _groups.ToArray();
            set => _groups = value.ToArrayOrEmpty();
        }

        public void SetTriggerGroup(ChatMessage chatMessage)
        {
            chatMessage.Source.TriggerGroupId = FindFirstTriggerGroup(chatMessage);
        }

        private string FindFirstTriggerGroup(ChatMessage message)
        {
            if (message.Source == null || message.Source.Original == null)
                return null;

            switch (message.Channel)
            {
                case ChatChannel.TellRecieve:
                case ChatChannel.TellSend:
                case ChatChannel.Echo:
                case ChatChannel.Error:
                    return null;
            }

            var searchName = message.Source.CharacterName ?? message.Source.Original;
            // NFKC-fold first so a name typed in decorative code points (e.g. math-bold) still matches
            // a plain-text trigger word; the displayed name/TriggerGroupId are unaffected.
            searchName = UnicodeNormalizer.Normalize(searchName).ToLowerInvariant();
            // A cross-world speaker's name carries a " [Server]" suffix (added in ChatMessageBuilder). Also
            // test the bare name so a member added as just "firstname lastname" matches them on any world;
            // a legacy member stored with a "[Server]" suffix still matches via the full searchName.
            var bareName = ChatUtil.StripServerName(searchName);

            foreach (var group in _groups)
            {
                if (!group.Active)
                    continue;

                if (group.FFGroup.HasValue)
                {
                    if (group.FFGroup.Value == message.Source.FfGroup)
                        return group.Id;
                    continue;
                }

                if (group.Trigger.Contains(searchName) || group.Trigger.Contains(bareName))
                    return group.Id;
            }

            return null;
        }
    }

    public sealed class TriggerGroup
    {
        public bool Active { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? FFGroup { get; set; }

        public string Id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Trigger { get; set; } = new List<string>();
    }
}