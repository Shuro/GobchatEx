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
    /// 2.0.12 adds the new <c>style.chat-frame.text-align</c> setting (the Formatting page's
    /// "Text alignment": left / center / right / justified). Old profiles have no such key, so seed it to
    /// the default <c>"left"</c> (the existing flush-left behaviour) to keep them in step with the default.
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_12 : IConfigUpgrade
    {
        public int MinVersion => 20011;

        public int MaxVersion => 20011;

        public int TargetVersion => 20012;

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

            var textAlign = chatFrame["text-align"];
            if (textAlign == null || textAlign.Type == JTokenType.Null)
                chatFrame["text-align"] = "left";

            return dst;
        }
    }
}
