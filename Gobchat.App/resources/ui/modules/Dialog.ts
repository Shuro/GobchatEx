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

import * as Utility from "./CommonUtility.js"

interface DialogOptionTypes {
    title: string
    dialogType: "YesNo" | "Ok" | "OkCancel" | "Yes"
    dialogIcon: "" | "Warning"
    dialogText: string
    dialogContent: HTMLElement | JQuery | null
    buttons: { [buttonText: string]: number }
    modal: boolean
    autoOpen: boolean
    localized: boolean
    height: "Auto" | number
    width: "Auto" | number
    resizable: boolean
}

export type DialogOptions = Partial<DialogOptionTypes>
export type ErrorDialogOptions = DialogOptions
export type ConfirmationDialogOptions = DialogOptions
export type MessageDialogOptions = DialogOptions

export function showErrorDialog(options: ErrorDialogOptions): Promise<number> {
    const nonOptionalOptions: DialogOptions = {
        title: "config.main.dialog.title.error",
        dialogType: "Ok",
        dialogIcon: "Warning"
    }
    return _showMessageDialog(options, nonOptionalOptions)
}

export function showConfirmationDialog(options: ConfirmationDialogOptions): Promise<number> {
    const nonOptionalOptions: DialogOptions = {
        title: "config.main.dialog.title.confirm",
        dialogType: "YesNo",
        dialogIcon: "Warning"
    }
    return _showMessageDialog(options, nonOptionalOptions)
}

export function showMessageDialog(options: MessageDialogOptions): Promise<number> {
    return _showMessageDialog(options, {})
}

interface ProfileSelectionDialogOptionTypes {
    exclude: string[]
}

export type ProfileSelectionDialogOptions = Partial<ProfileSelectionDialogOptionTypes>

export async function showProfileIdSelectionDialog(callback: (selection: string) => void, options: ProfileSelectionDialogOptions) {
    let defOptions: ProfileSelectionDialogOptionTypes = { exclude: [] }
    defOptions = $.extend(defOptions, options)

    let profileIds = gobConfig.profileIds
    if (defOptions.exclude)
        profileIds = _.without(profileIds, defOptions.exclude)

    const selector = $("<select/>")
    profileIds.forEach((profileId) => {
        var profile = gobConfig.getProfile(profileId)
        if (profile !== null)
            selector.append(new Option(profile.profileName, profileId))
    })

    const result = await showMessageDialog(
        {
            title: "config.profiles.dialog.copyprofilepage.title",
            dialogContent: selector,
            dialogType: "OkCancel"
        }
    )

    if (result === 1)
        callback(selector.val())
}



const DefaultDialogOptions: DialogOptionTypes = {
    resizable: false,
    width: "Auto",
    modal: true,
    autoOpen: false,
    buttons: {},
    dialogType: "Ok",
    dialogIcon: "",
    dialogText: "",
    dialogContent: null,
    localized: true,
    title: "",
    height: "Auto"
}

// Native <dialog> markup, styled by the settings stylesheet (.gx-dialog).
const popupTemplate =
`<dialog class="gx-dialog">
    <div class="gx-dialog_titlebar">
        <span class="gx-dialog_title"></span>
        <button type="button" class="gx-dialog_close" aria-label="Close"><i class="fas fa-times"></i></button>
    </div>
    <div class="gx-dialog_body">
        <div class="gx-dialog_icon">
            <span class="gx-dialog_icon-warning" hidden>
                <i class="fas fa-exclamation-triangle fa-3x gob-icon-warning"></i>
            </span>
        </div>
        <div class="gx-dialog_content"></div>
    </div>
    <div class="gx-dialog_buttons"></div>
</dialog>`

function _showMessageDialog(userOptions: DialogOptions, enforcedOptions: DialogOptions): Promise<number> {
    const mergedOptions: DialogOptionTypes = Utility.extendObject({ ...DefaultDialogOptions }, [userOptions, enforcedOptions], false, true, "both")

    return new Promise<number>(async function (resolve, reject) {
        try {
            await processOptions(mergedOptions)

            const $dialog = $(popupTemplate)
            const dialog = $dialog[0] as HTMLDialogElement

            $dialog.find(".gx-dialog_title").text(mergedOptions.title || "")

            if (mergedOptions.dialogText !== null && mergedOptions.dialogText.length > 0)
                $dialog.find(".gx-dialog_content").append(
                    $("<span></span>").html(mergedOptions.dialogText).addClass("gob-config-text")
                )

            if (mergedOptions.dialogContent)
                $dialog.find(".gx-dialog_content").append(
                    $(mergedOptions.dialogContent)
                )

            if (mergedOptions.dialogIcon === "Warning")
                $dialog.find(".gx-dialog_icon-warning").show()

            const close = (value: number) => {
                if (dialog.open) dialog.close()
                $dialog.remove()
                resolve(value)
            }

            const $buttons = $dialog.find(".gx-dialog_buttons")
            // First button is the primary action (matches the previous Yes/Ok placement).
            Object.keys(mergedOptions.buttons).forEach((text, index) => {
                const value = mergedOptions.buttons![text]
                $("<button type='button'></button>")
                    .addClass(index === 0 ? "gx-btn" : "gx-ghost")
                    .text(text)
                    .on("click", () => close(value))
                    .appendTo($buttons)
            })

            $dialog.find(".gx-dialog_close").on("click", () => close(0))

            // Previously closeOnEscape:false — keep ESC from dismissing the modal.
            $dialog.on("cancel", (e: any) => e.preventDefault())

            $("body").append($dialog)
            dialog.showModal()
        } catch (e1) {
            console.error(e1)
            reject(e1)
        }
    })
}

async function processOptions(option: DialogOptionTypes): Promise<void> {
    if (Object.keys(option.buttons).length == 0) {
        switch (option.dialogType) {
            case "YesNo":
                option.buttons = {
                    "config.main.dialog.btn.yes": 1,
                    "config.main.dialog.btn.no": 0
                }
                break;
            case "OkCancel":
                option.buttons = {
                    "config.main.dialog.btn.ok": 1,
                    "config.main.dialog.btn.cancel": 0
                }
                break;
            case "Yes":
                option.buttons = {
                    "config.main.dialog.btn.yes": 1
                }
                break;
            case "Ok":
                option.buttons = {
                    "config.main.dialog.btn.ok": 1
                }
                break;
        }
    }

    if (option.localized) {
        const lookupKeys = ([] as string[]).concat(Object.keys(option.buttons))

        if (option.title.length > 0)
            lookupKeys.push(option.title)

        if (option.dialogText.length > 0)
            lookupKeys.push(option.dialogText)

        const locales = await gobLocale.getAll(lookupKeys)

        if (option.title.length > 0)
            option.title = locales[option.title]

        if (option.dialogText.length > 0)
            option.dialogText = locales[option.dialogText]

        option.buttons = _.mapKeys(option.buttons, (v, k) => locales[k])

        if (option.dialogContent)
            await gobLocale.updateElement(option.dialogContent)
    }
}