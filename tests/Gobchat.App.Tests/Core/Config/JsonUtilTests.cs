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
    /// Pins the JSON merge semantics of <see cref="JsonUtil"/>. WHY this matters: these helpers carry
    /// every config write between the default profile, the on-disk profile, and the live blob. An
    /// inverted null-predicate (CFG-1) silently turned the no-predicate <c>Overwrite</c> into a no-op,
    /// and the reversed early-return in <c>OverwriteIfAvailable</c> (CFG-2) wrote nothing when the
    /// source was present. The "ignore predicate" cases double as a regression guard so the fix to the
    /// null path does not change the behaviour of the live (predicate-bearing) call sites.
    /// </summary>
    public sealed class JsonUtilTests
    {
        [Fact]
        public void Overwrite_WithoutPredicate_CopiesChangedAndMissingProperties()
        {
            // CFG-1: the 1-arg overload passes a null predicate. It must overwrite every differing or
            // missing property, not silently discard the whole write.
            var source = new JObject { ["same"] = 1, ["changed"] = 2, ["added"] = new JObject { ["deep"] = 3 } };
            var destination = new JObject { ["same"] = 1, ["changed"] = 99 };

            var (changed, _) = JsonUtil.Overwrite(source, destination);

            Assert.Equal(2, (int)destination["changed"]!);
            Assert.Equal(3, (int)destination["added"]!["deep"]!);
            Assert.Contains("changed", changed);
            Assert.Contains("added", changed);
            Assert.DoesNotContain("same", changed); // equal value -> no write
        }

        [Fact]
        public void Overwrite_WithIgnorePredicate_SkipsIgnoredPathButWritesTheRest()
        {
            // Regression guard for the live call sites (UnchangableValues.Contains): an ignored path is
            // preserved, everything else is overwritten. Must hold both before and after the CFG-1 fix.
            var source = new JObject { ["keepMine"] = 1, ["takeYours"] = 2 };
            var destination = new JObject { ["keepMine"] = 99, ["takeYours"] = 99 };

            var (changed, _) = JsonUtil.Overwrite(source, destination, path => path == "keepMine");

            Assert.Equal(99, (int)destination["keepMine"]!); // ignored -> untouched
            Assert.Equal(2, (int)destination["takeYours"]!); // not ignored -> overwritten
            Assert.Contains("takeYours", changed);
            Assert.DoesNotContain("keepMine", changed);
        }

        [Fact]
        public void RemoveUnused_WithoutPredicate_RemovesKeysAbsentFromSource()
        {
            // CFG-1 (same inverted predicate shape): the null-predicate path must remove stale keys.
            var source = new JObject { ["keep"] = 1 };
            var destination = new JObject { ["keep"] = 1, ["stale"] = 2 };

            var (changed, _) = JsonUtil.RemoveUnused(source, destination, null);

            Assert.Null(destination["stale"]);
            Assert.NotNull(destination["keep"]);
            Assert.Contains("stale", changed);
        }

        [Fact]
        public void RemoveUnused_WithIgnorePredicate_KeepsProtectedKey()
        {
            // Regression guard: an ignored key is retained even though it is absent from the source.
            var source = new JObject { ["keep"] = 1 };
            var destination = new JObject { ["keep"] = 1, ["stale"] = 2, ["protectedKey"] = 3 };

            var (changed, _) = JsonUtil.RemoveUnused(source, destination, path => path == "protectedKey");

            Assert.Null(destination["stale"]);
            Assert.NotNull(destination["protectedKey"]); // ignored -> retained
            Assert.Contains("stale", changed);
            Assert.DoesNotContain("protectedKey", changed);
        }

        [Fact]
        public void OverwriteIfAvailable_CopiesSourceValue_WhenSourceIsPresent()
        {
            // CFG-2: the copy must run when the source path resolves, not the inverted "only when absent".
            var src = new JObject { ["from"] = new JObject { ["val"] = 42 } };
            var dst = new JObject { ["to"] = new JObject { ["val"] = 0 } };

            var result = JsonUtil.OverwriteIfAvailable(src, "from.val", dst, "to.val");

            Assert.True(result);
            Assert.Equal(42, (int)dst["to"]!["val"]!);
        }
    }
}
