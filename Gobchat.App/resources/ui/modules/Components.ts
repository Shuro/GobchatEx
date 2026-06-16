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

import * as Databinding from "/module/Databinding"
import * as Dialog from "/module/Dialog"
import * as Locale from "/module/Locale"
import * as Utility from "/module/CommonUtility"

interface ColorSelectorOptionTypes {
    hasAlpha: boolean
    hasReset: boolean
    onBeforeShow: null | ((color: string) => boolean)
}

const DefaultColorSelectorOptions: ColorSelectorOptionTypes = {
    hasAlpha: true,
    hasReset: true,
    onBeforeShow: null
}

export type ColorSelectorOptions = Partial<ColorSelectorOptionTypes>

// --- Shared Ctrl-key tracking (used by reset buttons to unlock a "protected" revert) ---
let _ctrlTrackingInitialized = false
let _ctrlHeld = false
const _ctrlSubscribers = new Set<() => void>()

function ensureCtrlTracking(): void {
    if (_ctrlTrackingInitialized)
        return
    _ctrlTrackingInitialized = true
    const setHeld = (held: boolean) => {
        if (_ctrlHeld === held)
            return
        _ctrlHeld = held
        _ctrlSubscribers.forEach(fn => { try { fn() } catch (e) { console.error(e) } })
    }
    document.addEventListener("keydown", (e: KeyboardEvent) => { if (e.key === "Control") setHeld(true) })
    document.addEventListener("keyup", (e: KeyboardEvent) => { if (e.key === "Control") setHeld(false) })
    window.addEventListener("blur", () => setHeld(false))
}

function onCtrlChange(fn: () => void): void {
    ensureCtrlTracking()
    _ctrlSubscribers.add(fn)
}

function _makeColorSelector(element: JQuery, options: ColorSelectorOptionTypes): void {
    if (element.length < 1)
        throw new Error("An empty element can't be turned into a color selector")

    if (element.length > 1)
        throw new Error(`Unable to turn multiple elements into the same color selector`)

    if (typeof Coloris === "undefined") {
        console.error("Coloris is not loaded; unable to create a color selector")
        return
    }

    const el = element[0] as HTMLElement

    // Coloris opens for any element matching its global `el` selector ("[data-coloris]"),
    // so simply tagging the input enrolls it (works for pages loaded dynamically, too).
    element.attr("data-coloris", "")
    element.addClass("gob-color-selector")
    element.prop("readonly", true) // pick via the popup only, like the old swatch input

    // Per-element alpha override (the global default keeps alpha on). All current call
    // sites use alpha, so this normally does nothing.
    if (!options.hasAlpha && el.id)
        Coloris.setInstance("#" + el.id, { alpha: false, format: "hex" })

    // Wrap the input in Coloris' ".clr-field" so it shows a colour swatch (idempotent).
    Coloris.wrap(el)
}

export function makeColorSelector(element: HTMLElement | JQuery, options?: ColorSelectorOptions): void {
    _makeColorSelector($(element), $.extend({}, DefaultColorSelectorOptions, options))
}

export function makeResetButton(element: HTMLElement | JQuery, targetElement?: HTMLElement | JQuery): void {
    const $element = $(element)
    if ($element.length === 0)
        throw new Error("No html element found")

    $element.toggleClass("gob-config-icon-button", true)
    $element.empty()
    $element.append($("<i class='fas fa-undo-alt'></i>"))

    if ($element.attr(Locale.HtmlAttribute.TooltipId) === null)
        $element.attr(Locale.HtmlAttribute.TooltipId, "config.main.button.reset.tooltip")

    const getConfigKey = targetElement ?
        () => Databinding.getConfigKey(targetElement) :
        () => Databinding.getConfigKey(element)

    $element.on("click", () => {
        if ($element.prop("disabled"))
            return
        const key = getConfigKey()
        if (key)
            gobConfig.reset(key)
    })

    // Reset/revert visibility & lock state:
    //  - hidden            when the value already equals the default (nothing to revert)
    //  - shown but locked  when the value equals the saved profile (protect a deliberate setting);
    //                      holding Ctrl unlocks it
    //  - shown & enabled   when the value differs from the saved profile (an unsaved edit)
    const savedConfig = (window.opener as any)?.gobConfig as (typeof gobConfig) | undefined
    const updateState = () => {
        const key = getConfigKey()
        if (!key)
            return
        const current = gobConfig.get(key)
        const atDefault = _.isEqual(current, gobConfig.getDefault(key))
        const atSaved = savedConfig ? _.isEqual(current, savedConfig.get(key)) : true
        const locked = atDefault || (atSaved && !_ctrlHeld)
        $element.prop("hidden", atDefault)
        $element.prop("disabled", locked).toggleClass("is-disabled", locked)
    }

    const key = getConfigKey()
    if (key)
        gobConfig.addPropertyEventListener(key, updateState)
    gobConfig.addProfileEventListener(updateState)
    onCtrlChange(updateState)
    updateState()

    // if (targetElement && ($(targetElement).hasClass("is-disabled") || $(targetElement).prop("disabled")) )
    //     $element.addClass("is-disabled").prop("disabled", true)

    /*
    if ($(targetElement).length > 0) {
        var mutationObserver = new MutationObserver((mutations) => {
            for (const mutation of mutations) {
                if (mutation.attributeName === "disabled") {
                    if ((mutation.target as any).disabled) {
                        $element.toggleClass("is-disabled", true).prop("disabled", true)
                    } else {
                        $element.toggleClass("is-disabled", false).prop("disabled", false)
                    }
                } else if (!mutation.target.isConnected) {
                    mutationObserver.disconnect()
                }
            }
        })
        targetElement = $(targetElement)[0]
        mutationObserver.observe(targetElement, {attributes: true})
    }
    */
}

interface CopyProfileOptionTypes {
    callback: null | ((profileId: string) => boolean)
    configKeys: string[] | (() => string[])
}

const DefaultCopyProfileOptions: CopyProfileOptionTypes = {
    callback: null,
    configKeys: []
}

export type CopyProfileOptions = Partial<CopyProfileOptionTypes>

export function makeCopyProfileButton(element: HTMLElement | JQuery, userOptions?: CopyProfileOptions) {
    const $element = $(element)
    if ($element.length === 0)
        throw new Error("No html element found")

    $element.toggleClass("gob-config-icon-button", true)
    $element.toggleClass("gob-config-copypage-button", true)
    $element.attr(Locale.HtmlAttribute.TooltipId, "config.main.profile.copypage")

    $element.empty()
    $element.append($("<i class='fas fa-clone'></i>"))

    $element.on("click", event => Dialog.showProfileIdSelectionDialog(copyProfile, { exclude: [gobConfig.activeProfileId ?? ""] }))

    const options = !userOptions ? DefaultCopyProfileOptions : $.extend({}, DefaultCopyProfileOptions, userOptions)

    /*{
        const keys = Utility.isFunction(options.configKeys) ? options.configKeys() : options.configKeys
        const keySet = new Set<string>(keys)
        console.log(`Selected keys for copy profile {${$element.attr("id")}} are [${Array.from(keySet).join(", ")}]`)
    }*/

    function copyProfile(profileId: string) {
        if (options.callback) {
            const result = options.callback(profileId)
            if (!result)
                return
        }

        const srcProfile = gobConfig.getProfile(profileId)
        const dstProfile = gobConfig.activeProfile

        if (srcProfile === null)
            console.error(`Profile copy error. Profile '${profileId}' not found`)

        if (dstProfile === null)
            console.error(`Profile copy error. No active profile found`)

        if (srcProfile === null || dstProfile === null)
            return

        const keys = Utility.isFunction(options.configKeys) ? options.configKeys() : options.configKeys
        const keySet = new Set<string>(keys)

        for (const key of keySet) {
            if (key === null || key === "")
                continue

            try {
                dstProfile.copyFrom(srcProfile, key)
            } catch (e1) {
                console.error(`Profile copy Error in key '${key}'. Reason: ${e1}`)
            }
        }
    }

    const checkCopyProfileState = () => $element.prop("disabled", (gobConfig.profileIds.length <= 1))
    gobConfig.addProfileEventListener(event => checkCopyProfileState())
    checkCopyProfileState()
}