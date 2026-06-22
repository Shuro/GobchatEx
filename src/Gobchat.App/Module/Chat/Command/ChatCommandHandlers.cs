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
using System.Globalization;
using System.Linq;

namespace Gobchat.Module.Chat.Command
{
    /// <summary><c>profile load &lt;name&gt;</c> — switches the active config profile by (case-insensitive) name.</summary>
    internal sealed class ProfileSwitchCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "profile load" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.profile" };

        public void Execute(ChatCommandContext context, string commandName, string args)
        {
            var profileIds = context.Config.Profiles;

            if (!string.IsNullOrEmpty(args))
            {
                var needle = args.ToLowerInvariant();
                foreach (var profileId in profileIds)
                {
                    var name = GetProfileName(context, profileId);
                    if (name != null && needle == name.ToLowerInvariant())
                    {
                        context.Config.ActiveProfileId = profileId;
                        context.ReplyInfo(context.Format("main.cmdmanager.cmd.profile.load", name));
                        return;
                    }
                }
            }

            var profiles = string.Join(", ", profileIds
                .Select(id => GetProfileName(context, id))
                .Where(name => name != null));
            context.ReplyError(context.Format("main.cmdmanager.cmd.profile.load.invalid", profiles));
        }

        private static string? GetProfileName(ChatCommandContext context, string profileId)
            => context.Config.GetProfile(profileId).GetProperty<string>("profile.name", null!);
    }

    /// <summary><c>close</c> — shuts GobchatEx down.</summary>
    internal sealed class CloseCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "close" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.close" };

        public void Execute(ChatCommandContext context, string commandName, string args) => context.Exit();
    }

    /// <summary><c>config open</c> — opens the settings window (UI-side, forwarded to the overlay).</summary>
    internal sealed class ConfigOpenCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "config open" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.config.open" };

        public void Execute(ChatCommandContext context, string commandName, string args) => context.ForwardToUi("config open");
    }

    /// <summary><c>config reset frame</c> — resets the chat overlay's size and position to the defaults.</summary>
    internal sealed class ConfigResetCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "config reset frame" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.config.reset" };

        public void Execute(ChatCommandContext context, string commandName, string args)
        {
            context.ReplyInfo(context.Localize("main.cmdmanager.cmd.config.reset.frame"));
            // Delete the user overrides so the values fall back to the profile defaults (JS gobConfig.reset).
            context.Config.DeleteProperty("behaviour.frame.chat.size");
            context.Config.DeleteProperty("behaviour.frame.chat.position");
            context.SaveConfig();
        }
    }

    /// <summary><c>player count</c> — replies with the number of nearby players.</summary>
    internal sealed class PlayerCountCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "player count" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.player.count" };

        public void Execute(ChatCommandContext context, string commandName, string args)
            => context.ReplyInfo(context.Format("main.cmdmanager.cmd.playercount", context.Actors.GetPlayerCount()));
    }

    /// <summary><c>player list</c> — replies with nearby players and their distance, nearest first.</summary>
    internal sealed class PlayerListCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "player list" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.player.list" };

        public void Execute(ChatCommandContext context, string commandName, string args)
        {
            var list = GetPlayersAndDistance(context);
            context.ReplyInfo(context.Format("main.cmdmanager.cmd.playerlist", string.Join(", ", list)));
        }

        // Ported from GobchatBrowserAPI.GetPlayersAndDistance: pair each nearby player with its distance,
        // sort nearest-first, and format "Name: 0.00" (invariant, two decimals).
        private static string[] GetPlayersAndDistance(ChatCommandContext context)
        {
            var players = context.Actors.GetPlayersInArea();
            if (players.Length == 0)
                return Array.Empty<string>();

            var result = new List<(float Distance, string Name)>(players.Length);
            foreach (var player in players)
                result.Add((context.Actors.GetDistanceToPlayerWithName(player), player));

            result.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return result.Select(e => $"{e.Name}: {e.Distance.ToString("0.00", CultureInfo.InvariantCulture)}").ToArray();
        }
    }

    /// <summary><c>player distance &lt;name&gt;</c> — replies with the distance to a named player.</summary>
    internal sealed class PlayerDistanceCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "player distance" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.player.distance" };

        public void Execute(ChatCommandContext context, string commandName, string args)
        {
            var distance = context.Actors.GetDistanceToPlayerWithName(args);
            var formatted = distance.ToString("0.00", CultureInfo.InvariantCulture) + "y";
            // The "Distance to {0} is {1}." template takes the target name and the distance; the old JS
            // passed only the distance (leaving {0} as the name and {1} literal), so this also fixes that.
            context.ReplyInfo(context.Format("main.cmdmanager.cmd.playerdistance", args, formatted));
        }
    }

    /// <summary><c>info on|off</c> / <c>error on|off</c> — show/hide GobchatEx info or error lines (UI-side, forwarded).</summary>
    internal sealed class ShowSystemMessagesCommandHandler : IChatCommandHandler
    {
        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "info on", "info off", "error on", "error off" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.info", "main.cmdmanager.help.error" };

        public void Execute(ChatCommandContext context, string commandName, string args) => context.ForwardToUi(commandName);
    }

    /// <summary><c>help</c> — lists every command with a one-line, localized usage description.</summary>
    internal sealed class HelpCommandHandler : IChatCommandHandler
    {
        private readonly IReadOnlyList<IChatCommandHandler> _allHandlers;

        public HelpCommandHandler(IReadOnlyList<IChatCommandHandler> allHandlers)
        {
            _allHandlers = allHandlers ?? throw new ArgumentNullException(nameof(allHandlers));
        }

        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "help" };
        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.help" };

        public void Execute(ChatCommandContext context, string commandName, string args)
        {
            // One message per line: the chat overlay collapses newlines within a single entry to spaces
            // (no white-space: pre-wrap there), so a joined block would render as one unreadable line.
            // Each ReplyInfo becomes its own chat entry, giving a readable list.
            context.ReplyInfo(context.Localize("main.cmdmanager.help.header"));
            foreach (var handler in _allHandlers)
                foreach (var id in handler.HelpResourceIds)
                    context.ReplyInfo(context.Localize(id));
        }
    }
}
