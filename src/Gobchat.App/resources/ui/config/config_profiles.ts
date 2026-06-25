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

import * as Dialog from "/module/Dialog"
import * as ProfileControl from "/module/ProfileControl"

const AttributeProfileId = "data-profile-id"

// Creating / cloning / importing persists the whole config to the host immediately (so the profile is
// "actually created" and survives a later profile switch, which reloads from the host). They're only
// allowed from a saved state, so this commit can never sweep up unrelated pending per-profile edits.
// Reordering is deliberately NOT routed through here: it's an ordinary pending edit saved on Save, so a
// burst of up/down clicks doesn't spam the host with a save (and a "Profiles saved." chat line) each time.
function persistProfiles() {
    window.saveConfig(gobConfig.serialize())
}

//setup create profile
$("#cp-profiles_profile_new").on("click", async function (event) {
    if (!await ProfileControl.requireSavedState())
        return
    gobConfig.createNewProfile()
    persistProfiles()
})

//setup import profile
$("#cp-profiles_profile_import").on("click", async function (event) {
    if (!await ProfileControl.requireSavedState())
        return

    const stringifiedProfile = await GobchatAPI.importProfile()
    if (!stringifiedProfile)
        return

    try {
        const newProfile = JSON.parse(stringifiedProfile)
        gobConfig.importProfile(newProfile)
        persistProfiles()
    } catch (error) {
        Dialog.showErrorDialog({ dialogText: "config.profiles.importprofile.error" });
    }
})

// Moves a profile one slot up (-1) or down (+1) in display order. This is an ordinary pending edit (it
// only writes profile.sortIndex on the working copy); it's persisted with everything else when the user
// hits Save/Apply, so repeated clicks don't trigger a host save per click. Materializes a contiguous
// sortIndex across all profiles first so the swap is well-defined even before any prior reorder.
async function moveProfile(profileId: string, direction: -1 | 1): Promise<void> {
    const ordered = ProfileControl.sortedProfileIds()
    const index = ordered.indexOf(profileId)
    const targetIndex = index + direction
    if (index < 0 || targetIndex < 0 || targetIndex >= ordered.length)
        return

    ProfileControl.normalizeSortIndices()

    const profile = gobConfig.getProfile(profileId)
    const target = gobConfig.getProfile(ordered[targetIndex])
    if (!profile || !target)
        return

    const profileSort = profile.get("profile.sortIndex")
    const targetSort = target.get("profile.sortIndex")
    profile.set("profile.sortIndex", targetSort)
    target.set("profile.sortIndex", profileSort)

    await populateProfileTable()
}

const profileTable = $("#cp-profiles_profiles")
const template = $("#cp-profiles_template_profile-table_entry")

async function populateProfileTable() {
    profileTable.children(":not(.gob-config_cp-profile-table_header)").remove()

    const orderedIds = ProfileControl.sortedProfileIds()
    orderedIds.forEach((profileId, index) => {
        const profile = gobConfig.getProfile(profileId)
        if (profile === null)
            return

        const rowElement = $(template.html())
            .attr(AttributeProfileId, profile.profileId)

        profileTable.append(rowElement)

        const txtProfileName = rowElement.find(".js-name")
        const btnActiveProfile = rowElement.find(".js-activate")
        const btnExportProfile = rowElement.find(".js-export")
        const btnCloneProfile = rowElement.find(".js-clone")
        const btnMoveUp = rowElement.find(".js-move-up")
        const btnMoveDown = rowElement.find(".js-move-down")
        const btnDeleteProfile = rowElement.find(".js-delete")

        txtProfileName.on("change", function (event) {
            profile.profileName = event.target.value || "Unnamed"
        })
        txtProfileName.val(profile.profileName)

        btnActiveProfile.on("click", async function (event) {
            // Same shared switch as the toolbar dropdown: warns on unsaved edits, discards them on
            // confirm, then activates and applies live immediately.
            await ProfileControl.requestProfileSwitch(profile.profileId)
        })
        if (gobConfig.activeProfileId === profile.profileId) {
            btnActiveProfile.prop("disabled", true)
            rowElement.find(".js-active-badge").prop("hidden", false)
        }

        btnExportProfile.on("click", async function (event) {
            const selection = await GobchatAPI.saveFileDialog("Json files (*.json)|*.json", `profile_${profile.profileId}.json`)
            if (selection === null || selection === undefined || selection.length === 0)
                return
            await GobchatAPI.writeTextToFile(selection, JSON.stringify(profile.config))
        })

        btnCloneProfile.on("click", async function (event) {
            // Creating a copy is only allowed from a saved state (see persistProfiles), so the new
            // profile is committed on its own rather than alongside unrelated pending edits.
            if (!await ProfileControl.requireSavedState())
                return
            const newProfileId = gobConfig.createNewProfile()
            gobConfig.copyProfile(profile.profileId, newProfileId)
            persistProfiles()
        })

        btnMoveUp.on("click", function (event) { moveProfile(profile.profileId, -1) })
        btnMoveUp.prop("disabled", index === 0)
        btnMoveDown.on("click", function (event) { moveProfile(profile.profileId, 1) })
        btnMoveDown.prop("disabled", index === orderedIds.length - 1)

        btnDeleteProfile.on("click", async function (event) {
            const result = await Dialog.showConfirmationDialog({
                dialogText: "config.profiles.profile.delete.dialog.text",
            })

            if (result === 1) 
                gobConfig.deleteProfile(profile.profileId)            
        })
        if (gobConfig.profileIds.length <= 1)
            btnDeleteProfile.prop("disabled", true)
    })

    await gobLocale.updateElement(profileTable)
}

gobConfig.addProfileEventListener(async (event) => { await populateProfileTable() })
await populateProfileTable()

gobConfig.addPropertyEventListener("profile.name", (event) => {
    const profile = gobConfig.getProfile(event.sourceProfileId)
    if (profile)
        profileTable.find(`[${AttributeProfileId}='${profile.profileId}'] .js-name`).val(profile.get("profile.name"))
})


//# sourceURL=config_profiles.js