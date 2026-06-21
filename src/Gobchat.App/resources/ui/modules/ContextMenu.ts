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

// Labels for the chat entry's right-click menu. Kept as a standalone, dependency-free module so the
// label decision stays unit-testable (importing Chat.ts pulls in browser globals via Constants.js).
// The overlay toolbar uses hardcoded English titles, so these match that rather than going through gobLocale.

export const Label_Hide = "Hide Entry"
export const Label_Unhide = "Un-hide"

// The single menu item toggles wording: an already-hidden entry offers "Un-hide", otherwise "Hide Entry".
export function hideMenuLabel(isHidden: boolean): string {
    return isHidden ? Label_Unhide : Label_Hide
}
