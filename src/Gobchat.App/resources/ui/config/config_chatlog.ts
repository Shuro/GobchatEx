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

import * as Databinding from "/module/Databinding"
import * as Components from "/module/Components"
import * as Locale from "/module/Locale"
import * as ChatlogFormatPreview from "/module/ChatlogFormatPreview"

const binding = new Databinding.BindingContext(gobConfig)

const chkEnableChahlog = $("#cp-chatlog_active")
const chkCharacterFolders = $("#cp-chatlog_characterfolders")
const txtChatlogPath = $("#cp-chatlog_path")
const btnChatlogPathReset = $("#cp-chatlog_path_reset")
const btnChatlogPathSelect = $("#cp-chatlog_path_select")
const txtChatlogFormat = $("#cp-chatlog_format")
const selChatlogFormat = $("#cp-chatlog_format_selector")
const chatlogFormatPreview = $("#cp-chatlog_format_preview")

const chatlogTable = $("#cp-chatlog_table > tbody")
const chatlogTableLinkshells = $("#cp-chatlog_table-2 > tbody")
const templateChatlogTableEntry = $('#cp-chatlog_template_table_entry')
let chatlogEntryCount = 0

Databinding.bindCheckbox(binding, chkEnableChahlog)
Databinding.bindCheckbox(binding, chkCharacterFolders)
Components.makeResetButton(btnChatlogPathReset, txtChatlogPath)

txtChatlogPath.on("change", async function () {
    try { // show absolute path to user, but if possible, only store a relative path and/or symbolic link
        const currentPath = txtChatlogPath.val()
        const relCurrentPath = await GobchatAPI.getRelativeChatLogPath(currentPath)
        gobConfig.set(Databinding.getConfigKey(txtChatlogPath), relCurrentPath)
        const absCurrentPath = await GobchatAPI.getAbsoluteChatLogPath(relCurrentPath)
        txtChatlogPath.val(absCurrentPath)
    } catch (e) {
        console.error(e)
    }
})

binding.bindCallback(Databinding.getConfigKey(txtChatlogPath), async function (path) {
    try { // show absolute path to user
        const absCurrentPath = await GobchatAPI.getAbsoluteChatLogPath(path)
        txtChatlogPath.val(absCurrentPath)
    } catch (e) {
        console.error(e)
    }
})

btnChatlogPathSelect.on("click", async function () {
    try { // open directory selector in previously selected directory
        const relCurrentPath = gobConfig.get(Databinding.getConfigKey(txtChatlogPath))
        const absCurrentPath = await GobchatAPI.getAbsoluteChatLogPath(relCurrentPath)

        const absNewPath = await GobchatAPI.openDirectoryDialog(absCurrentPath)
        const relNewPath = await GobchatAPI.getRelativeChatLogPath(absNewPath) // only store a relative path and/or symbolic link
        gobConfig.set(Databinding.getConfigKey(txtChatlogPath), relNewPath)
    } catch (e) {
        console.error(e)
    }
})

Databinding.bindElement(binding, txtChatlogFormat)

// Illustrative sample row used to render the format preview. Mirrors CustomChatLogger's token
// vocabulary (Module/Misc/Chatlogger/Internal/CustomChatLogger.cs) so the *shape* of the preview
// always matches what gets written. Pad width and timezone are representative, not the live values.
const PREVIEW_CHANNEL_PAD = 8
const PREVIEW_SENDER = "Firstname Lastname"
const PREVIEW_TOKENS: { [name: string]: string } = {
    "TIME": "21:34:07",
    "TIME-SHORT": "21:34",
    "TIME-FULL": "21:34:07+02:00",
    "DATE": "2026-06-20",
    "CHANNEL": "Say",
    "CHANNEL-PADL": "Say".padStart(PREVIEW_CHANNEL_PAD),
    "CHANNEL-PADR": "Say".padEnd(PREVIEW_CHANNEL_PAD),
    "SENDER": PREVIEW_SENDER,
    "SENDER-CHA": `${PREVIEW_SENDER}:`, // Say falls to the C# `default` case -> "<name>:"
    "MESSAGE": "Well met, traveler!",
    "BREAK": "\n",
}

function updateChatlogPreview(format?: string) {
    const source = format ?? (txtChatlogFormat.val() as string)
    chatlogFormatPreview.text(ChatlogFormatPreview.renderFormatPreview(source, PREVIEW_TOKENS))
}

// The format text box is only editable for "Custom format" (the empty-value option). For any preset it
// shows that preset's string but stays greyed out, so the format can only be hand-edited via Custom.
function updateFormatFieldState() {
    txtChatlogFormat.prop("disabled", (selChatlogFormat.val() as string) !== "")
}

binding.bindCallback(txtChatlogFormat, value => {
    selChatlogFormat.val(value)
    const selectedFormat = selChatlogFormat.val()
    if (selectedFormat === null)
        selChatlogFormat.val("")
    updateFormatFieldState()
    updateChatlogPreview(value)
})

selChatlogFormat.on("change", function () {
    const selectedFormat = $(this).val()
    if (selectedFormat.length > 0)
        txtChatlogFormat.val(selectedFormat).change()
    updateFormatFieldState()
    updateChatlogPreview()
})

// Live preview while hand-editing a Custom format (the binding only commits on `change`/blur).
txtChatlogFormat.on("input", () => updateChatlogPreview())


Object.entries(Gobchat.Channels).forEach((entry) => {
    const channelData = entry[1]
    if (!channelData.relevant)
        return
    addEntryToTable(channelData)
})

function addEntryToTable(channelData: any) {
    const channelEnums = [].concat(channelData.chatChannel || [])
    if (channelEnums.length === 0)
        return // channel is not associated with any ingame channel

    const targetTable = /linkshell/i.test(channelData.configId ?? "") ? chatlogTableLinkshells : chatlogTable
    const id = `cp-chatlog_table_entry-${chatlogEntryCount++}`

    const entry = $(templateChatlogTableEntry.html())
    entry.appendTo(targetTable)

    entry.find(".js-label")
        .attr(Locale.HtmlAttribute.TextId, `${channelData.translationId}`)
        .attr(Locale.HtmlAttribute.TooltipId, `${channelData.tooltipId}`)
        .attr("for", id)

    const ckbApply = entry.find(".js-checkbox")
        .attr("id", id)

    Databinding.setConfigKey(ckbApply, "behaviour.channel.log")
    Databinding.bindCheckboxArrayInverse(binding, ckbApply, channelEnums)
}

binding.loadBindings()
updateChatlogPreview() // initial state, once the bound format value is in the field

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// every chat-log config key on this page (all inputs carrying a config key).
