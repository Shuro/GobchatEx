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

import * as Utility from './CommonUtility.js'

// The overlay paints itself from CSS generated out of the active profile (StyleBuilder). Those style
// values (colours, sizes, font-family) and the trigger-group/tab ids that become selectors are
// free-form fields the user edits in settings AND are persisted into the profile JSON — which can be
// hand-edited or imported from an untrusted file (see GobchatAPI.writeTextToFile / readTextFromFile).
// So everything that flows from config into a <style> sheet is treated as untrusted here.

// Characters that let a CSS *value* escape its declaration or the whole rule:
//   '}' / '{'  close/open a rule,  ';' starts a new declaration,  '@' opens an at-rule,
//   '<' / '>'  could close the host <style>,  backslash is a CSS escape,  and the comment
//   markers can hide a payload. No legitimate config value (a colour, a length, a font stack)
//   contains any of them, so a value that does is dropped rather than emitted.
const UNSAFE_CSS_VALUE = /[<>{}@;\\]|\/\*|\*\//

export function isSafeCssValue(value: unknown): boolean {
    return Utility.isString(value) && !UNSAFE_CSS_VALUE.test(value as string)
}

/**
 * Returns the value unchanged when it cannot break out of a CSS declaration, otherwise `null` so the
 * caller drops the property. `null`/`undefined` pass through unchanged (the emit logic already skips
 * them); non-strings are returned as-is for the same reason.
 */
export function sanitizeCssValue<T>(value: T): T | null {
    if (value === null || value === undefined)
        return value
    if (!Utility.isString(value))
        return value
    return isSafeCssValue(value) ? value : null
}

/**
 * Reduces a CSS identifier fragment (a trigger-group or tab id) to `[A-Za-z0-9_-]` so it cannot inject
 * selector syntax (whitespace, `{`, `.`, `:`, `,` …). App-generated ids are already alphanumeric, so
 * this is a no-op for them. The SAME function must be applied wherever the id becomes a DOM class AND
 * wherever it becomes the matching selector, so a sanitized class still matches its sanitized selector.
 */
export function sanitizeCssIdentifier(value: unknown): string {
    return String(value ?? "").replace(/[^A-Za-z0-9_-]/g, "_")
}
