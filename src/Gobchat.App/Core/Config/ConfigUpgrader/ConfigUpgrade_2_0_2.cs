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
using System.Text.RegularExpressions;

namespace Gobchat.Core.Config
{
    /// <summary>
    /// 2.0.2 carries three settings-page cleanups onto saved profiles:
    /// <list type="bullet">
    /// <item>the search-highlight background no longer stores a trailing <c>!important</c> (Coloris
    /// couldn't parse it, which broke the colour field) — it's re-applied at CSS-generation time;</item>
    /// <item>the removed "Config font size" control's <c>style.config.font-size</c> key is dropped;</item>
    /// <item>the default chat font moved from Times New Roman to the bundled IBM Plex Sans stack — a
    /// profile still on the old default is moved across, any custom font is left untouched.</item>
    /// </list>
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_2 : IConfigUpgrade
    {
        public int MinVersion => 20001;

        public int MaxVersion => 20001;

        public int TargetVersion => 20002;

        private const string OldTimesFont = "'Times New Roman', Times, sans-serif";
        private const string NewModernFont = "'IBM Plex Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif";

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            StripImportant(dst, "style.chatsearch.marked.background-color");
            RemoveKey(dst, "style.config", "font-size");
            MigrateIfEquals(dst, "style.channel.base.general.font-family", OldTimesFont, NewModernFont);

            return dst;
        }

        // Removes a trailing " !important" (with any surrounding whitespace) from a string value.
        private static void StripImportant(JObject root, string path)
        {
            var token = root.SelectToken(path);
            if (token == null || token.Type != JTokenType.String)
                return;

            var value = ((string?)token)!; // non-null: guarded above by token.Type == String
            var stripped = Regex.Replace(value, @"\s*!important\s*$", "");
            if (stripped != value)
                token.Replace(new JValue(stripped));
        }

        // Removes a child key, and the parent object too if it became empty.
        private static void RemoveKey(JObject root, string parentPath, string key)
        {
            if (root.SelectToken(parentPath) is JObject parent)
            {
                parent.Remove(key);
                if (parent.Count == 0 && parent.Parent is JProperty parentProperty)
                    parentProperty.Remove();
            }
        }

        // Rewrites the value at <paramref name="path"/> only when it is still exactly the old default.
        private static void MigrateIfEquals(JObject root, string path, string oldValue, string newValue)
        {
            var token = root.SelectToken(path);
            if (token != null && token.Type == JTokenType.String && (string?)token == oldValue)
                token.Replace(new JValue(newValue));
        }
    }
}
