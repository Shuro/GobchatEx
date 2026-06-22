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

// Pure decision behind MessageBuilder.getMessageVisibilityCssClass (Chat.ts). Kept as its own DOM-/config-free
// module so it can be unit-tested in isolation (tests/ui/ChatVisibility.test.ts) — importing Chat.ts instead
// pulls in browser globals via Constants.js. Chat.ts keeps the impure shell (read the two config values, format
// the CssClass) and feeds the resolved values in here.

// Which fade-out step a message at `visibility` (0..100, the range-filter opacity the engine assigned it)
// renders at, or null for "no fade-out class" (fully visible, or a mention while ignoreMention is on):
//   - visibility >= 100: solid, no class.
//   - ignoreMention && containsMentions: mentions bypass the distance fade entirely.
//   - otherwise: bucket into one of `opacitySteps` levels, rounding UP so anything below 100 fades at least
//     one step (ceil via the +stepSize-1 then truncate trick) and the level maps to a gob-chat-entry--fadeout-N
//     class that Style.ts defines per step.
export function getFadeOutLevel(visibility: number, containsMentions: boolean, ignoreMention: boolean, opacitySteps: number): number | null {
    if (visibility >= 100)
        return null
    if (ignoreMention && containsMentions)
        return null

    const fadeOutStepSize = 100 / opacitySteps
    return ((visibility + fadeOutStepSize - 1) / fadeOutStepSize) >> 0 // truncate decimals → integer level
}
