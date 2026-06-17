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
            // ConfigUpgrade_2_0_1 -> 20001). The transforms are all "if available" no-ops on this minimal
            // config; only that the chain reaches the final schema version is asserted here.
            var config = new JObject { ["version"] = 1906 };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(20001, (int)result["version"]!);
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
    }
}
