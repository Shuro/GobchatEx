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
import { clampVolumeFraction, percentToVolumeFraction } from '../../src/Gobchat.App/resources/ui/modules/AudioVolume'

// TSS-4: the mention volume reaches HTMLMediaElement.volume, which throws for anything outside [0,1].
// These pin the clamp so a crafted/out-of-range profile (or a NaN parse) can never reach that assignment.

describe('clampVolumeFraction', () => {
    it('passes through in-range values unchanged', () => {
        expect(clampVolumeFraction(0)).toBe(0)
        expect(clampVolumeFraction(0.5)).toBe(0.5)
        expect(clampVolumeFraction(1)).toBe(1)
    })

    it('clamps values above 1 down to 1', () => {
        expect(clampVolumeFraction(1.5)).toBe(1)
        expect(clampVolumeFraction(100)).toBe(1)
    })

    it('clamps negative values up to 0', () => {
        expect(clampVolumeFraction(-0.2)).toBe(0)
        expect(clampVolumeFraction(-100)).toBe(0)
    })

    it('treats non-finite input (NaN/Infinity) as muted', () => {
        expect(clampVolumeFraction(NaN)).toBe(0)
        expect(clampVolumeFraction(Infinity)).toBe(0)
        expect(clampVolumeFraction(-Infinity)).toBe(0)
    })
})

describe('percentToVolumeFraction', () => {
    it('maps a 0..100 slider percent to the 0..1 fraction', () => {
        expect(percentToVolumeFraction(0)).toBe(0)
        expect(percentToVolumeFraction(50)).toBe(0.5)
        expect(percentToVolumeFraction(100)).toBe(1)
    })

    it('clamps an out-of-range percent into the valid fraction range', () => {
        expect(percentToVolumeFraction(150)).toBe(1)
        expect(percentToVolumeFraction(-10)).toBe(0)
    })

    it('treats a failed parse (NaN) as muted, matching the slider default', () => {
        expect(percentToVolumeFraction(NaN)).toBe(0)
    })
})
