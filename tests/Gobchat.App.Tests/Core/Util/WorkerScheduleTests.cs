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
using Gobchat.Core.Util;
using Xunit;

namespace Gobchat.App.Tests.Core.Util
{
    /// <summary>
    /// Wait-time math for the polling workers (CHT-2). WHY this matters: the regression being pinned is
    /// the use of TimeSpan.Milliseconds (only the 0-999 sub-second component) instead of TotalMilliseconds.
    /// With the old math a cycle that ran longer than a second computed a large bogus wait, so the worker
    /// overslept and dropped queued chat/actor updates.
    /// </summary>
    public sealed class WorkerScheduleTests
    {
        [Fact]
        public void RemainingWaitMs_ReturnsRemainder_WhenWorkFinishedEarly()
        {
            // Arrange / Act
            var wait = WorkerSchedule.RemainingWaitMs(2000, TimeSpan.FromMilliseconds(500));

            // Assert
            Assert.Equal(1500, wait);
        }

        [Fact]
        public void RemainingWaitMs_ReturnsZero_WhenWorkMetTheInterval()
        {
            Assert.Equal(0, WorkerSchedule.RemainingWaitMs(2000, TimeSpan.FromMilliseconds(2000)));
        }

        [Fact]
        public void RemainingWaitMs_ReturnsZero_WhenWorkOverranTheInterval()
        {
            // The CHT-2 regression: 2100ms of work against a 2000ms interval must wait 0, not ~1900.
            // (TimeSpan.FromMilliseconds(2100).Milliseconds == 100, so the buggy math gave 2000-100 = 1900.)
            Assert.Equal(0, WorkerSchedule.RemainingWaitMs(2000, TimeSpan.FromMilliseconds(2100)));
        }

        [Fact]
        public void RemainingWaitMs_ReturnsZero_WhenWorkRanManySeconds()
        {
            // Multi-second overrun: TotalMilliseconds (5000) exceeds the interval, so the wait is 0.
            Assert.Equal(0, WorkerSchedule.RemainingWaitMs(2000, TimeSpan.FromSeconds(5)));
        }

        [Fact]
        public void RemainingWaitMs_DoesNotOverflow_WhenIntervalExceedsIntRange()
        {
            // A pathological interval is clamped to int.MaxValue rather than wrapping negative on the cast.
            Assert.Equal(int.MaxValue, WorkerSchedule.RemainingWaitMs(long.MaxValue, TimeSpan.Zero));
        }
    }
}
