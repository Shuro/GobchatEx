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
    /// 2.0.5 exposes the FFXIV Modern overlay's two appearance variants as settings: the chat-tab style
    /// (<c>style.chat-frame.tab-style</c> — "underline" | "pills" | "angled") and the chat density
    /// (<c>style.chat-frame.density</c> — "dense-plus" | "dense" | "breathable" | "breathable-plus").
    /// The overlay reads these keys directly to drive the <c>data-tab-style</c>/<c>data-chat-density</c>
    /// attributes, and the JS config layer
    /// can't fall back to the default profile, so seed both with their defaults on older profiles that
    /// predate them — leaving any present value untouched so re-running the chain is a no-op. It also
    /// drops the retired <c>style.chat-history.gap</c> key (the chat density now owns line spacing).
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_5 : IConfigUpgrade
    {
        public int MinVersion => 20004;

        public int MaxVersion => 20004;

        public int TargetVersion => 20005;

        public JObject Upgrade(JObject src)
        {
            JObject dst = (JObject)src.DeepClone();

            JsonUtil.SetIfUnavailable(dst, "style.chat-frame.tab-style", "underline");
            JsonUtil.SetIfUnavailable(dst, "style.chat-frame.density", "dense");

            // "Gap between entries" is retired in favour of the density steps; drop the old key.
            JsonUtil.DeleteIfAvailable(dst, "style.chat-history.gap");

            return dst;
        }
    }
}
