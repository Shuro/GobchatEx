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

import * as Utility from "/module/CommonUtility"

// Every option on this page is an application-global setting (gobAppConfig): profile-independent and
// applied instantly through the host — there is no Save/Cancel buffer here. (The per-profile "Chat
// Overlay Window" box and the search colours moved to the Formatting page.)

function bindAppValue($el: JQuery, key: string): void {
    $el.val(gobAppConfig.get(key))
    $el.on("change", () => gobAppConfig.set(key, $el.val()))
}

function bindAppCheckbox($el: JQuery, key: string): void {
    $el.prop("checked", !!gobAppConfig.get(key))
    $el.on("change", () => gobAppConfig.set(key, $el.prop("checked")))
}

bindAppValue($("#cp-app_language"), "behaviour.language")

try {
    const dpdThemes = $("#cp-app_theme")
    dpdThemes.empty()
    for (let style of gobStyles.styles) {
        $('<option>', {
            text: style.label,
            value: style.label
        }).appendTo(dpdThemes)
    }
    bindAppValue(dpdThemes, "style.theme")
} catch (e1) {
    console.error(e1)
}

// setup checkboxes
bindAppCheckbox($("#cp-app_hide"), "behaviour.hideOnMinimize")
bindAppCheckbox($("#cp-app_checkupdates"), "behaviour.appUpdate.checkOnline")
bindAppCheckbox($("#cp-app_checkbetaupdates"), "behaviour.appUpdate.acceptBeta")

const playerLocationAvailable = await GobchatAPI.isFeaturePlayerLocationAvailable()
$("#cp-app_characterlocations_feature").prop("hidden", playerLocationAvailable)   // "not available" notice
$("#cp-app_characterlocations_available").prop("hidden", !playerLocationAvailable) // green "Available" badge

bindAppCheckbox($("#cp-app_actor_updateActive"), "behaviour.actor.active")

const dpdProcessSelector = $("#cp-app_process_selector")
$("#cp-app_process_selector_refresh").on("click", function () {
    const $icon = $("#cp-app_process_selector_refresh").find("svg");
    (async () => {
        try {
            //$icon.addClass("fa-spin")

            const defaultElement = dpdProcessSelector.find("[value='-1']")
            const previousSelected = dpdProcessSelector.val()
            dpdProcessSelector.empty().append(defaultElement)

            const availableProcesses = await GobchatAPI.getAttachableFFXIVProcesses()
            for (const processId of availableProcesses)
                dpdProcessSelector.append(new Option(`FFXIV: ${processId}`, processId.toString()))

            if (dpdProcessSelector.find(`[value='${previousSelected}'`).length > 0) {
                dpdProcessSelector.val(previousSelected)
            } else {
                dpdProcessSelector.val("-1")
                await GobchatAPI.attachToFFXIVProcess(-1)
            }

            //$icon.removeClass("fa-spin")

            await process_UpdateLabel()
        } catch (e) {
            console.error(e)
        }
    })();
})

let process_IntervalTimer = 0
async function process_UpdateLabel() {
    try {
        const txtSearch = await gobLocale.get("config.app.process.info.search")
        const txtNotConnected = await gobLocale.get("config.app.process.info.notconnected")
        const txtConnectedTo = await gobLocale.get("config.app.process.info.connected")

        const txtLabel = $("#cp-app_process_info")
        const icon = $("#cp-app_process_selector_link").find("svg")

        async function updateLabel() {
            try {
                const connectionInfo = await GobchatAPI.getAttachedFFXIVProcess()
                const connectionState = connectionInfo.State //0 - none, 1 - connected, 2 - not found, 3 - searching, 4 - no access (FFXIV more elevated than us), 5 - outdated signatures
                const processId = connectionInfo.Id

                switch (connectionState) {
                    case 0:
                        break
                    case 1:
                        txtLabel.text(Utility.formatString(txtConnectedTo, processId));
                        icon.removeClass("fa-spin")
                        clearInterval(process_IntervalTimer)
                        process_IntervalTimer = 0
                        break;
                    case 2:
                        txtLabel.text(txtNotConnected);
                        break;
                    case 3:
                        txtLabel.text(txtSearch);
                        break
                    case 4:
                        // FFXIV is running but we can't read it (it's more elevated); the app
                        // separately offers a restart-as-administrator. Show it as not connected here.
                        txtLabel.text(txtNotConnected);
                        break
                    case 5:
                        // Attached but the memory signatures are outdated, so chat can't be read.
                        // No usable connection -> show as not connected (the greeter explains why).
                        txtLabel.text(txtNotConnected);
                        break
                }
            } catch (e) {
                console.error(e)
            }
        }

        clearInterval(process_IntervalTimer)
        process_IntervalTimer = setInterval(updateLabel, 1000)
    } catch (e) {
        console.error(e)
        throw e
    }
}

$("#cp-app_process_selector_link").on("click", async function () {
    try {
        $("#cp-app_process_selector_link").find("svg").addClass("fa-spin")

        const processId = dpdProcessSelector.val()
        if (processId != null && processId != undefined)
            GobchatAPI.attachToFFXIVProcess(parseInt(processId))

        await process_UpdateLabel()
    } catch (e) {
        console.error(e)
    }
})

// App-setting revert button: shows the undo icon, hides when the current value already equals the
// default (nothing to revert), and resets on click. Components.makeResetButton is gobConfig-bound and
// reads gobConfig.getDefault, so it can't be reused for the (separate) app-settings store; this is the
// equivalent for gobAppConfig. Returns an updater to call whenever the bound value changes.
function makeAppResetButton($btn: JQuery, defaultValue: any, getCurrent: () => any, reset: () => void): () => void {
    $btn.toggleClass("gob-config-icon-button", true).empty().append($("<i class='fas fa-undo-alt'></i>"))
    if ($btn.attr("data-gob-locale-tooltip") == null)
        $btn.attr("data-gob-locale-tooltip", "config.main.button.reset.tooltip")
    const update = () => $btn.prop("hidden", _.isEqual(getCurrent(), defaultValue))
    $btn.on("click", () => { reset(); update() })
    update()
    return update
}

// Show/Hide hotkey: decode the key event to text and write through instantly; reset clears it.
const $hotkey = $("#cp-app_hotkey_show")
$hotkey.val(gobAppConfig.get("behaviour.hotkeys.showhide"))
const updateHotkeyReset = makeAppResetButton(
    $("#cp-app_hotkey_show_reset"), "",
    () => $hotkey.val(),
    () => { $hotkey.val(""); gobAppConfig.set("behaviour.hotkeys.showhide", "") })
$hotkey.on("keydown", (event) => {
    const text = Utility.decodeKeyEventToText(event, true)
    $hotkey.val(text)
    gobAppConfig.set("behaviour.hotkeys.showhide", text)
    updateHotkeyReset()
})

// Update intervals (ms): clamp to the input's range, write through instantly, reset to the default.
function bindAppInterval(inputId: string, key: string, min: number, max: number, defaultValue: number): void {
    const $input = $(`#${inputId}`)
    $input.val(gobAppConfig.get(key))
    const updateReset = makeAppResetButton(
        $(`#${inputId}_reset`), defaultValue,
        () => Utility.toInt($input.val()),
        () => { $input.val(defaultValue); gobAppConfig.set(key, defaultValue) })
    $input.on("change", () => {
        let value = Utility.toInt($input.val())
        if (value === null)
            value = Utility.toInt(gobAppConfig.get(key)) ?? defaultValue
        if (value < min) value = min
        if (value > max) value = max
        $input.val(value)
        gobAppConfig.set(key, value)
        updateReset()
    })
}
bindAppInterval("cp-app_chat_updateInterval", "behaviour.chat.updateInterval", 50, 5000, 1000)
bindAppInterval("cp-app_actor_updateInterval", "behaviour.actor.updateInterval", 200, 20000, 1000)

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md).

//# sourceURL=config_app.js
