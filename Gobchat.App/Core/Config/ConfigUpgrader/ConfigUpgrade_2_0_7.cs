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
    /// 2.0.7 moves the application-global preferences out of the per-profile config and into a separate
    /// app-settings store (<c>appsettings.json</c>), so they no longer change with the active profile and
    /// apply instantly without a profile Save. The affected keys are <c>behaviour.language</c>,
    /// <c>style.theme</c>, <c>behaviour.hideOnMinimize</c>, <c>behaviour.appUpdate</c>,
    /// <c>behaviour.actor</c>, <c>behaviour.hotkeys</c> and <c>behaviour.chat.updateInterval</c>.
    /// <para>
    /// This upgrade only bumps the schema version. The one-time migration that lifts a user's existing
    /// values into the app-settings store and strips them from every profile is done by
    /// <see cref="GobchatConfigManager"/> after load, where the active profile (the source of the
    /// values to keep) is known. Re-running this is a no-op.
    /// </para>
    /// </summary>
    internal sealed class ConfigUpgrade_2_0_7 : IConfigUpgrade
    {
        public int MinVersion => 20006;

        public int MaxVersion => 20006;

        public int TargetVersion => 20007;

        public JObject Upgrade(JObject src)
        {
            return (JObject)src.DeepClone();
        }
    }
}
