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
using System.Globalization;
using System.Linq;
using Gobchat.App.Tests.Fakes;
using Gobchat.Core.Chat;
using Gobchat.Core.Util;
using Gobchat.Module.Chat.Command;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Gobchat.App.Tests.Module.Chat.Command
{
    /// <summary>
    /// Behaviour of the C# chat-command system that replaced the TypeScript <c>Command.ts</c>. The point of
    /// these tests is backward compatibility: every legacy <c>/e gc</c> form must produce the same config
    /// effects and the same localized replies it did in JS — and, just as importantly, an invalid command
    /// must produce <em>no</em> config write (a bad group index/name must never mutate the user's groups).
    /// Replies are checked against the real WebUIResources strings (invariant/English) so a regressed
    /// resource id or argument order fails the test.
    /// </summary>
    public sealed class ChatCommandManagerTests
    {
        private sealed class Harness
        {
            public FakeActorManager Actors { get; } = new();
            public FakeConfigManager Config { get; } = new();
            public List<(SystemMessageType Type, string Message)> Replies { get; } = new();
            public List<string> Forwarded { get; } = new();
            public int ExitCount { get; private set; }

            private readonly ChatCommandManager _manager;

            public Harness()
            {
                var context = new ChatCommandContext(
                    Actors,
                    Config,
                    Localize,
                    (type, message) => Replies.Add((type, message)),
                    () => ExitCount++,
                    command => Forwarded.Add(command));
                _manager = new ChatCommandManager(context);
            }

            public void Process(string message) => _manager.Process(message);

            // A single group with a (possibly seeded) trigger list under id "g1".
            public Harness WithGroup(string name, params string[] trigger)
            {
                Config.Seed("behaviour.groups.sorting", new JArray("g1"));
                Config.Seed("behaviour.groups.data.g1", new JObject
                {
                    ["name"] = name,
                    ["trigger"] = new JArray(trigger.Cast<object>().ToArray())
                });
                return this;
            }
        }

        private static string Localize(string id) => WebUIResources.ResourceManager.GetString(id, CultureInfo.InvariantCulture) ?? id;

        private static string Expected(string id, params object[] args) => StringFormat.Format(Localize(id), args);

        private static List<string> TriggerWrite(Harness h, string key)
            => h.Config.Writes.Single(w => w.Operation == "set" && w.Key == key).Value!.ToObject<List<string>>()!;

        // ----- group: backward-compatible forms -----

        [Fact]
        public void Group_Add_StoresNormalizedNameAndReplies()
        {
            var h = new Harness().WithGroup("Friends");

            h.Process("gc group 1 add Firstname Lastname");

            // Name is lowercased + whitespace-collapsed, exactly like the old JS handler.
            Assert.Equal(new[] { "firstname lastname" }, TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(1, h.Config.SaveProfilesCount);
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.add", "firstname lastname", 1, "Friends")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_Add_WithServerSuffix_KeepsServerInTrigger()
        {
            // The user's reported form: a second group, add with a [Server] suffix.
            var h = new Harness();
            h.Config.Seed("behaviour.groups.sorting", new JArray("g1", "g2"));
            h.Config.Seed("behaviour.groups.data.g1", new JObject { ["name"] = "Group1", ["trigger"] = new JArray() });
            h.Config.Seed("behaviour.groups.data.g2", new JObject { ["name"] = "Group2", ["trigger"] = new JArray() });

            h.Process("gc group 2 add Firstname Lastname [Server]");

            Assert.Equal(new[] { "firstname lastname [server]" }, TriggerWrite(h, "behaviour.groups.data.g2.trigger"));
        }

        [Fact]
        public void Group_TaskIsCaseInsensitive()
        {
            // The JS regex was case-insensitive; "ADD" must behave like "add".
            var h = new Harness().WithGroup("Friends");

            h.Process("gc group 1 ADD Firstname Lastname");

            Assert.Equal(new[] { "firstname lastname" }, TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
        }

        [Fact]
        public void Group_Alias_G_Clear_EmptiesTrigger()
        {
            var h = new Harness().WithGroup("Friends", "someone");

            h.Process("gc g 1 clear");

            Assert.Empty(TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.remove.all", 1, "Friends")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_Add_AlreadyPresent_DoesNotWrite()
        {
            var h = new Harness().WithGroup("Friends", "firstname lastname");

            h.Process("gc group 1 add Firstname Lastname");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(0, h.Config.SaveProfilesCount);
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.grouped", "firstname lastname", 1, "Friends")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_Remove_Present_RemovesAndReplies()
        {
            var h = new Harness().WithGroup("Friends", "firstname lastname");

            h.Process("gc group 1 remove Firstname Lastname");

            Assert.Empty(TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.remove", "firstname lastname", 1, "Friends")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_Remove_NotPresent_DoesNotWrite()
        {
            var h = new Harness().WithGroup("Friends");

            h.Process("gc group 1 remove Firstname Lastname");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.notgrouped", "firstname lastname", 1, "Friends")),
                h.Replies.Single());
        }

        // ----- group: both argument orders ("<n> <task> ..." and "<task> <n> ...") -----

        [Theory]
        [InlineData("gc group 1 add Firstname Lastname")] // index-first (legacy)
        [InlineData("gc group add 1 Firstname Lastname")] // task-first (new)
        public void Group_Add_AcceptsBothArgumentOrders(string command)
        {
            // The number and the add/remove/clear task may be given in either order; both must produce the
            // identical config write and reply.
            var h = new Harness().WithGroup("Friends");

            h.Process(command);

            Assert.Equal(new[] { "firstname lastname" }, TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.add", "firstname lastname", 1, "Friends")),
                h.Replies.Single());
        }

        [Theory]
        [InlineData("gc g 1 clear")] // index-first (legacy)
        [InlineData("gc g clear 1")] // task-first (new)
        public void Group_Clear_AcceptsBothArgumentOrders(string command)
        {
            var h = new Harness().WithGroup("Friends", "someone");

            h.Process(command);

            Assert.Empty(TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.remove.all", 1, "Friends")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_TaskFirst_Remove_RemovesAndReplies()
        {
            var h = new Harness().WithGroup("Friends", "firstname lastname");

            h.Process("gc group remove 1 Firstname Lastname");

            Assert.Empty(TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.remove", "firstname lastname", 1, "Friends")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_TaskFirst_WithServerSuffix_KeepsServerInTrigger()
        {
            // Task-first form must parse the [Server] suffix into the trigger just like the index-first form.
            var h = new Harness();
            h.Config.Seed("behaviour.groups.sorting", new JArray("g1", "g2"));
            h.Config.Seed("behaviour.groups.data.g1", new JObject { ["name"] = "Group1", ["trigger"] = new JArray() });
            h.Config.Seed("behaviour.groups.data.g2", new JObject { ["name"] = "Group2", ["trigger"] = new JArray() });

            h.Process("gc group add 2 Firstname Lastname [Server]");

            Assert.Equal(new[] { "firstname lastname [server]" }, TriggerWrite(h, "behaviour.groups.data.g2.trigger"));
        }

        // ----- group: invalid input must never mutate config -----

        [Fact]
        public void Group_IndexOutOfRange_RepliesErrorAndDoesNotWrite()
        {
            var h = new Harness().WithGroup("Friends"); // one group => valid indices are just [1, 1]

            h.Process("gc group 5 add Foo Bar");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(0, h.Config.SaveProfilesCount);
            Assert.Equal(
                (SystemMessageType.Error, Expected("main.cmdmanager.cmd.group.invalid.grpidx", 1)),
                h.Replies.Single());
        }

        [Fact]
        public void Group_IndexZero_RepliesErrorAndDoesNotWrite()
        {
            var h = new Harness().WithGroup("Friends");

            h.Process("gc group 0 clear");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(SystemMessageType.Error, h.Replies.Single().Type);
        }

        [Fact]
        public void Group_PremadeGroup_IsNotAddressable()
        {
            // Premade FFXIV friend groups (identified by an "ffgroup" field) are excluded from the command's
            // numbering entirely — even if one is in sorting (corrupt profile), it is filtered out, so the
            // index resolves against custom groups only and never touches a game-owned group. Here the only
            // id is premade, so there are no custom groups and index 1 is out of range.
            var h = new Harness();
            h.Config.Seed("behaviour.groups.sorting", new JArray("g1"));
            h.Config.Seed("behaviour.groups.data.g1", new JObject { ["name"] = "Star", ["hiddenName"] = "*", ["ffgroup"] = 0 });

            h.Process("gc group 1 add Foo Bar");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(
                (SystemMessageType.Error, Expected("main.cmdmanager.cmd.group.invalid.grpidx", 0)),
                h.Replies.Single());
        }

        [Fact]
        public void Group_CustomGroupWithoutTriggerKey_AcceptsAdd()
        {
            // Regression: a brand-new custom group has no "trigger" key yet (and the old data-template
            // seeded a typo'd "trigger:" key). It has no ffgroup, so it is NOT a premade group — the command
            // must add the member and create the trigger list, not reject it as "changed by the game".
            var h = new Harness();
            h.Config.Seed("behaviour.groups.sorting", new JArray("g1"));
            h.Config.Seed("behaviour.groups.data.g1", new JObject { ["name"] = "???" }); // no trigger, no ffgroup

            h.Process("gc group 1 add Firstname Lastname");

            Assert.Equal(new[] { "firstname lastname" }, TriggerWrite(h, "behaviour.groups.data.g1.trigger"));
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.add", "firstname lastname", 1, "???")),
                h.Replies.Single());
        }

        [Fact]
        public void GroupList_ListsCustomGroupsWithNumbers()
        {
            // "group list" shows the custom groups in order with their 1-based numbers (the same numbers
            // the add/remove/clear sub-commands use), and writes nothing.
            var h = new Harness();
            h.Config.Seed("behaviour.groups.sorting", new JArray("g1", "g2"));
            h.Config.Seed("behaviour.groups.data.g1", new JObject { ["name"] = "Friends", ["trigger"] = new JArray() });
            h.Config.Seed("behaviour.groups.data.g2", new JObject { ["name"] = "RP", ["trigger"] = new JArray() });

            h.Process("gc group list");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.group.list", "1. Friends, 2. RP")),
                h.Replies.Single());
        }

        [Fact]
        public void GroupList_Alias_G_Empty_RepliesNoGroups()
        {
            // With no custom groups, "g list" replies with the empty-list message (premade groups, having
            // no entries in sorting, are never listed).
            var h = new Harness();
            h.Config.Seed("behaviour.groups.sorting", new JArray());

            h.Process("gc g list");

            Assert.Equal(
                (SystemMessageType.Info, Localize("main.cmdmanager.cmd.group.list.empty")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_MalformedCommand_RepliesInvalidCmd()
        {
            var h = new Harness().WithGroup("Friends");

            h.Process("gc group 1 frobnicate Foo"); // not add/remove/clear

            Assert.Empty(h.Config.Writes);
            Assert.Equal(
                (SystemMessageType.Error, Localize("main.cmdmanager.cmd.group.invalid.cmd")),
                h.Replies.Single());
        }

        [Fact]
        public void Group_AddWithoutName_RepliesInvalidCmd()
        {
            var h = new Harness().WithGroup("Friends");

            h.Process("gc group 1 add");

            Assert.Empty(h.Config.Writes);
            Assert.Equal(
                (SystemMessageType.Error, Localize("main.cmdmanager.cmd.group.invalid.cmd")),
                h.Replies.Single());
        }

        // ----- profile -----

        [Fact]
        public void Profile_Load_SwitchesActiveProfileByName()
        {
            var h = new Harness();
            h.Config.AddProfile("p1", "Default").AddProfile("p2", "RP");

            h.Process("gc profile load rp"); // case-insensitive, like the JS handler

            Assert.Equal("p2", h.Config.ActiveProfileId);
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.profile.load", "RP")),
                h.Replies.Single());
        }

        [Fact]
        public void Profile_Load_UnknownName_DoesNotSwitch()
        {
            var h = new Harness();
            h.Config.AddProfile("p1", "Default").AddProfile("p2", "RP");
            var setsBefore = h.Config.ActiveProfileSetCount;

            h.Process("gc profile load nope");

            Assert.Equal(setsBefore, h.Config.ActiveProfileSetCount);
            Assert.Equal(
                (SystemMessageType.Error, Expected("main.cmdmanager.cmd.profile.load.invalid", "Default, RP")),
                h.Replies.Single());
        }

        [Fact]
        public void Profile_Load_NoName_RepliesError()
        {
            var h = new Harness();
            h.Config.AddProfile("p1", "Default");

            h.Process("gc profile load");

            Assert.Equal(0, h.Config.ActiveProfileSetCount);
            Assert.Equal(SystemMessageType.Error, h.Replies.Single().Type);
        }

        // ----- config reset frame -----

        [Fact]
        public void ConfigResetFrame_DeletesSizeAndPosition()
        {
            var h = new Harness();

            h.Process("gc config reset frame");

            Assert.Contains(h.Config.Writes, w => w.Operation == "delete" && w.Key == "behaviour.frame.chat.size");
            Assert.Contains(h.Config.Writes, w => w.Operation == "delete" && w.Key == "behaviour.frame.chat.position");
            Assert.Equal(1, h.Config.SaveProfilesCount);
            Assert.Equal(
                (SystemMessageType.Info, Localize("main.cmdmanager.cmd.config.reset.frame")),
                h.Replies.Single());
        }

        // ----- player data -----

        [Fact]
        public void PlayerCount_RepliesWithCount()
        {
            var h = new Harness();
            h.Actors.PlayerCount = 4;

            h.Process("gc player count");

            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.playercount", 4)),
                h.Replies.Single());
        }

        [Fact]
        public void PlayerList_RepliesNearestFirst()
        {
            var h = new Harness();
            h.Actors.PlayersInArea = new[] { "Alice", "Bob" };
            h.Actors.DistanceProvider = name => name == "Alice" ? 2.5f : 1.0f;

            h.Process("gc player list");

            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.playerlist", "Bob: 1.00, Alice: 2.50")),
                h.Replies.Single());
        }

        [Fact]
        public void PlayerDistance_RepliesWithNameAndDistance()
        {
            var h = new Harness();
            h.Actors.DistanceProvider = _ => 3.5f;

            h.Process("gc player distance Firstname Lastname");

            // Fixed vs. the old JS, which left the name placeholder empty: now both name and distance fill in.
            Assert.Equal(
                (SystemMessageType.Info, Expected("main.cmdmanager.cmd.playerdistance", "Firstname Lastname", "3.50y")),
                h.Replies.Single());
        }

        // ----- lifecycle + UI forwarding -----

        [Fact]
        public void Close_InvokesExit()
        {
            var h = new Harness();

            h.Process("gc close");

            Assert.Equal(1, h.ExitCount);
            Assert.Empty(h.Replies);
        }

        [Theory]
        [InlineData("gc info on", "info on")]
        [InlineData("gc info off", "info off")]
        [InlineData("gc error on", "error on")]
        [InlineData("gc error off", "error off")]
        [InlineData("gc config open", "config open")]
        public void UiCommands_AreForwardedNotExecuted(string input, string expectedForward)
        {
            var h = new Harness();

            h.Process(input);

            Assert.Equal(new[] { expectedForward }, h.Forwarded);
            Assert.Empty(h.Replies);
            Assert.Empty(h.Config.Writes);
        }

        // ----- help + unknown -----

        [Fact]
        public void Help_ListsEveryCommand()
        {
            var h = new Harness();

            h.Process("gc help");

            // One Info message per line so each renders as its own (readable) chat entry: header + one
            // line per command, with group and info/error contributing two each => 13 messages.
            Assert.All(h.Replies, r => Assert.Equal(SystemMessageType.Info, r.Type));
            var messages = h.Replies.Select(r => r.Message).ToList();
            Assert.Equal(13, messages.Count);
            Assert.Equal(Localize("main.cmdmanager.help.header"), messages[0]);
            foreach (var id in new[]
            {
                "main.cmdmanager.help.group", "main.cmdmanager.help.group.list", "main.cmdmanager.help.profile",
                "main.cmdmanager.help.close", "main.cmdmanager.help.config.open", "main.cmdmanager.help.config.reset",
                "main.cmdmanager.help.player.count", "main.cmdmanager.help.player.list", "main.cmdmanager.help.player.distance",
                "main.cmdmanager.help.info", "main.cmdmanager.help.error", "main.cmdmanager.help.help",
            })
            {
                Assert.Contains(Localize(id), messages);
            }
        }

        [Fact]
        public void UnknownCommand_RepliesErrorPointingAtHelp()
        {
            var h = new Harness();

            h.Process("gc frobnicate");

            Assert.Empty(h.Config.Writes);
            Assert.Empty(h.Forwarded);
            Assert.Equal(
                (SystemMessageType.Error, Expected("main.cmdmanager.cmd.unknown", "frobnicate")),
                h.Replies.Single());
        }

        [Fact]
        public void BareGc_RepliesUnknownError()
        {
            var h = new Harness();

            h.Process("gc");

            var reply = h.Replies.Single();
            Assert.Equal(SystemMessageType.Error, reply.Type);
            Assert.Contains("/e gc help", reply.Message);
        }

        [Fact]
        public void NonCommandEcho_IsIgnored()
        {
            var h = new Harness();

            h.Process("just chatting, not a command");

            Assert.Empty(h.Replies);
            Assert.Empty(h.Config.Writes);
            Assert.Empty(h.Forwarded);
        }
    }
}
