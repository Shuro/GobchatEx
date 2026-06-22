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
using System.Collections.Generic;

namespace Gobchat.Module.Chat.Command
{
    /// <summary>
    /// Detects and dispatches <c>/e gc</c> chat commands. This is a direct port of the former TypeScript
    /// <c>CommandManager</c> (resources/ui/modules/Command.ts): the same <c>"gc"</c> prefix, the same
    /// first-match-by-registration-order handler lookup, and the same accepted-name lists — so every legacy
    /// command form keeps working byte-for-byte. New on top: a localized <c>help</c> listing and a clear
    /// "unknown command" error instead of the old bare comma-separated command dump.
    /// </summary>
    internal sealed class ChatCommandManager
    {
        private const string CommandPrefix = "gc";

        private readonly List<IChatCommandHandler> _handlers = new();
        private readonly ChatCommandContext _context;

        public ChatCommandManager(ChatCommandContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));

            // Registration order is load-bearing: GetHandler returns the first accepted name that prefixes
            // the command, so this mirrors the TS constructor's order exactly. Do not reorder without
            // checking for prefix collisions (e.g. "config open" vs "config reset frame").
            Register(new PlayerGroupCommandHandler());
            Register(new ProfileSwitchCommandHandler());
            Register(new CloseCommandHandler());
            Register(new ConfigOpenCommandHandler());
            Register(new ConfigResetCommandHandler());
            Register(new PlayerCountCommandHandler());
            Register(new PlayerListCommandHandler());
            Register(new PlayerDistanceCommandHandler());
            Register(new ShowSystemMessagesCommandHandler());
            // Help reads the live handler list (populated above) to enumerate every command, so it must be
            // registered last; it adds itself to the list before it is ever executed.
            Register(new HelpCommandHandler(_handlers));
        }

        private void Register(IChatCommandHandler handler) => _handlers.Add(handler);

        /// <summary>
        /// Parses one ECHO-channel line and runs the matching command, if any. A line that does not start
        /// with the <c>gc</c> prefix is ignored (it is just a normal echo). Mirrors
        /// <c>CommandManager.processCommand</c>.
        /// </summary>
        public void Process(string? message)
        {
            if (message == null)
                return;

            message = message.Trim();
            if (!message.StartsWith(CommandPrefix, StringComparison.Ordinal))
                return;

            // TS did `message.substring(prefix.length + 1).trim()`; JS substring clamps past the end,
            // C# Substring throws, so clamp to "" when there is nothing past the prefix + separator.
            var rest = message.Length <= CommandPrefix.Length + 1
                ? string.Empty
                : message.Substring(CommandPrefix.Length + 1).Trim();

            var match = FindHandler(rest);
            if (match.Handler != null)
                match.Handler.Execute(_context, match.Name, match.Args);
            else
                _context.ReplyError(_context.Format("main.cmdmanager.cmd.unknown", rest));
        }

        // Port of CommandManager#getHandler: first handler, in registration order, whose accepted name (in
        // its own array order) is a prefix of the typed command. The match is case-sensitive, like JS
        // String.startsWith — commands are always typed lowercase.
        private (IChatCommandHandler? Handler, string Name, string Args) FindHandler(string message)
        {
            foreach (var handler in _handlers)
            {
                foreach (var name in handler.AcceptedCommandNames)
                {
                    if (message.StartsWith(name, StringComparison.Ordinal))
                    {
                        var args = message.Length <= name.Length + 1
                            ? string.Empty
                            : message.Substring(name.Length + 1).Trim();
                        return (handler, name, args);
                    }
                }
            }
            return (null, string.Empty, string.Empty);
        }
    }
}
