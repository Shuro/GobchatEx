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

'use strict'

// TSS-4: HTMLMediaElement.volume only accepts [0,1]; assigning anything outside that range throws
// IndexError/InvalidStateError. The mention volume is persisted as a fraction and read back both by the
// settings test-play button and by the live overlay, so a crafted/out-of-range profile would otherwise
// throw at the `audio.volume = ...` assignment. Clamp at every boundary instead of trusting the store.

/**
 * Clamp a media volume to the [0,1] range HTMLMediaElement.volume accepts. Non-finite input (NaN from a
 * failed parse, Infinity from a corrupt profile) collapses to 0 (muted) rather than throwing.
 */
export function clampVolumeFraction(value: number): number {
    if (!Number.isFinite(value))
        return 0
    return Math.min(Math.max(value, 0), 1)
}

/**
 * Convert a 0..100 slider percentage to the clamped [0,1] fraction stored in the config.
 */
export function percentToVolumeFraction(percent: number): number {
    return clampVolumeFraction((percent || 0) / 100)
}
