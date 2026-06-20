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

// Pure core of the Chatlog page's format preview (config_chatlog.ts). DOM-free so it can be unit-tested
// (tests/ui/ChatlogFormatPreview.test.ts); the page supplies the illustrative `tokens` sample vocabulary.

// Same regex + unknown-token passthrough as CustomChatLogger.SetLogFormat: a recognized {token}
// (case-insensitive) is replaced by its sample value from `tokens`; anything else is left verbatim.
export function renderFormatPreview(format: string, tokens: { [name: string]: string }): string {
    return (format ?? "").replace(/\{(\w+(?:[_-]\w+)*)\}/g, (whole, name: string) => {
        const sample = tokens[name.toUpperCase()]
        return sample !== undefined ? sample : whole
    })
}
