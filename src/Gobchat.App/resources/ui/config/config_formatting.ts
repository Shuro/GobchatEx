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
import * as Utility from "/module/CommonUtility"
import * as Dialog from "/module/Dialog"
import * as Locale from "/module/Locale"
import * as Config from "/module/Config"

const MessageSegments = {
    SAY: { type: Gobchat.MessageSegmentEnum.SAY, styleId: "style.segment.say", translationId: "main.chat.segment.type.say" },
    EMOTE: { type: Gobchat.MessageSegmentEnum.EMOTE, styleId: "style.segment.emote", translationId: "main.chat.segment.type.emote" },
    MENTION: { type: Gobchat.MessageSegmentEnum.MENTION, styleId: "style.segment.mention", translationId: "main.chat.segment.type.mention" },
    OOC: { type: Gobchat.MessageSegmentEnum.OOC, styleId: "style.segment.ooc", translationId: "main.chat.segment.type.ooc" }
}

const ConfigKeyData = "behaviour.segment.data"
const ConfigKeyOrder = "behaviour.segment.order"
const DataAttributeElementId = "data-gob-entryid"

// --------------------------------------------------------------------------------------------------------

const binding = new Databinding.BindingContext(gobConfig)

// --- Chat Overlay Window (per-profile): chat frame position/size + chat background ---
// Moved here from the App page (which is now application-global only). These stay profile-bound.
const parseNonNegativeNumber = (element: JQuery) => {
    const value = Utility.toInt(element.val())
    return Utility.isNumber(value) && value >= 0 ? value : undefined
}
const parseNumber = (element: JQuery) => {
    const value = Utility.toInt(element.val())
    return Utility.isNumber(value) ? value : undefined
}
Databinding.bindElement(binding, $("#cp-app_frame_x"), { elementToConfig: parseNumber })
Databinding.bindElement(binding, $("#cp-app_frame_y"), { elementToConfig: parseNumber })
Databinding.bindElement(binding, $("#cp-app_frame_height"), { elementToConfig: parseNonNegativeNumber })
Databinding.bindElement(binding, $("#cp-app_frame_width"), { elementToConfig: parseNonNegativeNumber })

// The chat background colour comes from the theme; this is an optional opaque override (transparency
// is the separate slider below), so the picker has no alpha and an empty value means "use the theme".
const clrChatboxBackground = $("#cp-app_chat-history_backgroundcolor")
Components.makeColorSelector(clrChatboxBackground, { hasAlpha: false })
clrChatboxBackground.attr("placeholder", await gobLocale.get("config.app.chatbox.backgroundcolor.placeholder"))
Databinding.bindColorSelector(binding, clrChatboxBackground)
Components.makeResetButton($("#cp-app_chat-history_backgroundcolor_reset"), clrChatboxBackground)

const rngChatboxOpacity = $("#cp-app_chat-history_backgroundopacity")
const lblChatboxOpacity = $("#cp-app_chat-history_backgroundopacity_value")
const showOpacityValue = (percent: number) => lblChatboxOpacity.text(`${Math.round(percent) || 0}%`)
rngChatboxOpacity.on("input", () => showOpacityValue(parseFloat(rngChatboxOpacity.val() as string) || 0))
Databinding.bindElement(binding, rngChatboxOpacity, {
    elementToConfig: (element) => Utility.toInt(element.val()) || 0,
    configToElement: (element, storedValue) => {
        element.val(storedValue as number)
        showOpacityValue(storedValue as number)
    },
})
Components.makeResetButton($("#cp-app_chat-history_backgroundopacity_reset"), rngChatboxOpacity)

// --- Search (per-profile colours) moved to the Colors page (config_channel.ts) ---

// group 1
// item 1 — font family is now a curated dropdown. configToElement keeps a user's off-list legacy stack
// selectable by appending a transient "Custom" option, so converting input->select never loses it.
const customFontLabel = await gobLocale.get("config.formatting.font-family.custom")
const fontSelect = $("#cp-formatting_chat_font-family")
Databinding.bindElement<string>(binding, fontSelect, {
    configToElement: ($element, value) => {
        $element.find("option.js-custom-font").remove()
        let known = false
        $element.find("option").each((_i, o) => { if ((o as HTMLOptionElement).value === value) known = true })
        if (value && !known)
            $element.append($("<option class='js-custom-font'></option>").val(value).text(customFontLabel))
        $element.val(value)
    }
})
Components.makeResetButton($("#cp-formatting_chat_font-family_reset"), fontSelect)

// item 1b/1c — tab style + chat density as mutually-exclusive button groups (the gx-scheme segmented
// control). The overlay mirrors these onto <html data-tab-style / data-chat-density> (see gobchat.ts)
// to drive the FFXIV Modern theme.
function bindButtonGroup(groupSelector: string, configKey: string): void {
    // Re-query the buttons by selector on every highlight (don't cache a jQuery snapshot) and run one
    // extra deferred highlight after the current frame, so a highlight that lands at an unlucky moment
    // during load still gets corrected even if no later config event re-fires. Mirrors config_channel.ts
    // otherwise: an immediate, idempotent highlight plus property/profile listeners (so a profile switch
    // re-highlights too).
    const highlight = () => {
        const value = gobConfig.get(configKey, null)
        $(`${groupSelector} .js-seg-btn`).each((_i, el) => {
            const $el = $(el)
            $el.toggleClass("is-active", $el.attr("data-value") === value)
        })
    }
    $(`${groupSelector} .js-seg-btn`).on("click", (event) => {
        const value = $(event.currentTarget).attr("data-value")
        if (value)
            gobConfig.set(configKey, value)
    })
    gobConfig.addPropertyEventListener(configKey, highlight)
    gobConfig.addProfileEventListener(highlight)
    highlight()
    // Once more after the current load settles, in case the buttons weren't in the DOM yet at first
    // highlight and no later config event re-triggers it.
    requestAnimationFrame(highlight)
}
bindButtonGroup(".js-tab-style", "style.chat-frame.tab-style")
bindButtonGroup(".js-chat-density", "style.chat-frame.density")

// item 2

function makeFontSizeSelector(id: string, minValue: number | null, maxValue: number | null, unit: string) {
    const input = $(`#${id}`)
    const slider = $(`#${id}_slider`)
    const selector = $(`#${id}_selector`)
    const btnReset = $(`#${id}_reset`)

    Databinding.bindElement<string>(binding, input, {
        elementToConfig: (element, event, storedValue) => {
            let newValue = Utility.toInt(element.val())

            if (newValue === null) {  // restore old value                    
                newValue = Utility.extractFirstNumber(storedValue)
                newValue = Utility.toInt(newValue)
            }

            if (newValue === null)
                newValue = 0

            if (minValue !== null && newValue < minValue)
                newValue = minValue

            if (maxValue !== null && newValue > maxValue)
                newValue = maxValue

            element.val(newValue)
            slider.val(newValue)
            selector.val(newValue)
            if (selector.val() === null)
                selector.val("")

            return `${newValue}${unit}`
        },

        configToElement: (element, storedValue) => {
            const value = Utility.extractFirstNumber(storedValue)
            element.val(value)
            slider.val(value)
            selector.val(value)
            if (selector.val() === null)
                selector.val("")
        }
    })

    slider.on("input", () => input.val(slider.val()).change())

    // Don't pop the revert button in/out while the slider is being dragged: its appearance reflows the
    // narrow row and fights the thumb, flickering between adjacent values. Mark the slider as dragging
    // (CSS keeps the button invisible but space-reserved) and let it settle once the mouse is released.
    const stopDrag = () => {
        slider.removeClass("is-dragging")
        document.removeEventListener("pointerup", stopDrag)
    }
    slider.on("pointerdown", () => {
        slider.addClass("is-dragging")
        document.addEventListener("pointerup", stopDrag)
    })
    
    selector.on("change", () => {
        const selectedValue = selector.val()
        if (selectedValue.length > 0) {
            input.val(selectedValue).change()
        } else {
            selector.val(input.val())
            if (selector.val() === null)
                selector.val("")
        }
    })

    Components.makeResetButton(btnReset, input)
}

makeFontSizeSelector("cp-formatting_chat-history_font-size", 8, 24, "px")
makeFontSizeSelector("cp-formatting_chat-ui_font-size", 8, 24, "px")

// group 2
// item 1
// Say/Emote have no colour of their own (null) — they inherit the matching channel's colour, so the
// picker would look empty. Show a placeholder naming the source instead of a blank field.
const SegmentColorPlaceholders: Record<string, string> = {
    "style.segment.say": "config.formatting.segment.say.placeholder",
    "style.segment.emote": "config.formatting.segment.emote.placeholder",
}
// Captured at table build so updatePlaceholders() can re-resolve them on language change.
const segmentColorPlaceholderInputs: { input: JQuery; localeId: string }[] = []

const colorTable = $("#cp-formatting_color-table")
const colorTableEntryTemplate = $('#cp-formatting_template_color-table_entry')
for (const messageSegment of Object.values(MessageSegments)) {
    const entry = $(colorTableEntryTemplate.html())
    colorTable.append(entry)

    const lblName = entry.find(".js-label")
    const selColor = entry.find(".js-color-selector")
    const btnResetColor = entry.find(".js-color-reset")

    lblName.attr(Locale.HtmlAttribute.TextId, `${messageSegment.translationId}`)
    lblName.attr(Locale.HtmlAttribute.TooltipId, `${messageSegment.translationId}.tooltip`)

    // Capture for updatePlaceholders() so the placeholder is resolved there (and re-resolved on
    // language change), tracking the language like the start/end-token fields do.
    const placeholderId = SegmentColorPlaceholders[messageSegment.styleId]
    if (placeholderId)
        segmentColorPlaceholderInputs.push({ input: selColor, localeId: placeholderId })

    Databinding.setConfigKey(selColor, `${messageSegment.styleId}.color`)
    Components.makeColorSelector(selColor)
    Components.makeResetButton(btnResetColor, selColor)
    Databinding.bindColorSelector(binding, selColor)
}
gobLocale.updateElement(colorTable)

// group 3
// item 1 — autodetect emote in the Say channel and (independently) in the Party channel.
Databinding.bindCheckbox(binding, $("#cp-formatting_autodetectemote"))
Databinding.bindCheckbox(binding, $("#cp-formatting_autodetectemoteparty"))

// item 2 — three fixed segment sections (Say / Emote / OOC). Each lists the locked baked-in
// marker pairs (toggle only) plus any user-added custom pairs. One start/end token per pair,
// stored as length-1 arrays (`startTokens`/`endTokens`) so the C# parser stays unchanged.
const ConfigKeyDataTemplate = "behaviour.segment.data-template"
const lockedTemplate = $("#cp-formatting_template_segment_locked")
const customTemplate = $("#cp-formatting_template_segment_custom")

// The sections render Say -> Emote -> OOC, but the functional `order` array stays grouped
// OOC -> Emote -> Say: the C# ReplaceTypeByToken applies formats in that precedence (OOC/emote
// claim their text before say), so that ordering must be preserved when custom pairs are added.
const SectionTypes = ["SAY", "EMOTE", "OOC"]

// `type` may be the string "SAY" (default profile) or the numeric enum (older data).
function normalizeType(type: any): string {
    if (typeof type === "string")
        return type.toUpperCase()
    switch (type) {
        case Gobchat.MessageSegmentEnum.OOC: return "OOC"
        case Gobchat.MessageSegmentEnum.EMOTE: return "EMOTE"
        default: return "SAY"
    }
}

function firstToken(value: any): string {
    if (Array.isArray(value))
        return value.length > 0 ? `${value[0]}` : ""
    return value === null || value === undefined ? "" : `${value}`
}

// A single token, stored as a length-1 array (or empty). Accepts \uXXXX input via decodeUnicode.
function toTokenArray(raw: any): string[] {
    const token = Utility.decodeUnicode((raw ?? "").toString().trim())
    return token.length > 0 ? [token] : []
}

function unicodeOf(token: string): string {
    return token.length > 0 ? Utility.encodeUnicode(token) : ""
}

// Regroups `order` into OOC -> Emote -> Say precedence, preserving relative order within each
// type, so the C# precedence stays stable no matter where a custom pair was added.
function regroupOrder(order: string[], data: any): string[] {
    const buckets: { [type: string]: string[] } = { OOC: [], EMOTE: [], SAY: [] }
    for (const id of order) {
        if (!(id in data))
            continue
        const bucket = buckets[normalizeType(data[id].type)] ?? buckets.SAY
        bucket.push(id)
    }
    return [...buckets.OOC, ...buckets.EMOTE, ...buckets.SAY]
}

// One "add custom pair" button per section; the new entry inherits that section's type and has
// no `locked` flag, so it renders as an editable/deletable custom row.
$(".js-segment-add").on("click", function () {
    const type = $(this).attr("data-segment-type") as string
    const data = gobConfig.get(ConfigKeyData)
    const id = Utility.generateId(6, Object.keys(data))

    const entry = gobConfig.getDefault(ConfigKeyDataTemplate)
    entry.type = type
    data[id] = entry
    gobConfig.set(ConfigKeyData, data)

    const order = gobConfig.get(ConfigKeyOrder) as string[]
    order.push(id)
    gobConfig.set(ConfigKeyOrder, regroupOrder(order, data))
})

// A single binding context for all three lists, rebuilt on every (re)build.
let sectionsBinding: Databinding.BindingContext | null = null

async function buildSegmentSections() {
    if (sectionsBinding)
        sectionsBinding.clearBindings()
    sectionsBinding = new Databinding.BindingContext(gobConfig)

    const data = gobConfig.get(ConfigKeyData)
    const order = gobConfig.get(ConfigKeyOrder) as string[]

    for (const type of SectionTypes) {
        const container = $(`.js-segment-list[data-segment-type="${type}"]`)
        container.empty()
        order
            .filter(id => (id in data) && normalizeType(data[id].type) === type)
            .forEach(id => buildSegmentRow(sectionsBinding!, container, id, data[id]))
        await gobLocale.updateElement(container)
    }

    sectionsBinding.loadBindings()
    await updatePlaceholders()
}

function buildSegmentRow(ctx: Databinding.BindingContext, container: JQuery, id: string, entryData: any) {
    const isLocked = entryData.locked === true
    const configKey = `${ConfigKeyData}.${id}`

    const row = $((isLocked ? lockedTemplate : customTemplate).html())
    row.attr(DataAttributeElementId, id)
    container.append(row)

    Databinding.bindCheckbox(ctx, row.find(".js-entry-active"), { configKey: `${configKey}.active` })

    const lblUnicode = row.find(".js-unicode")
    const refreshUnicode = () => {
        const start = firstToken(gobConfig.get(`${configKey}.startTokens`))
        const end = firstToken(gobConfig.get(`${configKey}.endTokens`))
        lblUnicode.text(`${unicodeOf(start)} … ${unicodeOf(end)}`)
    }

    if (isLocked) {
        // Read-only: show the pair text + its unicode; only the active toggle is interactive.
        row.find(".js-pair").text(`${firstToken(entryData.startTokens)} … ${firstToken(entryData.endTokens)}`)
        refreshUnicode()
        return
    }

    const txtStart = row.find(".js-start-token")
    const txtEnd = row.find(".js-end-token")

    Databinding.setConfigKey(txtStart, `${configKey}.startTokens`)
    Databinding.bindElement(ctx, txtStart, {
        elementToConfig: (element) => toTokenArray(element.val()),
        configToElement: (element, value) => element.val(firstToken(value)),
    })

    Databinding.setConfigKey(txtEnd, `${configKey}.endTokens`)
    Databinding.bindElement(ctx, txtEnd, {
        elementToConfig: (element) => toTokenArray(element.val()),
        configToElement: (element, value) => element.val(firstToken(value)),
    })

    ctx.bindCallback(txtStart, refreshUnicode)
    ctx.bindCallback(txtEnd, refreshUnicode)

    row.find(".js-delete-entry").on("click", async () => {
        const result = await Dialog.showConfirmationDialog({
            dialogText: "config.formatting.tbl.segment.entry.action.delete.confirm",
        })
        if (result !== 1)
            return
        try {
            gobConfig.remove(configKey)
            const order = gobConfig.get(ConfigKeyOrder) as string[]
            _.remove(order, e => e === id)
            gobConfig.set(ConfigKeyOrder, order)
        } catch (e1) {
            console.error(e1)
        }
    })
}

// Placeholders use the existing token labels; re-applied on rebuild and on language change
// (placeholder isn't a localizable attribute, so it's set in code).
async function updatePlaceholders() {
    const [startLabel, endLabel] = await Promise.all([
        gobLocale.get("config.formatting.tbl.segment.entry.tokenstart"),
        gobLocale.get("config.formatting.tbl.segment.entry.tokenend"),
    ])
    $(".js-start-token").attr("placeholder", startLabel)
    $(".js-end-token").attr("placeholder", endLabel)

    // Say/Emote colour pickers inherit their channel's colour (no own value), so they show a
    // placeholder naming the source instead of a blank field; re-resolve them here too.
    await Promise.all(segmentColorPlaceholderInputs.map(async ({ input, localeId }) =>
        input.attr("placeholder", await gobLocale.get(localeId))))
}

binding.bindConfigListener(ConfigKeyOrder, Databinding.createConfigListener(() => buildSegmentSections(), null, true), () => buildSegmentSections())
// The language is app-global now (not a gobConfig key), so re-resolve placeholders off the locale change
// instead. Init is already covered by buildSegmentSections() -> updatePlaceholders().
gobLocale.addLocaleChangeListener(() => { void updatePlaceholders() })

binding.loadBindings()

// --------------------------------------------------------------------------------------------------------

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// ["behaviour.segment.order", "behaviour.segment.data", "style.segment",
//  "style.channel.base.general.font-family", "style.chat-history.font-size",
//  "style.chatui.font-size"].


//# sourceURL=config_roleplay.js