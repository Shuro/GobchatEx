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
    /// 2.0.8 adds three opt-in per-character Player Mention switches: <c>matchFirstNamePartial</c>,
    /// <c>matchLastNamePartial</c> (substring matching) and <c>matchMiqote</c> (match the longest
    /// apostrophe segment of a Miqo'te forename). Saved profiles store each remembered character under
    /// <c>behaviour.mentions.player.data.*</c>, so the new keys must be seeded into every existing entry
    /// (and the <c>data-template</c>) — they all default off, preserving the old whole-word behaviour.
    /// The JS config layer reads these by exact path and can't fall back to the default profile for keys
    /// nested inside user data.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_8 : IConfigUpgrade
    {
        public int MinVersion => 20007;

        public int MaxVersion => 20007;

        public int TargetVersion => 20008;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            var player = dst.SelectToken("behaviour.mentions.player") as JObject;
            if (player != null)
            {
                SeedSwitches(player["data-template"] as JObject);

                if (player["data"] is JObject data)
                    foreach (var property in data.Properties())
                        SeedSwitches(property.Value as JObject);
            }

            return dst;
        }

        private static void SeedSwitches(JObject entry)
        {
            if (entry == null)
                return;
            SetIfMissing(entry, "matchFirstNamePartial");
            SetIfMissing(entry, "matchLastNamePartial");
            SetIfMissing(entry, "matchMiqote");
        }

        private static void SetIfMissing(JObject entry, string key)
        {
            if (entry[key] == null)
                entry[key] = false;
        }
    }
}
