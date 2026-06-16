# Settings redesign — TODO

Tracks deferred work from the settings-UI redesign (see the new design mockup in
[new_html/index.html](new_html/index.html)). The redesign is rolling out in phases; this file
lists things the mockup implies that are **not yet wired** plus follow-ups.

## Phase 1 (shell + App page) — deferred items

- **Title-bar Minimize button** — present but `disabled`. WebView2 settings window
  (`SettingsOverlayForm`) has no bridge method to minimize itself. To implement:
  add a `MinimizeSettings`-style method on the JS↔C# bridge (`GobchatBrowserAPI` in
  `Gobchat.App/Module/UI/BrowserAPI.cs`) that calls `WindowState = Minimized` on the
  form, expose it, and wire `#cp-main_titlebar-minimize` in `config.ts`.
  The title-bar **Close** button already works (maps to Cancel = discard + close).

- **i18n for new chrome strings** — a couple of new labels are currently hardcoded English
  because they have no `.resx` key yet:
  - The nav-rail "Active profile" label (`config.html`).
  - Add keys to `WebUIResources.*.resx` (en + de) and switch these back to
    `data-gob-locale-text`.

- **Toggle/section descriptions** — the mockup shows a small grey description under each
  toggle/section. These were omitted in Phase 1 to avoid inventing locale keys. If wanted,
  add `config.app.*.desc` keys (en + de) and a `.gx-row_desc` line per row.

- **"Track player locations" status badge** — the mockup shows a green "Available" badge.
  The live App page keeps the existing behaviour instead (an inline notice shown only when
  the player-location feature is *unavailable*, `#cp-app_characterlocations_feature`). A
  positive Available/unavailable badge could be added (needs JS to set state from
  `GobchatAPI.isFeaturePlayerLocationAvailable()`).

- **Coloris "Clear" button label** — the color-picker clear button text is English
  ("Clear"). Localize via `Coloris({ clearLabel })` once a locale key exists.

- **"Copy this page from another profile" button** — removed from **every** page during the
  Phase 2 conversion (the `makeCopyProfileButton`/`#cp-*_copyprofile` call was dropped from
  `config_app.ts`, `config_mentions.ts`, `config_channel.ts`, `config_chatlog.ts`,
  `config_rangefilter.ts`, `config_tabs.ts`, `config_formatting.ts`, `config_groups.ts`; each
  records the keys it used to copy in a TODO comment). The feature still exists in
  `Components.makeCopyProfileButton`; decide how to resurface it in the new design (e.g. a
  header action or a Profiles-page flow) and re-wire.

## Phase 2 (remaining pages) — DONE (reskinned), with two deferred editors

All `config_*.html` pages now use the new `.gx-*` design (App, Mentions, Channels, Chat log,
Range filter, Profiles, Chat tabs, Formatting, Groups, Debug). Each gained a `page.desc`
subtitle (+ a few row descriptions/eyebrow labels) in `WebUIResources.resx`/`.de.resx`.
Bound control `id`s and `data-gob-configkey`s were preserved; only wrapping markup/classes
changed, plus a handful of surgical TS selector tweaks (table → grid containers).

**Still on the old look (deferred — needs a native rewrite, not just a reskin):**

- **Formatting → Segment detection** (`#cp-formatting_segment-detection-table`) is still rendered with
  the **jQuery-UI `.accordion()` + `.sortable()`** widgets and the old `gob-config-*` template markup.
  To finish: replace the accordion with native collapsible cards (`<details>` or a small JS toggle)
  and the sortable with HTML5 drag-and-drop (reorder writes `behaviour.segment.order`), then restyle
  to `.gx-*`.

- **Groups → per-group editor** (`#cp-groups_group-table`) now uses the new `.gx-*` design (entry
  cards via `.gx-group-accordion`, Active toggle in the header, labelled colour rows, the 7 baked-in
  ff "symbol" groups locked: non-deletable/-renamable; custom groups can't be left unnamed). Drag-to-
  reorder was **removed** (the `.sortable()` widget is gone), so the editor is now driven only by the
  jQuery-UI `.accordion()` (collapsible cards). A future native rewrite (`<details>` or a small JS
  toggle) would let jQuery-UI be dropped here — see Cleanup.

## Cleanup (blocked until the two editors above are rewritten)

- **jQuery-UI must stay for now** — `config.html` still loads `jquery-ui-1.14.2.min.*` because
  the segment-detection accordion+sortable and the group accordion use it. Don't remove it until
  both are rewritten. (The overlay `gobchat.html` also loads jQuery-UI independently.) Note the
  Groups page no longer uses `.sortable()` — only `.accordion()` — but Formatting still uses both.
- **Old config SCSS partials must stay for now** — the two accordion editors still render with
  the legacy `gob-config-*` classes styled by the FFXIV-theme-injected stylesheet
  (`_config-*.scss`). Don't drop those partials until the editors are reskinned.
- **Spectrum is already dead in the settings UI** — `config.html` no longer loads it and no TS
  calls `.spectrum(...)` (Coloris replaced it). The vendored `lib/spectrum.js` / `lib/spectrum.css`
  and the `spectrum(...)` type declarations in `globals.d.ts` are now unused and can be deleted
  whenever convenient (purely dead weight; safe to remove).

## Chat overlay redesign

The chat overlay now uses the **FFXIV Modern** theme (`resources/ui/styles/ffxiv_modern_chat.css`),
default in `default_profile.json`. Deferred follow-ups:

- **Tab style + density selectors in Settings** — the theme supports `data-tab-style`
  (`underline`/`pills`/`angled`) and `data-chat-density` (`dense`/`breathable`) on `<html class="chat-frame">`,
  but they are currently hardcoded to `underline`/`dense` in `gobchat.html`. To expose them: add two
  config keys, two dropdowns on a settings page (+ locale strings), and bind them onto the `<html>`
  attributes in `gobchat.ts`.
- **Modern light variant** — `ffxiv_modern_chat.css` carries a `html.theme-light` token block, but the
  theme loader (`Style.activateStyles`) only injects `<link>`s and can't toggle that class. To offer a
  "FFXIV Modern Light": add a small `:root`-override CSS that re-declares the light tokens and register
  it as a second styles.json entry whose label contains "Light" (so `applyThemeMode` picks light mode).
  For now the legacy "FFXIV Light" covers light mode.
