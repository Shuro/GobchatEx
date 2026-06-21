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

using Gobchat.Core.Chat;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// The range filter is "active" only when a tab the user can actually see applies it. WHY this
    /// matters: it is the single signal that both fades chat (chat pipeline) and decides whether the app
    /// reads nearby players from game memory at all (actor poller). A hidden tab with the filter on must
    /// not trigger either.
    /// </summary>
    public sealed class RangeFilterConfigTests
    {
        private static JObject Tab(bool visible, bool rangefilter)
            => new JObject { ["visible"] = visible, ["formatting"] = new JObject { ["rangefilter"] = rangefilter } };

        private static JObject Tabs(params JObject[] tabs)
        {
            var root = new JObject();
            for (var i = 0; i < tabs.Length; ++i)
                root[$"tab{i}"] = tabs[i];
            return root;
        }

        [Fact]
        public void Null_IsNotActive()
            => Assert.False(RangeFilterConfig.IsActiveForVisibleTabs((JObject)null));

        [Fact]
        public void NoTabs_IsNotActive()
            => Assert.False(RangeFilterConfig.IsActiveForVisibleTabs(new JObject()));

        [Fact]
        public void VisibleTabWithFilter_IsActive()
            => Assert.True(RangeFilterConfig.IsActiveForVisibleTabs(Tabs(Tab(visible: true, rangefilter: true))));

        [Fact]
        public void VisibleTabWithoutFilter_IsNotActive()
            => Assert.False(RangeFilterConfig.IsActiveForVisibleTabs(Tabs(Tab(visible: true, rangefilter: false))));

        [Fact]
        public void HiddenTabWithFilter_IsNotActive()
            => Assert.False(RangeFilterConfig.IsActiveForVisibleTabs(Tabs(Tab(visible: false, rangefilter: true))));

        [Fact]
        public void AnyVisibleTabWithFilter_IsActive()
        {
            var tabs = Tabs(
                Tab(visible: false, rangefilter: true),  // on, but hidden -> ignored
                Tab(visible: true, rangefilter: false),  // visible, but off
                Tab(visible: true, rangefilter: true));  // the one that counts
            Assert.True(RangeFilterConfig.IsActiveForVisibleTabs(tabs));
        }

        [Fact]
        public void TabMissingFormatting_IsNotActive()
        {
            var tabs = Tabs();
            tabs["tab0"] = new JObject { ["visible"] = true }; // no formatting node at all
            Assert.False(RangeFilterConfig.IsActiveForVisibleTabs(tabs));
        }
    }
}
