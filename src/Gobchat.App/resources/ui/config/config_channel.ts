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

const binding = new Databinding.BindingContext(gobConfig)

const table = $("#cp-channel_channel-table")
const rowTemplate = $('#cp-channel_template_channel-table_entry')

// Persisted text-colour scheme ("classic" | "modern"). Switching it sets every channel's TEXT colour
// to the scheme's value; sender/background are never touched.
const ColorSchemeKey = "style.channel.colorscheme"

// Every channel's text-colour config key, collected as rows are built (drives the scheme switch + reset).
const textColorKeys: string[] = []

// Modern text colours, keyed by their text config key (style.channel.<name>.general.color), built from
// the backend-injected Gobchat.FFXIVModernColors (single source of truth — the bundled modern profile).
const modernColors: { [configKey: string]: string } = {}
{
    const source = (Gobchat as any).FFXIVModernColors ?? {}
    for (const [internalName, data] of Object.entries(source)) {
        const color = (data as any)?.general?.color
        if (typeof color === "string")
            modernColors[`style.channel.${internalName}.general.color`] = color
    }
}

// The colour a text key should revert to under the active scheme: the modern preset when "modern" has
// one for it, otherwise the plain config default (which is also the whole "classic" scheme).
function effectiveTextDefault(key: string): any {
    const scheme = gobConfig.get(ColorSchemeKey, "classic")
    if (scheme === "modern" && modernColors[key] !== undefined)
        return modernColors[key]
    return gobConfig.getDefault(key)
}

// Localized once for the empty-colour placeholder ("Default"); set as a real placeholder attribute so an
// unset colour field reads "Default" instead of looking blank.
const emptyColorPlaceholder = await gobLocale.get("config.colorpicker.empty")

function buildChannelEntry(channelData) {
    const rowEntry = $(rowTemplate.html())
    table.appendEvenly(rowEntry)

    const lblName = rowEntry.find(".js-name")
    const clrSelectorSender = rowEntry.find(".js-color-sender")
    const clrSelectorText = rowEntry.find(".js-color-text")
    const clrSelectorBackground = rowEntry.find(".js-color-background")

    const btnResetSender = rowEntry.find(".js-color-sender-reset")
    const btnResetText = rowEntry.find(".js-color-text-reset")
    const btnResetBackground = rowEntry.find(".js-color-background-reset")

    lblName.attr(Locale.HtmlAttribute.TextId, `${channelData.translationId}`)
    lblName.attr(Locale.HtmlAttribute.TooltipId, `${channelData.tooltipId}`)

    if (channelData.configId === null) {
        clrSelectorSender.parent().hide()
        clrSelectorText.parent().hide()
        clrSelectorBackground.parent().hide()
    } else {
        const textKey = channelData.configId + ".general.color"
        textColorKeys.push(textKey)

        Databinding.setConfigKey(clrSelectorSender, channelData.configId + ".sender.color")
        Databinding.setConfigKey(clrSelectorText, textKey)
        Databinding.setConfigKey(clrSelectorBackground, channelData.configId + ".general.background-color")

        Components.makeColorSelector(clrSelectorSender)
        Components.makeColorSelector(clrSelectorText)
        Components.makeColorSelector(clrSelectorBackground)

        clrSelectorSender.attr("placeholder", emptyColorPlaceholder)
        clrSelectorText.attr("placeholder", emptyColorPlaceholder)
        clrSelectorBackground.attr("placeholder", emptyColorPlaceholder)

        Databinding.bindColorSelector(binding, clrSelectorSender)
        Databinding.bindColorSelector(binding, clrSelectorText)
        Databinding.bindColorSelector(binding, clrSelectorBackground)

        Components.makeResetButton(btnResetSender, clrSelectorSender)
        // The text reset is scheme-aware: it reverts to (and hides at) the active scheme's colour, and
        // re-evaluates when the scheme changes. Sender/background keep the plain default reset.
        Components.makeResetButton(btnResetText, clrSelectorText, {
            getDefaultValue: (key) => effectiveTextDefault(key),
            onReset: (key) => gobConfig.set(key, effectiveTextDefault(key)),
            extraWatchKeys: [ColorSchemeKey],
        })
        Components.makeResetButton(btnResetBackground, clrSelectorBackground)
    }
}

buildChannelEntry({
    translationId: "main.chat.channel.general",
    configId: "style.channel.base",
    relevant: true
})

Object.entries(Gobchat.Channels).forEach((entry) => {
    const channelData = entry[1]
    if (!channelData.relevant)
        return
    buildChannelEntry(channelData)
})

// --- Classic / Modern text-colour scheme picker ---
const schemeButtons = $(".gx-scheme_btn")

function highlightActiveScheme() {
    const scheme = gobConfig.get(ColorSchemeKey, "classic")
    schemeButtons.each((_idx, el) => {
        const $el = $(el)
        $el.toggleClass("is-active", $el.attr("data-scheme") === scheme)
    })
}

schemeButtons.on("click", (event) => {
    const scheme = $(event.currentTarget).attr("data-scheme")
    if (!scheme)
        return
    gobConfig.set(ColorSchemeKey, scheme)
    for (const key of textColorKeys)
        gobConfig.set(key, effectiveTextDefault(key))
    highlightActiveScheme()
})

gobConfig.addPropertyEventListener(ColorSchemeKey, highlightActiveScheme)
// Also re-highlight on profile switch: that fires a profile event, not a colorscheme property event,
// so without this the active scheme button would keep showing the previous profile's selection.
gobConfig.addProfileEventListener(highlightActiveScheme)
highlightActiveScheme()

// --- Search (per-profile colours) — markup moved here from the Formatting page, so it's wired here. ---
const clrSearchMarked = $("#cp-app_search_marked")
Components.makeColorSelector(clrSearchMarked)
Databinding.bindColorSelector(binding, clrSearchMarked)
Components.makeResetButton($("#cp-app_search_marked_reset"), clrSearchMarked)

const clrSearchSelected = $("#cp-app_search_selected")
Components.makeColorSelector(clrSearchSelected)
Databinding.bindColorSelector(binding, clrSearchSelected)
Components.makeResetButton($("#cp-app_search_selected_reset"), clrSearchSelected)

binding.loadBindings()

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// every channel colour key on this page (all inputs carrying a config key).

//# sourceURL=config_channel.js
