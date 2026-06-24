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
using System.Collections.Generic;
using Xunit;

namespace Gobchat.App.Tests.Core.Config
{
    /// <summary>
    /// ARC-2: GetProperty&lt;JObject&gt;/&lt;JToken&gt;/&lt;JArray&gt; must hand back a detached snapshot, not the
    /// live node from the shared config tree. WHY this matters: ~20 modules share one mutable JObject behind a
    /// single lock; the lock guards access but not references that escape it. A caller (e.g. the chat manager's
    /// RememberCharacter on the actor-poll thread) mutating a returned token in place would corrupt the source
    /// of truth outside SetProperty/change-tracking and race the config/UI thread. These pin that escaped
    /// references are clones, while typed reads still round-trip correctly.
    /// </summary>
    public sealed class GobchatConfigProfileSnapshotTests
    {
        private static GobchatConfigProfile WritableProfile(JObject data) => new GobchatConfigProfile(data, true);

        [Fact]
        public void GetProperty_JObject_ReturnsClone_SoInPlaceMutationDoesNotLeakIntoTheTree()
        {
            var profile = WritableProfile(new JObject
            {
                ["behaviour"] = new JObject { ["data"] = new JObject { ["name"] = "original" } }
            });

            var escaped = profile.GetProperty<JObject>("behaviour.data");
            escaped["name"] = "tampered";
            escaped["injected"] = 1;

            // The stored tree is untouched: a fresh read still sees the original, unmodified object.
            var reread = profile.GetProperty<JObject>("behaviour.data");
            Assert.Equal("original", reread["name"]!.ToObject<string>());
            Assert.Null(reread["injected"]);
        }

        [Fact]
        public void GetProperty_JArray_ReturnsClone_SoMutatingItemsDoesNotLeak()
        {
            var profile = WritableProfile(new JObject
            {
                ["list"] = new JArray { "a", "b" }
            });

            var escaped = profile.GetProperty<JArray>("list");
            escaped.Add("c");

            var reread = profile.GetProperty<JArray>("list");
            Assert.Equal(2, reread.Count);
        }

        [Fact]
        public void GetProperty_TwoReads_ReturnDistinctInstances()
        {
            var profile = WritableProfile(new JObject { ["data"] = new JObject { ["x"] = 1 } });

            var first = profile.GetProperty<JObject>("data");
            var second = profile.GetProperty<JObject>("data");

            // Distinct snapshots, equal content.
            Assert.NotSame(first, second);
            Assert.True(JToken.DeepEquals(first, second));
        }

        [Fact]
        public void GetProperty_TypedReads_StillRoundTripValues()
        {
            // The clone only applies to raw-token reads; typed reads must be unaffected.
            var profile = WritableProfile(new JObject
            {
                ["flag"] = true,
                ["name"] = "abc",
                ["tags"] = new JArray { "x", "y" }
            });

            Assert.True(profile.GetProperty<bool>("flag"));
            Assert.Equal("abc", profile.GetProperty<string>("name"));
            Assert.Equal(new List<string> { "x", "y" }, profile.GetProperty<List<string>>("tags"));
        }

        [Fact]
        public void SetProperty_IsStillTheOnlyWayToMutate_AndItPersists()
        {
            var profile = WritableProfile(new JObject { ["data"] = new JObject { ["name"] = "before" } });

            profile.SetProperty("data.name", "after");

            Assert.Equal("after", profile.GetProperty<JObject>("data")["name"]!.ToObject<string>());
        }
    }
}
