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

// compiler doesn't recognize globals.d.ts for this file

'use strict'

import * as Databinding from './modules/Databinding.js'
import * as Config from './modules/Config.js'
import * as AppConfigModule from './modules/AppConfig.js'
import * as Locale from './modules/Locale.js'
import * as Style from './modules/Style.js'
import * as Chat from './modules/Chat.js'

// The host bridge only forwards console.* output to the log, not engine-level uncaught errors
// or rejected promises. Forward those explicitly so frontend failures show up in
// gobchatex_debug.log instead of dying silently in the WebView2.
window.addEventListener('error', (e) => {
    const err = (e as ErrorEvent).error
    console.error(`[uncaught] ${(e as ErrorEvent).message} @ ${(e as ErrorEvent).filename}:${(e as ErrorEvent).lineno}:${(e as ErrorEvent).colno}`, err && err.stack ? err.stack : '')
})
window.addEventListener('unhandledrejection', (e) => {
    const reason = (e as PromiseRejectionEvent).reason
    console.error(`[unhandledrejection] ${reason && reason.stack ? reason.stack : reason}`)
})

// Indicate that window resizing is possible
document.addEventListener("OverlayStateUpdate", function (event: OverlayStateUpdateEvent) {
    const isLocked = event.detail.isLocked
    if (!isLocked)
        document.documentElement.classList.add("gob-document--resize");
    else
        document.documentElement.classList.remove("gob-document--resize");
    // Accent the pin gold while unpinned (unlocked = editable); pinned/locked reads as the neutral grey.
    $("#gob_toggle_pin").css("color", isLocked ? "" : "#e0a44e")
} as EventListener);

// Search hotkey: the host has already focused the overlay + routed keyboard to the page; open the
// search bar and put the cursor in the input. showSearch() is idempotent, so a re-press just re-focuses.
document.addEventListener("FocusSearchEvent", function () {
    gobChatManager?.showSearch()
});

// initialize global variables
jQuery(async function ($) {
    window.gobConfig = new Config.GobchatConfig(true)
    await gobConfig.loadConfig()

    // Application-global settings (theme, language, …) live outside the profile and apply instantly.
    window.gobAppConfig = new AppConfigModule.AppConfig()

    window.gobLocale = new Locale.LocaleManager()

    window.gobStyles = new Style.StyleLoader(".")
    await gobStyles.initialize()
 
    window.gobChatManager = new Chat.ChatControl()

    $("#gob_toggle_search").on("click", () => gobChatManager.toggleSearch())

    // Pin = lock/unlock the overlay for moving + resizing (handled host-side via OverlayForm). The old
    // "show while logged out" pin moved to the tray. The pin's accent + the frame's resize affordances
    // follow the OverlayStateUpdate event (see the document listener near the top of this file).
    $("#gob_toggle_pin").on("click", async () => {
        await GobchatAPI.toggleOverlayLock()
    })
    // The overlay starts locked/pinned, which now reads as the neutral grey (gold means unpinned).
    $("#gob_toggle_pin").css("color", "")

    // While unlocked, dragging the top toolbar background/grip moves the window via the host; the
    // cog/search/pin icons and the tabs (all <button>s) stay clickable, so the pin can re-lock.
    $(".gob-chat-toolbar--top").on("mousedown", (event) => {
        if (event.button !== 0)
            return
        if (!document.documentElement.classList.contains("gob-document--resize"))
            return
        if ($(event.target as HTMLElement).closest("button, input").length)
            return
        GobchatAPI.beginWindowDrag()
    })
    // Resizing is handled host-side (OverlayForm hit-tests the edges/corner, shows the OS resize
    // cursors, and runs a custom resize that keeps the WebView2 live). The .gob-chat_resize--* spans
    // are just the gold edge ticks.

    // Language + theme are app-global now (gobAppConfig), not per-profile. Apply them once and re-apply
    // whenever the host pushes a change (instant, no Save). The light palette toggle + style-loader call
    // mirror the settings window's applyThemeMode (config.ts).
    async function applyAppSettings() {
        gobLocale.setLocale(gobAppConfig.get("behaviour.language"))
        gobLocale.updateElement($(document))

        const theme = gobAppConfig.get("style.theme")
        document.documentElement.classList.toggle("theme-light", /light/i.test(theme ?? ""))
        try {
            await gobStyles.activateStyles(theme, $("#gob_autogenerated_stylesheet"), "before")
        } catch (e1) {
            console.error(e1)
            await gobStyles.activateStyles()
        }
    }
    await applyAppSettings()
    gobAppConfig.onChange(() => { void applyAppSettings() })

    const binding = new Databinding.BindingContext(gobConfig)
    binding.bindCallback("style", (value) => {
        try {
            Style.StyleBuilder.generateAndSetCssRules("gob_autogenerated_stylesheet")
        } catch (e1) {
            console.error(e1)
        }
    })

    // FFXIV Modern theme reads these off the root element to pick the tab look + row spacing. They are
    // plain data-attributes (no generated CSS), so just mirror the config value onto <html>.
    binding.bindCallback("style.chat-frame.tab-style", (value) => {
        document.documentElement.setAttribute("data-tab-style", value)
    })
    binding.bindCallback("style.chat-frame.density", (value) => {
        document.documentElement.setAttribute("data-chat-density", value)
    })

    binding.loadBindings()
    
    gobChatManager.control($(`.${Chat.CssClass.Chat}`))
    gobChatManager.hideSearch()

    GobchatAPI.setUIReady(true)
});

// feature - open config
jQuery(function ($) {
    const localStorageKey = "gobchat-config-open"

    // A fresh page load means no config popup is associated with this overlay anymore. A leftover
    // "open" flag (e.g. from a popup whose close was never detected) would otherwise lock the user
    // out of settings permanently, so always clear it on load.
    window.localStorage.removeItem(localStorageKey)

    async function openGobchatConfig() {
        console.info("openGobchatConfig: requested")
        const isConfigOpen = window.localStorage.getItem(localStorageKey) || "false"

        if (isConfigOpen === "true") {
            // A settings window is flagged open — bring it to the front instead of opening a second one.
            // If the host has no settings window (a stale flag from a missed close), clear it and fall
            // through to open a fresh one.
            const focused = await GobchatAPI.focusSettings()
            if (focused) {
                console.info("openGobchatConfig: settings window already open, brought to front")
                return
            }
            console.info("openGobchatConfig: stale open flag (no settings window); reopening")
            window.localStorage.removeItem(localStorageKey)
        }

        window.localStorage.setItem(localStorageKey, "true")

        try {
            gobConfig.saveToLocalStore()

            const bounds = await GobchatAPI.getScreenDimensions()
            console.info(`openGobchatConfig: screen dimensions = ${JSON.stringify(bounds)}`)
            const screenWidth = bounds.Width
            const screenHeight = bounds.Height

            // Open at the settings design size. The window's placement is no longer read from the
            // profile: it's stored app-globally and restored (clamped on-screen) by the host on open,
            // so the position is independent of the active profile. Pass only the size here.
            const dialogWidth = 1200
            const dialogHeight = 880

            const features = `width=${dialogWidth},height=${dialogHeight}`

            const handle = window.open("config/config.html", 'Settings', features)
            if (handle === null) {
                console.error(`openGobchatConfig: window.open returned null (requested ${dialogWidth}x${dialogHeight}), settings popup did not open`)
                window.localStorage.removeItem(localStorageKey)
                return
            }

            handle.saveConfig = function () {
                gobConfig.loadFromLocalStore()
            }

            handle.focus()
            console.info("openGobchatConfig: settings popup opened")

            const timer = setInterval(function () {
                if (handle.closed) {
                    clearInterval(timer);
                    window.localStorage.removeItem(localStorageKey)
                }
            }, 1000);
        } catch (e) {
            console.error(`openGobchatConfig: failed to open settings`, (e as any)?.stack ?? e)
            window.localStorage.removeItem(localStorageKey)
        }
    }

    $("#gob_show_config").on("click", openGobchatConfig)
    window.openGobConfig = openGobchatConfig
})