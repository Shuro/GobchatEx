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
import {
    isSafeCssValue,
    sanitizeCssValue,
    sanitizeCssIdentifier,
} from '../../src/Gobchat.App/resources/ui/modules/CssSanitize'

// StyleBuilder emits profile-derived colours/sizes/ids into a live <style> sheet. A profile can be
// hand-edited or imported from an untrusted file, so a crafted value like "red}body{display:none"
// must never be allowed to close the rule. These guards are the chokepoint that prevents that.

describe('isSafeCssValue', () => {
    it('accepts ordinary style values', () => {
        // The actual shapes StyleBuilder produces: colours, max()/percent, font stacks, !important.
        for (const ok of [
            '#ff0000',
            'rgba(0,0,0,0.5)',
            'max(8px, 14px)',
            '90%',
            "'IBM Plex Sans', -apple-system, sans-serif",
            '#3a7bd5 !important',
            '0.5',
        ]) {
            expect(isSafeCssValue(ok)).toBe(true)
        }
    })

    it('rejects values that can break out of a declaration or rule', () => {
        for (const bad of [
            'red}body{display:none}',     // close the rule, inject a new one
            'red;position:fixed',          // sneak in an extra declaration
            'url(x)</style><script>',      // close the host <style>
            '@import url(evil)',           // at-rule
            'red/*c*/',                    // comment marker
            'red\\0a',                     // CSS escape
        ]) {
            expect(isSafeCssValue(bad)).toBe(false)
        }
    })

    it('treats non-strings as unsafe', () => {
        expect(isSafeCssValue(null)).toBe(false)
        expect(isSafeCssValue(undefined)).toBe(false)
        expect(isSafeCssValue(5)).toBe(false)
    })
})

describe('sanitizeCssValue', () => {
    it('returns safe values unchanged', () => {
        expect(sanitizeCssValue('#ff0000')).toBe('#ff0000')
        expect(sanitizeCssValue('max(8px, 14px)')).toBe('max(8px, 14px)')
    })

    it('drops unsafe values to null so the property is not emitted', () => {
        expect(sanitizeCssValue('red}body{display:none}')).toBeNull()
        expect(sanitizeCssValue('url(x);background:red')).toBeNull()
    })

    it('passes null/undefined through so existing skip-null emit logic still applies', () => {
        expect(sanitizeCssValue(null)).toBeNull()
        expect(sanitizeCssValue(undefined)).toBeUndefined()
    })
})

describe('sanitizeCssIdentifier', () => {
    it('leaves app-generated alphanumeric ids untouched', () => {
        expect(sanitizeCssIdentifier('aB3xZ9k2')).toBe('aB3xZ9k2')
        expect(sanitizeCssIdentifier('group-1_alt')).toBe('group-1_alt')
    })

    it('neutralizes selector-injecting characters', () => {
        // A crafted id can otherwise add ".gob-chat-entry{display:none" style selector fragments.
        expect(sanitizeCssIdentifier('x{display:none}')).toBe('x_display_none_')
        expect(sanitizeCssIdentifier('a .b')).toBe('a__b')
        expect(sanitizeCssIdentifier('a:hover')).toBe('a_hover')
    })

    it('produces the same output for a class and its selector (so they still match)', () => {
        const raw = 'evil id.x'
        // Whatever lands on the DOM element and whatever the selector targets go through the same
        // function, so the sanitized class is always selectable by the sanitized selector.
        expect(sanitizeCssIdentifier(raw)).toBe(sanitizeCssIdentifier(raw))
        expect(sanitizeCssIdentifier(raw)).toBe('evil_id_x')
    })
})
