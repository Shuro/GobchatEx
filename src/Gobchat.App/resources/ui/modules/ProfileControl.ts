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

// Per-profile reorder key. Stored inside the profile's own `profile` block so it rides the existing
// profile-map sync (JS <-> C#) as ordinary profile data — no schema bump or C# change needed. Profiles
// without it (never reordered) fall back to a name sort, matching the prior alphabetical dropdown order.
const SortIndexKey = "profile.sortIndex"

// The profile ids in their display order: by `profile.sortIndex` when present, then by name. Shared by
// the toolbar dropdown (config.ts) and the Profiles page (config_profiles.ts) so the two never diverge.
export function sortedProfileIds(): string[] {
    return gobConfig.profileIds
        .slice()
        .sort((a, b) => {
            const pa = gobConfig.getProfile(a)
            const pb = gobConfig.getProfile(b)
            const ia = pa?.get(SortIndexKey, undefined)
            const ib = pb?.get(SortIndexKey, undefined)
            const hasA = typeof ia === "number"
            const hasB = typeof ib === "number"
            if (hasA && hasB && ia !== ib)
                return ia - ib
            if (hasA !== hasB)
                return hasA ? -1 : 1 // indexed profiles sort ahead of un-indexed ones
            return (pa?.profileName ?? "").localeCompare(pb?.profileName ?? "")
        })
}

// Assigns a contiguous 0..n-1 `profile.sortIndex` to every profile in current display order. Called
// before a reorder so swaps operate on a fully-materialized index even if some profiles never had one.
export function normalizeSortIndices(): void {
    sortedProfileIds().forEach((profileId, index) => {
        gobConfig.getProfile(profileId)?.set(SortIndexKey, index)
    })
}

// Gate for the structural profile operations (create / clone / import / reorder): they persist the whole
// config immediately, so acting with pending per-profile edits would silently commit those too. When
// there are unsaved changes, inform the user to save or cancel first and return false (op aborted).
export async function requireSavedState(): Promise<boolean> {
    if (!hasUnsavedChanges())
        return true
    await Dialog.showMessageDialog({
        title: "config.main.dialog.title.confirm",
        dialogText: "config.profiles.savefirst.dialog",
        dialogType: "Ok",
        dialogIcon: "Warning",
    })
    return false
}

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
            // The active profile's contents match, so the divergence is in the config wrapper
            // (the activeProfile pointer, the profile list, or another profile). Diff the whole
            // serialized config so the log names exactly what differs instead of just guessing.
            const wholeDiffs: { path: string, saved: any, working: any }[] = []
            try {
                collectDiffs(JSON.parse(gobConfig.serialize()), JSON.parse(openerConfig.serialize()), "", wholeDiffs)
            } catch (e) {
                console.error("[settings] Failed to diff whole config", e)
            }
            if (wholeDiffs.length === 0) {
                console.info(`[settings] ${reason}: whole-config differs from saved, but no concrete diff found (likely a key-order or serialization artifact).`)
            } else {
                const wholeLines = wholeDiffs.map(d => `    ${d.path}: ${formatValue(d.saved)} -> ${formatValue(d.working)}`)
                console.info(`[settings] ${reason}: active profile '${activeId}' is unchanged; ${wholeDiffs.length} change(s) outside it (saved -> current):\n${wholeLines.join("\n")}`)
            }
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
    window.saveConfig(gobConfig.serialize())
    return true
}
