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
import * as ContextMenu from "/module/ContextMenu"

const DataAttributeElementId = "data-gob-entryid"
const ConfigKeyOrder = "behaviour.groups.sorting"
const ConfigKeyData = "behaviour.groups.data"
const ConfigKeyDataTemplate = "behaviour.groups.data-template"
const JQueryDataKey = "configbinding"

const tblGroups = $("#cp-groups_group-table")
const tblFfGroups = $("#cp-groups_ffgroup-table")
const tmplGroupsEntryTemplate = $("#cp-groups_template_group-table_entry")

// Localized placeholder used when a custom group's name is blanked. Kept in sync with the active
// language by the listener near the bottom; the literal is only a pre-load default.
let fallbackGroupName = "Empty Group"

async function populateGroupTable() {
    for (const table of [tblGroups, tblFfGroups]) {
        table.children().each(function () {
            $(this).data<Databinding.BindingContext>(JQueryDataKey).clearBindings()
        })
        table.empty()
    }

    // sorting holds custom group ids only (since 2.0.9): custom groups are numbered 1..n and reorderable.
    const customIds = gobConfig.get(ConfigKeyOrder) as string[]
    customIds.forEach((id, idx) => buildGroupTableEntry(tblGroups, id, idx, customIds.length, idx + 1))

    // The premade (ff) groups are a locked reference section, numbered 1-7 by their ffgroup.
    const allGroups = Object.values(gobConfig.get(ConfigKeyData)) as ContextMenu.GroupLike[]
    const premade = ContextMenu.premadeGroups(allGroups)
    premade.forEach((group, idx) => buildGroupTableEntry(tblFfGroups, group.id, idx, premade.length, (group.ffgroup ?? 0) + 1))

    await gobLocale.updateElement(tblGroups)
    await gobLocale.updateElement(tblFfGroups)
}

// Move a custom group one slot up (-1) or down (+1) in behaviour.groups.sorting. Re-reads the order
// fresh (rather than trusting the index captured when the row was built) so two quick clicks before the
// async rebuild compound correctly; the ConfigKeyOrder listener then rebuilds the table. No-op past an end.
function moveGroup(groupId: string, delta: number): void {
    const order = (gobConfig.get(ConfigKeyOrder, []) as string[]).slice()
    const index = order.indexOf(groupId)
    if (index < 0)
        return
    const target = index + delta
    if (target < 0 || target >= order.length)
        return
    ;[order[index], order[target]] = [order[target], order[index]]
    gobConfig.set(ConfigKeyOrder, order)
}

function buildGroupTableEntry(table: JQuery, groupId: string, index: number, listLength: number, displayNumber: number) {
    const entry = $(tmplGroupsEntryTemplate.html())
    entry.attr(DataAttributeElementId, groupId)
    table.append(entry)

    const binding = new Databinding.BindingContext(gobConfig)
    entry.data(JQueryDataKey, binding)

    const configKey = `${ConfigKeyData}.${groupId}`
    const groupData = gobConfig.get(configKey)

    const isNonCustomGroup = groupData.ffgroup != null

    const lblEntryIndex = entry.find(".js-header_index")
    const lblEntrySep = entry.find(".js-header_sep")
    const lblEntryName = entry.find(".js-header_name")
    const btnDeleteEntry = entry.find(".js-delete-entry")
    const btnMoveUp = entry.find(".js-group-up")
    const btnMoveDown = entry.find(".js-group-down")
    const chkEnableEntry = entry.find(".js-entry-active")
    const txtGroupName = entry.find(".js-txt-name")
    const btnGroupNameReset = entry.find(".js-txt-name_reset")

    const txtTriggers = entry.find(".js-group-triggers")

    lblEntryIndex.text(displayNumber)

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
    binding.addDisposer(Components.makeResetButton(btnGroupNameReset, txtGroupName))
    // Custom groups: the header shows the editable name. The 7 baked-in ff groups are unrenamable —
    // their header name is set to the localized "<symbol>-Group" by the language listener below.
    if (!isNonCustomGroup)
        Databinding.bindText(binding, lblEntryName, { configKey: Databinding.getConfigKey(txtGroupName)! })

    // Members are removable chips: type a full FFXIV name and press Enter/comma to add (stored lowercased,
    // which is how the chat matches them). isValidFFXIVPlayerName rejects anything that isn't a real name.
    Components.makeTagInput(txtTriggers, binding, {
        configKey: `${configKey}.trigger`,
        normalize: (word) => word.toLowerCase().trim(),
        validate: Utility.isValidFFXIVPlayerName,
        placeholder: "config.groups.tbl.group.entry.trigger.add",
    })

    function makeColorSelector(classId: string, configKey: string) {
        const selector = entry.find(`.${classId}`)
        const btnReset = entry.find(`.${classId}_reset`)

        Databinding.setConfigKey(selector, configKey)
        Components.makeColorSelector(selector)
        Databinding.bindColorSelector(binding, selector)

        binding.addDisposer(Components.makeResetButton(btnReset, selector))

        if (!isNonCustomGroup)
            btnReset.hide()
    }

    makeColorSelector("js-sender-fgcolor", `${configKey}.style.header.color`)
    makeColorSelector("js-sender-bgcolor", `${configKey}.style.header.background-color`)
    makeColorSelector("js-msg-bgcolor", `${configKey}.style.body.background-color`)

    if (isNonCustomGroup) {
        // The 7 baked-in ff "symbol" groups are a locked reference: non-deletable, non-renamable and
        // non-reorderable, shown numbered 1-7 (by ffgroup) as "<symbol>-Group".
        btnDeleteEntry.prop("disabled", true).hide()
        btnMoveUp.hide()
        btnMoveDown.hide()
        lblEntrySep.text(". ") // header reads "1. <symbol>-Group" (no "Name:" label)

        // Locked ff-group labels are localized at build time using the current locale. The language is
        // app-global now (not a gobConfig key), and populateGroupTable() rebuilds these entries on a live
        // language change (see the locale-change listener below), so this re-runs with the new locale.
        void (async () => {
            const name = gobConfig.get(`${configKey}.hiddenName`, "")

            const label = entry.find(".js-ffgroup-icon")
            const localization = await gobLocale.get(label.attr("data-gob-locale-id-text") as string)
            label.text(Utility.formatString(localization, name))

            // Header reads "<symbol>-Group" / "<symbol>-Gruppe".
            const headerTpl = await gobLocale.get("config.groups.tbl.group.entry.ffgroup.name")
            lblEntryName.text(Utility.formatString(headerTpl, name))
        })()

        entry.find(".js-mode-custom").hide()
    } else {
        btnGroupNameReset.prop("disabled", true).hide()

        entry.find(".js-mode-noncustom").hide()

        // Custom groups reorder via the header up/down arrows (disabled at the ends). They write
        // behaviour.groups.sorting; the ConfigKeyOrder listener below rebuilds + re-numbers the table.
        btnMoveUp.prop("disabled", index === 0)
        btnMoveDown.prop("disabled", index === listLength - 1)
        btnMoveUp.on("click", (event) => { event.stopPropagation(); moveGroup(groupId, -1) })
        btnMoveDown.on("click", (event) => { event.stopPropagation(); moveGroup(groupId, 1) })
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
// The language is app-global now (not a gobConfig key). Resolve the locale-dependent fallback group name
// before the table is first built, and on a live language change re-resolve it and rebuild the table so
// the locked ff-group names (localized per entry) re-localize too.
const updateFallbackGroupName = async () => {
    try {
        fallbackGroupName = await gobLocale.get("config.groups.tbl.group.entry.name.fallback")
    } catch (e) {
        console.error(e)
    }
}
await updateFallbackGroupName()
gobLocale.addLocaleChangeListener(() => { void updateFallbackGroupName().then(() => populateGroupTable()) })
binding.bindConfigListener(ConfigKeyOrder, Databinding.createConfigListener(() => populateGroupTable(), null, true), () => populateGroupTable())
binding.loadBindings()

// While this settings window is open, group changes made from the overlay's right-click menu (add/remove
// player, create group) land in the overlay's config, not this page's working copy — so without this they
// wouldn't show here and a later Save would revert them. One-way only: this page pushes its own edits on
// Save. To avoid wiping unsaved edits made here, only the parts the overlay can actually change are pulled
// in: an existing group's member list (which re-renders just that group's chips in place — no table
// rebuild) and a whole group newly created from "Create new group…". The table is rebuilt only when the
// group order actually changes (a new group), through the ConfigKeyOrder listener above — so a plain
// add/remove never rebuilds the table or disturbs a name/colour the user is editing.
const overlayConfig = (window.opener as any)?.gobConfig as (typeof gobConfig) | undefined
if (overlayConfig && overlayConfig !== gobConfig) {
    const overlayBinding = new Databinding.BindingContext(overlayConfig)
    // Deep-copy so the two windows' configs never share mutable references (config is plain JSON).
    const deepCopy = (value: unknown) => JSON.parse(JSON.stringify(value))

    const syncFromOverlay = () => {
        const overlayData = overlayConfig.get(ConfigKeyData) as Record<string, { trigger?: string[] }>
        const overlayOrder = overlayConfig.get(ConfigKeyOrder) as string[]

        for (const id of Object.keys(overlayData)) {
            const groupKey = `${ConfigKeyData}.${id}`
            if (gobConfig.has(groupKey))
                gobConfig.set(`${groupKey}.trigger`, deepCopy(overlayData[id].trigger ?? []))
            else
                gobConfig.set(groupKey, deepCopy(overlayData[id]))
        }

        const currentOrder = gobConfig.get(ConfigKeyOrder) as string[]
        const orderChanged = currentOrder.length !== overlayOrder.length
            || currentOrder.some((id, idx) => id !== overlayOrder[idx])
        if (orderChanged)
            gobConfig.set(ConfigKeyOrder, deepCopy(overlayOrder))
    }

    overlayBinding.bindCallback(ConfigKeyData, syncFromOverlay, false)
    overlayBinding.bindCallback(ConfigKeyOrder, syncFromOverlay, false)
    overlayBinding.loadBindings()
    // Block body (not an implicit-return arrow): clearBindings() returns the BindingContext for
    // chaining, and a truthy beforeunload return becomes event.returnValue — which makes Chromium pop
    // its native "Leave site?" prompt on every unload (e.g. when saving closes the window).
    $(window).on("beforeunload", () => { overlayBinding.clearBindings() })
}

// TODO: "Copy this page from another profile" button removed from the design for now;
// the per-page copy-profile feature will be reworked later (see TODO.md). It used to copy
// ["behaviour.groups.data", "behaviour.groups.sorting", "behaviour.groups.updateChat"].

//# sourceURL=config_groups.js