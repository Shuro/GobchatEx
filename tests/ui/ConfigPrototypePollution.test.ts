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
import { afterEach, beforeAll, describe, expect, it } from 'vitest'
import { GobchatConfig } from '../../src/Gobchat.App/resources/ui/modules/Config'

// TSS-1 (CWE-1321): importing an untrusted profile flows JSON.parse'd file content straight into
// GobchatConfig.importProfile -> writeObject, which merges a literal "__proto__"/"constructor"/"prototype"
// key onto Object.prototype. These tests drive the real public entry point with hostile payloads and pin
// that the global prototype is never polluted. They fail against the pre-fix writeObject (no key guard).

function loadDefaultProfile(): any {
    const here = dirname(fileURLToPath(import.meta.url))
    const path = resolve(here, '../../src/Gobchat.App/resources/default_profile.json')
    // default_profile.json is saved UTF-8 with BOM; strip it so JSON.parse doesn't choke.
    const raw = readFileSync(path, 'utf-8').replace(/^﻿/, '')
    return JSON.parse(raw)
}

beforeAll(() => {
    // The page injects these globally at runtime; the importProfile path needs the default-profile blob and
    // a single lodash call (_.includes in #generateId). Provide just enough for a headless run.
    ;(globalThis as any).Gobchat = { DefaultProfileConfig: loadDefaultProfile() }
    ;(globalThis as any)._ = {
        includes: (collection: unknown, value: unknown) =>
            Array.isArray(collection) && collection.indexOf(value) !== -1,
    }
})

afterEach(() => {
    // Defensive: if a regression ever re-introduces pollution, don't leak it into other suites.
    delete (Object.prototype as any).polluted
    delete (Object.prototype as any).injected
})

describe('GobchatConfig.importProfile rejects prototype pollution (TSS-1)', () => {
    it('does not pollute Object.prototype via a __proto__ key', () => {
        const config = new GobchatConfig(false)
        const malicious = JSON.parse('{"__proto__":{"polluted":"yes"}}')

        config.importProfile(malicious)

        expect(({} as any).polluted).toBeUndefined()
        expect((Object.prototype as any).polluted).toBeUndefined()
    })

    it('does not pollute Object.prototype via a constructor.prototype key', () => {
        const config = new GobchatConfig(false)
        const malicious = JSON.parse('{"constructor":{"prototype":{"injected":"yes"}}}')

        config.importProfile(malicious)

        expect(({} as any).injected).toBeUndefined()
        expect((Object.prototype as any).injected).toBeUndefined()
    })

    it('still imports a benign profile and stores it', () => {
        const config = new GobchatConfig(false)
        const before = config.profileIds.length

        const profileId = config.importProfile(JSON.parse('{"profile":{"name":"imported"}}'))

        expect(typeof profileId).toBe('string')
        expect(config.profileIds.length).toBe(before + 1)
        expect(config.getProfile(profileId)?.profileName).toBe('imported')
    })
})
