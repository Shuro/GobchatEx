# Settings redesign — TODO

Tracks deferred work from the settings-UI redesign (see the new design mockup in
[new_html/Settings/index.html](../new_html/Settings/index.html)). The redesign is rolling out in phases; this file
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

- ~~**Toggle/section descriptions**~~ — **DONE.** A `.gx-row_desc` line sits under each toggle/row on
  the App page (`config_app.html`), with `config.app.*.desc` keys added to `WebUIResources.resx` +
  `.de.resx`.

- ~~**"Track player locations" status badge**~~ — **DONE.** A green "Available" badge
  (`#cp-app_characterlocations_available`, key `config.app.ckb.actor.available`) shows next to the
  toggle when the feature is available; the inline "not available" notice
  (`#cp-app_characterlocations_feature`) shows when it isn't. Both are driven from
  `GobchatAPI.isFeaturePlayerLocationAvailable()` in `config_app.ts`.

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
- **Remove the dead `gobchat/deprecated/` pre-TypeScript layer (21 files, ~216 KB)** — fully
  superseded by the `modules/` TS layer + the C# chat/command pipeline, and **not referenced
  or loaded** by any live HTML/TS/JS/C# (grep-confirmed). It still ships in Release because
  `Gobchat.csproj` copies `resources\**` wholesale. Delete the folder. The only file with
  data not present elsewhere is `Datacenters.js` (a stale FFXIV world list for a removed
  server-picker feature — trivially replaceable). Full per-file analysis in
  [deprecated-code-audit.md](deprecated-code-audit.md).
- **Remove orphaned `[Obsolete]`/`@deprecated` members (zero live callers)** —
  `PlayerEventArgs`/`ChatlogEventArgs` (`Gobchat.Memory`), `JsonUtil.SwitchResult`/
  `SwitchError`/`TypeSwitchError` (+ dead commented lines 142-143), `JsonUtil`
  `.ReplaceArrayIfAvailable`, `Databinding.makeDatabinding`, and the no-op
  `PerformApplicationUpdate()` exit hook (obsolete since the Velopack migration). Keep the
  `JsonUtil.TypeSwitch` *method* (still heavily used).
- **De-stale misleading markers on still-used code (the `gc`-commands trap)** —
  `Gobchat.MessageSegmentEnum` (`globals.d.ts` `// deprecated`) and
  `Config.saveToLocalStore`/`loadFromLocalStore` (`//TODO remove later`) are **load-bearing**.
  Remove the misleading marker (or finish the migration) — do not delete the code.

## Chat overlay redesign

The chat overlay now uses the **FFXIV Modern** theme (`resources/ui/styles/ffxiv_modern_chat.css`),
default in `default_profile.json`. Deferred follow-ups:

- ~~**Tab style + density selectors in Settings**~~ — **DONE.** Two dropdowns on the **Formatting**
  page (`config_formatting.html`/`.ts`) drive `data-tab-style` (`underline`/`pills`/`angled`) and
  `data-chat-density` (`dense`/`breathable`) via new per-profile keys `style.chat-frame.tab-style` /
  `style.chat-frame.density` (defaults `underline`/`dense`). `gobchat.ts` mirrors them onto
  `<html class="chat-frame">` through `bindCallback`, so changes apply live. Old profiles auto-migrate:
  schema **version bumped to 20005** with `ConfigUpgrade_2_0_5` seeding both keys.
- ~~**Modern light variant**~~ — **DONE.** Registered a second `styles.json` entry **"FFXIV Modern
  Light"** that reuses `ffxiv_modern_chat.css`. Rather than duplicate the light tokens in a
  `:root`-override CSS, the overlay now toggles the existing `html.theme-light` class from the theme
  label (`/light/i.test`) inside the `style.theme` callback in `gobchat.ts` — the mechanism the SCSS
  comment already documented. The settings window picks light mode via `applyThemeMode` (label contains
  "Light"). The legacy "FFXIV Light" still covers light mode for the non-modern themes.
- ~~**Chat background from theme + transparency slider**~~ — **DONE.** The chat background **colour** now
  comes from the theme per mode (`--gob-chat_background`: dark `#101318` / light `#faf7f1`), with an
  optional opaque per-profile override (`style.chat-history.background-color`, default **null**, kept in
  the separate `--gob-chat_background-custom` property so a light theme's higher-specificity token can't
  out-rank it) and a separate **transparency** slider (`style.chat-history.background-opacity`, 0-100,
  default **90**). The surface composes them with `color-mix` in `ffxiv_modern_chat.scss`; the generator
  in `Style.ts` emits `--gob-chat_opacity` + (when set) `--gob-chat_background-custom` and no longer
  paints `.gob-chat_history`. Old profiles auto-migrate: schema **20006** / `ConfigUpgrade_2_0_6` nulls
  the saved colour and seeds opacity 90.

## Decorative / "fancy" chat text (Unicode + FFXIV PUA)

Players sometimes type styled letters (e.g. Mathematical Sans-Serif Bold `𝗙𝗟𝗨𝗫` instead of `FLUX`).
Two **distinct** representations were handled this session:

- ~~**Genuine astral math (U+1D400–U+1D7FF)**~~ — **DONE.** Display kept original; a NFKC-folded copy is
  used for **matching only** (`Core/Util/UnicodeNormalizer.cs` → exact/fuzzy mention replacers +
  `ChatMessageTriggerGroupSetter`). Rendering falls back per-glyph to the **bundled** `Noto Sans Math`
  (`lib/fonts/noto-sans-math/`), inserted into the chat font stack by `StyleBuilder.withMathFallback`.
  Note: WebView2 **cannot** resolve the installed `Cambria Math` by name (it's a sub-face inside
  `cambria.ttc`), so a bundled URL-loaded font is required.
- ~~**FFXIV Private Use Area (the actual in-game case)**~~ — **DONE.** FFXIV re-encodes pasted
  decorative letters into its own PUA glyph table **before** Gobchat reads memory: `U+E060–U+E08A`,
  contiguous with ASCII (boxed `A` = `U+E071` = `FFXIVUnicodes.Raid_A`). `ChatUtil.MapBoxedGlyphsToAscii`
  folds that block back to ASCII (`cp − 0xE030`), applied to the **message body** in
  `ChatManager.EnqueueMessage`. Display becomes readable plain text (no font can render PUA) and
  mentions/triggers match it.

**Follow-ups:**

- **Analyse [ChatTwo](https://github.com/Infiziert90/ChatTwo) — how it handles this.** Likely full
  **SeString / payload decoding** (Dalamud) rather than a single PUA range map. Could inform: handling
  **fancy *sender names*** (current `MapBoxedGlyphsToAscii` is **body-only**, so styled names still
  tofu), and other PUA glyph classes (gamepad buttons, item links, HQ icon, auto-translate) that are
  currently passed through or stripped.
- **Fancy sender names** — extend the PUA fold to `CharacterName` *after* raid/party/group marker
  parsing in `ChatMessageBuilder.SetMessageSource` (don't fold before, or marker detection breaks).
