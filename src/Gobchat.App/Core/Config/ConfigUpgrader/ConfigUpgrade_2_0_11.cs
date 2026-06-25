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
    /// 2.0.11 adds the new <c>style.chat-frame.indentation</c> setting (the Formatting page's
    /// "Indentation style": full / timestamp / character). Old profiles have no such key, so seed it to
    /// the default <c>"full"</c> (the existing flush-left behaviour) to keep them in step with the default.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_11 : IConfigUpgrade
    {
        public int MinVersion => 20010;

        public int MaxVersion => 20010;

        public int TargetVersion => 20011;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            var style = dst["style"] as JObject;
            if (style == null)
            {
                style = new JObject();
                dst["style"] = style;
            }

            var chatFrame = style["chat-frame"] as JObject;
            if (chatFrame == null)
            {
                chatFrame = new JObject();
                style["chat-frame"] = chatFrame;
            }

            var indentation = chatFrame["indentation"];
            if (indentation == null || indentation.Type == JTokenType.Null)
                chatFrame["indentation"] = "full";

            return dst;
        }
    }
}
