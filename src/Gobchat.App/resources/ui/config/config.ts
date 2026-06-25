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

'use strict';

import "/module/JQueryExtensions"
import * as Databinding from '/module/Databinding'
import * as Config from '/module/Config'
import * as AppConfigModule from '/module/AppConfig'
import * as Locale from '/module/Locale'
import * as Style from '/module/Style'
import * as Dialog from '/module/Dialog'
import * as NavControl from '/module/MenuNavigationComponent'
import * as ProfileControl from '/module/ProfileControl'
import * as SettingsSearch from '/module/SettingsSearch'
import * as KonamiEgg from '/module/KonamiEasterEgg'

//import '/module/WebComponents'

// Forward uncaught errors / rejected promises to the host log. Without this, a failure in this window
// (e.g. one that stops the page reaching revealSettings) is invisible and only shows up as the host's
// reveal watchdog firing. Mirrors gobchat.ts.
window.addEventListener('error', (e) => {
    const err = (e as ErrorEvent).error
    console.error(`[uncaught] ${(e as ErrorEvent).message} @ ${(e as ErrorEvent).filename}:${(e as ErrorEvent).lineno}:${(e as ErrorEvent).colno}`, err && err.stack ? err.stack : '')
})
window.addEventListener('unhandledrejection', (e) => {
    const reason = (e as PromiseRejectionEvent).reason
    console.error(`[unhandledrejection] ${reason && reason.stack ? reason.stack : reason}`)
})

// initialize global variables
window.gobConfig = new Config.GobchatConfig()
// TSO-13: pull the current config straight from the overlay that opened us (same-origin window.opener),
// the same live reference config_groups/config_mentions/ProfileControl already read, instead of a
// localStorage handoff. Undefined (no opener) loads nothing and leaves defaults.
window.gobConfig.loadFromJson((window.opener as any)?.gobConfig?.serialize())

// Application-global settings (theme, language, …) live outside the profile and apply instantly.
window.gobAppConfig = new AppConfigModule.AppConfig()

window.gobLocale = new Locale.LocaleManager()

window.gobStyles = new Style.StyleLoader("..")
await gobStyles.initialize()

// Coloris (color picker) global defaults. Individual inputs are enrolled by
// Components.makeColorSelector via the "[data-coloris]" attribute, so this only needs
// to run once. themeMode is kept in sync with the active light/dark theme below.
if (typeof Coloris !== "undefined") {
    Coloris({
        el: "[data-coloris]",
        wrap: true,
        alpha: true,
        format: "auto",
        formatToggle: false,
        clearButton: true,
        focusInput: false,
        themeMode: "dark",
    })
}

// The new settings design carries its dark/light palette on the root element's
// data-theme attribute; mirror the active FFXIV theme (…Dark / …Light) onto it (and onto Coloris).
function applyThemeMode(themeName: string | null | undefined): void {
    const mode = /light/i.test(themeName ?? "") ? "light" : "dark"
    // Put it on <html> so the design tokens inherit everywhere, including modal <dialog>s
    // (which render in the top layer but still inherit custom properties from their ancestors).
    document.documentElement.setAttribute("data-theme", mode)
    if (typeof Coloris !== "undefined") Coloris({ themeMode: mode })
}

{
    // TSS-12: guard the opener; opening settings without the overlay as opener would otherwise throw a
    // TypeError that silently aborts all settings setup via the global unhandledrejection handler.
    const openerConfig = window.opener?.gobConfig
    let configBinding: Databinding.BindingContext | null = null
    if (!openerConfig) {
        console.warn("Settings window opened without an overlay opener; overlay-position bindings are inactive")
    } else {
        configBinding = new Databinding.BindingContext(openerConfig)
        configBinding.bindCallback("behaviour.frame.chat.position.x", value => gobConfig.set("behaviour.frame.chat.position.x", value))
        configBinding.bindCallback("behaviour.frame.chat.position.y", value => gobConfig.set("behaviour.frame.chat.position.y", value))
        configBinding.bindCallback("behaviour.frame.chat.size.width", value => gobConfig.set("behaviour.frame.chat.size.width", value))
        configBinding.bindCallback("behaviour.frame.chat.size.height", value => gobConfig.set("behaviour.frame.chat.size.height", value))
        configBinding.loadBindings()
    }

    $(window).on("beforeunload", function () {
        configBinding?.clearBindings()
    })
}


const binding = new Databinding.BindingContext(gobConfig)

// Set the locale up front (before the panels load below): panel modules call gobLocale.get(...) during
// their load for programmatic labels/placeholders, which aren't refreshed by updateElement afterwards.
gobLocale.setLocale(gobAppConfig.get("behaviour.language"))

// Language + theme are app-global now (gobAppConfig), not per-profile. Apply them once and re-apply
// when the host pushes a change (instant, no Save). Coloris' "Clear" label isn't a taggable DOM node,
// so it's re-issued here on language change.
// IMPORTANT: this is awaited before the window signals ready (revealSettings) on first run, so it must
// never reject and must not stall — otherwise the page never signals ready and the host's reveal
// watchdog has to step in. The locale pass (fast) is awaited so labels are localized before reveal; the
// theme stylesheet load is fire-and-forget (it may finish just after reveal, as it did originally).
async function applyAppSettings() {
    try {
        const language = gobAppConfig.get("behaviour.language")
        gobLocale.setLocale(language)
        await gobLocale.updateElement($(document))
        if (typeof Coloris !== "undefined") {
            Coloris({ clearLabel: await gobLocale.get("config.colorpicker.clear", language) })
        }
    } catch (e1) {
        console.error("Failed to apply locale to settings", e1)
    }

    try {
        const theme = gobAppConfig.get("style.theme")
        applyThemeMode(theme)
        // Don't await: the stylesheet load must not delay the window reveal.
        gobStyles.activateStyles(theme, $("#gob_autogenerated_stylesheet"), "before")
            .catch(async (e1) => {
                console.error(e1)
                try { await gobStyles.activateStyles() } catch (e2) { console.error(e2) }
            })
    } catch (e1) {
        console.error("Failed to apply theme to settings", e1)
    }
}
// NOTE: applyAppSettings() is both first invoked and subscribed to gobAppConfig changes *after* the
// config panels are loaded (see below) — its gobLocale.updateElement($(document)) must run once the
// panels exist, or their labels stay blank.

binding.bindCallback("style", (value) => {
    try {
        Style.StyleBuilder.generateAndSetCssRules("gob_autogenerated_stylesheet")
    } catch (e1) {
        console.error(e1)
    }

    // Mirror the chosen chat font onto the settings page itself: --gx-user-font drives the base
    // font of .gx-root / .gx-dialog (config.scss), so changing the font picker re-fonts the whole
    // window live. Remove (don't set "") when unset so the CSS var() fallback to IBM Plex Sans applies.
    try {
        const userFont = gobConfig.get("style.channel.base.general.font-family", null) as string | null
        if (userFont)
            document.documentElement.style.setProperty("--gx-user-font", userFont)
        else
            document.documentElement.style.removeProperty("--gx-user-font")
    } catch (e1) {
        console.error(e1)
    }
})

const selProfile = $("#cp-main_profile-select")
selProfile.on("change", async (event) => {
    const profileId = event.target.value as string
    // The switch (warn-on-unsaved, discard, apply-live) is shared with the Profiles page; on a
    // cancelled or aborted switch, snap the dropdown back to the still-active profile.
    const switched = await ProfileControl.requestProfileSwitch(profileId)
    if (!switched)
        selProfile.val(gobConfig.activeProfileId ?? "")
})

function populateProfileSelection() {
    selProfile.empty()

    // Same order as the Profiles page (ProfileControl.sortedProfileIds) so dropdown and list never diverge.
    ProfileControl.sortedProfileIds()
        .forEach(profileId => {
            const profile = gobConfig.getProfile(profileId) as Config.ConfigProfile
            selProfile.append(new Option(profile.profileName, profileId))
        })

    selProfile.val(gobConfig.activeProfileId)
}

gobConfig.addProfileEventListener((event) => {
    if (event.action === "active") {
        selProfile.val(event.newProfileId)
    } else {
        populateProfileSelection()
    }
})

gobConfig.addPropertyEventListener("profile.name", () => {
    const profileId = selProfile.val()
    populateProfileSelection()
    selProfile.val(profileId) // restore old selection
})

populateProfileSelection()

const mutationObserver = new window.MutationObserver((mutations, observer) => {
    for (const mutation of mutations) {
        let updateElement = false

        if (mutation.type === 'attributes')
            updateElement = mutation.attributeName === Locale.HtmlAttribute.TextId || mutation.attributeName === Locale.HtmlAttribute.TooltipId

        if (updateElement && mutation.target instanceof HTMLElement) {
            const target = mutation.target
            if (target.getAttribute(Locale.HtmlAttribute.ActiveLocale) !== gobLocale.locale)
                gobLocale.updateElement(target)
        }
            
    }
})

mutationObserver.observe(document.body, { childList: false, subtree: true, attributes: true })

// Text selection in the settings UI is a development convenience. Release builds disable it
// (see config.scss) so the dialog reads like a finished app; Debug builds opt back in via the
// `is-debug` root class. The Debug page is also developer-only: drop its nav entry in Release
// builds before the panels are built so its page is never fetched.
const isDebugBuild = await GobchatAPI.isDebugBuild()
if (isDebugBuild)
    document.getElementById("gob-config-root")?.classList.add("is-debug")
else
    $(".gob-config-navigation_entry[data-gob-nav-target='config_debug.html']").remove()

await NavControl.makeControl($(".gob-config-navigation"))

// Remember which settings tab is open for the rest of this app session. The settings window is
// destroyed on close, but our opener (the overlay window) outlives it, so a plain property on it is
// session-scoped: it survives close/reopen and resets on app restart (no persistence wanted). Record
// every nav switch — including the programmatic ones below — so the next open can restore it.
$(".gob-config-navigation_entry").on("click", function () {
    const target = $(this).attr("data-gob-nav-target") as string | undefined
    if (target && window.opener)
        window.opener.gobLastSettingsTab = target
})

const isDryRun = await GobchatAPI.isDryRun()

// A tab remembered from this session always wins; otherwise dry-run launches straight into the Debug
// page (its Dry Run section is the focus) and a normal launch stays on App (the HTML default). A
// missing/stale target (e.g. config_debug.html in a Release build, where the Debug entry was removed
// above) is a safe no-op.
let targetTab = window.opener?.gobLastSettingsTab as string | undefined
if (!targetTab && isDryRun)
    targetTab = "config_debug.html"

if (targetTab && targetTab !== "config_app.html") {
    // Native click dispatches the event the nav entry's `.on("click")` handler listens for.
    const nav = $(`.gob-config-navigation_entry[data-gob-nav-target='${targetTab}']`)[0] as HTMLElement | undefined
    nav?.click()
}

// Now that every panel is in the DOM, apply theme + locale (the latter localizes the just-loaded
// panels via updateElement($(document))). This matches the old timing, when the language/theme
// bindings fired at loadBindings() below — after the panels were built.
await applyAppSettings()
// Subscribe only now — after the first apply and after the panels are built — so a host-pushed
// SynchronizeAppConfigEvent during init can't run updateElement over a partially-built DOM.
// Mirrors gobchat.ts's ordering.
gobAppConfig.onChange(() => { void applyAppSettings() })

binding.loadBindings()

// Settings search lives in the nav rail. Wire it after the panels are built + localized (above) so its
// label index is complete and in the current language; it (re)builds on focus, so language changes are
// handled too. Failure here must not block the window reveal below.
try {
    await SettingsSearch.makeControl($(".gob-config-navigation"), $("#cp-main_search"), $("#cp-main_search_results"))
} catch (e) {
    console.error("Failed to initialize settings search", e)
}

// initialize main buttons

// The single Save button now saves AND closes the settings window.
$("#cp-main_save-config").on("click", function () {
    ProfileControl.logUnsavedChanges("Settings save requested - changes being written")
    window.saveConfig(window.gobConfig.serialize())
    window.close()
})

// The disk button beside Save persists the config but keeps the settings window open.
$("#cp-main_apply-config").on("click", function () {
    ProfileControl.logUnsavedChanges("Settings apply requested - changes being written")
    window.saveConfig(window.gobConfig.serialize())
})

const cancelConfig = async function () {
    // Only nag when there's something to lose: with no unsaved edits, close straight away.
    if (!ProfileControl.hasUnsavedChanges()) {
        window.close()
        return
    }
    ProfileControl.logUnsavedChanges("Settings close/cancel requested - unsaved changes will be discarded")
    const result = await Dialog.showConfirmationDialog({ dialogText: "config.main.nav.cancel.dialog" })
    if (result)
        window.close()
}

$("#cp-main_cancel-config").on("click", cancelConfig)

// The title-bar close button discards unsaved changes (same as Cancel).
$("#cp-main_titlebar-close").on("click", cancelConfig)

// The title-bar minimize button sends the (now taskbar-listed) settings window to the taskbar.
$("#cp-main_titlebar-minimize").on("click", function () {
    GobchatAPI.minimizeSettings()
})

// Easter egg: the Konami code turns the whole title bar into a moving rainbow and plays a short
// tune. Guarded so a failure here can never block the window reveal below.
try {
    KonamiEgg.installKonamiEasterEgg($(".gx-titlebar")[0])
} catch (e) {
    console.error("Failed to install settings easter egg", e)
}

// Companion egg on the About page: light each Konami-hint glyph gold as the code is typed in order.
// The page is already loaded into the DOM by makeControl above, so the hint exists. Guarded the same way.
try {
    KonamiEgg.installKonamiHint($(".cp-about_konami")[0])
} catch (e) {
    console.error("Failed to install About-page Konami hint", e)
}

$("#cp-main_close-gobchat").on("click", async function () {
    // With unsaved changes, warn about losing them first, then confirm the exit itself (two prompts).
    if (ProfileControl.hasUnsavedChanges()) {
        ProfileControl.logUnsavedChanges("Exit GobchatEx requested - unsaved changes will be discarded")
        const discard = await Dialog.showConfirmationDialog({ dialogText: "config.main.nav.cancel.dialog" })
        if (!discard)
            return
    }
    const result = await Dialog.showConfirmationDialog({ dialogText: "config.main.nav.closegobchat.dialog" })
    if (result) {
        window.close()
        GobchatAPI.closeGobchat()
    }
})

// Everything above has built and themed the page. Reveal the (until-now hidden) settings window so it
// appears already rendered, with no empty-frame flash. The host also has a watchdog + failed-load
// fallback, so a failure here can't leave the window invisible.
try {
    await GobchatAPI.revealSettings()
} catch (e) {
    console.error("Failed to signal settings window reveal", e)
}

// While the settings window is open, keep nearby-position scanning alive on the host so the Range Filter
// preview and the Debug "nearby players" panel work even when no chat tab has the range filter enabled.
// The host keepalive self-expires ~5s after the last ping, so an abrupt window close just lets it lapse.
const pingActorPreview = () => { GobchatAPI.keepActorPreviewAlive().catch(e => console.error("keepActorPreviewAlive failed", e)) }
pingActorPreview()
const actorPreviewHeartbeat = setInterval(pingActorPreview, 2000)
$(window).on("beforeunload", () => clearInterval(actorPreviewHeartbeat))
