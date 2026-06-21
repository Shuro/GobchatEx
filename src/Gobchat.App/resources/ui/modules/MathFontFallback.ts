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

'use strict'

// Decorative "math" letters (Mathematical Alphanumeric Symbols, U+1D400–U+1D7FF — e.g. someone typing
// "𝗙𝗟𝗨𝗫" for "FLUX") aren't in the chat fonts and render as tofu. Inserting the bundled Noto Sans Math
// (it covers the whole block) into a font-family stack lets the browser fall back to it per-glyph for
// exactly those code points, while every normal glyph still uses the chosen font, for any weight.
// Bundled + URL-loaded on purpose: the installed Windows math font (Cambria Math) lives in a .ttc
// collection that WebView2 won't resolve by name for text fallback.

export const MathFallbackFont = "'Noto Sans Math'"

// CSS generic family keywords. A family listed AFTER a generic is unreachable — the generic already
// hands off to system fallback — so the math font must be inserted BEFORE a trailing generic.
const GenericFontFamilies = new Set([
    "serif", "sans-serif", "monospace", "cursive", "fantasy", "system-ui",
    "ui-serif", "ui-sans-serif", "ui-monospace", "ui-rounded", "math", "emoji", "fangsong",
])

/**
 * Returns <paramref/> `fontFamily` with the bundled math fallback inserted just before any trailing
 * generic family (otherwise appended). Returns the input string unchanged — by identity, so callers
 * can detect the no-op — when it is empty/blank or already contains the fallback. Idempotent: safe to
 * run on every style rebuild without the stack growing.
 */
export function withMathFallback(fontFamily: string): string {
    if (typeof fontFamily !== "string" || fontFamily.trim().length === 0)
        return fontFamily
    if (/noto\s+sans\s+math/i.test(fontFamily))
        return fontFamily // already present — don't double up

    const families = fontFamily.split(",").map(f => f.trim()).filter(f => f.length > 0)
    const lastIndex = families.length - 1
    if (lastIndex >= 0 && GenericFontFamilies.has(families[lastIndex].toLowerCase()))
        families.splice(lastIndex, 0, MathFallbackFont)
    else
        families.push(MathFallbackFont)

    return families.join(", ")
}
