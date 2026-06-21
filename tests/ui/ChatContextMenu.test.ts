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
import * as ContextMenu from '../../src/Gobchat.App/resources/ui/modules/ContextMenu'

// The per-entry right-click menu has a single item whose wording must track the entry's current state:
// an already-hidden entry must offer to bring it back ("Un-hide"), a visible one must offer to hide it
// ("Hide Entry"). WHY it matters: the label is the only cue for what a click does, and the whole
// hide -> eye-reveal -> un-hide workflow depends on the same item flipping its meaning. A label that
// didn't follow the state would make the action ambiguous.
describe('ContextMenu.hideMenuLabel', () => {
    it('offers "Hide Entry" for a currently-visible entry', () => {
        expect(ContextMenu.hideMenuLabel(false)).toBe('Hide Entry')
    })

    it('offers "Un-hide" for an already-hidden entry', () => {
        expect(ContextMenu.hideMenuLabel(true)).toBe('Un-hide')
    })

    it('flips wording with the hidden state so the click action is never ambiguous', () => {
        expect(ContextMenu.hideMenuLabel(false)).not.toBe(ContextMenu.hideMenuLabel(true))
    })
})
