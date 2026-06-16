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
        Databinding.setConfigKey(clrSelectorSender, channelData.configId + ".sender.color")
        Databinding.setConfigKey(clrSelectorText, channelData.configId + ".general.color")
        Databinding.setConfigKey(clrSelectorBackground, channelData.configId + ".general.background-color")

        Components.makeColorSelector(clrSelectorSender)
        Components.makeColorSelector(clrSelectorText)
        Components.makeColorSelector(clrSelectorBackground)

        Databinding.bindColorSelector(binding, clrSelectorSender)
        Databinding.bindColorSelector(binding, clrSelectorText)
        Databinding.bindColorSelector(binding, clrSelectorBackground)

        Components.makeResetButton(btnResetSender, clrSelectorSender)
        Components.makeResetButton(btnResetText, clrSelectorText)
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

binding.loadBindings()

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// every channel colour key on this page (all inputs carrying a config key).

//# sourceURL=config_channel.js
