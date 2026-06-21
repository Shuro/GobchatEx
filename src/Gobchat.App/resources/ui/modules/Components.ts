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

interface ResetButtonOptionTypes {
    // Overrides what "default" the reset reverts to / compares against (e.g. a scheme-specific colour
    // instead of the bare config default). Receives the target's config key.
    getDefaultValue: null | ((key: string) => any)
    // Overrides the reset action (defaults to gobConfig.reset, which restores the config default).
    onReset: null | ((key: string) => void)
    // Extra config keys whose changes should re-evaluate this button's visibility/lock state (e.g. the
    // active colour scheme, when getDefaultValue depends on it).
    extraWatchKeys: string[]
}

const DefaultResetButtonOptions: ResetButtonOptionTypes = {
    getDefaultValue: null,
    onReset: null,
    extraWatchKeys: []
}

export type ResetButtonOptions = Partial<ResetButtonOptionTypes>

export function makeResetButton(element: HTMLElement | JQuery, targetElement?: HTMLElement | JQuery, userOptions?: ResetButtonOptions): void {
    const $element = $(element)
    if ($element.length === 0)
        throw new Error("No html element found")

    const options = $.extend({}, DefaultResetButtonOptions, userOptions) as ResetButtonOptionTypes

    $element.toggleClass("gob-config-icon-button", true)
    $element.empty()
    $element.append($("<i class='fas fa-undo-alt'></i>"))

    if ($element.attr(Locale.HtmlAttribute.TooltipId) === null)
        $element.attr(Locale.HtmlAttribute.TooltipId, "config.main.button.reset.tooltip")

    const getConfigKey = targetElement ?
        () => Databinding.getConfigKey(targetElement) :
        () => Databinding.getConfigKey(element)

    const defaultValueFor = (key: string) => options.getDefaultValue ? options.getDefaultValue(key) : gobConfig.getDefault(key)

    $element.on("click", () => {
        if ($element.prop("disabled"))
            return
        const key = getConfigKey()
        if (!key)
            return
        if (options.onReset)
            options.onReset(key)
        else
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
        // Reading the current/default/saved value can throw for keys absent from a profile — e.g. a
        // reset button belonging to a custom trigger group, whose dynamically generated key has no
        // entry in the default profile (getDefault throws), or which the now-active profile no longer
        // contains. Such a value has no default to revert to, so treat any such failure as "nothing to
        // revert" (hide + lock). Crucially this also stops the throw from bubbling out of the config
        // event dispatch, which would abort every later listener and break profile switching.
        let atDefault: boolean
        let atSaved: boolean
        try {
            const current = gobConfig.get(key)
            atDefault = _.isEqual(current, defaultValueFor(key))
            atSaved = savedConfig ? _.isEqual(current, savedConfig.get(key)) : true
        } catch (e) {
            // Expected for keys absent from the active/default profile (see above); logged at debug so
            // a genuinely unexpected failure is still traceable without spamming the normal case.
            console.debug(`makeResetButton: no revertable value for key '${key}', hiding`, e)
            $element.prop("hidden", true)
            $element.prop("disabled", true).toggleClass("is-disabled", true)
            return
        }
        const locked = atDefault || (atSaved && !_ctrlHeld)
        $element.prop("hidden", atDefault)
        $element.prop("disabled", locked).toggleClass("is-disabled", locked)
    }

    const key = getConfigKey()
    if (key)
        gobConfig.addPropertyEventListener(key, updateState)
    for (const watchKey of options.extraWatchKeys)
        gobConfig.addPropertyEventListener(watchKey, updateState)
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

interface TagInputOptionTypes {
    // Config key holding the bound string[]; falls back to the element's data-gob-configkey.
    configKey: string | null
    // Applied to each entered word before storing (e.g. lowercase for global mentions).
    normalize: (value: string) => string
    // Locale key for the input placeholder (optional).
    placeholder: string | null
}

const DefaultTagInputOptions: TagInputOptionTypes = {
    configKey: null,
    normalize: (value: string) => value.trim(),
    placeholder: null,
}

export type TagInputOptions = Partial<TagInputOptionTypes>

/**
 * Turns a container into a tag/chip editor bound to a string[] config value: a text field that
 * commits on Enter or comma into removable "word ×" chips. Duplicates (case-insensitive) are
 * silently ignored; Backspace on an empty field drops the last chip. Pass the BindingContext that
 * owns the surrounding page/row so the re-render listener is added on loadBindings and removed on
 * clearBindings (no leak when a dynamic row is rebuilt).
 */
export function makeTagInput(element: HTMLElement | JQuery, bindingContext: Databinding.BindingContext, userOptions?: TagInputOptions): void {
    const $element = $(element)
    if ($element.length === 0)
        throw new Error("No html element found")

    const options = $.extend({}, DefaultTagInputOptions, userOptions) as TagInputOptionTypes
    const configKey = options.configKey ?? Databinding.getConfigKey($element)
    if (!configKey)
        throw new Error("makeTagInput requires a config key (option or data-gob-configkey)")

    $element.addClass("gx-tags").empty()
    const $chips = $("<div class='gx-tags_chips'></div>").appendTo($element)
    const $input = $("<input type='text' class='gx-tags_input' />").appendTo($element)

    if (options.placeholder && typeof gobLocale !== "undefined") {
        gobLocale.get(options.placeholder)
            .then((text: string) => $input.attr("placeholder", text))
            .catch((e: unknown) => console.error(e))
    }

    const currentWords = (): string[] => {
        const value = gobConfig.get(configKey) as string[] | null
        return Array.isArray(value) ? value : []
    }

    const addWords = (raw: string): void => {
        // Duplicates (case-insensitive) and blanks are silently dropped by mergeTags.
        const next = Utility.mergeTags(currentWords(), raw, options.normalize)
        if (next)
            gobConfig.set(configKey, next)
    }

    const removeWord = (word: string): void => {
        const words = currentWords()
        const next = words.filter(w => w !== word)
        if (next.length !== words.length)
            gobConfig.set(configKey, next)
    }

    const render = (): void => {
        $chips.empty()
        for (const word of currentWords()) {
            const $chip = $("<span class='gx-tag'></span>")
            $("<span class='gx-tag_text'></span>").text(word).appendTo($chip)
            $("<button type='button' class='gx-tag_remove' tabindex='-1'>&times;</button>")
                .on("click", () => removeWord(word))
                .appendTo($chip)
            $chips.append($chip)
        }
    }

    $input.on("keydown", (e) => {
        if (e.key === "Enter" || e.key === ",") {
            e.preventDefault()
            addWords($input.val() as string)
            $input.val("")
        } else if (e.key === "Backspace" && (($input.val() as string) ?? "").length === 0) {
            const words = currentWords()
            if (words.length > 0)
                removeWord(words[words.length - 1])
        }
    })

    $input.on("blur", () => {
        const value = ($input.val() as string) ?? ""
        if (value.trim().length > 0) {
            addWords(value)
            $input.val("")
        }
    })

    // Render now and whenever the bound array changes.
    bindingContext.bindCallback(configKey, () => render())
}
