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

import { describe, expect, it } from 'vitest'
import * as RangeFilterPreview from '../../Gobchat.App/resources/ui/modules/RangeFilterPreview'

// The Range Filter page draws a fade preview so users can see how the cutoff/fade-out/opacity settings
// treat a message at a given distance. WHY these matter: opacityAtDistance must mirror the live engine
// (ChatMessageActorDataSetter.CalculateVisibility) or the preview would lie about what gets culled, and
// parseNearbyPlayers turns the C# bridge's "Name: 12.34" lines into points on that bar — it must drop
// junk and the self-entry and stay within the cap, since that data drives what the user sees.

describe('opacityAtDistance', () => {
    // start=100, end=0, fadeout=10, cutoff=50 unless noted.
    it('is fully solid at or below the fade-out distance', () => {
        expect(RangeFilterPreview.opacityAtDistance(0, 50, 10, 100, 0)).toBe(100)
        expect(RangeFilterPreview.opacityAtDistance(10, 50, 10, 100, 0)).toBe(100)
    })

    it('fades linearly from start to end between fade-out and cutoff', () => {
        // Halfway through the 10..50 fade band (distance 30) -> halfway between start(100) and end(0).
        expect(RangeFilterPreview.opacityAtDistance(30, 50, 10, 100, 0)).toBe(50)
        // A quarter in (distance 20) -> 75% of the way to end.
        expect(RangeFilterPreview.opacityAtDistance(20, 50, 10, 100, 0)).toBe(75)
    })

    it('lands on the end opacity exactly at the cutoff', () => {
        expect(RangeFilterPreview.opacityAtDistance(50, 50, 10, 100, 0)).toBe(0)
        expect(RangeFilterPreview.opacityAtDistance(50, 50, 10, 100, 20)).toBe(20)
    })

    it('is hidden (0) past the cutoff', () => {
        expect(RangeFilterPreview.opacityAtDistance(51, 50, 10, 100, 20)).toBe(0)
    })

    it('returns 0 for a non-positive cutoff (filter effectively off / nothing visible)', () => {
        expect(RangeFilterPreview.opacityAtDistance(5, 0, 0, 100, 0)).toBe(0)
    })

    it('stays solid when fade-out is at or beyond the cutoff (no fade band)', () => {
        expect(RangeFilterPreview.opacityAtDistance(40, 50, 50, 100, 0)).toBe(100)
    })

    it('honours a non-100 start opacity at the fade-out line', () => {
        // start=60: the moment fading begins (just past fadeout) it should head down from 60, not 100.
        expect(RangeFilterPreview.opacityAtDistance(30, 50, 10, 60, 0)).toBe(30)
    })
})

describe('parseNearbyPlayers', () => {
    it('parses "Name: distance" lines into name+distance pairs', () => {
        expect(RangeFilterPreview.parseNearbyPlayers(['Alice: 12.34', 'Bob: 5'], 30))
            .toEqual([{ name: 'Alice', distance: 12.34 }, { name: 'Bob', distance: 5 }])
    })

    it('drops the self-entry at ~0 distance (covered by the gold "you" dot)', () => {
        expect(RangeFilterPreview.parseNearbyPlayers(['Me: 0.00', 'Far: 8.0'], 30))
            .toEqual([{ name: 'Far', distance: 8 }])
    })

    it('skips malformed rows instead of producing NaN points', () => {
        expect(RangeFilterPreview.parseNearbyPlayers(['no distance here', 'Bad: abc', 'Good: 3.5'], 30))
            .toEqual([{ name: 'Good', distance: 3.5 }])
    })

    it('trims the name and tolerates extra spacing after the colon', () => {
        expect(RangeFilterPreview.parseNearbyPlayers(['  Spaced Name :   9.1'], 30))
            .toEqual([{ name: 'Spaced Name', distance: 9.1 }])
    })

    it('caps the result to the nearest N (input is pre-sorted ascending)', () => {
        const raw = ['A: 1', 'B: 2', 'C: 3', 'D: 4']
        expect(RangeFilterPreview.parseNearbyPlayers(raw, 2))
            .toEqual([{ name: 'A', distance: 1 }, { name: 'B', distance: 2 }])
    })

    it('returns an empty array for no input', () => {
        expect(RangeFilterPreview.parseNearbyPlayers([], 30)).toEqual([])
    })
})
