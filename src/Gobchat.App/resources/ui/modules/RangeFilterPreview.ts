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

// Pure helpers behind the Range Filter page's fade preview (config_rangefilter.ts). Kept DOM-free so
// they can be unit-tested in isolation (tests/ui/RangeFilterPreview.test.ts).

// Opacity (0..100) a message at `distance` would render at, mirroring the engine
// (ChatMessageActorDataSetter.CalculateVisibility + the Style.ts level mapping): solid below the
// fade-out distance, a linear start->end fade up to the cutoff, and hidden (0) beyond it.
export function opacityAtDistance(distance: number, cutoff: number, fadeout: number, startOpacity: number, endOpacity: number): number {
    if (cutoff <= 0 || distance > cutoff)
        return 0
    if (distance <= fadeout || cutoff <= fadeout)
        return 100
    const visibility = 1 - (distance - fadeout) / (cutoff - fadeout)
    return endOpacity + (startOpacity - endOpacity) * visibility
}

// Parse the bridge's `getPlayersAndDistance()` output ("Name: 12.34" lines, invariant-culture decimal,
// already sorted by distance ascending) into name+distance pairs, dropping malformed rows and yourself
// (distance ~0, covered by the gold "you" dot). Returns at most `cap` entries (the nearest ones).
export function parseNearbyPlayers(raw: string[], cap: number): { name: string, distance: number }[] {
    const players: { name: string, distance: number }[] = []
    for (const entry of raw) {
        const match = /^(.*):\s*([0-9]+(?:\.[0-9]+)?)$/.exec(entry) // "Name: 12.34" (invariant culture)
        if (!match)
            continue
        const distance = parseFloat(match[2])
        if (!isFinite(distance) || distance < 0.05) // skip yourself (~0); the gold "you" dot covers that
            continue
        players.push({ name: match[1].trim(), distance })
    }
    return players.slice(0, cap)
}
