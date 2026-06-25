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
    /// 2.0.10 gives every group in <c>behaviour.groups.data</c> an explicit <c>trigger</c> array. The
    /// premade ("ff") groups never carried one (only custom groups did), so the settings page materialized
    /// it to <c>[]</c> on open and that showed up as a spurious unsaved change against the saved profile.
    /// Normalizing a missing/null trigger to <c>[]</c> here keeps old profiles in step with the default.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_10 : IConfigUpgrade
    {
        public int MinVersion => 20009;

        public int MaxVersion => 20009;

        public int TargetVersion => 20010;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            var data = dst.SelectToken("behaviour.groups.data") as JObject;
            if (data == null)
                return dst;

            foreach (var property in data.Properties())
            {
                if (property.Value is not JObject group)
                    continue;

                var trigger = group["trigger"];
                if (trigger == null || trigger.Type == JTokenType.Null)
                    group["trigger"] = new JArray();
            }

            return dst;
        }
    }
}
