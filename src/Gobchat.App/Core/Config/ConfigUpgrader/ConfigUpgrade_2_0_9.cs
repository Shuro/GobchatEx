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

using Newtonsoft.Json.Linq;

namespace Gobchat.Core.Config
{
    /// <summary>
    /// 2.0.9 separates the 7 built-in premade ("ff") groups from the user's custom groups:
    /// <c>behaviour.groups.sorting</c> now holds <b>custom group ids only</b>. The premade groups stay in
    /// <c>behaviour.groups.data</c> (identified by their <c>ffgroup</c> field) and are enumerated by that
    /// field instead. For existing profiles this drops every ff id from <c>sorting</c>, keeping the
    /// remaining custom ids in their saved order, so custom groups become 1..n and the <c>/e gc group</c>
    /// command and settings index them directly.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_9 : IConfigUpgrade
    {
        public int MinVersion => 20008;

        public int MaxVersion => 20008;

        public int TargetVersion => 20009;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            var groups = dst.SelectToken("behaviour.groups") as JObject;
            var data = groups?["data"] as JObject;
            var sorting = groups?["sorting"] as JArray;
            if (data == null || sorting == null)
                return dst;

            // Keep only ids that exist in data and are NOT premade (no ffgroup field), preserving order.
            var customIds = new JArray();
            foreach (var token in sorting)
            {
                var id = token?.ToString();
                if (id == null)
                    continue;
                if (data[id] is JObject entry && entry["ffgroup"] == null)
                    customIds.Add(id);
            }

            groups!["sorting"] = customIds;
            return dst;
        }
    }
}
