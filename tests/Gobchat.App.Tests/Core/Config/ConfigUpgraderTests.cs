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

using Gobchat.Core.Config;
using Newtonsoft.Json.Linq;
using System.Linq;
using Xunit;

namespace Gobchat.App.Tests.Core.Config
{
    /// <summary>
    /// User profiles are JSON with a schema "version"; on load, old profiles are migrated forward.
    /// WHY this matters: a wrong version read or a chain that fails to terminate would corrupt or
    /// reset a user's settings. These cover the orchestration (version coercion, guards, applying an
    /// upgrade to the final schema) and one representative transform in isolation.
    /// </summary>
    public sealed class ConfigUpgraderTests
    {
        [Fact]
        public void UpgradeConfig_MissingVersion_Throws()
        {
            Assert.Throws<MissingPropertyException>(() => new ConfigUpgrader().UpgradeConfig(new JObject()));
        }

        [Fact]
        public void UpgradeConfig_VersionWrongType_Throws()
        {
            var config = new JObject { ["version"] = new JArray() };

            Assert.Throws<InvalidPropertyTypeException>(() => new ConfigUpgrader().UpgradeConfig(config));
        }

        [Fact]
        public void UpgradeConfig_StringVersion_IsCoercedToInteger()
        {
            // 999999 is above every known upgrade range, so no transform runs; only the coercion is exercised.
            var config = new JObject { ["version"] = "999999" };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(JTokenType.Integer, result["version"]!.Type);
            Assert.Equal(999999, (int)result["version"]!);
        }

        [Fact]
        public void UpgradeConfig_AlreadyNewerThanAnyUpgrade_IsUnchanged()
        {
            var config = new JObject { ["version"] = 999999 };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(999999, (int)result["version"]!);
        }

        [Fact]
        public void UpgradeConfig_RunsChainToFinalSchemaVersion()
        {
            // A profile at an old schema version is migrated forward through the whole chain
            // (1906 -> ConfigUpgrade_1_12_0 -> 11200 -> ConfigUpgrade_2_0_0 -> 20000 ->
            // ConfigUpgrade_2_0_1 -> 20001 -> ConfigUpgrade_2_0_2 -> 20002 ->
            // ConfigUpgrade_2_0_3 -> 20003 -> ConfigUpgrade_2_0_4 -> 20004 ->
            // ConfigUpgrade_2_0_5 -> 20005 -> ConfigUpgrade_2_0_6 -> 20006 -> ConfigUpgrade_2_0_7 ->
            // 20007 -> ConfigUpgrade_2_0_8 -> 20008 -> ConfigUpgrade_2_0_9 -> 20009 ->
            // ConfigUpgrade_2_0_10 -> 20010 -> ConfigUpgrade_2_0_11 -> 20011). The transforms are all "if
            // available" no-ops/additions on this minimal config; only that the chain reaches the final
            // schema version is asserted here.
            var config = new JObject { ["version"] = 1906 };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(20011, (int)result["version"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_7_BumpsVersionAndPreservesContent()
        {
            // 2.0.7 only bumps the schema version: the move of the app-global keys into the separate
            // app-settings store (and their strip from profiles) is done by GobchatConfigManager after
            // load, where the active profile is known. The transform itself must leave the profile intact.
            var input = JObject.Parse(@"{ ""version"": 20006, ""behaviour"": { ""language"": ""de"" }, ""style"": { ""theme"": ""FFXIV Modern Light"" } }");

            var upgrade = new ConfigUpgrade_2_0_7();
            var result = upgrade.Upgrade(input);

            Assert.Equal(20006, upgrade.MinVersion);
            Assert.Equal(20007, upgrade.TargetVersion);
            // Values are untouched by the transform (the manager does the actual migration on load).
            Assert.Equal("de", (string)result["behaviour"]!["language"]!);
            Assert.Equal("FFXIV Modern Light", (string)result["style"]!["theme"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_0_MovesOldDefaultThemeToModern()
        {
            // 2.0's whole point: a profile still on the previous default theme adopts FFXIV Modern.
            var input = JObject.Parse(@"{ ""version"": 11200, ""style"": { ""theme"": ""FFXIV Dark"" } }");

            var result = new ConfigUpgrade_2_0_0().Upgrade(input);

            Assert.Equal("FFXIV Modern", (string)result["style"]!["theme"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_0_PreservesACustomisedTheme()
        {
            // A deliberately chosen non-default theme must survive untouched, otherwise the migration
            // would clobber the user's choice instead of only nudging stale defaults.
            var input = JObject.Parse(@"{ ""version"": 11200, ""style"": { ""theme"": ""FFXIV Light"" } }");

            var result = new ConfigUpgrade_2_0_0().Upgrade(input);

            Assert.Equal("FFXIV Light", (string)result["style"]!["theme"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_6_TakesBackgroundFromTheme_AndMigratesLegacyDark()
        {
            // The chat background colour now comes from the theme: a saved custom colour is cleared (so
            // the theme's own colour shows), transparency moves to its own 0-100 setting, and a profile
            // still on the retired FFXIV Dark theme is moved to FFXIV Modern so it keeps a background.
            var input = JObject.Parse(@"{
              ""version"": 20005,
              ""style"": { ""theme"": ""FFXIV Dark"", ""chat-history"": { ""background-color"": ""rgba(16, 19, 24, 0.86)"" } }
            }");

            var result = new ConfigUpgrade_2_0_6().Upgrade(input);

            Assert.Equal(JTokenType.Null, result["style"]!["chat-history"]!["background-color"]!.Type);
            Assert.Equal(90, (int)result["style"]!["chat-history"]!["background-opacity"]!);
            Assert.Equal("FFXIV Modern", (string)result["style"]!["theme"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_6_MigratesLegacyLight_AndPreservesAnExistingOpacity()
        {
            // FFXIV Light maps to the light Modern variant; a transparency the user already chose must
            // not be reset to the default (the seed is "if unavailable" only).
            var input = JObject.Parse(@"{
              ""version"": 20005,
              ""style"": { ""theme"": ""FFXIV Light"", ""chat-history"": { ""background-opacity"": 75 } }
            }");

            var result = new ConfigUpgrade_2_0_6().Upgrade(input);

            Assert.Equal("FFXIV Modern Light", (string)result["style"]!["theme"]!);
            Assert.Equal(75, (int)result["style"]!["chat-history"]!["background-opacity"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_6_LeavesAModernThemeUntouched()
        {
            // Only the two legacy themes are remapped; a Modern selection must survive unchanged.
            var input = JObject.Parse(@"{ ""version"": 20005, ""style"": { ""theme"": ""FFXIV Modern Light"" } }");

            var result = new ConfigUpgrade_2_0_6().Upgrade(input);

            Assert.Equal("FFXIV Modern Light", (string)result["style"]!["theme"]!);
        }

        [Fact]
        public void ConfigUpgrade_1_3_0_AddsMentionsStructureAndBumpsVersion()
        {
            var input = JObject.Parse(@"{ ""version"": 2, ""behaviour"": {} }");

            var result = new ConfigUpgrade_1_3_0().Upgrade(input);

            Assert.Equal(3, (int)result["version"]!);
            Assert.NotNull(result["behaviour"]!["mentions"]!["data"]!["base"]);
            Assert.IsType<JArray>(result["behaviour"]!["mentions"]!["order"]);
        }

        [Fact]
        public void ConfigUpgrade_2_0_1_MigratesOldSegmentToLockedSections()
        {
            // The Formatting redesign: old (unlocked) segment data becomes the fixed locked baked-in set,
            // the multi-token guillemet entry splits into mirrored say5/say6, custom pairs and per-pair
            // on/off state survive, and order is regrouped to OOC-first precedence. WHY: this must update
            // a saved profile without dropping a user's custom pairs or silently re-enabling a pair they
            // turned off.
            var input = JObject.Parse(@"{
              ""version"": 20000,
              ""behaviour"": { ""segment"": {
                ""order"": [ ""ooc"", ""say3"", ""say5"", ""custom1"" ],
                ""data-template"": { ""active"": true, ""type:"": ""SAY"", ""startTokens"": [], ""endTokens"": [] },
                ""data"": {
                  ""ooc"":     { ""active"": true,  ""type"": ""OOC"", ""startTokens"": [ ""(("" ], ""endTokens"": [ ""))"" ] },
                  ""say3"":    { ""active"": false, ""type"": ""SAY"", ""startTokens"": [ ""„"" ], ""endTokens"": [ ""”"" ] },
                  ""say5"":    { ""active"": true,  ""type"": ""SAY"", ""startTokens"": [ ""»"", ""«"" ], ""endTokens"": [ ""«"", ""»"" ] },
                  ""custom1"": { ""active"": true,  ""type"": ""SAY"", ""startTokens"": [ ""!!"" ], ""endTokens"": [ ""!!"" ] }
                }
              } }
            }");

            var result = new ConfigUpgrade_2_0_1().Upgrade(input);
            var data = (JObject)result["behaviour"]!["segment"]!["data"]!;

            // Baked-in pairs are now locked; the user's off-state on say3 is preserved.
            Assert.True((bool)data["say1"]!["locked"]!);
            Assert.False((bool)data["say3"]!["active"]!);

            // say5 split into single-token say5/say6 that mirror each other (»…« and «…»).
            Assert.Single((JArray)data["say5"]!["startTokens"]!);
            Assert.Equal((string)data["say5"]!["startTokens"]![0]!, (string)data["say6"]!["endTokens"]![0]!);
            Assert.Equal((string)data["say5"]!["endTokens"]![0]!, (string)data["say6"]!["startTokens"]![0]!);

            // The custom pair survives untouched and stays unlocked (editable).
            Assert.Null(data["custom1"]!["locked"]);
            Assert.Equal("!!", (string)data["custom1"]!["startTokens"]![0]!);

            // Order keeps OOC-first precedence and includes the new say6.
            var order = ((JArray)result["behaviour"]!["segment"]!["order"]!).Select(t => (string)t!).ToList();
            Assert.Equal("ooc", order[0]);
            Assert.Contains("say6", order);
            Assert.Contains("custom1", order);

            // The data-template typo ("type:") is corrected to "type".
            Assert.Equal("SAY", (string)result["behaviour"]!["segment"]!["data-template"]!["type"]!);
            Assert.Null(result["behaviour"]!["segment"]!["data-template"]!["type:"]);
        }

        [Fact]
        public void ConfigUpgrade_2_0_1_LeavesAlreadyMigratedDataUntouched()
        {
            // Idempotent: a profile that already carries the new (locked) shape must not be rebuilt, or
            // re-running the chain would clobber it / inject duplicate baked-in entries.
            var input = JObject.Parse(@"{
              ""version"": 20000,
              ""behaviour"": { ""segment"": {
                ""order"": [ ""ooc"" ],
                ""data"": { ""ooc"": { ""locked"": true, ""active"": true, ""type"": ""OOC"", ""startTokens"": [ ""(("" ], ""endTokens"": [ ""))"" ] } }
              } }
            }");

            var result = new ConfigUpgrade_2_0_1().Upgrade(input);
            var data = (JObject)result["behaviour"]!["segment"]!["data"]!;

            Assert.Single(data.Properties());
            Assert.Null(data["say6"]);
        }

        [Fact]
        public void ConfigUpgrade_2_0_2_StripsImportantFromSearchHighlight()
        {
            // Coloris can't parse a " !important" suffix, so storing it broke the search-highlight colour
            // field. The suffix now lives only at CSS-generation time; a saved profile must shed it.
            var input = JObject.Parse(@"{ ""version"": 20001, ""style"": { ""chatsearch"": { ""marked"": { ""background-color"": ""rgba(224, 164, 78, 0.16) !important"" } } } }");

            var result = new ConfigUpgrade_2_0_2().Upgrade(input);

            Assert.Equal("rgba(224, 164, 78, 0.16)", (string)result["style"]!["chatsearch"]!["marked"]!["background-color"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_2_DropsRemovedConfigFontSizeKey()
        {
            // The "Config font size" control was removed; its key must not linger in saved profiles.
            var input = JObject.Parse(@"{ ""version"": 20001, ""style"": { ""config"": { ""font-size"": ""1.56vw"" } } }");

            var result = new ConfigUpgrade_2_0_2().Upgrade(input);

            // The now-empty parent object is removed too.
            Assert.Null(result["style"]!["config"]);
        }

        [Fact]
        public void ConfigUpgrade_2_0_2_MovesOldDefaultFontToModernStack()
        {
            // A profile still on the old Times New Roman default adopts the new IBM Plex Sans stack.
            var input = JObject.Parse(@"{ ""version"": 20001, ""style"": { ""channel"": { ""base"": { ""general"": { ""font-family"": ""'Times New Roman', Times, sans-serif"" } } } } }");

            var result = new ConfigUpgrade_2_0_2().Upgrade(input);

            Assert.StartsWith("'IBM Plex Sans'", (string)result["style"]!["channel"]!["base"]!["general"]!["font-family"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_2_LeavesACustomisedSearchColourUntouched()
        {
            // A search colour the user changed (no !important suffix) must survive verbatim, otherwise the
            // migration would be rewriting deliberate choices rather than only fixing the stale default.
            var input = JObject.Parse(@"{ ""version"": 20001, ""style"": { ""chatsearch"": { ""marked"": { ""background-color"": ""rgba(10, 20, 30, 0.5)"" } } } }");

            var result = new ConfigUpgrade_2_0_2().Upgrade(input);

            Assert.Equal("rgba(10, 20, 30, 0.5)", (string)result["style"]!["chatsearch"]!["marked"]!["background-color"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_3_SeedsPlayerMentionsWhenMissing()
        {
            // The Player Mentions feature is brand new; an older profile has no subtree for it. WHY: the
            // JS config layer reads these keys directly and can't fall back to the default profile, so the
            // upgrade must seed the player-mentions block — the feature flag on, but the character list
            // empty (and the template inactive) so nothing mentions until the user opts a character in.
            var input = JObject.Parse(@"{ ""version"": 20002, ""behaviour"": { ""mentions"": { ""trigger"": [ ""legion"" ] } } }");

            var result = new ConfigUpgrade_2_0_3().Upgrade(input);

            var player = result["behaviour"]!["mentions"]!["player"]!;
            Assert.True((bool)player["enabled"]!);
            Assert.IsType<JArray>(player["sorting"]);
            Assert.IsType<JObject>(player["data"]);
            // New characters start disabled, so auto-remembering one never changes mention behaviour.
            Assert.False((bool)player["data-template"]!["active"]!);
            Assert.True((bool)player["data-template"]!["matchFirstName"]!);
            // The existing global trigger words must be left untouched.
            Assert.Equal("legion", (string)result["behaviour"]!["mentions"]!["trigger"]![0]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_3_LeavesExistingPlayerDataUntouched()
        {
            // Idempotent: a profile that already remembered characters must not have them wiped by a
            // re-seed, or re-running the chain would drop the user's per-character settings.
            var input = JObject.Parse(@"{ ""version"": 20002, ""behaviour"": { ""mentions"": { ""player"": {
                ""enabled"": true, ""sorting"": [ ""char-1"" ],
                ""data"": { ""char-1"": { ""name"": ""Max Mustermiqo'te"", ""active"": true } } } } } }");

            var result = new ConfigUpgrade_2_0_3().Upgrade(input);

            var player = result["behaviour"]!["mentions"]!["player"]!;
            Assert.True((bool)player["enabled"]!);
            Assert.Equal("Max Mustermiqo'te", (string)player["data"]!["char-1"]!["name"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_4_SeedsFuzzyKeysOnTemplateAndCharacters()
        {
            // Fuzzy matching is new in 2.0.4; the JS config layer reads these keys directly, so they must
            // be seeded on both the template and every remembered character — off, Conservative by default
            // — without disturbing the character's existing fields.
            var input = JObject.Parse(@"{ ""version"": 20003, ""behaviour"": { ""mentions"": { ""player"": {
                ""data-template"": { ""matchLastName"": true },
                ""data"": { ""char-1"": { ""name"": ""Max"", ""active"": true } } } } } }");

            var result = new ConfigUpgrade_2_0_4().Upgrade(input);

            var player = result["behaviour"]!["mentions"]!["player"]!;
            Assert.False((bool)player["data-template"]!["matchFuzzy"]!);
            Assert.Equal("conservative", (string)player["data-template"]!["fuzzyLevel"]!);
            Assert.False((bool)player["data"]!["char-1"]!["matchFuzzy"]!);
            Assert.Equal("conservative", (string)player["data"]!["char-1"]!["fuzzyLevel"]!);
            // Existing fields are left intact.
            Assert.Equal("Max", (string)player["data"]!["char-1"]!["name"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_4_LeavesExistingFuzzyChoicesUntouched()
        {
            // Idempotent: a character that already opted into aggressive fuzzy must keep that choice, or
            // re-running the chain would reset their setting back to the default.
            var input = JObject.Parse(@"{ ""version"": 20003, ""behaviour"": { ""mentions"": { ""player"": {
                ""data"": { ""char-1"": { ""name"": ""Max"", ""matchFuzzy"": true, ""fuzzyLevel"": ""aggressive"" } } } } } }");

            var result = new ConfigUpgrade_2_0_4().Upgrade(input);

            var entry = result["behaviour"]!["mentions"]!["player"]!["data"]!["char-1"]!;
            Assert.True((bool)entry["matchFuzzy"]!);
            Assert.Equal("aggressive", (string)entry["fuzzyLevel"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_8_SeedsPartialAndMiqoteKeysOnTemplateAndCharacters()
        {
            // The partial-name / Miqo'te switches are new in 2.0.8; the JS config layer reads these keys
            // directly, so they must be seeded on both the template and every remembered character — all
            // off by default (preserving whole-word matching) — without disturbing existing fields.
            var input = JObject.Parse(@"{ ""version"": 20007, ""behaviour"": { ""mentions"": { ""player"": {
                ""data-template"": { ""matchFirstName"": true },
                ""data"": { ""char-1"": { ""name"": ""Max"", ""active"": true } } } } } }");

            var result = new ConfigUpgrade_2_0_8().Upgrade(input);

            var player = result["behaviour"]!["mentions"]!["player"]!;
            foreach (var key in new[] { "matchFirstNamePartial", "matchLastNamePartial", "matchMiqote" })
            {
                Assert.False((bool)player["data-template"]![key]!);
                Assert.False((bool)player["data"]!["char-1"]![key]!);
            }
            // Existing fields are left intact.
            Assert.Equal("Max", (string)player["data"]!["char-1"]!["name"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_8_LeavesExistingPartialChoicesUntouched()
        {
            // Idempotent: a character that already opted into partial first-name matching must keep that
            // choice, or re-running the chain would reset their setting back to off.
            var input = JObject.Parse(@"{ ""version"": 20007, ""behaviour"": { ""mentions"": { ""player"": {
                ""data"": { ""char-1"": { ""name"": ""Max"", ""matchFirstNamePartial"": true } } } } } }");

            var result = new ConfigUpgrade_2_0_8().Upgrade(input);

            var entry = result["behaviour"]!["mentions"]!["player"]!["data"]!["char-1"]!;
            Assert.True((bool)entry["matchFirstNamePartial"]!);
            Assert.False((bool)entry["matchMiqote"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_9_RemovesPremadeGroupsFromSorting_KeepingCustomOrder()
        {
            // 2.0.9 splits the premade (ff) groups out of the user's custom sorting list: sorting now holds
            // custom ids only, so /e gc group <n> and the settings index address custom groups (group 1 =
            // first custom). WHY: with ff ids interleaved a custom group could land at index 8, and "group 1
            // add" hit a game-owned premade group. The premade entries stay in data (matched by ffgroup).
            var input = JObject.Parse(@"{
              ""version"": 20008,
              ""behaviour"": { ""groups"": {
                ""sorting"": [ ""group-ff-1"", ""custom-a"", ""group-ff-2"", ""custom-b"" ],
                ""data"": {
                  ""group-ff-1"": { ""id"": ""group-ff-1"", ""ffgroup"": 0 },
                  ""group-ff-2"": { ""id"": ""group-ff-2"", ""ffgroup"": 1 },
                  ""custom-a"": { ""id"": ""custom-a"", ""trigger"": [ ""foo bar"" ] },
                  ""custom-b"": { ""id"": ""custom-b"", ""trigger"": [] }
                }
              } }
            }");

            var upgrade = new ConfigUpgrade_2_0_9();
            var result = upgrade.Upgrade(input);

            Assert.Equal(20008, upgrade.MinVersion);
            Assert.Equal(20009, upgrade.TargetVersion);
            // ff ids dropped, custom ids kept in their original relative order.
            var sorting = ((JArray)result["behaviour"]!["groups"]!["sorting"]!).Select(t => (string)t!).ToList();
            Assert.Equal(new[] { "custom-a", "custom-b" }, sorting);
            // Premade groups remain in data so they still highlight ff-marked players.
            Assert.NotNull(result["behaviour"]!["groups"]!["data"]!["group-ff-1"]);
            Assert.NotNull(result["behaviour"]!["groups"]!["data"]!["group-ff-2"]);
        }

        [Fact]
        public void ConfigUpgrade_2_0_9_LeavesACustomOnlySortingUntouched()
        {
            // Idempotent: a sorting that already holds only custom ids must survive re-running the chain.
            var input = JObject.Parse(@"{
              ""version"": 20008,
              ""behaviour"": { ""groups"": {
                ""sorting"": [ ""custom-a"", ""custom-b"" ],
                ""data"": {
                  ""custom-a"": { ""id"": ""custom-a"", ""trigger"": [] },
                  ""custom-b"": { ""id"": ""custom-b"", ""trigger"": [] }
                }
              } }
            }");

            var result = new ConfigUpgrade_2_0_9().Upgrade(input);

            var sorting = ((JArray)result["behaviour"]!["groups"]!["sorting"]!).Select(t => (string)t!).ToList();
            Assert.Equal(new[] { "custom-a", "custom-b" }, sorting);
        }

        [Fact]
        public void ConfigUpgrade_2_0_10_GivesGroupsMissingOrNullTriggerAnEmptyArray()
        {
            // 2.0.10 normalizes every group's trigger to []. The premade (ff) groups never carried one, so
            // the settings page materialized it on open and that surfaced as a spurious unsaved change.
            var input = JObject.Parse(@"{
              ""version"": 20009,
              ""behaviour"": { ""groups"": { ""data"": {
                ""group-ff-1"": { ""id"": ""group-ff-1"", ""ffgroup"": 0 },
                ""group-ff-2"": { ""id"": ""group-ff-2"", ""ffgroup"": 1, ""trigger"": null },
                ""custom-a"": { ""id"": ""custom-a"", ""trigger"": [ ""foo bar"" ] }
              } } }
            }");

            var upgrade = new ConfigUpgrade_2_0_10();
            var result = upgrade.Upgrade(input);

            Assert.Equal(20009, upgrade.MinVersion);
            Assert.Equal(20010, upgrade.TargetVersion);
            var data = result["behaviour"]!["groups"]!["data"]!;
            // Missing and null triggers become empty arrays...
            Assert.Equal(JTokenType.Array, data["group-ff-1"]!["trigger"]!.Type);
            Assert.Empty((JArray)data["group-ff-1"]!["trigger"]!);
            Assert.Equal(JTokenType.Array, data["group-ff-2"]!["trigger"]!.Type);
            Assert.Empty((JArray)data["group-ff-2"]!["trigger"]!);
            // ...while an existing trigger list is left untouched.
            Assert.Equal(new[] { "foo bar" }, ((JArray)data["custom-a"]!["trigger"]!).Select(t => (string)t!).ToArray());
        }

        [Fact]
        public void ConfigUpgrade_2_0_11_SeedsIndentationDefaultWhenMissing()
        {
            // The overlay reads style.chat-frame.indentation directly to drive data-chat-indent; the JS
            // config layer can't fall back to the default profile, so an old profile must be seeded with
            // the default "full" (the existing flush-left wrapping) so nothing changes until the user opts in.
            var input = JObject.Parse(@"{ ""version"": 20010, ""style"": { ""chat-frame"": { ""tab-style"": ""pills"", ""density"": ""dense"" } } }");

            var upgrade = new ConfigUpgrade_2_0_11();
            var result = upgrade.Upgrade(input);

            Assert.Equal(20010, upgrade.MinVersion);
            Assert.Equal(20011, upgrade.TargetVersion);
            Assert.Equal("full", (string)result["style"]!["chat-frame"]!["indentation"]!);
            // Existing chat-frame values are left intact.
            Assert.Equal("pills", (string)result["style"]!["chat-frame"]!["tab-style"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_11_SeedsIndentationWhenChatFrameAbsent()
        {
            // A profile with no chat-frame block at all (very old, or hand-edited) must still gain the key,
            // not throw — the upgrade creates the missing parents on the way down.
            var input = JObject.Parse(@"{ ""version"": 20010, ""behaviour"": {} }");

            var result = new ConfigUpgrade_2_0_11().Upgrade(input);

            Assert.Equal("full", (string)result["style"]!["chat-frame"]!["indentation"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_11_LeavesExistingIndentationUntouched()
        {
            // Idempotent: a user who picked "character" must keep it when the chain re-runs.
            var input = JObject.Parse(@"{ ""version"": 20010, ""style"": { ""chat-frame"": { ""indentation"": ""character"" } } }");

            var result = new ConfigUpgrade_2_0_11().Upgrade(input);

            Assert.Equal("character", (string)result["style"]!["chat-frame"]!["indentation"]!);
        }

        [Fact]
        public void ConfigUpgrade_2_0_5_SeedsTabStyleAndDensityDefaults()
        {
            // The overlay reads these keys directly to drive data-tab-style / data-chat-density; the JS
            // config layer can't fall back to the default profile, so they must be seeded on old profiles.
            var input = JObject.Parse(@"{ ""version"": 20004, ""style"": { ""chat-history"": { ""font-size"": ""16px"", ""gap"": ""2px"" } } }");

            var result = new ConfigUpgrade_2_0_5().Upgrade(input);

            Assert.Equal("underline", (string)result["style"]!["chat-frame"]!["tab-style"]!);
            Assert.Equal("dense", (string)result["style"]!["chat-frame"]!["density"]!);
            // Existing style values are left intact, but the retired "gap" key is dropped.
            Assert.Equal("16px", (string)result["style"]!["chat-history"]!["font-size"]!);
            Assert.Null(result["style"]!["chat-history"]!["gap"]);
        }

        [Fact]
        public void ConfigUpgrade_2_0_5_LeavesExistingChoicesUntouched()
        {
            // Idempotent: a user who picked pills/breathable must keep them when the chain re-runs.
            var input = JObject.Parse(@"{ ""version"": 20004, ""style"": { ""chat-frame"": { ""tab-style"": ""pills"", ""density"": ""breathable"" } } }");

            var result = new ConfigUpgrade_2_0_5().Upgrade(input);

            Assert.Equal("pills", (string)result["style"]!["chat-frame"]!["tab-style"]!);
            Assert.Equal("breathable", (string)result["style"]!["chat-frame"]!["density"]!);
        }

        [Fact]
        public void ConfigUpgrade_1_6_0_FlattensRangeFilterAndUpdateKeysIntoTheirNewHomes()
        {
            // 1.6.0 is the big rename pass: the old behaviour.fadeout.* block becomes behaviour.rangefilter.*
            // (with .mention -> .ignoreMention), behaviour.channel.fadeout -> behaviour.channel.rangefilter, and
            // the loose update flags move under behaviour.appUpdate. WHY: a dropped key here would silently reset
            // the user's range-filter and auto-update preferences to defaults on first load after the upgrade.
            var input = JObject.Parse(@"{
              ""version"": 3,
              ""behaviour"": {
                ""channel"": { ""fadeout"": [ ""SAY"" ] },
                ""fadeout"": { ""cutoff"": 30, ""mention"": true },
                ""checkForUpdate"": false
              }
            }");

            var upgrade = new ConfigUpgrade_1_6_0();
            var result = upgrade.Upgrade(input);

            Assert.Equal(3, upgrade.MinVersion);
            Assert.Equal(16, upgrade.TargetVersion);
            Assert.Equal(30, (int)result["behaviour"]!["rangefilter"]!["cutoff"]!);
            Assert.True((bool)result["behaviour"]!["rangefilter"]!["ignoreMention"]!);
            Assert.Equal("SAY", (string)result["behaviour"]!["channel"]!["rangefilter"]![0]!);
            Assert.False((bool)result["behaviour"]!["appUpdate"]!["checkOnline"]!);
            // The old shapes are gone (dst is rebuilt from only the new paths).
            Assert.Null(result["behaviour"]!["fadeout"]);
            Assert.Null(result["behaviour"]!["checkForUpdate"]);
        }

        [Fact]
        public void ConfigUpgrade_1_7_1_RenamesChannelStyleKeysAndStampsFfGroupIndices()
        {
            // 1.7.1 follows FFXIV's own channel renames (roll -> random, worldlinkshell -> crossworldlinkshell)
            // and stamps each baked-in group with its ffgroup index (used for group-icon matching). WHY: a missed
            // style rename would lose the user's per-channel colour, and a missing ffgroup index would stop the
            // group from matching by icon. The ffgroup is only set where the group already exists.
            var input = JObject.Parse(@"{
              ""version"": 16,
              ""behaviour"": { ""groups"": { ""data"": { ""group-ff-1"": { ""name"": ""Star"" } } } },
              ""style"": { ""channel"": {
                ""roll"": { ""color"": ""#fff"" },
                ""worldlinkshell-1"": { ""color"": ""#aaa"" }
              } }
            }");

            var upgrade = new ConfigUpgrade_1_7_1();
            var result = upgrade.Upgrade(input);

            Assert.Equal(16, upgrade.MinVersion);
            Assert.Equal(1701, upgrade.TargetVersion);
            Assert.Equal(0, (int)result["behaviour"]!["groups"]!["data"]!["group-ff-1"]!["ffgroup"]!);
            Assert.Equal("#fff", (string)result["style"]!["channel"]!["random"]!["color"]!);
            Assert.Null(result["style"]!["channel"]!["roll"]);
            Assert.Equal("#aaa", (string)result["style"]!["channel"]!["crossworldlinkshell-1"]!["color"]!);
            Assert.Null(result["style"]!["channel"]!["worldlinkshell-1"]);
        }

        [Fact]
        public void ConfigUpgrade_1_8_0_MovesPerTabSettingsUnderTheNewChattabsModel()
        {
            // 1.8.0 introduces multi-tab chat: a tab's visible channels, timestamps and range-filter toggle now
            // live under behaviour.chattabs.data.chat. WHY: if the move dropped them, the user's single existing
            // chat would come back with no channel selection and default formatting.
            var input = JObject.Parse(@"{
              ""version"": 1701,
              ""behaviour"": {
                ""autodetectEmoteInSay"": true,
                ""channel"": { ""visible"": [ ""SAY"" ] },
                ""showTimestamp"": true,
                ""rangefilter"": { ""active"": true }
              }
            }");

            var upgrade = new ConfigUpgrade_1_8_0();
            var result = upgrade.Upgrade(input);

            Assert.Equal(1701, upgrade.MinVersion);
            Assert.Equal(1800, upgrade.TargetVersion);
            Assert.True((bool)result["behaviour"]!["chat"]!["autodetectEmoteInSay"]!);
            Assert.Equal("SAY", (string)result["behaviour"]!["chattabs"]!["data"]!["chat"]!["channel"]!["visible"]![0]!);
            Assert.True((bool)result["behaviour"]!["chattabs"]!["data"]!["chat"]!["formatting"]!["timestamps"]!);
            Assert.True((bool)result["behaviour"]!["chattabs"]!["data"]!["chat"]!["formatting"]!["rangefilter"]!);
            // The flat originals are gone.
            Assert.Null(result["behaviour"]!["showTimestamp"]);
            Assert.Null(result["behaviour"]!["channel"]!["visible"]);
        }

        [Fact]
        public void ConfigUpgrade_1_9_0_RenamesWriteChatLogToChatlogActive()
        {
            // A one-key rename, but it is the "write a chat log to disk" toggle: losing it would silently stop
            // logging for a user who had it on, with no visible cue that anything changed.
            var input = JObject.Parse(@"{ ""version"": 1800, ""behaviour"": { ""writeChatLog"": true } }");

            var upgrade = new ConfigUpgrade_1_9_0();
            var result = upgrade.Upgrade(input);

            Assert.Equal(1800, upgrade.MinVersion);
            Assert.Equal(1900, upgrade.TargetVersion);
            Assert.True((bool)result["behaviour"]!["chatlog"]!["active"]!);
            Assert.Null(result["behaviour"]!["writeChatLog"]);
        }

        [Fact]
        public void ConfigUpgrade_1_12_0_RenamesChatboxAndConvertsTheFontSizeKeyword()
        {
            // 1.12.0 renames style.chatbox -> style.chat-history and turns the old named font sizes into px,
            // moving the value onto chat-history. WHY: "large" etc. are no longer understood downstream, so an
            // unconverted value would render at the fallback size instead of what the user picked.
            var input = JObject.Parse(@"{
              ""version"": 1900,
              ""style"": { ""chatbox"": { ""width"": ""100px"" }, ""channel"": { ""base"": { ""font-size"": ""large"" } } }
            }");

            var upgrade = new ConfigUpgrade_1_12_0();
            var result = upgrade.Upgrade(input);

            Assert.Equal(1900, upgrade.MinVersion);
            Assert.Equal(11200, upgrade.TargetVersion);
            Assert.Equal("100px", (string)result["style"]!["chat-history"]!["width"]!);
            Assert.Null(result["style"]!["chatbox"]);
            Assert.Equal("18px", (string)result["style"]!["chat-history"]!["font-size"]!); // "large" -> 18px
            Assert.Null(result["style"]!["channel"]!["base"]!["font-size"]);
        }

        [Fact]
        public void ConfigUpgrade_1_12_0_FlattensMentionsBaseAndNestsChannelStylingUnderGeneral()
        {
            // 1.12.0 also lifts the single mentions.data.base.* block up to mentions.* (and drops the now-defunct
            // data/order), and re-nests each channel's bare colour under a "general" sub-object (channels now
            // separate general vs sender styling). WHY: a botched move would lose the user's mention trigger words
            // or wipe their per-channel colours.
            var input = JObject.Parse(@"{
              ""version"": 1900,
              ""style"": { ""channel"": { ""say"": { ""color"": ""#abc"" } } },
              ""behaviour"": { ""mentions"": { ""data"": { ""base"": { ""trigger"": [ ""legion"" ] } }, ""order"": [ ""base"" ] } }
            }");

            var result = new ConfigUpgrade_1_12_0().Upgrade(input);

            Assert.Equal("legion", (string)result["behaviour"]!["mentions"]!["trigger"]![0]!);
            Assert.Null(result["behaviour"]!["mentions"]!["data"]);
            Assert.Null(result["behaviour"]!["mentions"]!["order"]);
            Assert.Equal("#abc", (string)result["style"]!["channel"]!["say"]!["general"]!["color"]!);
            Assert.Null(result["style"]!["channel"]!["say"]!["color"]);
            // Each channel gains the new (empty) sender styling slot.
            Assert.IsType<JObject>(result["style"]!["channel"]!["say"]!["sender"]);
        }
    }
}
