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
import * as Utility from '../../src/Gobchat.App/resources/ui/modules/CommonUtility'

// CommonUtility holds the pure helpers used across the whole UI (type guards, number coercion,
// string templating, html/unicode (de)coding). WHY these matter: config values arrive as untyped
// strings, and formatString builds the very CSS class names the range-filter fade relies on.

describe('type guards', () => {
    it('isString', () => {
        expect(Utility.isString('x')).toBe(true)
        expect(Utility.isString(5)).toBe(false)
        expect(Utility.isString(null)).toBe(false)
    })

    it('isNonEmptyString', () => {
        expect(Utility.isNonEmptyString('x')).toBe(true)
        expect(Utility.isNonEmptyString('  ')).toBe(false)
        expect(Utility.isNonEmptyString('')).toBe(false)
    })

    it('isNumber rejects NaN and Infinity', () => {
        expect(Utility.isNumber(3)).toBe(true)
        expect(Utility.isNumber(NaN)).toBe(false)
        expect(Utility.isNumber(Infinity)).toBe(false)
        expect(Utility.isNumber('3')).toBe(false)
    })

    it('isArray / isObject', () => {
        expect(Utility.isArray([])).toBe(true)
        expect(Utility.isArray({})).toBe(false)
        expect(Utility.isObject({})).toBe(true)
        expect(Utility.isObject([])).toBe(false)
        expect(Utility.isObject(null)).toBe(false)
    })
})

describe('number coercion', () => {
    it('toInt', () => {
        expect(Utility.toInt('42')).toBe(42)
        expect(Utility.toInt(3.7)).toBe(4) // rounds
        expect(Utility.toInt(true)).toBe(1)
        expect(Utility.toInt(false)).toBe(0)
        expect(Utility.toInt(null)).toBeNull()
        expect(Utility.toInt(null, 9)).toBe(9) // fallback
    })

    it('toInt clamps to the safe-integer range', () => {
        expect(Utility.toInt(Number.MAX_SAFE_INTEGER + 10)).toBe(Number.MAX_SAFE_INTEGER)
    })

    it('toFloat', () => {
        expect(Utility.toFloat('1.5')).toBe(1.5)
        expect(Utility.toFloat(true)).toBe(1)
        expect(Utility.toFloat(null)).toBeNull()
        expect(Utility.toFloat(null, 2.5)).toBe(2.5)
    })
})

describe('number extraction', () => {
    it('extractNumbers pulls ints and decimals in order', () => {
        expect(Utility.extractNumbers('a12b3.5c')).toEqual([12, 3.5])
        expect(Utility.extractNumbers('none')).toEqual([])
    })

    it('extractFirstNumber', () => {
        expect(Utility.extractFirstNumber('x42y')).toBe(42)
        expect(Utility.extractFirstNumber('none')).toBeNull()
    })
})

describe('formatString', () => {
    it('substitutes positional placeholders', () => {
        expect(Utility.formatString('a {0} b {1}', 'X', 2)).toBe('a X b 2')
    })

    it('replaces every occurrence of a placeholder', () => {
        expect(Utility.formatString('{0}-{0}', 'x')).toBe('x-x')
    })
})

describe('encoding helpers', () => {
    it('html entities round-trip', () => {
        expect(Utility.encodeHtmlEntities('<')).toBe('&#60;')
        expect(Utility.decodeHtmlEntities('&#60;')).toBe('<')
    })

    it('unicode round-trip', () => {
        expect(Utility.decodeUnicode('U+0041')).toBe('A')
        expect(Utility.encodeUnicode('A')).toBe('U+0041')
    })
})

describe('extend', () => {
    it('merges sources into the target', () => {
        expect(Utility.extend({ a: 1 }, { b: 2 })).toEqual({ a: 1, b: 2 })
    })
})

// isValidFFXIVPlayerName gates manual entry of group members to real character names. WHY it matters:
// the group's highlight only fires when a stored name actually matches a chat sender, so junk like a
// single word, an over-long part, or a non-name string would be a silent dead entry — the validator is
// the only thing stopping the user from saving members that can never match.
describe('isValidFFXIVPlayerName', () => {
    it('accepts a normal first + last name', () => {
        expect(Utility.isValidFFXIVPlayerName('Khada Iriq')).toBe(true)
    })

    it('accepts an apostrophe (the only allowed special character)', () => {
        expect(Utility.isValidFFXIVPlayerName("Y'shtola Rhul")).toBe(true)
    })

    it('is case-insensitive (members are stored lowercased)', () => {
        expect(Utility.isValidFFXIVPlayerName('khada iriq')).toBe(true)
    })

    it('rejects a single word — a full name needs both parts', () => {
        expect(Utility.isValidFFXIVPlayerName('Khada')).toBe(false)
    })

    it('rejects a cross-server [Server] suffix (brackets are not allowed)', () => {
        expect(Utility.isValidFFXIVPlayerName('Khada Iriq [Balmung]')).toBe(false)
    })

    it('rejects a part shorter than 2 characters', () => {
        expect(Utility.isValidFFXIVPlayerName('A Bbb')).toBe(false)
    })

    it('rejects a part longer than 15 characters', () => {
        expect(Utility.isValidFFXIVPlayerName('Abcdefghijklmnop Last')).toBe(false)
    })

    it('rejects a combined length over 20 characters even when each part is in range', () => {
        // 12 + 12 = 24 > 20, with each part within the 2–15 range.
        expect(Utility.isValidFFXIVPlayerName('Abcdefghijkl Mnopqrstuvwx')).toBe(false)
    })

    it('rejects digits and other punctuation', () => {
        expect(Utility.isValidFFXIVPlayerName('Khada1 Iriq')).toBe(false)
        expect(Utility.isValidFFXIVPlayerName('Khada-Iriq Rhul')).toBe(false)
    })
})

// stripServerName drops a cross-world name's "[Server]" suffix so grouping/mentions are server-agnostic.
// WHY it matters: a member is stored as just the player name, so without stripping it would never match a
// cross-world speaker (whose source carries the suffix).
describe('stripServerName', () => {
    it('removes a trailing [Server] suffix and trims', () => {
        expect(Utility.stripServerName('Khada Iriq [Balmung]')).toBe('Khada Iriq')
    })

    it('leaves a plain name untouched', () => {
        expect(Utility.stripServerName('Khada Iriq')).toBe('Khada Iriq')
    })
})
