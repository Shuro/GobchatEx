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

import { EventDispatcher } from './EventDispatcher.js'

/**
 * Client for the application-global settings (theme, language, update prefs, intervals, show/hide
 * hotkey). These live outside any profile in a separate store on the host and apply instantly — there
 * is no Save/Cancel buffer. Initial values are injected as `Gobchat.AppConfig`; the host raises the
 * `SynchronizeAppConfigEvent` DOM event after any change, and `set()` writes through immediately.
 */
export class AppConfig {
    #data: any
    #dispatcher = new EventDispatcher()

    constructor() {
        this.#data = (Gobchat as any).AppConfig ?? {}
        const onSync = () => { void this.reload() }
        document.addEventListener("SynchronizeAppConfigEvent", onSync)

        // The settings dialog runs in its own window with its own AppConfig, but the host dispatches the
        // sync event to the *overlay* (our window.opener) only. Listen on the opener's document too, so an
        // external change - e.g. toggling "always show overlay" from the tray - refreshes this window live
        // instead of silently going stale. Removed on unload so we don't leave a dead listener (capturing
        // this closed window's reload) on the long-lived opener. The overlay itself has no opener, so this
        // is a no-op there.
        const opener = (window.opener as Window | null) ?? null
        if (opener && opener !== window) {
            try {
                opener.document.addEventListener("SynchronizeAppConfigEvent", onSync)
                window.addEventListener("pagehide", () => {
                    try { opener.document.removeEventListener("SynchronizeAppConfigEvent", onSync) } catch { /* opener gone */ }
                })
            } catch (e) {
                console.error("Failed to bridge app-config sync from opener", e)
            }

            // The injected Gobchat.AppConfig is a one-time startup snapshot, so a window opened later (this
            // settings dialog) shows stale values if an app setting changed since - e.g. a tray toggle made
            // while settings was closed. Pull the authoritative values now; the resulting "change" refreshes
            // the bound controls.
            void this.reload()
        }
    }

    /** Re-pull the authoritative values from the host (after a SynchronizeAppConfigEvent). */
    async reload(): Promise<void> {
        try {
            const json = await GobchatAPI.getAppSettingsAsJson()
            this.#data = JSON.parse(json)
            this.#dispatcher.dispatch("change", {})
        } catch (e) {
            console.error("Failed to reload app settings", e)
        }
    }

    get(key: string, defaultValue: any = null): any {
        let node = this.#data
        for (const step of key.split(".")) {
            if (node === null || node === undefined || typeof node !== "object" || !(step in node))
                return defaultValue
            node = node[step]
        }
        return node === undefined ? defaultValue : node
    }

    /** Persist + apply instantly through the host (no profile Save). Updates the local copy optimistically. */
    async set(key: string, value: any): Promise<void> {
        this.#setLocal(key, value)
        this.#dispatcher.dispatch("change", {})
        try {
            await GobchatAPI.setAppSetting(key, JSON.stringify(value ?? null))
        } catch (e) {
            console.error(`Failed to set app setting '${key}'`, e)
        }
    }

    #setLocal(key: string, value: any): void {
        const steps = key.split(".")
        let node = this.#data
        for (let i = 0; i < steps.length - 1; i++) {
            if (node[steps[i]] === null || node[steps[i]] === undefined || typeof node[steps[i]] !== "object")
                node[steps[i]] = {}
            node = node[steps[i]]
        }
        node[steps[steps.length - 1]] = value
    }

    /** Fires after any change (local set or host-pushed reload). */
    onChange(callback: () => void): void {
        this.#dispatcher.on("change", callback)
    }
}
