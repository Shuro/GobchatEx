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

// The chat overlay's right-click menu offers "Add Player to Custom Group" (every custom group) and
// "Remove Player from Custom Group" (only groups the player is already in). These pure helpers decide
// exactly which groups each submenu lists. The 7 baked-in "ff" groups are matched by group icon, not by a
// player-name trigger, so they must never appear: adding a name to one would silently do nothing.
describe('ContextMenu.customGroups', () => {
    const groups: ContextMenu.GroupLike[] = [
        { id: 'group-ff-1', name: '★', ffgroup: 0 },
        { id: 'a1b2c3', name: 'Friends', trigger: ['alice arandi'] },
        { id: 'd4e5f6', name: 'Rivals', trigger: [] },
        { id: 'group-ff-2', name: '●', ffgroup: 1 },
    ]

    it('lists only user-defined custom groups, excluding the baked-in ff groups', () => {
        expect(ContextMenu.customGroups(groups).map(g => g.id)).toEqual(['a1b2c3', 'd4e5f6'])
    })

    it('preserves the configured order so the submenu matches the settings page', () => {
        const reordered: ContextMenu.GroupLike[] = [groups[2], groups[1]]
        expect(ContextMenu.customGroups(reordered).map(g => g.name)).toEqual(['Rivals', 'Friends'])
    })
})

describe('ContextMenu.groupsContainingPlayer', () => {
    const groups: ContextMenu.GroupLike[] = [
        { id: 'group-ff-1', name: '★', ffgroup: 0 },
        { id: 'a1b2c3', name: 'Friends', trigger: ['alice arandi', 'bob brandt'] },
        { id: 'd4e5f6', name: 'Rivals', trigger: ['carol crane'] },
    ]

    it('matches case-insensitively so the entry name and stored trigger always line up', () => {
        // data-source carries the display-cased name; triggers are stored lowercased.
        expect(ContextMenu.groupsContainingPlayer(groups, 'Alice Arandi').map(g => g.id)).toEqual(['a1b2c3'])
    })

    it('returns every group the player is in, in order', () => {
        const both: ContextMenu.GroupLike[] = [
            { id: 'a1b2c3', name: 'Friends', trigger: ['alice arandi'] },
            { id: 'd4e5f6', name: 'Rivals', trigger: ['alice arandi'] },
        ]
        expect(ContextMenu.groupsContainingPlayer(both, 'alice arandi').map(g => g.id)).toEqual(['a1b2c3', 'd4e5f6'])
    })

    it('returns empty when the player is in no group — this is what grays out the Remove item', () => {
        expect(ContextMenu.groupsContainingPlayer(groups, 'Dave Doe')).toEqual([])
    })

    it('never matches an ff group even if one somehow carried the name', () => {
        const ffWithTrigger: ContextMenu.GroupLike[] = [
            { id: 'group-ff-1', name: '★', ffgroup: 0, trigger: ['alice arandi'] },
        ]
        expect(ContextMenu.groupsContainingPlayer(ffWithTrigger, 'alice arandi')).toEqual([])
    })

    it('matches a cross-world player (source carries [Server]) against a bare-name trigger', () => {
        // The Remove submenu must list the group even when the clicked line is from another world.
        expect(ContextMenu.groupsContainingPlayer(groups, 'Alice Arandi [Shiva]').map(g => g.id)).toEqual(['a1b2c3'])
    })
})

describe('ContextMenu.normalizePlayerName', () => {
    it('trims and lowercases so it matches how triggers are stored', () => {
        expect(ContextMenu.normalizePlayerName('  Alice Arandi  ')).toBe('alice arandi')
    })

    it('strips the [Server] suffix so members are stored/matched server-agnostically', () => {
        expect(ContextMenu.normalizePlayerName('Alice Arandi [Shiva]')).toBe('alice arandi')
    })
})
