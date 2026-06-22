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
import * as ChatVisibility from '../../src/Gobchat.App/resources/ui/modules/ChatVisibility'

// getFadeOutLevel decides which fade-out step the range filter renders an out-of-range message at. WHY it
// matters: Style.ts turns this level into the entry's actual opacity (level 0 -> display:none, level 1 -> most
// faded/endopacity, top level -> nearly solid/startopacity; null -> no class at all -> full default opacity).
// If the bucketing or the round-up drifts, distant players either vanish when they should be dimly visible or
// stay fully bright when they should fade — which is the entire point of the filter. All cases use 10 opacity
// steps (the default profile) unless noted, so the fade-out step size is 100/10 = 10.
describe('ChatVisibility.getFadeOutLevel', () => {
    it('returns null at full visibility so a solid message gets no fade-out class', () => {
        // >= 100 means in range; the entry keeps its default opacity rather than any fadeout-N rule.
        expect(ChatVisibility.getFadeOutLevel(100, false, false, 10)).toBeNull()
        expect(ChatVisibility.getFadeOutLevel(150, false, false, 10)).toBeNull()
    })

    it('lets a mention bypass the distance fade only when ignoreMention is on', () => {
        // The "always show mentions regardless of range" toggle: a mention is exempt, everything else still fades.
        expect(ChatVisibility.getFadeOutLevel(50, true, true, 10)).toBeNull()
        // same message, toggle off -> it fades like any other out-of-range line.
        expect(ChatVisibility.getFadeOutLevel(50, true, false, 10)).toBe(5)
        // toggle on but no mention -> no exemption.
        expect(ChatVisibility.getFadeOutLevel(50, false, true, 10)).toBe(5)
    })

    it('maps only an exactly-zero visibility to the hidden level 0 (display:none)', () => {
        expect(ChatVisibility.getFadeOutLevel(0, false, false, 10)).toBe(0)
    })

    it('rounds a barely-visible message UP to level 1 so >0 visibility is never fully hidden', () => {
        // The +stepSize-1 round-up is deliberate: anything in (0, 10] must show faded (level 1), not vanish.
        expect(ChatVisibility.getFadeOutLevel(1, false, false, 10)).toBe(1)
        expect(ChatVisibility.getFadeOutLevel(10, false, false, 10)).toBe(1)
        // crossing a step boundary moves to the next bucket.
        expect(ChatVisibility.getFadeOutLevel(11, false, false, 10)).toBe(2)
    })

    it('puts near-full visibility on the top step (least faded)', () => {
        expect(ChatVisibility.getFadeOutLevel(90, false, false, 10)).toBe(9)
        expect(ChatVisibility.getFadeOutLevel(91, false, false, 10)).toBe(10)
        expect(ChatVisibility.getFadeOutLevel(99, false, false, 10)).toBe(10)
    })

    it('scales the bucketing to a different opacitysteps setting', () => {
        // 4 steps -> step size 25; the level count tracks the user's configured granularity.
        expect(ChatVisibility.getFadeOutLevel(50, false, false, 4)).toBe(2)
        expect(ChatVisibility.getFadeOutLevel(99, false, false, 4)).toBe(4)
    })
})
