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

using System.Linq;
using Gobchat.Core.Config;
using Newtonsoft.Json.Linq;

namespace Gobchat.Core.Chat
{
    /// <summary>
    /// Reads, from the chat-tab configuration, whether the range filter is in use. Shared by the chat
    /// pipeline (which applies the distance-based fade) and the actor poller (which only scans nearby
    /// player positions while some visible tab actually needs them), so both react to the same single
    /// source of truth instead of each tracking their own toggle.
    /// </summary>
    public static class RangeFilterConfig
    {
        /// <summary>
        /// True when at least one <em>visible</em> chat tab has the range filter enabled
        /// (its <c>formatting.rangefilter</c> flag).
        /// </summary>
        public static bool IsActiveForVisibleTabs(IConfigManager config)
        {
            return IsActiveForVisibleTabs(config.GetProperty<JObject>("behaviour.chattabs.data"));
        }

        /// <summary>
        /// Pure form over the raw <c>behaviour.chattabs.data</c> object, so the rule can be unit-tested
        /// without a full config manager. Tolerates absent <c>visible</c>/<c>formatting.rangefilter</c>
        /// nodes (treated as off).
        /// </summary>
        internal static bool IsActiveForVisibleTabs(JObject chatTabs)
        {
            if (chatTabs == null)
                return false;

            return chatTabs.Properties()
                .Select(p => p.Value)
                .Where(tab => tab.Value<bool>("visible"))
                .Any(tab => tab["formatting"]?["rangefilter"]?.ToObject<bool>() == true);
        }
    }
}
