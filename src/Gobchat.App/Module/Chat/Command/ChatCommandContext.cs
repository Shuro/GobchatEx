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

using System;
using Gobchat.Core.Chat;
using Gobchat.Core.Config;
using Gobchat.Core.Util;
using Gobchat.Module.Actor;

namespace Gobchat.Module.Chat.Command
{
    /// <summary>
    /// Everything a <see cref="IChatCommandHandler"/> needs to do its work, injected as plain
    /// interfaces/delegates so the command logic stays unit-testable without WinForms or a live FFXIV
    /// process. The hosting module (<see cref="AppModuleChatCommandManager"/>) wires the real implementations.
    /// </summary>
    internal sealed class ChatCommandContext
    {
        private readonly Func<string, string> _localize;
        private readonly Action<SystemMessageType, string> _reply;

        public ChatCommandContext(
            IActorManager actors,
            IConfigManager config,
            Func<string, string> localize,
            Action<SystemMessageType, string> reply,
            Action exit,
            Action<string> forwardToUi)
        {
            Actors = actors ?? throw new ArgumentNullException(nameof(actors));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            _localize = localize ?? throw new ArgumentNullException(nameof(localize));
            _reply = reply ?? throw new ArgumentNullException(nameof(reply));
            Exit = exit ?? throw new ArgumentNullException(nameof(exit));
            ForwardToUi = forwardToUi ?? throw new ArgumentNullException(nameof(forwardToUi));
        }

        /// <summary>Nearby-player data (count/list/distance commands).</summary>
        public IActorManager Actors { get; }

        /// <summary>Profile + group configuration (group/profile/config commands).</summary>
        public IConfigManager Config { get; }

        /// <summary>Shuts the application down (the <c>close</c> command).</summary>
        public Action Exit { get; }

        /// <summary>Hands a genuinely UI-side command (info/error on-off, config open) to the overlay.</summary>
        public Action<string> ForwardToUi { get; }

        /// <summary>The localized template for <paramref name="id"/>, with no argument substitution.</summary>
        public string Localize(string id) => _localize(id);

        /// <summary>The localized template for <paramref name="id"/> with <paramref name="args"/> substituted (<c>{0}</c>, …).</summary>
        public string Format(string id, params object[] args) => StringFormat.Format(_localize(id), args);

        public void ReplyInfo(string message) => _reply(SystemMessageType.Info, message);

        public void ReplyError(string message) => _reply(SystemMessageType.Error, message);

        /// <summary>
        /// Persists a config mutation the same way the old JS <c>gobConfig.saveConfig()</c> did: flush the
        /// queued change events (which re-syncs the overlay so highlighting/frame react) and write the
        /// changed profile(s) to disk.
        /// </summary>
        public void SaveConfig()
        {
            Config.DispatchChangeEvents();
            Config.SaveProfiles();
        }
    }
}
