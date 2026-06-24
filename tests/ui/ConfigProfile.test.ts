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
import { ConfigProfile } from '../../src/Gobchat.App/resources/ui/modules/Config'

// TSS-1: ConfigProfile.set() with a null/empty key replaces the whole config and must return immediately.
// Before the fix it fell through to resolvePath(null, ...) on the just-replaced object; these pin that a
// whole-config replacement (a) takes effect, (b) leaves nested access working, and (c) does not throw.

function makeProfile(extra: Record<string, unknown> = {}) {
    return new ConfigProfile({ profile: { id: 'p1', name: 'first' }, ...extra } as any)
}

describe('ConfigProfile.set whole-config replacement (null/empty key)', () => {
    it('replaces the entire config when the key is null', () => {
        const profile = makeProfile({ a: { b: 1 } })

        const replacement = { profile: { id: 'p2', name: 'second' }, x: { y: 42 } }
        profile.set(null, replacement as any)

        expect(profile.profileId).toBe('p2')
        expect(profile.get(null)).toEqual(replacement)
        expect(profile.get('x.y')).toBe(42)
        // The old tree is gone, not merged (missing key falls back to the supplied default).
        expect(profile.get('a', 'GONE')).toBe('GONE')
    })

    it('replaces the entire config when the key is the empty string', () => {
        const profile = makeProfile()

        const replacement = { profile: { id: 'p3', name: 'third' } }
        profile.set('', replacement as any)

        expect(profile.profileId).toBe('p3')
        expect(profile.get(null)).toEqual(replacement)
    })

    it('still resolves nested keys for non-empty paths after a replacement', () => {
        const profile = makeProfile()
        profile.set(null, { profile: { id: 'p1', name: 'first' }, a: { b: 1 } } as any)

        profile.set('a.b', 5)

        expect(profile.get('a.b')).toBe(5)
    })
})
