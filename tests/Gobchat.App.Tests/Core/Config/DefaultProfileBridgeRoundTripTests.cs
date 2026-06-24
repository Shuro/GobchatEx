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
using System.IO;
using Gobchat.Core.Config;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Gobchat.App.Tests.Core.Config
{
    /// <summary>
    /// ARC-9: the C#&lt;-&gt;TS config seam. The full config is shipped to the page as one JSON blob and pushed
    /// back via <c>synchronizeConfig</c>; the only thing keeping the two encodings in agreement is a pair of
    /// converters that must stay mutual inverses: <see cref="JsonValueToEnum"/> turns the authored enum
    /// strings (e.g. channel <c>"Say"</c>) into the integer form the modules and the page exchange, and
    /// <see cref="JsonEnumToString"/> turns them back when a profile is written to disk. WHY this matters:
    /// these are two hand-maintained path lists. If a channel array (or the segment-type map, or a chat-tab's
    /// visible list) is registered in one converter but not the other, a save/reload silently rewrites that
    /// value to a different type and the highlight/range/log channels drift — with no compile error. These
    /// pin the round-trip on the real <c>default_profile.json</c> so a one-sided edit fails the build.
    /// </summary>
    public sealed class DefaultProfileBridgeRoundTripTests
    {
        private static JObject LoadRawDefaultProfile()
        {
            // The loader with no functions just parses the file (BOM-tolerant) — the same source of truth
            // both LoadDefaultProfile and the on-disk profiles are built from, before any enum transform.
            var path = Path.Combine(FindRepoRoot(), "src", "Gobchat.App", "resources", "default_profile.json");
            Assert.True(File.Exists(path), $"default_profile.json not found at {path}");
            return new JsonConfigLoader().LoadConfig(path);
        }

        [Fact]
        public void EnumConverters_AreMutualInverses_OnTheShippedConfigForm()
        {
            var toEnum = new JsonValueToEnum();
            var toString = new JsonEnumToString();

            // The integer form is exactly what crosses to the page (LoadDefaultProfile applies JsonValueToEnum
            // before ToJson()). A save->reload cycle is JsonEnumToString then JsonValueToEnum; it must land back
            // on the identical integer form, or a channel/segment value changed type across the boundary.
            var shippedForm = toEnum.Apply((JObject)LoadRawDefaultProfile().DeepClone());
            var afterSaveReload = toEnum.Apply(toString.Apply((JObject)shippedForm.DeepClone()));

            Assert.True(JToken.DeepEquals(shippedForm, afterSaveReload),
                "JsonValueToEnum/JsonEnumToString are not inverses on default_profile.json — a channel or segment-type path is registered in only one converter.");
        }

        [Fact]
        public void JsonValueToEnum_ActuallyTransforms_SoTheRoundTripIsNotVacuous()
        {
            // Guard against the converters becoming no-ops (which would make the inverse test pass trivially):
            // the authored roleplay channel list is enum *strings*, and the shipped form must be integers.
            var raw = LoadRawDefaultProfile();
            var rawChannel = (JArray?)raw.SelectToken("behaviour.channel.roleplay");
            Assert.NotNull(rawChannel);
            Assert.NotEmpty(rawChannel!);
            Assert.Equal(JTokenType.String, rawChannel![0].Type);

            var shipped = new JsonValueToEnum().Apply((JObject)raw.DeepClone());
            var shippedChannel = (JArray)shipped.SelectToken("behaviour.channel.roleplay")!;
            Assert.Equal(JTokenType.Integer, shippedChannel[0].Type);
        }

        [Fact]
        public void ShippedBlob_CarriesTheTopLevelKeysTheTsSideReads()
        {
            // The page's Gobchat.DefaultProfileConfig is this object; the TS Config layer keys off these.
            var shipped = new JsonValueToEnum().Apply(LoadRawDefaultProfile());

            Assert.NotNull(shipped["version"]);
            Assert.NotNull(shipped["profile"]);
            Assert.NotNull(shipped["behaviour"]);
            Assert.NotNull(shipped["style"]);
            Assert.Equal(JTokenType.Integer, shipped["version"]!.Type);
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Gobchat.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new FileNotFoundException("Could not locate Gobchat.sln above the test output directory");
            return dir.FullName;
        }
    }
}
