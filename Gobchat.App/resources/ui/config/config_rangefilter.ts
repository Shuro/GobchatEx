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
import * as Utility from "/module/CommonUtility"
import * as Components from "/module/Components"
import * as Chat from "/module/Chat"
import * as Locale from "/module/Locale"
import * as RangeFilterPreview from "/module/RangeFilterPreview"

const binding = new Databinding.BindingContext(gobConfig)

const parseNonNegativeNumber = (element: JQuery) => {
    const value = Utility.toInt(element.val())
    if (value === null)
        return undefined
    return value >= 0 ? value : undefined
}

const txtCutOff = $("#cp-rangefilter_cutoff")
const txtFadeOut = $("#cp-rangefilter_fadeout")
const txtStartOpacity = $("#cp-rangefilter_startopacity")
const txtEndOpacity = $("#cp-rangefilter_endopacity")
const previewBar = $("#cp-rangefilter_preview_bar")
const previewDots = $("#cp-rangefilter_preview_dots")
const previewStatus = $("#cp-rangefilter_preview_status")
const btnSnapshot = $("#cp-rangefilter_preview_snapshot")
const btnRefresh = $("#cp-rangefilter_preview_refresh")
// Sample player positions along the bar, as fractions of the cutoff distance; the last one is beyond
// the cutoff (vanished). Each becomes a hoverable dot showing an example emote at that distance.
const PREVIEW_SAMPLE_FRACTIONS = [0.1, 0.3, 0.5, 0.7, 0.9, 1.1]
const PREVIEW_SAMPLE_NAME = "Firstname"
let previewDotEls: { dot: JQuery, name: JQuery, emote: JQuery, meta: JQuery }[] | null = null
let previewEmoteTemplate = "{0} waves." // "{0}" = the player name; replaced by the localized string
let previewYouText = "That's you!" // the gold self-marker's tooltip; replaced by the localized string
// A frozen snapshot of nearby players (name + exact distance) captured on demand; null = show samples.
let snapshotPlayers: { name: string, distance: number }[] | null = null
let showingSnapshot = false

const channelTable = $("#cp-rangefilter_channel-table > tbody")
const channelTableLinkshells = $("#cp-rangefilter_channel-table-2 > tbody")
const channelTableEntryTemplate = $("#cp-rangefilter_template_channel-table_entry")
let rangefilterEntryCount = 0

// Channels FFXIV itself only delivers from players within ~20 yalm (horizontal) — Say and Emote. A
// range-filter cutoff beyond that can't reveal anyone farther on these channels, so their rows carry a note.
const PROXIMITY_CULLED_CHANNELS = ["style.channel.say", "style.channel.emote"]

Databinding.bindElement(binding, txtCutOff, { elementToConfig: parseNonNegativeNumber })
Components.makeResetButton($("#cp-rangefilter_cutoff_reset"), txtCutOff)

Databinding.bindElement(binding, txtFadeOut, { elementToConfig: parseNonNegativeNumber })
Components.makeResetButton($("#cp-rangefilter_fadeout_reset"), txtFadeOut)

Databinding.bindElement(binding, txtStartOpacity, { elementToConfig: parseNonNegativeNumber })
Components.makeResetButton($("#cp-rangefilter_startopacity_reset"), txtStartOpacity)

Databinding.bindElement(binding, txtEndOpacity, { elementToConfig: parseNonNegativeNumber })
Components.makeResetButton($("#cp-rangefilter_endopacity_reset"), txtEndOpacity)

// ---- Fade preview ------------------------------------------------------------------------------
// Illustrative only: the live engine fades in discrete steps (behaviour.rangefilter.opacitysteps,
// see modules/Chat.ts); the bar shows the smooth intent. Opacity is conveyed by drawing the chat
// ink colour at the computed alpha over a checkerboard (the bar's CSS background), so the fade reads
// as transparency. The visible 0..cutoff range fills PREVIEW_VISIBLE_WIDTH; beyond the cutoff the
// gradient goes transparent, leaving the checkerboard exposed = the message has vanished.
const PREVIEW_VISIBLE_WIDTH = 82 // % of the bar used for 0..cutoff; the rest is the "vanished" tail
const PREVIEW_INK_FALLBACK = { r: 232, g: 234, b: 238 } // matches default behaviour.channel.base color
// Distance (as a multiple of the cutoff) past which a point falls off the right end of the bar.
const PREVIEW_BAR_MAX_FRACTION = 100 / PREVIEW_VISIBLE_WIDTH

function readNumber(element: JQuery, fallback: number): number {
    const value = Utility.toInt(element.val())
    return value === null ? fallback : value
}

function parseHexColor(value: unknown): { r: number, g: number, b: number } | null {
    if (typeof value !== "string")
        return null
    const match = /^#?([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(value.trim())
    if (!match)
        return null
    let hex = match[1]
    if (hex.length === 3)
        hex = hex[0] + hex[0] + hex[1] + hex[1] + hex[2] + hex[2]
    return {
        r: parseInt(hex.slice(0, 2), 16),
        g: parseInt(hex.slice(2, 4), 16),
        b: parseInt(hex.slice(4, 6), 16),
    }
}

function buildDots(count: number) {
    previewDots.empty()
    const els: { dot: JQuery, name: JQuery, emote: JQuery, meta: JQuery }[] = []
    for (let i = 0; i < count; ++i) {
        const name = $("<span>").addClass("gx-range-preview_tip-name")
        const emote = $("<span>").addClass("gx-range-preview_tip-emote")
        const meta = $("<span>").addClass("gx-range-preview_tip-meta")
        const tip = $("<span>").addClass("gx-range-preview_tip").append(name).append(emote).append(meta)
        const dot = $("<span>").addClass("gx-range-preview_dot").append(tip)
        previewDots.append(dot)
        els.push({ dot, name, emote, meta })
    }
    previewDotEls = els
}

function updateRangeDots(cutoff: number, fadeout: number, startOpacity: number, endOpacity: number, ink: { r: number, g: number, b: number }) {
    if (previewDots.length === 0)
        return

    // A golden "you" marker at distance 0, then either the illustrative samples or a frozen snapshot of
    // real nearby players. Only points that land on the bar (within the cutoff + short vanished tail) are
    // drawn; the "you" marker is always shown.
    const others = snapshotPlayers !== null
        ? snapshotPlayers
        : PREVIEW_SAMPLE_FRACTIONS.map(fraction => ({ name: PREVIEW_SAMPLE_NAME, distance: fraction * cutoff }))
    const points: { name: string, distance: number, isSelf?: boolean }[] = [{ name: "", distance: 0, isSelf: true }, ...others]
    const visible = points.filter(p => p.isSelf || (cutoff > 0 && p.distance <= cutoff * PREVIEW_BAR_MAX_FRACTION))

    if (previewDotEls === null || previewDotEls.length !== visible.length)
        buildDots(visible.length)
    previewDots.toggleClass("is-snapshot", snapshotPlayers !== null)

    visible.forEach((point, i) => {
        const refs = previewDotEls![i]
        const posFraction = cutoff > 0 ? point.distance / cutoff : 0
        const leftPct = Math.min(Math.max(posFraction * PREVIEW_VISIBLE_WIDTH, 0), 99)
        refs.dot.toggleClass("is-self", !!point.isSelf)

        if (point.isSelf) {
            // That's you — the player's own position (0), in the theme's gold; no fade, no meta line.
            refs.dot.css({ "left": `${leftPct}%`, "background-color": "var(--gold)" })
            refs.dot.removeClass("is-vanished")
            refs.name.text(previewYouText)
            refs.emote.css("opacity", "1").text("")
            refs.meta.text("")
            return
        }

        const opacity = RangeFilterPreview.opacityAtDistance(point.distance, cutoff, fadeout, startOpacity, endOpacity)
        const alpha = Math.min(Math.max(opacity, 0), 100) / 100
        refs.dot.css({
            "left": `${leftPct}%`,
            "background-color": `rgba(${ink.r},${ink.g},${ink.b},${Math.max(alpha, 0.14)})`,
        })
        refs.dot.toggleClass("is-vanished", opacity <= 0)
        // The name is always shown so you can tell who it is even when the message is fully culled; the
        // emote below it is drawn at the message's actual opacity (at 0% it disappears entirely), and the
        // meta line gives the exact distance and percentage.
        refs.name.text(point.name)
        refs.emote.css("opacity", `${alpha}`).text(Utility.formatString(previewEmoteTemplate, point.name))
        refs.meta.text(`${point.distance.toFixed(1)} yalm · ${Math.round(opacity)}%`)
    })
}

function updateRangePreview() {
    if (previewBar.length === 0)
        return

    const cutoff = readNumber(txtCutOff, 0)
    // Clamp fadeout into [0, cutoff]: a fadeout wider than the cutoff just means "always fading".
    const fadeout = Math.min(Math.max(readNumber(txtFadeOut, 0), 0), Math.max(cutoff, 0))
    const startOpacity = readNumber(txtStartOpacity, 100)
    const endOpacity = readNumber(txtEndOpacity, 0)

    const ink = parseHexColor(gobConfig.get("behaviour.channel.base.general.color", null)) ?? PREVIEW_INK_FALLBACK
    const rgba = (opacityPercent: number) => `rgba(${ink.r},${ink.g},${ink.b},${Math.min(Math.max(opacityPercent, 0), 100) / 100})`

    // Matches the engine (ChatMessageActorDataSetter.CalculateVisibility): a message stays solid until
    // the fade-out distance, then fades linearly down to the cutoff, and vanishes past it. So the solid
    // plateau is the near 0..fadeout span as a fraction of the visible 0..cutoff range.
    const plateauFraction = cutoff > 0 ? Math.min(Math.max(fadeout / cutoff, 0), 1) : 0
    const plateauPct = plateauFraction * PREVIEW_VISIBLE_WIDTH
    const fadeEndPct = PREVIEW_VISIBLE_WIDTH

    // 0..fadeout: solid — the engine leaves messages at full opacity below the fade-out distance and
    // only drops to the start opacity *at* that line; then a linear fade to the end opacity at the
    // cutoff, and transparent (vanished) beyond it. The step from full to start opacity at the
    // fade-out line is intentional (it mirrors the engine's full -> startOpacity jump there).
    const gradient = `linear-gradient(90deg,` +
        ` ${rgba(100)} 0%,` +
        ` ${rgba(100)} ${plateauPct}%,` +
        ` ${rgba(startOpacity)} ${plateauPct}%,` +
        ` ${rgba(endOpacity)} ${fadeEndPct}%,` +
        ` transparent ${fadeEndPct}%,` +
        ` transparent 100%)`
    previewBar.css("background-image", gradient)

    updateRangeDots(cutoff, fadeout, startOpacity, endOpacity, ink)
}

;[txtCutOff, txtFadeOut, txtStartOpacity, txtEndOpacity].forEach(el => {
    el.on("input", updateRangePreview)
    el.on("change", updateRangePreview) // reset buttons commit via `change`, not `input`
})

// ---- Nearby-player snapshot --------------------------------------------------------------------
// The button drops a one-time snapshot of real nearby players (name + exact distance) onto the bar.
// It's a snapshot, not live tracking: the distances are frozen, but they re-fade as the cutoff/opacity
// settings change so you can see how the current filter would treat the people actually around you.
const PREVIEW_SNAPSHOT_CAP = 30 // nearest N; the list arrives already sorted by distance ascending

async function setSnapshotStatus(key: string, params?: (string | number)[]) {
    try {
        previewStatus.text(params ? await gobLocale.getAndFormat(key, params) : await gobLocale.get(key))
    } catch (e) {
        console.error(e)
        previewStatus.text("")
    }
}

async function updateSnapshotButtonLabel() {
    // Swap the button's localized-text key (not just its text) so it still re-localizes on a later
    // language change; setLocalizedTextId clears the active-locale stamp so updateElement re-fetches.
    Locale.setLocalizedTextId(btnSnapshot, showingSnapshot ? "config.rangefilter.preview.sample" : "config.rangefilter.preview.snapshot")
    try {
        await gobLocale.updateElement(btnSnapshot)
    } catch (e) {
        console.error(e)
    }
}

// Capture (or re-capture) a fresh snapshot of nearby players. Shared by the toggle button's "load"
// path and the refresh button, which re-runs it while a snapshot is already shown.
async function loadSnapshot() {
    btnSnapshot.prop("disabled", true)
    btnRefresh.prop("disabled", true)
    try {
        if (!(await GobchatAPI.isFeaturePlayerLocationAvailable())) {
            await setSnapshotStatus("config.rangefilter.preview.offline")
            return
        }
        const players = RangeFilterPreview.parseNearbyPlayers(await GobchatAPI.getPlayersAndDistance(), PREVIEW_SNAPSHOT_CAP)
        if (players.length === 0) {
            await setSnapshotStatus("config.rangefilter.preview.noplayers")
            return
        }
        snapshotPlayers = players
        showingSnapshot = true
        await setSnapshotStatus("config.rangefilter.preview.players", [players.length])
        await updateSnapshotButtonLabel()
        btnRefresh.prop("hidden", false)
        updateRangePreview()
    } catch (e) {
        console.error("Failed to snapshot nearby players", e)
        await setSnapshotStatus("config.rangefilter.preview.offline")
    } finally {
        btnSnapshot.prop("disabled", false)
        btnRefresh.prop("disabled", false)
    }
}

btnSnapshot.on("click", async () => {
    if (showingSnapshot) { // toggle back to the illustrative examples
        snapshotPlayers = null
        showingSnapshot = false
        previewStatus.text("")
        btnRefresh.prop("hidden", true)
        await updateSnapshotButtonLabel()
        updateRangePreview()
        return
    }
    await loadSnapshot()
})

btnRefresh.on("click", () => { void loadSnapshot() })

Object.values(Gobchat.Channels).forEach(channelData => {
    if (!channelData.relevant)
        return
    addEntryToTable(channelData)
})

function addEntryToTable(channelData: Chat.Channel) {
    const channelEnums = ([] as Chat.ChatChannelEnum[]).concat(channelData.chatChannel || [])
    if (channelEnums.length === 0)
        return // channel is not associated with any ingame channel

    const targetTable = /linkshell/i.test(channelData.configId ?? "") ? channelTableLinkshells : channelTable
    const id = `cp-rangefilter_channel-table_entry-${rangefilterEntryCount++}`

    const entry = $(channelTableEntryTemplate.html())
        .appendTo(targetTable)

    entry.find(".js-label")
        .attr(Locale.HtmlAttribute.TextId, `${channelData.translationId}`)
        .attr(Locale.HtmlAttribute.TooltipId, `${channelData.tooltipId}`)
        .prop("for", id)

    // Say/Emote: FFXIV only delivers them from players within ~20 yalm (horizontal), so a larger cutoff
    // can't reveal anyone farther on these channels. Wrap the name and add an info note that says so.
    if (PROXIMITY_CULLED_CHANNELS.includes((channelData.configId ?? "").toLowerCase())) {
        const label = entry.find(".js-label")
        const wrap = $('<span class="gx-checklist_name"></span>')
        label.before(wrap)
        wrap.append(label)
        // Append first, THEN set the locale attribute: the page localizes dynamically-added content via a
        // mutation observer that only watches attribute changes (childList:false). Setting the attribute
        // while the note is still detached and appending afterwards is never observed, so the title would
        // never be written (the channel labels above work because their attrs are set once in the DOM).
        const note = $('<span class="gx-cull-note" tabindex="0"><svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><line x1="12" y1="16" x2="12" y2="12"/><line x1="12" y1="8" x2="12.01" y2="8"/></svg></span>')
            .appendTo(wrap)
        note.attr(Locale.HtmlAttribute.TooltipId, "config.rangefilter.cull.tooltip")
    }

    const ckbApply = entry.find(".js-checkbox")
        .prop("id", id)

    Databinding.setConfigKey(ckbApply, "behaviour.channel.rangefilter")
    Databinding.bindCheckboxArray(binding, ckbApply, channelEnums)
}

binding.loadBindings()
updateRangePreview() // initial state, once the four fields hold their bound values

// The dot tooltips' example emote is a programmatic string (not a data-gob-locale-text node), so fetch
// it via the locale manager and refresh on language change.
async function loadPreviewEmote() {
    try {
        const lookup = await gobLocale.getAll(["config.rangefilter.preview.emote", "config.rangefilter.preview.you"])
        if (lookup["config.rangefilter.preview.emote"])
            previewEmoteTemplate = lookup["config.rangefilter.preview.emote"]
        if (lookup["config.rangefilter.preview.you"])
            previewYouText = lookup["config.rangefilter.preview.you"]
    } catch (e) {
        console.error("Failed to load range-filter preview strings", e)
    }
    updateRangePreview()
}
gobLocale.addLocaleChangeListener(() => {
    previewStatus.text("") // a transient snapshot status would otherwise be left in the old language
    void loadPreviewEmote()
})
void loadPreviewEmote()

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// every range-filter config key on this page (all inputs carrying a config key).

