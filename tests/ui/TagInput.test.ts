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
import * as Utility from '../../Gobchat.App/resources/ui/modules/CommonUtility'

// mergeTags is the pure core of the mentions tag/chip input: it turns typed (possibly comma-pasted)
// text into the stored string[]. WHY this matters: the UI must silently reject duplicates (the spec
// asks for no warning) and apply the caller's normalization (lowercase for global mentions, case-
// preserving for player mentions) without ever mutating the existing array.

const lower = (v: string) => v.toLowerCase().trim()
const trim = (v: string) => v.trim()

describe('mergeTags', () => {
    it('adds a single normalized word', () => {
        expect(Utility.mergeTags(['legion'], 'Bahamut', lower)).toEqual(['legion', 'bahamut'])
    })

    it('splits comma-separated input and trims each piece', () => {
        expect(Utility.mergeTags([], ' a , b ,c ', lower)).toEqual(['a', 'b', 'c'])
    })

    it('returns null when nothing was added (silent duplicate, no warning)', () => {
        expect(Utility.mergeTags(['legion'], 'LEGION', lower)).toBeNull()
    })

    it('drops case-insensitive duplicates against the existing list', () => {
        expect(Utility.mergeTags(['Max'], 'max, Bob', trim)).toEqual(['Max', 'Bob'])
    })

    it('drops duplicates within the same input', () => {
        expect(Utility.mergeTags([], 'foo, FOO, foo', lower)).toEqual(['foo'])
    })

    it('ignores blank pieces', () => {
        expect(Utility.mergeTags(['a'], ' , , ', lower)).toBeNull()
    })

    it('preserves casing for player mentions (trim normalizer)', () => {
        expect(Utility.mergeTags([], "Max Mustermiqo'te", trim)).toEqual(["Max Mustermiqo'te"])
    })

    it('does not mutate the existing array', () => {
        const existing = ['a']
        Utility.mergeTags(existing, 'b', lower)
        expect(existing).toEqual(['a'])
    })
})
