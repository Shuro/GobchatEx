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
    /// 2.0.6 reworks the chat-overlay background: the colour now comes from the theme, with an optional
    /// per-profile override and a separate transparency setting.
    /// <list type="bullet">
    /// <item>Force <c>style.chat-history.background-color</c> to <c>null</c> so the theme's own (per
    /// dark/light) colour shows; a colour the user re-picks later becomes an explicit override again.</item>
    /// <item>Seed <c>style.chat-history.background-opacity</c> (0-100, default 90) — the transparency the
    /// overlay applies to whichever colour wins. The JS config layer can't fall back to the default
    /// profile, so it must exist on older profiles.</item>
    /// <item>Migrate the two legacy themes onto their Modern equivalents (they are being retired):
    /// <c>FFXIV Dark → FFXIV Modern</c>, <c>FFXIV Light → FFXIV Modern Light</c>. Without this, a profile
    /// still on a legacy theme would lose its (previously config-driven) background once the colour is
    /// nulled.</item>
    /// </list>
    /// Re-running the chain is a no-op: the colour is already null, the opacity is left untouched once
    /// present, and a non-legacy theme is left as-is.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_6 : IConfigUpgrade
    {
        public int MinVersion => 20005;

        public int MaxVersion => 20005;

        public int TargetVersion => 20006;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            // Colour now comes from the theme; drop any saved custom colour.
            JsonUtil.SetIfAvailable(dst, "style.chat-history.background-color", JValue.CreateNull());

            // Transparency is its own setting now; seed it without clobbering a value that's already there.
            JsonUtil.SetIfUnavailable(dst, "style.chat-history.background-opacity", 90);

            // Retire the legacy themes by moving existing selections onto the Modern equivalents.
            JsonUtil.ModifyIfAvailable(dst, "style.theme", token =>
            {
                var theme = token?.Type == JTokenType.String ? (string)token : null;
                if (theme == "FFXIV Dark") return "FFXIV Modern";
                if (theme == "FFXIV Light") return "FFXIV Modern Light";
                return token;
            });

            return dst;
        }
    }
}
