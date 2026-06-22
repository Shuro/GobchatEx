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

// Labels for the chat entry's right-click menu. Kept as a standalone module (only CommonUtility, which is
// itself dependency-free) so the label/group decisions stay unit-testable — importing Chat.ts instead
// pulls in browser globals via Constants.js. The overlay toolbar uses hardcoded English titles, so these
// match that rather than going through gobLocale.

export const Label_Hide = "Hide Entry"
export const Label_Unhide = "Un-hide"
export const Label_AddToGroup = "Add Player to Custom Group"
export const Label_RemoveFromGroup = "Remove Player from Custom Group"
export const Label_CreateNewGroup = "Create new group…"

// The single menu item toggles wording: an already-hidden entry offers "Un-hide", otherwise "Hide Entry".
export function hideMenuLabel(isHidden: boolean): string {
    return isHidden ? Label_Unhide : Label_Hide
}

// A custom group is a user-defined highlight group; the 7 baked-in "ff" groups carry an `ffgroup`
// field and are matched by group icon rather than by player name, so they can't take name triggers
// and are excluded from the add/remove submenus.
export interface GroupLike {
    id: string
    name: string
    ffgroup?: number
    trigger?: string[]
}

export interface GroupRef {
    id: string
    name: string
}

// Trigger names are stored lowercased and without the world suffix (see config_groups.ts), and the chat
// source is matched case-insensitively and server-agnostically (Chat.ts ChatGroupControl), so the same
// normalization — strip "[Server]", lowercase — must gate add/remove/contains.
export function normalizePlayerName(source: string): string {
    return Utility.stripServerName(source).trim().toLowerCase()
}

function isCustomGroup(group: GroupLike): boolean {
    return !("ffgroup" in group)
}

// The "Add Player to Custom Group" submenu lists every custom group, in their configured order.
export function customGroups(groups: GroupLike[]): GroupRef[] {
    return groups.filter(isCustomGroup).map(g => ({ id: g.id, name: g.name }))
}

// The premade ("ff") groups in their intrinsic order (by ffgroup, 0..6 -> shown as 1..7). They live
// outside the user's custom `sorting` list, so callers that need the full set of groups append these
// after the custom groups. That canonical order ("custom first, then premade") also makes a custom
// group's highlight win over a premade one when a player is in both (first match wins). Returns the
// full objects so callers can read ffgroup/trigger; the generic keeps the caller's element type.
export function premadeGroups<T extends GroupLike>(groups: T[]): T[] {
    return groups.filter(g => !isCustomGroup(g)).sort((a, b) => (a.ffgroup ?? 0) - (b.ffgroup ?? 0))
}

// The "Remove Player from Custom Group" submenu lists only the custom groups the player is already in.
// An empty result is what drives the grayed-out, non-expandable Remove item.
export function groupsContainingPlayer(groups: GroupLike[], playerName: string): GroupRef[] {
    const normalized = normalizePlayerName(playerName)
    return groups
        .filter(isCustomGroup)
        .filter(g => (g.trigger ?? []).includes(normalized))
        .map(g => ({ id: g.id, name: g.name }))
}
