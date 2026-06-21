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
import { withMathFallback, MathFallbackFont } from '../../src/Gobchat.App/resources/ui/modules/MathFontFallback'

// withMathFallback splices the bundled Noto Sans Math into a chat font stack so decorative "math"
// letters (𝗙𝗟𝗨𝗫) fall back to a font that has those glyphs instead of rendering as tofu. WHY the exact
// placement matters: a family listed AFTER a CSS generic (sans-serif/…) is never reached — the generic
// already hands off to system fallback — so the math font is useless unless it is inserted BEFORE the
// trailing generic. And because StyleBuilder re-runs this on every config change, it must be idempotent
// or the font stack would grow without bound.

describe('withMathFallback', () => {
    it('inserts the math font just before a trailing generic family', () => {
        // The default Modern stack: math fallback must land before sans-serif, not after it.
        const input = "'IBM Plex Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif"

        const result = withMathFallback(input)

        expect(result).toBe(`'IBM Plex Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, ${MathFallbackFont}, sans-serif`)
        // The generic must stay last so normal text still falls back to the system sans-serif.
        expect(result.endsWith('sans-serif')).toBe(true)
    })

    it('appends the math font when there is no trailing generic', () => {
        // Cambria/Georgia stacks have no generic keyword to sit in front of, so it goes at the end.
        expect(withMathFallback('Cambria, Georgia')).toBe(`Cambria, Georgia, ${MathFallbackFont}`)
    })

    it('appends for a single non-generic family', () => {
        expect(withMathFallback('Lexend')).toBe(`Lexend, ${MathFallbackFont}`)
    })

    it('detects the trailing generic case-insensitively', () => {
        // Font values come from a config string, so casing is not guaranteed.
        expect(withMathFallback('Arial, SANS-SERIF')).toBe(`Arial, ${MathFallbackFont}, SANS-SERIF`)
    })

    it('is idempotent — does not stack the math font on re-render', () => {
        const once = withMathFallback("'IBM Plex Sans', sans-serif")

        expect(withMathFallback(once)).toBe(once)
    })

    it('does not re-add when the math font is already present in any position', () => {
        const stack = `'Noto Sans Math', 'IBM Plex Sans', sans-serif`

        expect(withMathFallback(stack)).toBe(stack)
    })

    it('returns empty/blank input unchanged so an unset font stays unset', () => {
        expect(withMathFallback('')).toBe('')
        expect(withMathFallback('   ')).toBe('   ')
    })
})
