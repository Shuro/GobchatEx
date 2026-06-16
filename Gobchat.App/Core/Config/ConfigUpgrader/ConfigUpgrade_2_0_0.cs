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
    /// 2.0 ships the <b>FFXIV Modern</b> chat-overlay theme and a new surface palette as the defaults.
    /// Existing profiles keep whatever they had saved, so this moves a profile that is still on the
    /// previous defaults onto the new look. Anything the user deliberately changed (a different theme,
    /// custom colours) is matched exactly against the old default and otherwise left untouched.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_0 : IConfigUpgrade
    {
        public int MinVersion => 11200;

        public int MaxVersion => 19999;

        public int TargetVersion => 20000;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            MigrateIfEquals(dst, "style.theme", "FFXIV Dark", "FFXIV Modern");
            MigrateIfEquals(dst, "style.chat-history.background-color", "rgba(20, 20, 20, 0.95)", "rgba(16, 19, 24, 0.86)");
            MigrateIfEquals(dst, "style.channel.base.general.color", "#DEDEDE", "#e8eaee");
            MigrateIfEquals(dst, "style.chatsearch.selected.border-color", "yellow", "#e0a44e");
            MigrateIfEquals(dst, "style.chatsearch.marked.background-color", "rgba(239, 140, 11, 0.15) !important", "rgba(224, 164, 78, 0.16) !important");

            return dst;
        }

        // Rewrites the value at <paramref name="path"/> to <paramref name="newValue"/> only when it is
        // still exactly the old default <paramref name="oldValue"/>; any other (customised) value is kept.
        private static void MigrateIfEquals(JObject root, string path, string oldValue, string newValue)
        {
            var token = root.SelectToken(path);
            if (token != null && token.Type == JTokenType.String && (string)token == oldValue)
                token.Replace(new JValue(newValue));
        }
    }
}
