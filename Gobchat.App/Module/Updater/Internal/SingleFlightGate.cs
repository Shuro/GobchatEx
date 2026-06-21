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

using System.Threading;

namespace Gobchat.Module.Updater.Internal
{
    /// <summary>
    /// A non-reentrant single-flight latch. Only one update check/download may run at a time, so a
    /// manual About-page check can't overlap the startup check (which would risk a double download).
    /// </summary>
    internal sealed class SingleFlightGate
    {
        private int _busy; // 0 = idle, 1 = running

        /// <summary>Marks the gate busy and returns true if it was idle; returns false if a run is already in flight.</summary>
        public bool TryEnter() => Interlocked.CompareExchange(ref _busy, 1, 0) == 0;

        /// <summary>Releases the latch so the next caller can enter.</summary>
        public void Exit() => Interlocked.Exchange(ref _busy, 0);

        public bool IsBusy => Volatile.Read(ref _busy) != 0;
    }
}
