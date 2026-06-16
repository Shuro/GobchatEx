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
import * as Dialog from "/module/Dialog"
import * as Utility from "/module/CommonUtility"
import * as Chat from "/module/Chat"
import * as Locale from "/module/Locale"

const binding = new Databinding.BindingContext(gobConfig)

const txtTrigegrWords = $("#cp-mentions_triggerwords")
const chkCheckUserMsgForMentions = $("#cp-mentions_mentions-on-user")
const chkPlaySound = $("#cp-mentions_audio-play")
const txtAudioFilePath = $("#cp-mentions_audio-path")
const btnOpenAudioFileDialog = $("#cp-mentions_audio-path_select")
const btnPlayAudio = $("#cp-mentions_audio-test")
const sliderAudioVolume = $("#cp-mentions_audio-volume")
const lblAudioVolumeValue = $("#cp-mentions_audio-volume_value")
const txtAudioReplayInterval = $("#cp-mentions_audio-replay-interval")
const chkIgnoreRangeFilter = $("#cp-mentions_ignore-range-filter")
const mentionsTable = $("#cp-mentions_mentions-table > tbody")
const mentionsTableLinkshells = $("#cp-mentions_mentions-table-2 > tbody")
const mentionsTableEntryTemplate = $("#cp-mentions_template_mentions-table_entry")
// Unique id counter shared across both columns.
let mentionsEntryCount = 0

const iconCanPlay = $("")
const iconMaybePlay = $("")
const iconProbablyPlay = $("")

Databinding.bindElement(binding, txtTrigegrWords, {
    elementToConfig: (element) => {
        let words = element.val().split(",")
        words = words.filter(w => w !== null && w !== undefined).map(w => w.toLowerCase().trim()).filter(w => w.length > 0)
        element.val(words.join(", "))
        return words
    },
    configToElement: (element, storedValue) => {
        element.val((storedValue || []).join(", "))
    }
})

Databinding.bindCheckbox(binding, chkCheckUserMsgForMentions)
Databinding.bindCheckbox(binding, chkPlaySound)


function isSoundFileValid(path) {
    return Utility.isNonEmptyString(path)
}

// A custom sound picked from an arbitrary location is stored as an absolute (rooted/UNC) path; a
// bundled one as "../sounds/X.mp3".
function isAbsoluteSoundPath(path) {
    return /^[a-zA-Z]:[\\/]/.test(path) || path.startsWith("\\\\")
}

// Adds an <option> for the given sound path if it isn't already listed. Used both to fill the
// dropdown from resources/sounds and to keep a custom/legacy path selectable (appended at the end).
function ensureSoundOption(path) {
    if (!Utility.isNonEmptyString(path))
        return
    const exists = txtAudioFilePath.children("option").get().some(o => (o as HTMLOptionElement).value === path)
    if (!exists) {
        // Label with just the file name (strip the bundled prefix or any directory part).
        const label = (path as string).replace(/^\.\.\/sounds\//i, "").replace(/^.*[\\/]/, "")
        txtAudioFilePath.append(new Option(label, path as string))
    }
}

function showPlayabilityIcon(format) {
    const audio = new Audio();
    const canPlay = audio.canPlayType(format)
    const result = canPlay === "probably" ? 2 : canPlay === "maybe" ? 1 : 0

    iconCanPlay.hide()
    iconMaybePlay.hide()
    iconProbablyPlay.hide()

    if (result === 0) iconCanPlay.show()
    if (result === 1) iconMaybePlay.show()
    if (result === 2) iconProbablyPlay.show()
}

Databinding.bindElement(binding, txtAudioFilePath, {
    elementToConfig: (element) => {
        const newSoundFile = element.val()
        const isValid = isSoundFileValid(newSoundFile)
        btnPlayAudio.prop("disabled", !isValid)
        return newSoundFile
    },
    configToElement: (element, storedValue) => {
        ensureSoundOption(storedValue) // a custom/legacy path must be selectable in the dropdown
        const isValid = isSoundFileValid(storedValue)
        btnPlayAudio.prop("disabled", !isValid)
        element.val(storedValue)
    }
})

btnOpenAudioFileDialog.on("click", async function () {
    try {
        // Use the native dialog so we get the real absolute path (a browser <input type=file> only
        // exposes the bare file name, which breaks for files outside resources/sounds).
        const file = await GobchatAPI.openFileDialog("Audio files (*.mp3;*.ogg;*.wav)|*.mp3;*.ogg;*.wav|All files (*.*)|*.*")
        if (!Utility.isNonEmptyString(file))
            return
        ensureSoundOption(file) // a freshly picked sound joins the list at the end
        txtAudioFilePath.val(file).change()
    } catch (e) {
        console.error(e)
    }
})

btnPlayAudio.on("click", async function () {
    const soundPath = gobConfig.get(Databinding.getConfigKey(txtAudioFilePath))
    try {
        // Absolute (custom) paths can't be served by the virtual host; read them through the bridge
        // as a data: URL. Bundled relative paths play directly (../sounds/X from /config/).
        const src = isAbsoluteSoundPath(soundPath)
            ? await GobchatAPI.getSoundDataUrl(soundPath)
            : "../" + soundPath
        if (!Utility.isNonEmptyString(src))
            throw new Error("sound source unavailable")
        const audio = new Audio(src as string);
        audio.volume = gobConfig.get(Databinding.getConfigKey(sliderAudioVolume))
        await audio.play()
    } catch (e) {
        console.error(e)
        Dialog.showErrorDialog({ dialogText: "config.mentions.audio.test.error" });
    }
})

function showVolumeValue(percent) {
    lblAudioVolumeValue.text(`${Math.round(percent) || 0}%`)
}

sliderAudioVolume.on("input", () => showVolumeValue(parseFloat(sliderAudioVolume.val()) || 0))

Databinding.bindElement(binding, sliderAudioVolume, {
    elementToConfig: (element) => {
        return (parseFloat(element.val()) || 0) / 100
    },
    configToElement: (element, storedValue) => {
        element.val(storedValue * 100)
        showVolumeValue(storedValue * 100)
    }
})

Databinding.bindElement(binding, txtAudioReplayInterval, {
    elementToConfig: (element) => {
        return (parseFloat(element.val()) || 0) * 1000
    },
    configToElement: (element, storedValue) => {
        element.val(storedValue / 1000)
    }
})

Databinding.bindCheckbox(binding, chkIgnoreRangeFilter)

Object.values(Gobchat.Channels).forEach(channelData => {
    if (!channelData.relevant)
        return
    addEntryToTable(channelData)
})

function addEntryToTable(channelData: Chat.Channel) {
    const channelEnums = ([] as Chat.ChatChannelEnum[]).concat(channelData.chatChannel || [])
    if (channelEnums.length === 0)
        return // channel is not associated with any ingame channel

    // Linkshell + cross-world linkshell channels go in the second column; everything else (the
    // standard channels, ending at Random) in the first.
    const targetTable = /linkshell/i.test(channelData.configId ?? "") ? mentionsTableLinkshells : mentionsTable
    const id = `cp-mentions_mentions-table_entry-${mentionsEntryCount++}`

    const entry = $(mentionsTableEntryTemplate.html())
        .appendTo(targetTable)

    entry.find(".js-label")
        .attr(Locale.HtmlAttribute.TextId, `${channelData.translationId}`)
        .attr(Locale.HtmlAttribute.TooltipId, `${channelData.tooltipId}`)
        .prop("for", id)

    const ckbApply = entry.find(".js-checkbox")
        .prop("id", id)

    Databinding.setConfigKey(ckbApply, "behaviour.channel.mention")
    Databinding.bindCheckboxArray(binding, ckbApply, channelEnums)
}

// Fill the sound dropdown from the files shipped under resources/sounds before the bindings load
// (so the stored value matches an existing option). A custom/legacy path is appended by the
// configToElement above when it isn't one of these.
try {
    const soundFiles = await GobchatAPI.getSoundFiles()
    soundFiles.forEach(path => ensureSoundOption(path))
} catch (e) {
    console.error(e)
}

binding.loadBindings()

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// ["behaviour.mentions", "behaviour.rangefilter.ignoreMention", "behaviour.channel.mention"].

//# sourceURL=config_mentions.js