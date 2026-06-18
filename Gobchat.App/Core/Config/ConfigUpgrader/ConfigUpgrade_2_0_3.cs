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
    /// 2.0.3 introduces the per-character "Player Mentions" feature. Saved profiles predating it
    /// have no <c>behaviour.mentions.player</c> subtree, so seed it with the feature flag on but an
    /// empty character list — with no remembered characters (and auto-remembered ones starting
    /// inactive) nothing actually mentions until the user opts a character in — all without touching
    /// the existing global trigger words. Brand-new keys would otherwise only resolve through the
    /// default-profile fallback, which the JS config layer cannot rely on.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_3 : IConfigUpgrade
    {
        public int MinVersion => 20002;

        public int MaxVersion => 20002;

        public int TargetVersion => 20003;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            JsonUtil.SetIfUnavailable(dst, "behaviour.mentions.player", BuildDefaultPlayerMentions);

            return dst;
        }

        // Mirrors the "behaviour.mentions.player" block in resources/default_profile.json.
        private static JToken BuildDefaultPlayerMentions()
        {
            return new JObject
            {
                ["enabled"] = true,
                ["sorting"] = new JArray(),
                ["data-template"] = new JObject
                {
                    ["name"] = "",
                    ["active"] = false,
                    ["matchFullName"] = true,
                    ["matchFirstName"] = true,
                    ["matchLastName"] = true,
                    ["mentions"] = new JArray()
                },
                ["data"] = new JObject()
            };
        }
    }
}
