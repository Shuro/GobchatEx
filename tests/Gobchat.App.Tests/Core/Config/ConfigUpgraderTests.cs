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
            // (1906 -> ConfigUpgrade_1_12_0 -> 11200 -> ConfigUpgrade_2_0_0 -> 20000). The transforms are
            // all "if available" no-ops on this minimal config; only that the chain reaches the final
            // schema version is asserted here.
            var config = new JObject { ["version"] = 1906 };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(20000, (int)result["version"]!);
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
    }
}
