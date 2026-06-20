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

'use strict'

// Settings search: indexes the labels on every config page and lets the user jump to a setting by
// name from the nav rail. Pure DOM scan — it reads the existing `.gob-config-navigation_entry`
// labels and the headings/labels inside each loaded panel, so it needs no per-page wiring and stays
// correct as pages change. The index is (re)built on focus, after the panels have been localized, so
// it always reflects the current language.

const NAV_ENTRY = ".gob-config-navigation_entry"
const NAV_DIVIDER = ".gx-nav-divider"
const NAV_SECTION = ".gx-nav-section"
const PANEL_STACK = "#cp-main_nav_content"
const PANEL_ENTRY = ".gob-config-navigation-panel_entry"
// The headings / field labels that name a setting on a page.
const LABEL_SELECTOR = ".gx-eyebrow, .gx-row_title, .gx-flabel, .gx-acc_title"
const PAGE_HEAD = ".gx-page_head"

const DEBOUNCE_MS = 120
const HIGHLIGHT_MS = 1500
const MAX_RESULTS = 30
const HIT_CLASS = "gx-search-hit"

interface IndexEntry {
    navTarget: string
    pageName: string
    label: string        // "" for a whole-page entry
    el: HTMLElement       // scroll/highlight target
    $navEntry: JQuery
    isPage: boolean
}

function cleanText(node: Element | null | undefined): string {
    return (node?.textContent ?? "").replace(/\s+/g, " ").trim()
}

export async function makeControl($nav: JQuery, $input: JQuery, $results: JQuery): Promise<void> {
    let index: IndexEntry[] = []
    let lastHits: IndexEntry[] = []
    let debounceTimer: number | undefined
    let highlightTimer: number | undefined
    let highlighted: HTMLElement | null = null
    let blurTimer: number | undefined
    let placeholder = ""
    let noResultsText = ""

    async function localizeChrome(): Promise<void> {
        try {
            const lookup = await gobLocale.getAll(["config.main.search.placeholder", "config.main.search.noresults"])
            placeholder = lookup["config.main.search.placeholder"] ?? ""
            noResultsText = lookup["config.main.search.noresults"] ?? ""
            $input.attr("placeholder", placeholder)
        } catch (e) {
            console.error("Failed to localize settings search", e)
        }
    }

    function buildIndex(): void {
        const entries: IndexEntry[] = []
        $nav.find(NAV_ENTRY).each(function () {
            const $navEntry = $(this)
            const navTarget = $navEntry.attr("data-gob-nav-target") as string
            if (!navTarget)
                return
            const pageName = cleanText($navEntry.children("span")[0])
            if (!pageName)
                return

            const panel = $(`${PANEL_STACK} > ${PANEL_ENTRY}[data-gob-nav-id="${navTarget}"]`)

            // A whole-page entry so the page is always reachable by its name; jump to its head.
            const head = panel.find(PAGE_HEAD)[0] ?? panel[0]
            if (head)
                entries.push({ navTarget, pageName, label: "", el: head, $navEntry, isPage: true })

            const seen = new Set<string>()
            panel.find(LABEL_SELECTOR).each(function () {
                const label = cleanText(this)
                if (!label || seen.has(label))
                    return
                seen.add(label)
                entries.push({ navTarget, pageName, label, el: this, $navEntry, isPage: false })
            })
        })
        index = entries
    }

    function clearHighlight(): void {
        if (highlightTimer !== undefined) {
            window.clearTimeout(highlightTimer)
            highlightTimer = undefined
        }
        if (highlighted) {
            highlighted.classList.remove(HIT_CLASS)
            highlighted = null
        }
    }

    function showAllNav(): void {
        $nav.find(NAV_ENTRY).prop("hidden", false)
        $nav.find(NAV_DIVIDER).prop("hidden", false)
        $nav.find(NAV_SECTION).prop("hidden", false)
    }

    function hideResults(): void {
        $results.empty().prop("hidden", true)
        lastHits = []
    }

    function activate(entry: IndexEntry | undefined): void {
        if (!entry)
            return
        // Reuse the nav component's own click handler to switch to the page + show its panel. The
        // handler listens for a native click event (same path config.ts uses for the dry-run jump).
        entry.$navEntry[0].click()
        entry.el.scrollIntoView({ block: "center", behavior: "smooth" })

        clearHighlight()
        entry.el.classList.add(HIT_CLASS)
        highlighted = entry.el
        highlightTimer = window.setTimeout(clearHighlight, HIGHLIGHT_MS)

        $input.val("")
        hideResults()
        showAllNav()
    }

    function renderResults(query: string): void {
        const q = query.toLowerCase()

        const labelHits = index.filter(e => !e.isPage && e.label.toLowerCase().includes(q))
        const pagesWithLabelHit = new Set(labelHits.map(e => e.navTarget))

        // Iterate the index in order so results stay grouped by page; a page entry only appears when
        // the page name matches and none of its labels already matched (avoids flooding one page).
        const hits: IndexEntry[] = []
        for (const e of index) {
            if (hits.length >= MAX_RESULTS)
                break
            if (e.isPage) {
                if (e.pageName.toLowerCase().includes(q) && !pagesWithLabelHit.has(e.navTarget))
                    hits.push(e)
            } else if (e.label.toLowerCase().includes(q)) {
                hits.push(e)
            }
        }
        lastHits = hits

        $results.empty()
        if (hits.length === 0) {
            $results.append($("<li>").addClass("gx-rail_search-result is-empty").text(noResultsText))
        } else {
            for (const entry of hits) {
                const $li = $("<li>").addClass("gx-rail_search-result")
                $li.append($("<span>").addClass("gx-rail_search-result_page").text(entry.pageName))
                if (!entry.isPage) {
                    $li.append($("<span>").addClass("gx-rail_search-result_sep").text("—"))
                    $li.append($("<span>").addClass("gx-rail_search-result_label").text(entry.label))
                }
                $li.on("click", () => activate(entry))
                $results.append($li)
            }
        }
        $results.prop("hidden", false)

        // Hide nav entries whose page produced no match (and the now-orphaned divider), so the rail
        // mirrors the result set.
        const matchedPages = new Set(hits.map(e => e.navTarget))
        $nav.find(NAV_ENTRY).each(function () {
            const target = $(this).attr("data-gob-nav-target") as string
            $(this).prop("hidden", !matchedPages.has(target))
        })
        $nav.find(NAV_DIVIDER).prop("hidden", true)
        $nav.find(NAV_SECTION).prop("hidden", true)
    }

    function onQuery(): void {
        const query = ($input.val() as string ?? "").trim()
        if (query.length === 0) {
            hideResults()
            showAllNav()
            return
        }
        renderResults(query)
    }

    $input.on("focus", () => {
        buildIndex() // rebuild each focus so the index is always in the current locale
    })

    $input.on("input", () => {
        if (debounceTimer !== undefined)
            window.clearTimeout(debounceTimer)
        debounceTimer = window.setTimeout(onQuery, DEBOUNCE_MS)
    })

    $input.on("keydown", (event) => {
        if (event.key === "Enter") {
            event.preventDefault()
            activate(lastHits[0])
        } else if (event.key === "Escape") {
            event.preventDefault()
            $input.val("")
            hideResults()
            showAllNav()
        }
    })

    // Leaving the field clears the query and restores the full nav; the delay lets a result click
    // (which itself clears + jumps) register first.
    $input.on("blur", () => {
        blurTimer = window.setTimeout(() => {
            $input.val("")
            hideResults()
            showAllNav()
        }, 200)
    })
    $input.on("focus", () => {
        if (blurTimer !== undefined)
            window.clearTimeout(blurTimer)
    })

    gobLocale.addLocaleChangeListener(() => {
        // The panels re-localize asynchronously on a language change, so drop the (now stale-locale)
        // index and any open results; the next focus rebuilds the index in the new language.
        index = []
        void localizeChrome()
        hideResults()
        showAllNav()
    })

    await localizeChrome()
}
