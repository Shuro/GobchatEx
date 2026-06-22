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

using System.Collections.Generic;

namespace Gobchat.Module.Chat.Command
{
    /// <summary>
    /// A single <c>/e gc</c> sub-command. Ported one-to-one from the former TypeScript
    /// <c>Command.ts</c> handlers; the registry/dispatch lives in <see cref="ChatCommandManager"/>.
    /// </summary>
    internal interface IChatCommandHandler
    {
        /// <summary>
        /// The names this handler answers to. Order matters: <see cref="ChatCommandManager"/> matches the
        /// first name that is a prefix of the typed command, so longer names must precede shorter ones that
        /// share a prefix (e.g. <c>"group"</c> before <c>"g"</c>) — exactly as the TS array order did.
        /// </summary>
        IReadOnlyList<string> AcceptedCommandNames { get; }

        /// <summary>
        /// Resource ids of the one-line help entries this handler contributes to <c>/e gc help</c>. Most
        /// handlers return a single id; the info/error handler returns one per concern.
        /// </summary>
        IEnumerable<string> HelpResourceIds { get; }

        /// <param name="commandName">The accepted name that matched (e.g. <c>"g"</c> or <c>"info off"</c>).</param>
        /// <param name="args">The remainder of the command after the matched name, trimmed.</param>
        void Execute(ChatCommandContext context, string commandName, string args);
    }
}
