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
using Gobchat.Core.Runtime;
using Xunit;

namespace Gobchat.App.Tests.Core.Runtime
{
    /// <summary>
    /// Velopack installs the app into a versioned <c>…\GobchatEx\current\</c> folder and replaces that
    /// folder atomically on every update. Nothing here may anchor resource paths to a captured or
    /// hardcoded install location: they must be derived from the <i>running</i> executable's base
    /// directory, so that after an update swaps <c>current\</c> the app loads the new version's
    /// <c>resources\</c> instead of stale files. These tests pin that relationship.
    /// </summary>
    public sealed class GobchatContextTests
    {
        [Fact]
        public void ApplicationLocation_TracksRunningExecutableBaseDirectory()
        {
            // If this stopped tracking AppDomain.BaseDirectory (e.g. a path captured at first run),
            // a Velopack update that relocates the exe into a new current\ would leave the app
            // pointing at the old folder.
            Assert.Equal(AppDomain.CurrentDomain.BaseDirectory, GobchatContext.ApplicationLocation);
        }

        [Fact]
        public void ResourceLocation_IsRootedAtApplicationLocation()
        {
            // resources\ ships next to the exe (vpk pack publishes it into current\). Resolving it
            // relative to ApplicationLocation is what lets a current\ swap bring new resources along;
            // an absolute path here would survive the swap and break after the first update.
            var resourceLocation = GobchatContext.ResourceLocation;

            Assert.StartsWith(GobchatContext.ApplicationLocation, resourceLocation, StringComparison.Ordinal);
            Assert.Equal("resources", new DirectoryInfo(resourceLocation).Name);
        }

        [Fact]
        public void AppConfigLocation_NestsUnderAppDataLocation()
        {
            // The config folder lives inside the user-data location (roaming %AppData%\GobchatEx in
            // Release), which is deliberately outside the install dir so it survives updates and is
            // shared between the portable and installed builds. Lock the nesting so config can't drift
            // out from under the data root.
            Assert.Equal(
                Path.Combine(GobchatContext.AppDataLocation, "config"),
                GobchatContext.AppConfigLocation);
        }
    }
}
