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
import * as Components from "/module/Components"
import * as AudioVolume from "/module/AudioVolume"

const binding = new Databinding.BindingContext(gobConfig)

const tagsTriggerWords = $("#cp-mentions_triggerwords")
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

// Global mentions: free-text words committed into removable chips (lowercased + trimmed).
Components.makeTagInput(tagsTriggerWords, binding, {
    configKey: "behaviour.mentions.trigger",
    normalize: (word) => word.toLowerCase().trim(),
    placeholder: "config.mentions.trigger.add",
})

Databinding.bindCheckbox(binding, chkCheckUserMsgForMentions)
Databinding.bindCheckbox(binding, chkPlaySound)

// The play-sound toggle lives in the accordion's <summary>; stop its clicks from expanding/collapsing it.
$(".js-noaccordion-toggle").on("click", (e) => e.stopPropagation())


function isSoundFileValid(path: string) {
    return Utility.isNonEmptyString(path)
}

// A custom sound picked from an arbitrary location is stored as an absolute (rooted/UNC) path; a
// bundled one as "../sounds/X.mp3".
function isAbsoluteSoundPath(path: string) {
    return /^[a-zA-Z]:[\\/]/.test(path) || path.startsWith("\\\\")
}

// Adds an <option> for the given sound path if it isn't already listed. Used both to fill the
// dropdown from resources/sounds and to keep a custom/legacy path selectable (appended at the end).
function ensureSoundOption(path: string) {
    if (!Utility.isNonEmptyString(path))
        return
    const exists = txtAudioFilePath.children("option").get().some(o => (o as HTMLOptionElement).value === path)
    if (!exists) {
        // Label with just the file name (strip the bundled prefix or any directory part).
        const label = (path as string).replace(/^\.\.\/sounds\//i, "").replace(/^.*[\\/]/, "")
        txtAudioFilePath.append(new Option(label, path as string))
    }
}

function showPlayabilityIcon(format: string) {
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
        // TSS-4: clamp before the assignment; a stored fraction outside [0,1] would throw here.
        audio.volume = AudioVolume.clampVolumeFraction(gobConfig.get(Databinding.getConfigKey(sliderAudioVolume)))
        await audio.play()
    } catch (e) {
        console.error(e)
        Dialog.showErrorDialog({ dialogText: "config.mentions.audio.test.error" });
    }
})

function showVolumeValue(percent: number) {
    lblAudioVolumeValue.text(`${Math.round(percent) || 0}%`)
}

sliderAudioVolume.on("input", () => showVolumeValue(parseFloat(sliderAudioVolume.val()) || 0))

Databinding.bindElement(binding, sliderAudioVolume, {
    // TSS-4: volume is persisted as a [0,1] fraction and later assigned to HTMLMediaElement.volume, which
    // throws outside that range. Clamp on the way in (slider -> config) and on the way out (config ->
    // slider) so neither a wild slider value nor a crafted/imported profile can store an invalid volume.
    elementToConfig: (element) => {
        return AudioVolume.percentToVolumeFraction(parseFloat(element.val()))
    },
    configToElement: (element, storedValue) => {
        const fraction = AudioVolume.clampVolumeFraction(storedValue)
        element.val(fraction * 100)
        showVolumeValue(fraction * 100)
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

// --- Player mentions ---------------------------------------------------------------------------

const PlayerEnabledKey = "behaviour.mentions.player.enabled"
const PlayerDataKey = "behaviour.mentions.player.data"
const PlayerSortingKey = "behaviour.mentions.player.sorting"

const chkPlayerEnable = $("#cp-mentions_player-enable")
const playerList = $("#cp-mentions_player-list")
const playerEmpty = $(".js-player-empty")
const playerTemplate = $("#cp-mentions_template_player-character")

Databinding.bindCheckbox(binding, chkPlayerEnable)

// Grey out (and block interaction with) the character list while the master switch is off.
binding.bindCallback(PlayerEnabledKey, (enabled) => playerList.toggleClass("is-disabled", !enabled))

// Removing a remembered character: drop its data entry and its slot in the order. The sorting
// change triggers a rebuild via the listener below.
async function deleteCharacter(id: string): Promise<void> {
    // Confirm first as an extra barrier. Deletion is in-memory only (no autosave), so even after
    // confirming it can still be undone by closing settings without saving.
    const result = await Dialog.showConfirmationDialog({ dialogText: "config.mentions.player.character.delete.confirm" })
    if (result !== 1)
        return
    const data = (gobConfig.get(PlayerDataKey, {}) as Record<string, any>) || {}
    if (id in data)
        gobConfig.remove(`${PlayerDataKey}.${id}`)
    const sorting = (gobConfig.get(PlayerSortingKey, []) as string[]) || []
    gobConfig.set(PlayerSortingKey, sorting.filter(x => x !== id))
}

// The character ids in display order: the saved sorting, minus ids whose data is gone, plus any data
// ids missing from it. Read fresh from config (which is written synchronously) so reorder clicks
// compound correctly even before the async accordion rebuild has caught up.
function orderedCharacterIds(): string[] {
    const data = (gobConfig.get(PlayerDataKey, {}) as Record<string, any>) || {}
    const sorting = (gobConfig.get(PlayerSortingKey, []) as string[]) || []
    return sorting.filter(id => id in data).concat(Object.keys(data).filter(id => sorting.indexOf(id) < 0))
}

// Move a character one slot up (-1) or down (+1). Re-reads the current order from config and locates the
// character by id (rather than trusting the index captured when the row was built), so two quick clicks
// before the accordion rebuilds compound into two steps instead of both acting on the stale order. The
// PlayerSortingKey listener then rebuilds the accordion. No-op past either end.
function moveCharacter(id: string, delta: number): void {
    const order = orderedCharacterIds()
    const index = order.indexOf(id)
    if (index < 0)
        return
    const target = index + delta
    if (target < 0 || target >= order.length)
        return
    const next = order.slice()
    ;[next[index], next[target]] = [next[target], next[index]]
    gobConfig.set(PlayerSortingKey, next)
}

function buildPlayerRow(ctx: Databinding.BindingContext, id: string, entryData: any, titleTemplate: string, currentPlayerLower: string, order: string[], index: number): void {
    const configKey = `${PlayerDataKey}.${id}`
    const row = $(playerTemplate.html())
    playerList.append(row)

    const name = (entryData && entryData.name) || ""
    row.find(".js-character-title").text(Utility.formatString(titleTemplate, name))

    // The currently logged-in character can't be deleted (only de-activated); hide its trashcan and
    // expand its accordion by default since it's the one in play.
    const isLoggedIn = currentPlayerLower.length > 0 && name.toLowerCase() === currentPlayerLower
    const trash = row.find(".js-character-delete")
    if (isLoggedIn) {
        trash.remove()
        row.addClass("is-loggedin")
        row.prop("open", true)
    } else {
        trash.on("click", (e) => { e.preventDefault(); e.stopPropagation(); deleteCharacter(id).catch(err => console.error(err)) })
    }

    // Reorder controls (also inside <summary>): disabled at the ends; preventDefault stops the summary
    // from toggling the accordion when clicked.
    const btnUp = row.find(".js-character-up").prop("disabled", index === 0)
    const btnDown = row.find(".js-character-down").prop("disabled", index === order.length - 1)
    btnUp.on("click", (e) => { e.preventDefault(); e.stopPropagation(); moveCharacter(id, -1) })
    btnDown.on("click", (e) => { e.preventDefault(); e.stopPropagation(); moveCharacter(id, 1) })

    // Controls inside <summary> must not toggle the accordion when clicked.
    row.find(".js-character-active").on("click", (e) => e.stopPropagation())

    Databinding.bindCheckbox(ctx, row.find(".js-character-active"), { configKey: `${configKey}.active` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-full"), { configKey: `${configKey}.matchFullName` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-first"), { configKey: `${configKey}.matchFirstName` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-last"), { configKey: `${configKey}.matchLastName` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-first-partial"), { configKey: `${configKey}.matchFirstNamePartial` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-last-partial"), { configKey: `${configKey}.matchLastNamePartial` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-miqote"), { configKey: `${configKey}.matchMiqote` })
    Databinding.bindCheckbox(ctx, row.find(".js-match-fuzzy"), { configKey: `${configKey}.matchFuzzy` })

    // Miqo'te mode only derives a name from an apostrophe forename (e.g. "Y'shtola"); for any other
    // name it's inert, so disable and grey it out to make that clear. The name can't change for a built
    // row, so this is a one-time gate (matches what PlayerMentionResolver does C#-side).
    const firstName = (name.trim().split(/\s+/)[0]) || ""
    const miqoteApplies = firstName.indexOf("'") >= 0
    row.find(".js-match-miqote").prop("disabled", !miqoteApplies)
    row.find(".js-miqote-row").toggleClass("is-disabled", !miqoteApplies)

    // Fuzzy strength: a 3-way segmented toggle bound to {configKey}.fuzzyLevel (mirrors the channel
    // colour-scheme picker). The whole row is greyed out while fuzzy matching is off.
    const fuzzyLevelKey = `${configKey}.fuzzyLevel`
    const strengthRow = row.find(".js-fuzzy-strength-row")
    const strengthButtons = row.find(".js-fuzzy-level")
    ctx.bindCallback(fuzzyLevelKey, (level) => {
        strengthButtons.each((_i, el) => {
            const $el = $(el)
            $el.toggleClass("is-active", $el.attr("data-level") === level)
        })
    })
    strengthButtons.on("click", (event) => {
        const level = $(event.currentTarget).attr("data-level")
        if (level)
            gobConfig.set(fuzzyLevelKey, level)
    })
    ctx.bindCallback(`${configKey}.matchFuzzy`, (enabled) => strengthRow.toggleClass("is-disabled", !enabled))

    // Per-character custom mentions preserve their casing (only applies while logged in as them).
    Components.makeTagInput(row.find(".js-character-mentions"), ctx, {
        configKey: `${configKey}.mentions`,
        normalize: (word) => word.trim(),
        placeholder: "config.mentions.player.custom.add",
    })
}

let playerBinding: Databinding.BindingContext | null = null
// Monotonic token guarding overlapping rebuilds: both the sorting listener and the 1.5s poll can
// kick off buildPlayerCharacters, and it awaits (getCurrentPlayer / locale) between claiming its
// binding context and loading it. Each run captures its own token and context up front; if a newer
// run started while it was awaiting, the stale run bails instead of rendering into — and
// double-loading — a context that's already been cleared and replaced.
let playerBuildSeq = 0

async function buildPlayerCharacters(): Promise<void> {
    const mySeq = ++playerBuildSeq
    if (playerBinding)
        playerBinding.clearBindings()
    const ctx = new Databinding.BindingContext(gobConfig)
    playerBinding = ctx

    const data = (gobConfig.get(PlayerDataKey, {}) as Record<string, any>) || {}
    const ids = orderedCharacterIds()

    let currentPlayer: string | null = null
    try {
        currentPlayer = await GobchatAPI.getCurrentPlayer()
    } catch (e) {
        console.error(e)
    }
    const currentPlayerLower = (currentPlayer || "").toLowerCase()
    const titleTemplate = await gobLocale.get("config.mentions.player.character.title")

    // A newer rebuild superseded this one while awaiting — drop out so we don't fight over the DOM
    // or load bindings into a context that's no longer current.
    if (mySeq !== playerBuildSeq)
        return

    playerList.empty()
    playerEmpty.toggle(ids.length === 0)

    ids.forEach((id, index) =>
        buildPlayerRow(ctx, id, data[id], titleTemplate, currentPlayerLower, ids, index))

    await gobLocale.updateElement(playerList)
    if (mySeq !== playerBuildSeq)
        return
    ctx.loadBindings()
}

// (Re)build the accordion list on load and whenever a character is added/removed.
binding.bindCallback(PlayerSortingKey, () => { buildPlayerCharacters().catch(e => console.error(e)) })

// Live-refresh on login/logout. The settings window is a separate WebView2 and never receives the
// ConnectionStateEvent (it's dispatched only to the overlay), so poll the current player. When it
// changes, pull the freshest remembered characters from the authoritative overlay config (the
// settings window only holds a snapshot taken when it opened) and rebuild — so a character added by
// logging in shows up at once, and the one you just logged out from gets its trashcan back.
let lastKnownPlayer: string | null = null
let playerPollPrimed = false
async function refreshPlayerSectionOnPlayerChange(): Promise<void> {
    let current: string | null = null
    try {
        current = await GobchatAPI.getCurrentPlayer()
    } catch (e) {
        console.error(e)
        return
    }
    if (playerPollPrimed && current === lastKnownPlayer)
        return
    playerPollPrimed = true
    lastKnownPlayer = current

    let dataChanged = false
    const overlayConfig = (window.opener as any)?.gobConfig as (typeof gobConfig) | undefined
    if (overlayConfig) {
        try {
            const overlayData = overlayConfig.get(PlayerDataKey, {})
            const overlaySorting = overlayConfig.get(PlayerSortingKey, [])
            if (!_.isEqual(overlayData, gobConfig.get(PlayerDataKey, {}))) {
                gobConfig.set(PlayerDataKey, overlayData)
                dataChanged = true
            }
            if (!_.isEqual(overlaySorting, gobConfig.get(PlayerSortingKey, []))) {
                gobConfig.set(PlayerSortingKey, overlaySorting)
                dataChanged = true
            }
        } catch (e) {
            console.error(e)
        }
    }

    // A data change already triggers a rebuild via the sorting listener above; otherwise (e.g. a plain
    // logout) rebuild here so the trashcan/auto-open gating updates for the new login state.
    if (!dataChanged)
        await buildPlayerCharacters()
}
// SIF-7: the poll used to only console.error on failure, so a persistent error (e.g. the bridge or
// window.opener going away) left it silently retrying forever while the player panel stayed stale. Count
// consecutive failures and, after MAX_POLL_FAILURES, stop the poll and reveal an inline notice so the user
// knows live refresh stopped (and can reopen Settings to restart it) instead of seeing a frozen panel.
const MAX_POLL_FAILURES = 5
const playerRefreshFailed = $(".js-player-refresh-failed")
let consecutivePollFailures = 0
// TSS-5: capture the handle and stop polling when the settings window closes. Without this the 1500ms
// timer keeps firing on a torn-down page, awaiting getCurrentPlayer() and possibly writing to gobConfig /
// window.opener after teardown (config_groups.ts clears its bindings on beforeunload for the same reason).
const playerPollTimer = setInterval(async () => {
    try {
        await refreshPlayerSectionOnPlayerChange()
        consecutivePollFailures = 0
    } catch (e) {
        consecutivePollFailures++
        console.error(`Player-mentions live refresh failed (${consecutivePollFailures}/${MAX_POLL_FAILURES})`, e)
        if (consecutivePollFailures >= MAX_POLL_FAILURES) {
            clearInterval(playerPollTimer)
            playerRefreshFailed.prop("hidden", false)
        }
    }
}, 1500)
$(window).on("beforeunload", () => { clearInterval(playerPollTimer) })

binding.loadBindings()

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// ["behaviour.mentions", "behaviour.rangefilter.ignoreMention", "behaviour.channel.mention"].

//# sourceURL=config_mentions.js