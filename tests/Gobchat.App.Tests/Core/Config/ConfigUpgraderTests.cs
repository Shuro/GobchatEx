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
            // A profile at the last pre-final schema version (1900..1906) is migrated by ConfigUpgrade_1_12_0
            // to its target. The transforms are all "if available" no-ops on this minimal config.
            var config = new JObject { ["version"] = 1906 };

            var result = new ConfigUpgrader().UpgradeConfig(config);

            Assert.Equal(11200, (int)result["version"]!);
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
