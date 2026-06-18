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

// True when this settings page's working config differs from the opener's saved config — i.e. there
// are real unsaved edits. Compared as parsed objects (not strings) so key-order differences don't
// register as changes. After Apply/Save the opener reloads from local storage, so the two match again.
export function hasUnsavedChanges(): boolean {
    try {
        const openerConfig = (window.opener as any)?.gobConfig as (typeof gobConfig) | undefined
        if (!openerConfig)
            return false
        return !_.isEqual(JSON.parse(gobConfig.serialize()), JSON.parse(openerConfig.serialize()))
    } catch (e) {
        console.error(e)
        // If we can't tell, assume there might be changes so the user still gets the safety prompt.
        return true
    }
}

function isPlainObject(value: any): boolean {
    return value !== null && typeof value === "object" && !Array.isArray(value)
}

// Walks two config subtrees in parallel, collecting the leaf keys whose values differ (dotted paths).
function collectDiffs(working: any, saved: any, path: string, out: { path: string, saved: any, working: any }[]): void {
    if (isPlainObject(working) && isPlainObject(saved)) {
        const keys = new Set<string>([...Object.keys(working), ...Object.keys(saved)])
        for (const key of keys)
            collectDiffs(working[key], saved[key], path ? `${path}.${key}` : key, out)
        return
    }
    if (!_.isEqual(working, saved))
        out.push({ path: path || "(root)", saved, working })
}

function formatValue(value: any): string {
    if (value === undefined)
        return "(unset)"
    try {
        const text = JSON.stringify(value)
        return text.length > 120 ? `${text.slice(0, 117)}...` : text
    } catch {
        return String(value)
    }
}

// Logs (at console/INFO level, which the host routes into the app log) a key-by-key diff of the active
// profile between this page's working copy and the opener's saved config. Called whenever an
// unsaved-changes warning is about to be shown (profile switch / cancel / close) so the log records
// exactly what was pending — and, when the user proceeds, what gets discarded.
export function logUnsavedChanges(reason: string): void {
    try {
        const openerConfig = (window.opener as any)?.gobConfig as (typeof gobConfig) | undefined
        if (!openerConfig)
            return

        const activeId = gobConfig.activeProfileId
        const working = gobConfig.activeProfile?.config
        const saved = openerConfig.getProfile(activeId)?.config

        if (saved === undefined) {
            console.info(`[settings] ${reason}: active profile '${activeId}' has no saved counterpart yet (new, unsaved profile).`)
            return
        }

        const diffs: { path: string, saved: any, working: any }[] = []
        collectDiffs(working, saved, "", diffs)

        if (diffs.length === 0) {
            console.info(`[settings] ${reason}: whole-config differs from saved, but active profile '${activeId}' is unchanged (a different profile, or the profile list, was edited).`)
            return
        }

        const lines = diffs.map(d => `    ${d.path}: ${formatValue(d.saved)} -> ${formatValue(d.working)}`)
        console.info(`[settings] ${reason}: ${diffs.length} unsaved change(s) in active profile '${activeId}' (saved -> current):\n${lines.join("\n")}`)
    } catch (e) {
        console.error("[settings] Failed to log unsaved-changes diff", e)
    }
}

// Switch the active profile and push it live to the app (same path as Save), so the overlay reflects
// the new profile right away. Switching drops the page's unsaved edits to the current profile, so when
// there are any this first warns; on confirm it discards them (reverting the working copy to the saved
// config) before switching — rather than persisting them. Returns true only if the active profile
// actually changed, so callers (the toolbar dropdown / the Profiles page) can revert their own UI when
// the user cancels or the switch is aborted.
export async function requestProfileSwitch(profileId: string): Promise<boolean> {
    if (!profileId || profileId === gobConfig.activeProfileId)
        return false

    if (hasUnsavedChanges()) {
        logUnsavedChanges("Profile switch requested with unsaved changes")
        const proceed = await Dialog.showConfirmationDialog({ dialogText: "config.main.profileswitch.dialog" })
        if (!proceed)
            return false
        // Proceeding means "lose those changes": revert the working copy to the saved config so the
        // edits are discarded rather than persisted by the save below.
        try {
            await gobConfig.loadConfig()
        } catch (e) {
            console.error("Failed to discard unsaved changes before switching profile", e)
        }
        // Discarding may have removed a not-yet-saved profile (possibly the one just selected); if the
        // target is gone, there's nothing to switch to.
        if (!gobConfig.getProfile(profileId))
            return false
    }

    gobConfig.activeProfileId = profileId
    gobConfig.saveToLocalStore()
    window.saveConfig()
    return true
}
