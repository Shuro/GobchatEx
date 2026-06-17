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
import * as Components from "/module/Components"
import * as Utility from "/module/CommonUtility"

const DataAttributeElementId = "data-gob-entryid"
const ConfigKeyOrder = "behaviour.groups.sorting"
const ConfigKeyData = "behaviour.groups.data"
const ConfigKeyDataTemplate = "behaviour.groups.data-template"
const JQueryDataKey = "configbinding"

const tblGroups = $("#cp-groups_group-table")
const tmplGroupsEntryTemplate = $("#cp-groups_template_group-table_entry")

// Localized placeholder used when a custom group's name is blanked. Kept in sync with the active
// language by the listener near the bottom; the literal is only a pre-load default.
let fallbackGroupName = "Empty Group"

async function populateGroupTable() {
    tblGroups.children().each(function () {
        $(this).data<Databinding.BindingContext>(JQueryDataKey).clearBindings()
    })
    tblGroups.empty()

    const groupIds = gobConfig.get(ConfigKeyOrder) as string[]
    groupIds.forEach(async (id, idx) => await buildGroupTableEntry(id, idx))

    await gobLocale.updateElement(tblGroups)
}

async function buildGroupTableEntry(groupId: string, groupIndex: number) {
    const entry = $(tmplGroupsEntryTemplate.html())
    entry.attr(DataAttributeElementId, groupId)
    tblGroups.append(entry)

    const binding = new Databinding.BindingContext(gobConfig)
    entry.data(JQueryDataKey, binding)

    const configKey = `${ConfigKeyData}.${groupId}`
    const groupData = gobConfig.get(configKey)

    const isNonCustomGroup = "ffgroup" in groupData

    const lblEntryIndex = entry.find(".js-header_index")
    const lblEntrySep = entry.find(".js-header_sep")
    const lblEntryName = entry.find(".js-header_name")
    const btnDeleteEntry = entry.find(".js-delete-entry")
    const chkEnableEntry = entry.find(".js-entry-active")
    const txtGroupName = entry.find(".js-txt-name")
    const btnGroupNameReset = entry.find(".js-txt-name_reset")

    const txtTriggers = entry.find(".js-group-triggers")

    lblEntryIndex.text(groupIndex + 1)

    // Native collapsible: a header click toggles this card. The Active toggle and delete button in
    // the header call stopPropagation (below), so their clicks don't also collapse/expand the card.
    entry.find(".gx-group_head").on("click", () => entry.toggleClass("is-open"))

    btnDeleteEntry.on("click", async (event) => {
        event.stopPropagation() // don't toggle the card when deleting
        const result = await Dialog.showConfirmationDialog({
            dialogText: "config.groups.tbl.group.entry.deleteconfirm",
        })

        if (result === 1) {
            try {
                gobConfig.remove(configKey)
                const order = gobConfig.get(ConfigKeyOrder) as string[]
                _.remove(order, e => e === groupId)
                gobConfig.set(ConfigKeyOrder, order)
            } catch (e1) {
                console.error(e1)
            }
        }
    })

    // The Active switch lives in the card header; stop its clicks from also toggling / collapsing
    // the card.
    chkEnableEntry.on("click", (event) => event.stopPropagation())
    Databinding.bindCheckbox(binding, chkEnableEntry, { configKey: `${configKey}.active` })

    Databinding.setConfigKey(txtGroupName, `${configKey}.name`)
    // A custom group must keep a name: a blank/whitespace-only edit falls back to the localized
    // "Empty Group" placeholder (config + input + header all follow through the binding).
    Databinding.bindElement(binding, txtGroupName, {
        elementToConfig: (element) => {
            let value = (element.val() ?? "").toString().trim()
            if (value.length === 0) {
                value = fallbackGroupName
                element.val(value)
            }
            return value
        }
    })
    Components.makeResetButton(btnGroupNameReset, txtGroupName)
    // Custom groups: the header shows the editable name. The 7 baked-in ff groups are unrenamable —
    // their header name is set to the localized "<symbol>-Group" by the language listener below.
    if (!isNonCustomGroup)
        Databinding.bindText(binding, lblEntryName, { configKey: Databinding.getConfigKey(txtGroupName)! })

    Databinding.bindElement(binding, txtTriggers, {
        configKey: `${configKey}.trigger`,
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

    function makeColorSelector(classId: string, configKey: string) {
        const selector = entry.find(`.${classId}`)
        const btnReset = entry.find(`.${classId}_reset`)

        Databinding.setConfigKey(selector, configKey)
        Components.makeColorSelector(selector)
        Databinding.bindColorSelector(binding, selector)

        Components.makeResetButton(btnReset, selector)

        if (!isNonCustomGroup)
            btnReset.hide()
    }

    makeColorSelector("js-sender-fgcolor", `${configKey}.style.header.color`)
    makeColorSelector("js-sender-bgcolor", `${configKey}.style.header.background-color`)
    makeColorSelector("js-msg-bgcolor", `${configKey}.style.body.background-color`)

    if (isNonCustomGroup) {
        // The 7 baked-in ff "symbol" groups are locked: non-deletable and non-renamable.
        btnDeleteEntry.prop("disabled", true).hide()
        lblEntryIndex.hide()
        lblEntrySep.hide()

        Databinding.bindListener(binding, "behaviour.language", async (value) => {
            const name = gobConfig.get(`${configKey}.hiddenName`, "")

            const label = entry.find(".js-ffgroup-icon")
            const localization = await gobLocale.get(label.attr("data-gob-locale-id-text") as string, value)
            label.text(Utility.formatString(localization, name))

            // Header reads "<symbol>-Group" / "<symbol>-Gruppe".
            const headerTpl = await gobLocale.get("config.groups.tbl.group.entry.ffgroup.name", value)
            lblEntryName.text(Utility.formatString(headerTpl, name))
        })

        entry.find(".js-mode-custom").hide()
    } else {
        btnGroupNameReset.prop("disabled", true).hide()

        entry.find(".js-mode-noncustom").hide()
    }

    binding.loadBindings()
}

function makeNewGroup(addFront) {
    const groups = gobConfig.get(ConfigKeyData)
    const groupId = Utility.generateId(6, Object.keys(groups))

    groups[groupId] = gobConfig.getDefault(ConfigKeyDataTemplate)
    groups[groupId].id = groupId

    gobConfig.set(ConfigKeyData, groups)
    const sorting = gobConfig.get(ConfigKeyOrder)
    if (addFront) {
        sorting.unshift(groupId)
    } else {
        sorting.push(groupId)
    }
    gobConfig.set(ConfigKeyOrder, sorting)
}

const btnAddNewGroupTop = $("#cp-groups_addnewgrouptop")
btnAddNewGroupTop.on("click", function () {
    makeNewGroup(true)
})

const btnAddNewGroupBottom = $("#cp-groups_addnewgroupbot")
btnAddNewGroupBottom.on("click", function () {
    makeNewGroup(false)
})

// Drag-to-reorder was removed (not needed); the order follows ConfigKeyOrder, changed only by
// adding/removing groups. The cards are native collapsibles — a header click toggles `.is-open`,
// wired per-entry in buildGroupTableEntry.

const binding = new Databinding.BindingContext(gobConfig)
Databinding.bindCheckbox(binding, $("#cp-groups_updateChat"))
binding.bindCallback("behaviour.language", async (language) => {
    try {
        fallbackGroupName = await gobLocale.get("config.groups.tbl.group.entry.name.fallback", language)
    } catch (e) {
        console.error(e)
    }
})
binding.bindConfigListener(ConfigKeyOrder, Databinding.createConfigListener(() => populateGroupTable(), null, true), () => populateGroupTable())
binding.loadBindings()

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// ["behaviour.groups.data", "behaviour.groups.sorting", "behaviour.groups.updateChat"].

//# sourceURL=config_groups.js