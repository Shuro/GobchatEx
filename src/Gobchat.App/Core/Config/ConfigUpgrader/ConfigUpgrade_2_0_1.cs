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
using System.Collections.Generic;
using System.Linq;

namespace Gobchat.Core.Config
{
    /// <summary>
    /// 2.0.1 reworks the Formatting → Segment detection editor into three fixed sections (Say / Emote /
    /// OOC) of locked baked-in marker pairs plus user-added custom pairs. This brings a profile's saved
    /// <c>behaviour.segment</c> onto the new shape: the 9 baked-in pairs gain <c>"locked": true</c>, the
    /// old multi-token guillemet entry (<c>say5</c>: »/« either way) is split into single-token
    /// <c>say5</c> (»…«) and <c>say6</c> («…»), and <c>order</c> is regrouped to the OOC→Emote→Say
    /// precedence the new UI keeps. Custom pairs the user added are preserved (left unlocked); each
    /// baked-in pair keeps its on/off (<c>active</c>) state. Already-migrated data (a <c>locked</c> flag
    /// present) is left untouched.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_1 : IConfigUpgrade
    {
        public int MinVersion => 20000;

        public int MaxVersion => 20000;

        public int TargetVersion => 20001;

        // Ids of the pairs that ship as baked-in defaults; everything else in `data` is a user custom.
        private static readonly string[] BakedIds = { "ooc", "emote1", "emote2", "say1", "say2", "say3", "say4", "say5", "say6" };

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            var segment = dst.SelectToken("behaviour.segment") as JObject;
            if (segment == null)
                return dst;

            var data = segment["data"] as JObject;
            if (data == null)
                return dst;

            // Idempotent: a baked-in `locked` flag means the profile is already on the new shape (e.g. it
            // synced the new default at version 20000 before this upgrade existed). Leave it untouched.
            var alreadyMigrated = data.Properties()
                .Select(p => p.Value as JObject)
                .Any(o => o != null && o["locked"] != null);
            if (alreadyMigrated)
                return dst;

            bool Active(string id)
            {
                var token = (data[id] as JObject)?["active"];
                return token != null && token.Type == JTokenType.Boolean ? (bool)token : true;
            }

            // The old multi-token say5 carried both guillemet directions; both halves inherit its state.
            var guillemetsActive = Active("say5");
            var quote = ((char)0x22).ToString(); // straight double quote " (U+0022)

            var newData = new JObject
            {
                ["ooc"] = MakeLocked("OOC", Active("ooc"), "((", "))"),
                ["emote1"] = MakeLocked("EMOTE", Active("emote1"), "*", "*"),
                ["emote2"] = MakeLocked("EMOTE", Active("emote2"), "<", ">"),
                ["say1"] = MakeLocked("SAY", Active("say1"), quote, quote),
                ["say2"] = MakeLocked("SAY", Active("say2"), "„", "“"),
                ["say3"] = MakeLocked("SAY", Active("say3"), "„", "”"),
                ["say4"] = MakeLocked("SAY", Active("say4"), "“", "”"),
                ["say5"] = MakeLocked("SAY", guillemetsActive, "»", "«"),
                ["say6"] = MakeLocked("SAY", guillemetsActive, "«", "»"),
            };

            // Preserve any custom (non-baked) pairs the user added; they stay unlocked → editable.
            foreach (var prop in data.Properties())
                if (!BakedIds.Contains(prop.Name))
                    newData[prop.Name] = prop.Value.DeepClone();

            segment["data"] = newData;

            // Fix the long-standing data-template typo ("type:" -> "type") so "add custom pair" works.
            var template = segment["data-template"] as JObject;
            if (template != null)
            {
                template.Remove("type:");
                template["active"] = true;
                template["type"] = "SAY";
                if (template["startTokens"] == null) template["startTokens"] = new JArray();
                if (template["endTokens"] == null) template["endTokens"] = new JArray();
            }

            // Regroup order into OOC -> Emote -> Say precedence (matches the new UI's regroupOrder, so the
            // C# ReplaceTypeByToken keeps applying OOC/emote before say).
            segment["order"] = RegroupOrder(newData);

            return dst;
        }

        private static JObject MakeLocked(string type, bool active, string start, string end)
        {
            return new JObject
            {
                ["locked"] = true,
                ["active"] = active,
                ["type"] = type,
                ["startTokens"] = new JArray { start },
                ["endTokens"] = new JArray { end },
            };
        }

        private static string NormalizeType(JToken? typeToken)
        {
            if (typeToken == null)
                return "SAY";
            if (typeToken.Type == JTokenType.String)
                return ((string)typeToken)!.ToUpperInvariant();
            switch (typeToken.Value<int>())
            {
                case 3: return "OOC";
                case 2: return "EMOTE";
                default: return "SAY";
            }
        }

        private static JArray RegroupOrder(JObject data)
        {
            var buckets = new Dictionary<string, List<string>>
            {
                ["OOC"] = new List<string>(),
                ["EMOTE"] = new List<string>(),
                ["SAY"] = new List<string>(),
            };

            foreach (var prop in data.Properties())
            {
                var type = NormalizeType((prop.Value as JObject)?["type"]);
                if (!buckets.ContainsKey(type))
                    type = "SAY";
                buckets[type].Add(prop.Name);
            }

            var order = new JArray();
            foreach (var id in buckets["OOC"].Concat(buckets["EMOTE"]).Concat(buckets["SAY"]))
                order.Add(id);
            return order;
        }
    }
}
