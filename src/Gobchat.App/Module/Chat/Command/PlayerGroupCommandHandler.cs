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
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Gobchat.Module.Chat.Command
{
    /// <summary>
    /// <c>group|g &lt;n&gt; add|remove|clear &lt;player&gt; [server]</c> edits a custom highlight group's player
    /// list, and <c>group list</c> lists the custom groups. The group number and the add/remove/clear task
    /// may be given in either order (<c>group 1 add Foo</c> or <c>group add 1 Foo</c>). Only the user's
    /// <b>custom</b> groups are ever addressed: the premade FFXIV friend groups (identified by an
    /// <c>ffgroup</c> field) are excluded from the numbering entirely, so the command can never touch a
    /// game-owned group. The add/remove/clear matching and message order are a port of the former
    /// TypeScript handler.
    /// </summary>
    internal sealed class PlayerGroupCommandHandler : IChatCommandHandler
    {
        // Both argument orders are accepted: "<n> <task> <player> [server]" (index-first, the legacy form)
        // and "<task> <n> <player> [server]" (task-first). Each regex captures the same named groups so the
        // parsing below is order-agnostic:
        //   idx       - group number (\d+)
        //   task      - add | remove | clear
        //   composite - the optional "<player> [server]" tail; name = player, server = [server]
        // The name tail is a port of the JS regex's class, which includes an acute accent (U+00B4); it is
        // written here as the regex hex escape \xB4 so this source stays pure ASCII (the engine matches the
        // same U+00B4 character). Index-first is tried first, so every legacy "<n> <task> ..." line parses
        // exactly as the former TypeScript handler parsed it.
        private const string NameTailPattern = @"\b(?<composite>(?<name>[ \w'`\xB4-]+)(?<server>\s*\[\w+\])?)?";

        private static readonly Regex GroupRegexIndexFirst = new Regex(
            @"(?<idx>\d+)\b\s+\b(?<task>add|remove|clear)\b\s*.*?" + NameTailPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex GroupRegexTaskFirst = new Regex(
            @"(?<task>add|remove|clear)\b\s+\b(?<idx>\d+)\b\s*.*?" + NameTailPattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex CollapseWhitespace = new Regex(@"\s\s+");

        public IReadOnlyList<string> AcceptedCommandNames { get; } = new[] { "group", "g" };

        public IEnumerable<string> HelpResourceIds { get; } = new[] { "main.cmdmanager.help.group", "main.cmdmanager.help.group.list" };

        public void Execute(ChatCommandContext context, string commandName, string args)
        {
            // Only the user's custom groups are addressable; premade (ff) groups are excluded entirely.
            var customIds = CustomGroupIds(context);

            // "group list" / "g list": show the custom groups and their numbers.
            if (args.Trim().Equals("list", StringComparison.OrdinalIgnoreCase))
            {
                ListGroups(context, customIds);
                return;
            }

            // Try the legacy index-first order, then the task-first order; both yield the same named groups.
            var match = GroupRegexIndexFirst.Match(args);
            if (!match.Success)
                match = GroupRegexTaskFirst.Match(args);

            if (!match.Success)
            {
                context.ReplyError(context.Localize("main.cmdmanager.cmd.group.invalid.cmd"));
                return;
            }

            // JS reports an unmatched optional group as null; .NET reports !Success with an empty Value.
            string? Group(string name) => match.Groups[name].Success ? match.Groups[name].Value : null;

            // idx is \d+ and required for a successful match, so this parse always succeeds; guarded anyway.
            if (!int.TryParse(Group("idx"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var groupIdx))
            {
                context.ReplyError(context.Localize("main.cmdmanager.cmd.group.invalid.cmd"));
                return;
            }

            var task = match.Groups["task"].Value.ToLowerInvariant(); // add | remove | clear
            var playerNameComposite = Group("name");
            var serverName = Group("server");

            // add/remove need a target name; clear does not.
            if ((task == "add" || task == "remove") && string.IsNullOrEmpty(Group("composite")))
            {
                context.ReplyError(context.Localize("main.cmdmanager.cmd.group.invalid.cmd"));
                return;
            }

            if (groupIdx <= 0 || customIds.Count < groupIdx)
            {
                context.ReplyError(context.Format("main.cmdmanager.cmd.group.invalid.grpidx", customIds.Count));
                return;
            }

            var groupId = customIds[groupIdx - 1];
            var group = context.Config.GetProperty<JObject>($"behaviour.groups.data.{groupId}");
            var groupName = group["name"]?.ToObject<string>();

            if (task == "clear")
            {
                context.Config.SetProperty($"behaviour.groups.data.{groupId}.trigger", new List<string>());
                context.ReplyInfo(context.Format("main.cmdmanager.cmd.group.remove.all", groupIdx, groupName!));
                context.SaveConfig();
                return;
            }

            // Reached only for add/remove, where the name composite (group 4) is guaranteed present.
            var playerNameAndServer = serverName != null
                ? playerNameComposite!.Trim() + " " + serverName.Trim()
                : playerNameComposite!.Trim();
            playerNameAndServer = CollapseWhitespace.Replace(playerNameAndServer.ToLowerInvariant(), " ");

            if (playerNameAndServer.Length == 0)
            {
                context.ReplyError(context.Format("main.cmdmanager.cmd.group.invalid.name", playerNameAndServer));
                return;
            }

            // A custom group may have no trigger key yet (a brand-new group); treat that as an empty list
            // and let the add below create the key.
            var trigger = group["trigger"]?.ToObject<List<string>>() ?? new List<string>();

            if (task == "add")
            {
                if (!trigger.Contains(playerNameAndServer))
                {
                    trigger.Add(playerNameAndServer);
                    context.Config.SetProperty($"behaviour.groups.data.{groupId}.trigger", trigger);
                    context.ReplyInfo(context.Format("main.cmdmanager.cmd.group.add", playerNameAndServer, groupIdx, groupName!));
                    context.SaveConfig();
                }
                else
                {
                    context.ReplyInfo(context.Format("main.cmdmanager.cmd.group.grouped", playerNameAndServer, groupIdx, groupName!));
                }
            }
            else // remove
            {
                if (trigger.Contains(playerNameAndServer))
                {
                    trigger.RemoveAll(name => name == playerNameAndServer);
                    context.Config.SetProperty($"behaviour.groups.data.{groupId}.trigger", trigger);
                    context.ReplyInfo(context.Format("main.cmdmanager.cmd.group.remove", playerNameAndServer, groupIdx, groupName!));
                    context.SaveConfig();
                }
                else
                {
                    context.ReplyInfo(context.Format("main.cmdmanager.cmd.group.notgrouped", playerNameAndServer, groupIdx, groupName!));
                }
            }
        }

        // The custom group ids in display order: behaviour.groups.sorting with any premade (ff) group id
        // filtered out. sorting holds custom ids only (since 2.0.9), so the filter is defensive — it
        // guarantees /e gc group never addresses a game-owned group even on a hand-edited profile.
        private static List<string> CustomGroupIds(ChatCommandContext context)
        {
            var sorting = context.Config.GetProperty<List<string>>("behaviour.groups.sorting");
            var result = new List<string>(sorting.Count);
            foreach (var id in sorting)
            {
                var group = context.Config.GetProperty<JObject>($"behaviour.groups.data.{id}", null!);
                if (group != null && group["ffgroup"] == null)
                    result.Add(id);
            }
            return result;
        }

        private static void ListGroups(ChatCommandContext context, List<string> customIds)
        {
            if (customIds.Count == 0)
            {
                context.ReplyInfo(context.Localize("main.cmdmanager.cmd.group.list.empty"));
                return;
            }

            var entries = new List<string>(customIds.Count);
            for (var i = 0; i < customIds.Count; i++)
            {
                var group = context.Config.GetProperty<JObject>($"behaviour.groups.data.{customIds[i]}", null!);
                var name = group?["name"]?.ToObject<string>() ?? "";
                entries.Add($"{i + 1}. {name}");
            }
            context.ReplyInfo(context.Format("main.cmdmanager.cmd.group.list", string.Join(", ", entries)));
        }
    }
}
