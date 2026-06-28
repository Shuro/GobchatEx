<div align="center">

<img src="logo.svg" alt="GobchatEx logo" width="96" />

# GobchatEx — Design Reference

**The visual design language of GobchatEx — its surfaces, theme tokens, typography, colours and components.**

</div>

---

## About this document

This is a reference for *how GobchatEx looks*, derived from the shipped theme CSS
([`ffxiv_modern_chat.scss`](../src/Gobchat.App/resources/ui/styles/ffxiv_modern_chat.scss),
[`config.scss`](../src/Gobchat.App/resources/ui/styles/config.scss),
[`system.html`](../src/Gobchat.App/resources/ui/system.html)) and the default profile
([`default_profile.json`](../src/Gobchat.App/resources/default_profile.json)).

Everything below describes the **out-of-the-box appearance**: the **FFXIV Modern** theme
(dark mode) with default profile settings. Where the user can change a value, that is noted;
the value quoted is the default. Colour values are the literal tokens in source — this doc is
meant to be kept in sync with those files, not to replace them.

The three visual surfaces are:

| Surface | Window | Source |
|---|---|---|
| **Chat overlay** | transparent, click-through, composition-hosted, topmost | `ffxiv_modern_chat.scss` + `gobchat.html` |
| **Settings window** | borderless, draggable, windowed WebView2 | `config.scss` + `config/*.html` |
| **System overlay** | fullscreen, transparent, click-through (greeter + toasts) | `system.html` (self-contained `<style>`) |

All three deliberately share one look: **IBM Plex Sans**, a layered dark surface, and a single
**gold** accent. The system overlay duplicates a minimal token set inline so it can render with
zero dependencies before any module loads.

---

## 1. Design language

GobchatEx's house style is called **FFXIV Modern**. Its character:

- **Layered dark neutrals** — a near-black background with progressively lighter "surface"
  planes for cards, controls and popovers. A light variant inverts these to warm parchment tones.
- **A single warm gold accent** (`#e0a44e`) used for every active/selected/focused state —
  active tabs, toggled switches, focus rings, the pin button, primary buttons, the app glyph.
  There is intentionally no second brand colour competing with it.
- **Soft, generous rounding** (8–13px radii) and **low, wide shadows** for an app-like float.
- **Restraint on the chat surface**: the overlay theme never sets message *text* colour — that
  stays fully owned by the per-channel config so roleplay colour-coding is preserved. The theme
  only paints chrome, spacing and a barely-there zebra/hover wash.
- **Functional accents** beyond gold: cyan for informational/"DEV"/live-data markers, a muted
  red for danger/destructive actions, green for "OK"/success badges.

---

## 2. Design tokens

Both the overlay and the settings window are built on CSS custom properties (`--gob-*` for the
overlay, `--*` for the settings window). They describe the same palette under two naming schemes.

### 2.1 Colour palette — dark (default)

| Role | Settings token | Overlay token | Value |
|---|---|---|---|
| Window background | `--bg` | — | `#0e1014` |
| Surface (panels) | `--surface` | — | `#171a20` |
| Surface raised (cards) | `--surface-2` | — | `#1e2128` |
| Surface highest (controls/popovers) | `--surface-3` | `--gob-surface-3` | `#272b33` |
| Border | `--border` | `--gob-border` | `#2c303a` / `rgba(255,255,255,.085)` |
| Border (strong) | `--border-strong` | `--gob-border-strong` | `#3a3f4b` / `rgba(255,255,255,.16)` |
| Text (primary "ink") | `--ink` | `--gob-ink` | `#e8eaee` |
| Text (secondary) | `--ink-2` | `--gob-ink-2` | `#a0a7b4` |
| Text (muted) | `--ink-3` | `--gob-ink-3` | `#6b7280` |
| **Accent gold** | `--gold` | `--gob-gold` | `#e0a44e` |
| Accent gold (bright) | `--gold-2` | `--gob-gold-2` | `#f0c074` |
| Accent gold (soft wash) | `--gold-soft` | `--gob-gold-soft` | `rgba(224,164,78,.14)` / `rgba(224,164,78,.16)` |
| Info / live data | `--cyan` | — | `#5bb7d6` |
| Danger | `--danger` | — | `#e0645a` |
| Success | `--ok` | — | `#74c47a` |
| Overlay chat background | — | `--gob-chat_background` | `#101318` |

### 2.2 Colour palette — light

Activated by `<html data-theme="light">` (settings) / `<html class="theme-light">` (overlay).
Inverts to a warm-parchment scheme:

| Role | Value |
|---|---|
| Window background | `#e9e5db` |
| Surface | `#fbf9f4` |
| Text primary | `#2c2823` |
| Accent gold | `#b07d22` (bright `#c79b45`) |
| Overlay chat background | `#faf7f1` |

The light palette keeps the same structure — only hues and lightness change; gold stays the
single accent (darkened for contrast on light surfaces).

### 2.3 Radii & elevation

| Token | Value | Use |
|---|---|---|
| `--gob-radius` | `13px` | overlay frame, cards, dialogs, sections |
| `--gob-radius-sm` | `8px` | icon buttons, tabs (pill variant) |
| `--gob-radius-xs` | `6px` | search field, small inputs |
| Window shadow | `0 18px 44px -22px rgba(0,0,0,.62)` (overlay) / `0 28px 64px -18px rgba(0,0,0,.72)` (settings/dialogs) | the floating "lift" |

Controls in the settings window use 8–9px radii; the toggle switch and round elements use full
`999px` pills.

---

## 3. Typography

| Family | Where | Notes |
|---|---|---|
| **IBM Plex Sans** | default UI + chat font everywhere | vendored woff2, weights 400/500/600/700; no CDN |
| **IBM Plex Mono** | counters, hex colour values, format previews, code | weights 400/500/600 |
| Lexend, Noto Sans, OpenDyslexic | user-selectable chat fonts | bundled; OpenDyslexic for accessibility |
| Noto Sans Math | automatic per-glyph fallback | renders decorative "𝗳𝗮𝗻𝗰𝘆" Unicode math letters that chat fonts lack (would otherwise be tofu) |
| Segoe UI Black | the "G" app glyph only | in the settings title bar |

The font stack default is
`'IBM Plex Sans', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif`.
The settings window adopts the user's chosen chat font via `--gx-user-font`, so changing the
chat font re-fonts the whole settings page too. All surfaces enable `-webkit-font-smoothing:antialiased`.

### Default sizes (from the profile)

| Setting | Default |
|---|---|
| Chat UI font size (`style.chatui.font-size`) | **16px** |
| Chat message text size (`style.chat-history.font-size`) | **14px** |
| Settings window base size | 14px (titles 15–23px, labels 12–13.5px) |

---

## 4. Chat overlay

The overlay (`gobchat.html`) renders as a single floating frame `.gob-chat`: a 13px-rounded,
1px-bordered card with a low wide shadow, composed top-to-bottom as a flex column.

```
┌─────────────────────────────────────────────┐  ← .gob-chat (rounded, bordered, shadowed)
│ [⠿] Tab  Tab  Tab │ ⌕  ⚙  📌    ← top toolbar │  (border-bottom)
├─────────────────────────────────────────────┤
│                                             │
│   message history (transparent over frame)  │  ← .gob-chat_history (flex:1, scrolls)
│   zebra-striped, hover wash, config colours │
│                                             │
├─────────────────────────────────────────────┤
│ [ search… ]            ⌃ ⌄  3 / 11          │  ← search toolbar (toggled, border-top)
└─────────────────────────────────────────────┘
```

### 4.1 Frame surface & opacity

The frame colour and opacity are **two independent settings** composed with `color-mix`:

```
background: color-mix(in srgb,
              var(--gob-chat_background-custom, var(--gob-chat_background))   /* colour */
              var(--gob-chat_opacity, 90%),                                  /* opacity */
              transparent);
```

- **Colour** — theme default `#101318` (dark) / `#faf7f1` (light), overridable per profile
  (`style.chat-history.background-color`, default `null` = use theme).
- **Opacity** — default **90 %** (`style.chat-history.background-opacity`). Because the host
  window is a per-pixel-alpha composition surface, the remaining 10 % is genuinely transparent
  to the game behind it.

The history pane itself is forced transparent so the whole frame reads as one seamless surface.

### 4.2 Top toolbar

A single row (`.gob-chat-toolbar--top`) holding, left to right: an (unlock-only) **move grip**,
the **tab bar**, a thin separator, and the **icon buttons** (search ⌕, settings ⚙, pin 📌).

- Icon buttons are 1.85rem squares, transparent until hover (`--gob-hover` wash, text brightens
  to `--gob-ink`); active press nudges down 0.5px; disabled drops to 50 % opacity.
- The search toggle stays "lit" (gold-soft fill + gold text + inset gold ring) while the search
  bar is open.
- While the overlay is **unlocked** the whole top toolbar becomes the drag handle (`cursor:grab`
  → `grabbing`), and a gold move-grip glyph appears at the left.

### 4.3 Tabs

Tabs are 1.85rem-tall text buttons, weight 600, secondary-ink colour, brightening on hover.
The active-tab treatment has **three selectable styles** (`style.chat-frame.tab-style`):

| Variant | Active look | Default? |
|---|---|---|
| **Underline** | 2px gold underline (`inset 0 -2px 0`), no fill | **✓ default** |
| Pills | gold-soft rounded fill + gold text + inset gold ring | |
| Angled | angled (`clip-path`) tab with a raised surface + 2px gold top edge | |

**Mention / new-message effects** are preserved and recoloured to gold: a static gold-soft
overlay (level 1), a 2-second blinking gold wash (level 2, `@keyframes gob-chat-blink`), or a
green text highlight (`#7fd83f`, level 3). Defaults: new messages → effect 1, mentions → effect 2.

### 4.4 Message history

Each direct child is one message line. The theme sets **only spacing and a wash** — never text
colour:

- **Zebra**: even rows get `--gob-zebra` (`rgba(255,255,255,.022)`), applied via `:where()` at
  zero specificity so any explicit per-message colour (trigger-group highlight, search hit) always wins.
- **Hover**: `--gob-hover` wash on the whole row.

**Density** (`style.chat-frame.density`, four steps) tunes row padding + line-height:

| Density | Padding | Line-height |
|---|---|---|
| dense-plus | `0 .55rem` | 1.3 |
| **dense (default)** | `.1rem .55rem` | 1.42 |
| breathable | `.24rem .6rem` | 1.54 |
| breathable-plus | `.36rem .6rem` | 1.62 |

**Indentation** (`style.chat-frame.indentation`): `full` (default — wrapped lines return to the
left edge), `timestamp` (hang-indent past the time column), or `character` (hang past time + sender).

Scrollbars are thin, `--gob-border-strong` thumbs over a transparent track (10px wide, fully rounded).

### 4.5 Lock / unlock (move & resize)

The overlay is normally **locked** (click-through). The 📌 button toggles **unlock**
(`html.gob-document--resize`), which:

- adds a **1.5px gold ring** around the frame plus a deeper shadow ("lifted"),
- shows the move grip and makes the toolbar a drag handle,
- reveals **gold resize affordances**: thin mid-edge accent ticks on each side and chevron
  corner grips (drawn with layered linear-gradients pointing outward). These set the cursor;
  the host window performs the actual resize.

### 4.6 Hidden entries & reveal mode

Right-click "Hide Entry" removes a single line (`display:none`). A closed-eye toolbar toggle
enters **reveal mode** (`.gob-chat--reveal-hidden`), bringing hidden lines back **dimmed
(opacity .45) + italic** so they can be spotted and un-hidden. The eye icon swaps closed↔open
purely in CSS.

---

## 5. Settings window

A borderless, draggable, **windowed** WebView2 (windowed, not composition-hosted, so native
`<select>` popups work) titled **"GobchatEx Configuration"**. Default size **1200×880**. It is a
classic two-pane app shell.

```
┌──────────────────────────────────────────────────────────────┐
│ [G] GobchatEx Configuration            📌  ▢  ✕   ← title bar │
├───────────────┬──────────────────────────────────────────────┤
│ PROFILE  ▾    │  Page title                                   │
│ ⌕ search      │  short description                            │
│               │  ┌──────────────────────────────────────────┐ │
│ GENERAL       │  │  ⌗ EYEBROW                                │ │
│  ▸ App        │  │  Setting label        ……………………  [ ⬤ ]   │ │  ← rows w/ dividers
│  ▸ Profiles   │  │  Setting label        ……………………  [ ▾ ]   │ │
│  ▸ Chat Log   │  └──────────────────────────────────────────┘ │
│ APPEARANCE    │  ┌──────────────────────────────────────────┐ │
│  ▸ Formatting │  │  …more cards…                            │ │
│  ▸ Channels   │  └──────────────────────────────────────────┘ │
│ CHAT          │                                               │
│  ▸ Tabs …     │                                               │
│ ───────────   │                                               │
│  ▸ About      │                                               │
│  ▸ Debug DEV  │                                               │
│ [Save] [Exit] │                                               │
└───────────────┴──────────────────────────────────────────────┘
```

### 5.1 Title bar

A draggable region (`-webkit-app-region:drag`; buttons opt out). Left: the **app glyph** — a
30px rounded square with a 150° gold gradient and a black cap-height "G" (Segoe UI Black) — beside
a two-line title (`GobchatEx Configuration` + a muted subtitle). Right: window action icons
including a **Pin** (always-on-top) that lights gold when active, Minimize and Close.

> *Easter egg:* entering the Konami code floods the title bar with a seamlessly scrolling rainbow
> gradient (`@keyframes gob-rainbow`), and the About page has a faint per-glyph hint that lights gold as you type it.

### 5.2 Navigation rail

262px wide, own border. Top: a **profile selector** (small uppercase label + dropdown) and a
**search box** (filters all settings; results drop down as a popover, and jumping to a setting
flashes it gold via `@keyframes gxhit`). Then a scrolling list of nav entries grouped under
uppercase **section headers** (General / Appearance / Chat), a divider, then About + Debug.

- Nav entry: 13.5px, secondary ink, icon at 80 % opacity; hover → `--surface-3`.
- **Active entry**: gold-soft fill, gold-2 text, **inset 3px gold left bar**, icon fully gold.
- A small cyan-outlined **"DEV" badge** marks the Debug page.

Footer pins the action buttons: a gold-gradient **Save**, a ghost **Cancel**, and compact outline
**Apply** / **Exit**.

### 5.3 Content area

Centered max-840px column with a stable scrollbar gutter on both edges (so the column never shifts
between short and tall pages). Each page fades in (`@keyframes gxfade`, 6px rise). Page head =
23px bold title + 13.5px muted description.

Settings are grouped into **cards** (`.gx-section`: `--surface-2`, 1px border, 13px radius),
introduced by a tiny uppercase **eyebrow** label. Inside, settings are **rows** (`.gx-row`):
label (+ optional 12px muted description) on the left, control on the right, separated by 1px
dividers (last row borderless). Sub-rows indent 26px with a muted arrow.

### 5.4 Settings pages

| Group | Page | Content |
|---|---|---|
| General | **App** | language, theme, global toggles, hotkey, polling intervals (app-global instant settings) |
| | **Profiles** | profile cards — avatar tile, inline-editable name, per-card actions; reorder arrows |
| | **Chat Log** | written-log channels (checklist) + format box with a live mono preview |
| Appearance | **Formatting** | chat font/size, overlay background, tab-style, density, indentation, roleplay **segment** colours (collapsible token editor), search highlight |
| | **Channels** | per-channel sender/text/background colour grid + Classic/Modern scheme picker |
| Chat | **Tabs** | tab list (editable name, visible toggle, row actions, active row gold-soft) + per-tab channel toggles |
| | **Mentions** | global mention chips + per-character accordions with a 2×2 match-mode grid + fuzzy strength |
| | **Groups** | premade FF groups + custom groups, each a collapsible card with colour rows + reorder |
| | **Range Filter** | distance fade settings with an **interactive fade preview bar** (checkerboard + sample/live player dots with hover tooltips) |
| — | **About** | branding, links (`openExternalLink`), Konami hint |
| — | **Debug** `DEV` | dry-run injectors and diagnostics |

### 5.5 Controls

A complete custom control set, all sharing the gold focus treatment
(`:focus → border gold + 3px gold-soft glow`; keyboard `:focus-visible → 2px gold-2 outline`):

| Control | Look |
|---|---|
| **Toggle** (`.gx-tgl`) | 40×22 pill; off = surface-3 + grey knob; **on = solid gold track + black knob slid right** |
| **Checkbox** (`.gx-cbx`) | 18px rounded square; checked = gold fill + black tick |
| **Select** (`.gx-sel`) | surface-3, custom chevron, 8px radius |
| **Text / textarea** (`.gx-in`/`.gx-ta`) | surface-3, 8px radius; mono variant for codes |
| **Range slider** (`.gx-rng`) | 5px strong-border track + 16px gold thumb ringed in surface-2 |
| **Primary button** (`.gx-btn`) | 150° gold gradient, black text, brightens on hover |
| **Ghost** (`.gx-ghost`) | transparent + border, fills surface-3 on hover |
| **Danger** (`.gx-danger`) | transparent until hover → danger-soft fill + red text/border |
| **Icon button** (`.gx-icon`) | 36px square, surface-3 |
| **Colour field** (`.clr-field`) | Coloris swatch + mono hex; empty reads "Default"; border-colours show an outline-only swatch |
| **Tag/chip input** (`.gx-tags`) | gold-soft pill chips with a round remove ×; box turns red on an invalid entry |
| **Accordion** (`.gx-acc` / `.gx-group`) | rounded card, header chevron rotates 90° when open |
| **Segmented control** (`.gx-scheme`) | inset pill group; active segment = gold-soft fill |
| **Dialog** (`.gx-dialog`) | native `<dialog>`, surface-2, 13px radius, dimmed backdrop |

---

## 6. System overlay (greeter + notifications)

A fullscreen, transparent, click-through overlay on the primary monitor, with a **self-contained**
copy of the FFXIV Modern tokens (so it renders before any module/CSS loads).

- **Greeter** — a centered splash shown until GobchatEx connects to FFXIV: a `rgba(23,26,32,0.92)`
  rounded card with the **`Gobchat`*`Ex`*** wordmark (42px, the "Ex" in gold), a gold-topped
  spinner (`@keyframes gob-spin`), a live status line ("Searching FFXIV Process…"), and a top-right
  close ✕ that turns gold on hover. Fades out (0.35s) once connected.
- **Toasts** — brief top-right notifications: surface card with a **3px gold left border**, sliding
  in from the right (`translateX(24px)` → `0`, opacity fade).

---

## 7. Default channel & segment colours

The overlay theme leaves message text colour to config; these are the **default profile** values
(`style.channel.*.general.color`). They are FFXIV's classic channel palette:

| Channel | Colour | | Channel | Colour |
|---|---|---|---|---|
| Say | `#FFFFFF` | | Tell (receive) | `#A118BC` |
| Emote / Animated emote | `#F19212` | | Tell (send) | `#AA3DC0` |
| Yell / Shout | `#D1DE09` | | Linkshell 1–8 | `#03fc73` |
| Party | `#05f7ff` | | Cross-world LS 1–8 | `#03fc73` |
| Free Company (guild) | `#50DE09` | | Gobchat info | `#3660ff` |
| Alliance | `#ff5005` | | Gobchat error | `#cb0101` |
| Error | `#d40420` | | Base text | `#e8eaee` |

The channel **colour scheme** picker (`style.channel.colorscheme`) defaults to **`classic`**
(the values above); **`modern`** swaps in FFXIV's newer in-game palette via an injected
`Gobchat.FFXIVModernColors` map.

**Roleplay segment** highlight colours (`style.segment.*`), layered on top of channel text:

| Segment | Colour |
|---|---|
| Mention | `#9358E4` |
| OOC (`(( … ))`) | `#FF5920` |
| Link | `#FF0000` |
| Say / Emote | inherit the channel colour (`$fallback`) |

**Search** highlight: selected match ringed in `2px #e0a44e` (gold); other matches washed
`rgba(224,164,78,0.16)` — both tying back to the single gold accent.

---

## 8. Quick reference — changing the look

| To change… | Where (settings page) | Profile key |
|---|---|---|
| Light/dark theme | App | app-global theme |
| Overlay transparency | Formatting | `style.chat-history.background-opacity` (90) |
| Overlay background colour | Formatting | `style.chat-history.background-color` (null = theme) |
| Chat font / size | Formatting | `style.channel.base.general.font-family`, `style.chatui.font-size` (16px) |
| Tab style | Formatting | `style.chat-frame.tab-style` (underline) |
| Row density | Formatting | `style.chat-frame.density` (dense) |
| Indentation | Formatting | `style.chat-frame.indentation` (full) |
| Per-channel colours | Channels | `style.channel.*` |
| Channel colour scheme | Channels | `style.channel.colorscheme` (classic) |

---

*Sources of truth: [`ffxiv_modern_chat.scss`](../src/Gobchat.App/resources/ui/styles/ffxiv_modern_chat.scss),
[`config.scss`](../src/Gobchat.App/resources/ui/styles/config.scss),
[`system.html`](../src/Gobchat.App/resources/ui/system.html),
[`default_profile.json`](../src/Gobchat.App/resources/default_profile.json). Update this doc when those change.*
