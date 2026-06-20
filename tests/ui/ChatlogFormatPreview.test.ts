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
import { renderFormatPreview } from '../../Gobchat.App/resources/ui/modules/ChatlogFormatPreview'

// renderFormatPreview shows the user what their chat-log {token} format string will produce. WHY it
// matters: it must match CustomChatLogger.SetLogFormat exactly — recognized {token}s (case-insensitive,
// hyphen/underscore names) get substituted, and anything unrecognized is left verbatim so the preview
// never silently hides a typo'd token the C# writer would also pass through unchanged.

const tokens: { [name: string]: string } = {
    'TIME': '21:34',
    'CHANNEL': 'Say',
    'CHANNEL-PADR': 'Say     ',
    'SENDER-CHA': 'Firstname Lastname:',
    'MESSAGE': 'Well met!',
    'BREAK': '\n',
}

describe('renderFormatPreview', () => {
    it('substitutes recognized tokens', () => {
        expect(renderFormatPreview('{channel} {sender-cha} {message}', tokens))
            .toBe('Say Firstname Lastname: Well met!')
    })

    it('matches token names case-insensitively', () => {
        expect(renderFormatPreview('{TIME}|{Time}|{time}', tokens)).toBe('21:34|21:34|21:34')
    })

    it('substitutes hyphenated token names', () => {
        expect(renderFormatPreview('[{channel-padr}]', tokens)).toBe('[Say     ]')
    })

    it('leaves an unrecognized token verbatim (mirrors the C# passthrough)', () => {
        expect(renderFormatPreview('{message} {nope}', tokens)).toBe('Well met! {nope}')
    })

    it('replaces every occurrence of a repeated token', () => {
        expect(renderFormatPreview('{time} - {time}', tokens)).toBe('21:34 - 21:34')
    })

    it('does not touch braces that are not a bare {token}', () => {
        expect(renderFormatPreview('{ not a token } {message}', tokens)).toBe('{ not a token } Well met!')
    })

    it('keeps literal text and unknown punctuation untouched', () => {
        expect(renderFormatPreview('[{channel}] plain text', tokens)).toBe('[Say] plain text')
    })

    it('returns an empty string for empty or missing input', () => {
        expect(renderFormatPreview('', tokens)).toBe('')
        expect(renderFormatPreview(undefined as unknown as string, tokens)).toBe('')
    })
})
