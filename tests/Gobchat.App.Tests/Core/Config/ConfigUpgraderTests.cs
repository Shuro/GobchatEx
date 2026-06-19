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
            // ConfigUpgrade_2_0_5 -> 20005). The transforms are all "if available" no-ops/additions on
            // this minimal config; only that the chain reaches the final schema version is asserted here.
            var config = new JObject { ["version"] = 1906 };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(20005, (int)result["version"]!);
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
    }
}
