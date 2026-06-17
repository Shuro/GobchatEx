# Settings redesign — TODO

Tracks deferred work from the settings-UI redesign (see the new design mockup in
[new_html/index.html](new_html/index.html)). The redesign is rolling out in phases; this file
lists things the mockup implies that are **not yet wired** plus follow-ups.

## Phase 1 (shell + App page) — deferred items

- ~~**Title-bar Minimize button**~~ — **DONE** (and extended). The settings window is now a normal
  taskbar window (`SettingsOverlayForm`: `ShowInTaskbar=true`, the `WS_EX_TOOLWINDOW` override removed,
  `TopMost=false`), so a minimize has a restore affordance. Bridge methods `MinimizeSettings` /
  `SetSettingsAlwaysOnTop` were added (`BrowserAPI.cs` → `BrowserAPIManager` → `ManagedWebBrowser`,
  which now tracks the live `SettingsOverlayForm`) and wired in `config.ts`. A new title-bar **Pin**
  button (`#cp-main_titlebar-pin`, left of minimize) toggles always-on-top, off by default.

- ~~**i18n for new chrome strings**~~ — **DONE** for the chrome shipped so far: the nav-rail
  "Active profile" label (`config.main.rail.activeprofile`) and the title-bar pin/minimize/close
  tooltips (`config.main.titlebar.*`) now resolve from `WebUIResources.*.resx` (en + de) via
  `data-gob-locale-text` / `data-gob-locale-tooltip`.

- **Toggle/section descriptions** — the mockup shows a small grey description under each
  toggle/section. These were omitted in Phase 1 to avoid inventing locale keys. If wanted,
  add `config.app.*.desc` keys (en + de) and a `.gx-row_desc` line per row.

- **"Track player locations" status badge** — the mockup shows a green "Available" badge.
  The live App page keeps the existing behaviour instead (an inline notice shown only when
  the player-location feature is *unavailable*, `#cp-app_characterlocations_feature`). A
  positive Available/unavailable badge could be added (needs JS to set state from
  `GobchatAPI.isFeaturePlayerLocationAvailable()`).

- ~~**Coloris "Clear" button label**~~ — **DONE.** `config.ts` re-issues `Coloris({ clearLabel })`
  on language change using the new `config.colorpicker.clear` key (en + de).

- **"Copy this page from another profile" button** — removed from **every** page during the
  Phase 2 conversion (the `makeCopyProfileButton`/`#cp-*_copyprofile` call was dropped from
  `config_app.ts`, `config_mentions.ts`, `config_channel.ts`, `config_chatlog.ts`,
  `config_rangefilter.ts`, `config_tabs.ts`, `config_formatting.ts`, `config_groups.ts`; each
  records the keys it used to copy in a TODO comment). The feature still exists in
  `Components.makeCopyProfileButton`; decide how to resurface it in the new design (e.g. a
  header action or a Profiles-page flow) and re-wire.

## Phase 2 (remaining pages) — DONE (reskinned + both editors rewritten natively)

All `config_*.html` pages now use the new `.gx-*` design (App, Mentions, Channels, Chat log,
Range filter, Profiles, Chat tabs, Formatting, Groups, Debug). Each gained a `page.desc`
subtitle (+ a few row descriptions/eyebrow labels) in `WebUIResources.resx`/`.de.resx`.
Bound control `id`s and `data-gob-configkey`s were preserved; only wrapping markup/classes
changed, plus a handful of surgical TS selector tweaks (table → grid containers).

**Both deferred editors are now rewritten natively (no jQuery-UI):**

- ~~**Formatting → Segment detection**~~ — **DONE.** Replaced the flat editable list with **three fixed
  sections — Say / Emote / OOC** (`config_formatting.html`, lists filled by `config_formatting.ts`).
  Each section lists the **locked baked-in marker pairs** (read-only pair text + an on/off toggle) plus
  any user-added **custom pairs** (editable start/end token + toggle + delete), via the
  `cp-formatting_template_segment_locked` / `_custom` templates. One start + one end token per pair
  (stored as **length-1 arrays** in `startTokens`/`endTokens`, so the C# parser is unchanged). The 9
  baked-in pairs in `default_profile.json` carry `"locked": true` (the flag the UI keys on, like the
  Groups page's `ffgroup`); the old multi-token guillemet entry was split into single-token `say5`
  (`»…«`) + `say6` (`«…»`). Drag-to-reorder was **removed**; the visual order is Say→Emote→OOC, but the
  functional `behaviour.segment.order` stays grouped **OOC→Emote→Say** (`regroupOrder`) to preserve the
  C# `ReplaceTypeByToken` precedence. Existing profiles auto-migrate: schema **version bumped to 20001**
  and `ConfigUpgrade_2_0_1` rewrites a saved `behaviour.segment` onto the new shape (baked-ins gain
  `locked`, say5 splits, order regrouped) while preserving custom pairs and each pair's on/off state;
  already-new data (a `locked` flag present) is left untouched. There is **no Reset button** on the
  page.

- ~~**Groups → per-group editor**~~ — **DONE.** `#cp-groups_group-table` dropped the jQuery-UI
  `.accordion()`; the cards are the same native collapsible toggle as above. The `.gx-*` design, the
  header Active toggle, the locked 7 ff "symbol" groups, and the no-empty-name rule are unchanged.

## Cleanup

- ~~**jQuery-UI in `config.html`**~~ — **DONE.** Both editors are native now, so the
  `jquery-ui-1.14.2.min.*` `<link>`/`<script>` were removed from `config.html` (and the now-dead
  `accordion`/`sortable` type declarations dropped from `globals.d.ts`). The overlay `gobchat.html`
  still loads jQuery-UI independently — out of scope here.
- **Old config SCSS partials still stay** — even though the segment-detection editor no longer uses
  `gob-config-*` markup, the `_config-*.scss` partials remain **required**: `.gob-config-page` wraps
  every config page (13 files), other `gob-config-*` classes are still used by other (un-rewritten)
  config pages, and `_config-button.scss` is referenced by `Components.ts` / `WebComponents.ts`. So
  this work does **not** unblock deleting those partials — that needs the remaining legacy pages and
  the copy-profile component to be converted first.
- ~~**Spectrum dead code**~~ — **DONE.** Deleted `lib/spectrum.js` / `lib/spectrum.css` and the
  `spectrum(...)` type declarations in `globals.d.ts` (confirmed nothing loads or calls it). The
  overlay theme's `_thirdparty_spectrum.scss` partial + its `@use` in `ffxiv_dark.scss` were left
  alone (overlay-theme scope; harmless CSS).

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
