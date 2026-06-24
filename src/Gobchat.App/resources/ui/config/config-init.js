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

// WVH-10: this ran as an inline <script> in config.html, which a strict Content-Security-Policy
// (script-src 'self') would block. Extracted to a served file so the CSP needs no 'unsafe-inline' for
// scripts. It still runs as a classic (non-deferred) script before config.js, sharing the global scope.
// The settings window shares its opener's GobchatAPI/console/Gobchat (window.opener), so wire those first.
window.console = window.opener.console

window.addEventListener('error', function (evt) {
    console.error(evt.error.stack)
})

window.GobchatAPI = window.opener.GobchatAPI
window.Gobchat = window.opener.Gobchat
