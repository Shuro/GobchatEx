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

import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'
import { describe, expect, it } from 'vitest'
import { ConfigProfile } from '../../src/Gobchat.App/resources/ui/modules/Config'

// ARC-9: the C# side ships the whole config to the page as one JSON blob built from default_profile.json
// (Gobchat.DefaultProfileConfig). This is the TS half of the round-trip contract: the same source-of-truth
// file the C# DefaultProfileBridgeRoundTripTests pins must be ingestible by the actual TS consumer, and the
// dotted keys the C# modules and the page exchange must resolve. A shape drift on either side (a renamed or
// dropped section) breaks the page silently; this fails the build instead.

function loadDefaultProfile(): any {
    const here = dirname(fileURLToPath(import.meta.url))
    const path = resolve(here, '../../src/Gobchat.App/resources/default_profile.json')
    // default_profile.json is saved UTF-8 with BOM; strip it so JSON.parse doesn't choke.
    const raw = readFileSync(path, 'utf-8').replace(/^﻿/, '')
    return JSON.parse(raw)
}

describe('default_profile.json is consumable by the TS ConfigProfile (C#->TS seam)', () => {
    it('loads the real default profile without throwing', () => {
        expect(() => new ConfigProfile(loadDefaultProfile())).not.toThrow()
    })

    it('resolves the shared dotted keys the C# modules key off', () => {
        const profile = new ConfigProfile(loadDefaultProfile())

        // Schema version: a positive integer both sides compare for upgrades.
        expect(typeof profile.get('version')).toBe('number')
        expect(profile.get('version')).toBeGreaterThan(0)

        // Sections the page binds against.
        expect(profile.get('profile')).not.toBeNull()
        expect(profile.get('behaviour')).not.toBeNull()
        expect(profile.get('style')).not.toBeNull()

        // A representative leaf the settings UI binds: mentions sound toggle is a boolean.
        expect(typeof profile.get('behaviour.mentions.playSound')).toBe('boolean')
        expect(typeof profile.get('behaviour.mentions.player.enabled')).toBe('boolean')
    })

    it('returns the supplied default for a key the profile does not define', () => {
        const profile = new ConfigProfile(loadDefaultProfile())
        expect(profile.get('behaviour.this.path.does.not.exist', 'FALLBACK')).toBe('FALLBACK')
    })

    it('get(null) hands back the whole config blob unchanged', () => {
        const blob = loadDefaultProfile()
        const profile = new ConfigProfile(blob)
        expect(profile.get(null)).toEqual(blob)
    })
})
