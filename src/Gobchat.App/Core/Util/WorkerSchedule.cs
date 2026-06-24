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

namespace Gobchat.Core.Util
{
    /// <summary>
    /// Timing helpers shared by the polling background workers (chat, actor).
    /// </summary>
    public static class WorkerSchedule
    {
        /// <summary>
        /// How long a poll worker should sleep so a full cycle lasts <paramref name="intervalMs"/>,
        /// given that the work already took <paramref name="elapsed"/>. Returns 0 when the work met or
        /// exceeded the interval (never a negative or oversized wait).
        /// </summary>
        /// <remarks>
        /// CHT-2: this must use <see cref="TimeSpan.TotalMilliseconds"/> (the whole elapsed span), not
        /// <see cref="TimeSpan.Milliseconds"/> (only the 0-999 sub-second component). With the latter a
        /// cycle that ran 2100ms against a 2000ms interval computed a 1900ms wait instead of 0, so the
        /// worker overslept and dropped queued messages.
        /// </remarks>
        public static int RemainingWaitMs(long intervalMs, TimeSpan elapsed)
        {
            var remaining = intervalMs - elapsed.TotalMilliseconds;
            if (remaining <= 0)
                return 0;
            return (int)Math.Min(remaining, int.MaxValue);
        }
    }
}
