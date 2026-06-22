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

using System.IO;
using Gobchat.Core.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Gobchat.App.Tests.Core.Runtime
{
    /// <summary>
    /// The first-run screen writes the user's language/theme/auto-update choices into appsettings.json before
    /// the config module reads it. WHY this matters: this file is what makes the choices take effect on the
    /// very first launch and marks the run complete, and when the user also migrates their old folder it must
    /// keep the migrated settings while letting the explicit choices win. A wrong merge would silently drop a
    /// migrated setting or re-show the screen every launch.
    /// </summary>
    public sealed class FirstRunSetupTests
    {
        [Fact]
        public void BuildAppSettings_FromNothing_WritesTheThreeChoices()
        {
            var result = FirstRunSetup.BuildAppSettings(null, "de", "FFXIV Modern", autoUpdate: false);

            Assert.Equal("de", (string)result.SelectToken("behaviour.language"));
            Assert.Equal("FFXIV Modern", (string)result.SelectToken("style.theme"));
            Assert.False((bool)result.SelectToken("behaviour.appUpdate.checkOnline"));
        }

        [Fact]
        public void BuildAppSettings_PreservesOtherKeys_AndOverridesTheChoices()
        {
            // Stand-in for a migrated appsettings.json: an unrelated key plus old values for the three choices.
            var migrated = new JObject
            {
                ["behaviour"] = new JObject
                {
                    ["hideOnMinimize"] = true,
                    ["language"] = "en",
                    ["appUpdate"] = new JObject { ["checkOnline"] = true, ["acceptBeta"] = true },
                },
                ["style"] = new JObject { ["theme"] = "FFXIV Modern" },
            };

            var result = FirstRunSetup.BuildAppSettings(migrated, "de", "FFXIV Modern Light", autoUpdate: false);

            // The three choices win...
            Assert.Equal("de", (string)result.SelectToken("behaviour.language"));
            Assert.Equal("FFXIV Modern Light", (string)result.SelectToken("style.theme"));
            Assert.False((bool)result.SelectToken("behaviour.appUpdate.checkOnline"));
            // ...while every other migrated key is left untouched.
            Assert.True((bool)result.SelectToken("behaviour.hideOnMinimize"));
            Assert.True((bool)result.SelectToken("behaviour.appUpdate.acceptBeta"));
        }

        [Fact]
        public void BuildAppSettings_DoesNotMutateTheInput()
        {
            var migrated = new JObject { ["behaviour"] = new JObject { ["language"] = "en" } };

            FirstRunSetup.BuildAppSettings(migrated, "de", "FFXIV Modern", autoUpdate: true);

            Assert.Equal("en", (string)migrated.SelectToken("behaviour.language"));
        }

        [Fact]
        public void IsFirstRun_TrueWhenAppSettingsMissing_FalseOncePresent()
        {
            var dir = Path.Combine(Path.GetTempPath(), "gobchat-firstrun-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                Assert.True(FirstRunSetup.IsFirstRun(dir));

                File.WriteAllText(Path.Combine(dir, "appsettings.json"), "{}");
                Assert.False(FirstRunSetup.IsFirstRun(dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }

        [Fact]
        public void WriteAppSettings_LayersChoicesOverMigratedFile_AndCompletesFirstRun()
        {
            // Exercises the full on-disk path the "import had no effect" fix lives on: a just-migrated
            // appsettings.json is read, the explicit choices are layered on top, and the file is written back.
            // WHY: if the choices didn't win, a migrated setting would override the user's pick; if the file
            // weren't written, IsFirstRun would stay true and the screen would reappear every launch.
            var dir = Path.Combine(Path.GetTempPath(), "gobchat-firstrun-" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            try
            {
                var migrated = new JObject
                {
                    ["behaviour"] = new JObject
                    {
                        ["hideOnMinimize"] = true,
                        ["language"] = "en",
                        ["appUpdate"] = new JObject { ["checkOnline"] = true },
                    },
                    ["style"] = new JObject { ["theme"] = "FFXIV Modern" },
                };
                File.WriteAllText(Path.Combine(dir, "appsettings.json"), migrated.ToString());

                FirstRunSetup.WriteAppSettings(dir, "de", "FFXIV Modern Light", autoUpdate: false);

                var written = JObject.Parse(File.ReadAllText(Path.Combine(dir, "appsettings.json")));
                // The three choices win on disk...
                Assert.Equal("de", (string)written.SelectToken("behaviour.language"));
                Assert.Equal("FFXIV Modern Light", (string)written.SelectToken("style.theme"));
                Assert.False((bool)written.SelectToken("behaviour.appUpdate.checkOnline"));
                // ...the unrelated migrated key survives...
                Assert.True((bool)written.SelectToken("behaviour.hideOnMinimize"));
                // ...and the run is now marked complete, so the screen won't reappear.
                Assert.False(FirstRunSetup.IsFirstRun(dir));
            }
            finally
            {
                Directory.Delete(dir, true);
            }
        }
    }
}
