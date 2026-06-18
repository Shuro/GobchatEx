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
    /// 2.0.4 adds per-character fuzzy matching to Player Mentions: a <c>matchFuzzy</c> flag (off) and a
    /// <c>fuzzyLevel</c> strength ("conservative" | "balanced" | "aggressive", default "conservative").
    /// Profiles created at 2.0.3 already hold remembered characters without these keys, and the JS config
    /// layer reads them directly (it can't fall back to the default profile), so seed them on both the
    /// <c>data-template</c> and every existing character entry — leaving any present value untouched so
    /// re-running the chain is a no-op.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_4 : IConfigUpgrade
    {
        public int MinVersion => 20003;

        public int MaxVersion => 20003;

        public int TargetVersion => 20004;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            SeedFuzzyKeys(dst.SelectToken("behaviour.mentions.player.data-template") as JObject);

            if (dst.SelectToken("behaviour.mentions.player.data") is JObject data)
                foreach (var property in data.Properties())
                    SeedFuzzyKeys(property.Value as JObject);

            return dst;
        }

        // Adds the two fuzzy keys to a player entry only when absent (mirrors the default profile).
        private static void SeedFuzzyKeys(JObject entry)
        {
            if (entry == null)
                return;
            if (entry["matchFuzzy"] == null)
                entry["matchFuzzy"] = false;
            if (entry["fuzzyLevel"] == null)
                entry["fuzzyLevel"] = "conservative";
        }
    }
}
