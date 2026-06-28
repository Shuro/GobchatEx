# Changelog
All notable changes to this project will be documented in this file.
The format is based on [Keep a Changelog](https://keepachangelog.com)

## [2.0.1] - 2026.06.27
### Fixed
- When **Final Fantasy XIV runs as administrator**, GobchatEx now reliably offers to restart with administrator rights, instead of occasionally skipping the prompt and failing to connect.
- Quitting GobchatEx no longer writes a harmless stray error to the log.

## [2.0.0] - 2026.06.13
### Added
- New **FFXIV Modern** chat-overlay theme — a layered dark look with a single gold accent, matching the redesigned settings window. It is now the **default** theme, with a light version, **FFXIV Modern Light**, also selectable under *Settings → App → Theme*. Its background colour, base text colour and search-highlight accent are the new defaults; per-channel message colours are unchanged. Existing profiles still on the previous defaults are moved to the new look automatically on first start; a theme or colours you changed yourself are left as they are.
- An importable **FFXIV Modern (colours)** profile that retunes the per-channel message colours to the new palette. Import it from *Settings → Profiles → Import profile* (it ships in the install's `resources\profiles\` folder, where the import dialog opens).
- **Player Mentions** on the *Mentions* page: turn on *Player Name Mentions* and GobchatEx remembers each character you log in as. Every character gets its own entry where you choose whether its **full name**, **first name** and/or **last name** highlights messages, plus a list of **extra words** that only apply while you are logged in as that character. A character's mentions only fire while you are actually playing it; the trash icon removes a character you are not currently logged in as.
- Per-character **Fuzzy mention** for Player Mentions (off by default): also highlights typo'd spellings of that character's names — missing/extra/swapped letters and dropped apostrophes. A **Conservative / Balanced / Aggressive** strength picker (default *Conservative*) controls how forgiving it is; very short names always match exactly to avoid noise, and Global Mentions are never fuzzy.
- More per-character Player Mention switches (all off by default): **Partial first name** and **Partial Surname** highlight that name even inside a longer word (e.g. *John* → *Johntastic*, *Mediocrejohn*; *Gobchat* → *Gobchatting*), highlighting just the matching part; and **Miqo'te mode**, which — for a forename with an apostrophe — also matches the main part of the name (e.g. *A'nabelle* → *Nabelle*, *Kiht'to* → *Kiht*). Leaving them off keeps the existing whole-word matching.
- A configurable **Focus search** global hotkey (*Settings → App → Hotkeys*): one keypress brings the chat overlay to the front and opens its search bar with the cursor already in the search field, so you can search your backlog without first making the overlay interactive and clicking around. The overlay's click-through/lock state is left unchanged.
- **Tab style** and **Chat density** pickers on the *Formatting* page (FFXIV Modern theme): pick the chat-tab look (**Underline** / **Pills** / **Angled**) and the chat-line spacing in four steps (**Dense+** / **Dense** / **Breathable** / **Breathable+**). Changes apply to the overlay live and are saved per profile. Density now owns line spacing (it adjusts each line's padding and leading), replacing the old per-pixel *Gap between entries* control.
- An **Indentation style** picker on the *Formatting* page (FFXIV Modern theme): choose how a chat line that wraps onto a second line is indented — **Full** (the continuation starts at the left edge, as before), **Timestamp** (it lines up just after the `[time]`), or **Character** (it lines up after the `[time] Name:`), so each speaker's lines form a clean block. Changes apply to the overlay live and are saved per profile; in a tab that hides timestamps, *Timestamp* looks the same as *Full*.
- **Autodetect emotes in party channel** on the *Formatting* page: the same emote autodetection that exists for Say is now available for the **Party** channel as a separate toggle beside it (off by default). When direct speech is marked in party chat, the rest of the line is flagged as emote.
- **Reorder remembered characters** in *Player Mentions*: each character entry now has up/down arrows next to its trash icon to move it within the list.
- A live **Example** line under the chat-log **format** box on the *Chatlog* page: it shows a sample log line built from the current format string and updates as you switch presets or hand-edit a Custom format, so you can see what the cryptic `{tokens}` produce without saving and tailing the file.
- A **Character folders** toggle on the *Logs* page (on by default): each character's chat logs are written into their own subfolder, e.g. `log\John Gobchat\`. Toggling it while logging moves the current log file into or out of the subfolder, so the active session stays in one continuous file.
- A **Preview** bar on the *Range Filter* page that illustrates how messages fade with distance for the current cutoff / fade-out / opacity values — nearer messages stay solid, distant ones fade and vanish past the cutoff (an illustrative guide; the overlay fades in discrete steps). It carries example player dots you can hover to see a sample emote at that distance's opacity, and a **Show nearby players** button drops a one-time snapshot of the people actually around you (with their exact ranges) onto the bar so you can see how the current filter would treat them. The *Say* and *Emote* channel rows also carry an info note that the game itself only delivers those from players within ~20 yalm (horizontal), so a larger cutoff has no extra effect there.
- A **settings search** box in the navigation rail: type part of a setting's name to filter the page list and jump straight to that setting, which is briefly highlighted. Works in both languages.
- Settings now **reopens on the tab you last had open** during this session — switch to *Formatting*, close, and it comes back on *Formatting* instead of *App*. It still starts on *App* after restarting GobchatEx (the remembered tab is per session, not saved to disk).
- A **Check for updates** button on the *About* page: GobchatEx normally only checks at startup, so this lets you check on demand without restarting. On an installed build it runs the same update prompt as startup (and can apply the update and restart); it works even when *Check for updates on start* is turned off. It shows a short status next to the button — *You're up to date*, *Couldn't check for updates*, or that the releases page was opened — and is disabled while a check is running (clicking again while one is in progress is a safe no-op).
- A **first-time setup screen** shown once on first launch, before the greeter: choose your **Language**, **Theme** and whether to **automatically check for updates**, and — when an existing `%AppData%\Gobchat` install is found — whether to **import your existing Gobchat profiles**. Your choices take effect on that first launch, and the screen does not appear again afterwards.
- A **close (X) button** on the startup greeter (the splash shown while GobchatEx is waiting for / can't reach Final Fantasy XIV): clicking it quits GobchatEx, so you can exit straight from the greeter without going to the tray icon. While the greeter is shown the splash is clickable; clicks elsewhere on the screen still pass through to the game.
- More chat **font** choices on the *Formatting* page: the dyslexia-friendly **OpenDyslexic** plus **Noto Sans**, **Lexend**, **Verdana** and **Cambria**, alongside the existing IBM Plex Sans/Mono and system options. Each entry in the dropdown is now drawn **in its own font**, so you can preview the look before picking one. Changing the chat font also re-fonts the **settings window** itself.
- **Hide individual chat lines**: right-click any line in the overlay and pick **Hide Entry** to remove it from view (right-click again for **Un-hide**). A new **closed-eye** toolbar button — between the cog and search icons — opens to reveal every hidden line dimmed so you can un-hide them, then closes to tuck them away again. Hiding lasts for the current session only (the overlay's history clears on reload/restart), and the menu/button work while the overlay is interactive, like the other toolbar buttons.
- **Add players to a custom group from chat**: the same right-click menu now has **Add Player to Custom Group** (a sub-menu listing your custom groups, with **Create new group…** at the end — which makes a new group named after that player) and **Remove Player from Custom Group** (listing only the groups that player is already in, greyed out when there are none). This is the quick way to start highlighting someone you just saw speak, without opening *Settings → Groups* and typing their name. The change is saved to your profile, so a group's colours apply to that player's lines straight away — and if the settings window happens to be open, its *Groups* page updates to match instead of overwriting the change on the next save.
- A redesigned **tray icon** — a letter **"G"** — that now shows three states at a glance: a **gold "G"** when GobchatEx is connected to FFXIV and the chat overlay is on screen, a **black "G" with a gold outline** when it's connected but the chat is hidden (e.g. at the title screen, or auto-hidden because another window is focused), and a plain **black "G"** when it isn't connected to Final Fantasy XIV.
- A **`/e gc help`** in-game chat command that lists every GobchatEx chat command with its usage, plus clearer error replies: an unknown or mistyped `/e gc` command now answers with a short message pointing you at `/e gc help` instead of a bare list of command names. The existing commands (`group`, `profile load`, `player`, `config`, `info`/`error`, `close`) keep working exactly as before — they are now handled natively inside GobchatEx.
- An **Always show the chat overlay** toggle on the *App* page (with a matching tray-menu item): keep the chat overlay on screen even when no character is logged in, instead of it appearing on login and hiding at the title screen or between logins. Like the other *App* settings it applies to GobchatEx as a whole and takes effect immediately.
- **Reorder your profiles** on the *Profiles* page with per-row up/down arrows. The active-profile dropdown and the Profiles list now follow the same order, so they can no longer disagree (the dropdown used to sort alphabetically while the list kept insertion order). **Creating, cloning or importing** a profile now saves it straight away, so a freshly made profile is no longer lost when you next switch profiles.

### Fixed
- The startup **greeter** no longer hangs on *"Searching for FFXIV process…"* for up to two minutes when **Final Fantasy XIV is running as administrator**. GobchatEx now detects the elevated game right away and offers to restart with administrator rights, instead of stalling on a failed memory scan first.
- The startup **greeter** now correctly reads **GobchatEx** (it briefly showed *GobchatEX*).
- Decorative Unicode **"math" letters** — the Mathematical Alphanumeric Symbols some people type for emphasis (e.g. `𝗙𝗟𝗨𝗫` instead of `FLUX`) — no longer render as empty boxes (□) in chat: they now fall back to a font that has those glyphs. They are also folded back to plain letters for **matching only**, so **mention/keyword** and **trigger-group** rules written normally still fire on names and words typed in those characters. The chat still shows the original styled text.
- **Auto-hide** (*Settings → App*, formerly "Hide when FF is minimized") now works in borderless-windowed mode. It no longer waits for the game to be *minimized* (which never happens in borderless windowed mode) — instead the overlay hides whenever you switch to another application and reappears as soon as FFXIV, or GobchatEx itself (e.g. its settings window), is the active window again.
- The **Windows taskbar** now appears normally when you alt-tab from FFXIV to another application (e.g. Explorer) while GobchatEx is running. The overlay no longer covers the whole screen edge-to-edge, which the Windows shell had mistaken for a fullscreen app and used to keep the taskbar hidden the entire session. The overlay still floats over the game exactly as before.

### Changed
- Removed the **Collect information about character locations** toggle from *Settings → App*. GobchatEx now reads nearby players from the game only while the **Range Filter** is actually enabled on a tab, and reads your own character (for login detection, chat logging and highlighting your own messages) at all times. The Range Filter no longer needs a second switch turned on as well, and turning it off no longer disables those unrelated features. The *"character locations available / not available"* indicator moved from the *App* page to the *Range Filter* page.
- *Trigger words* on the *Mentions* page is now **Global Mentions** and is edited as removable **tags/chips** (type a word and press *Enter* or comma; duplicates are ignored) instead of a comma-separated text box. The words and how they match are unchanged.
- The **members of a custom group** on the *Groups* page are now edited as removable **chips** too (type a name and press *Enter*; click the × to remove), replacing the comma-separated text box. Only **full character names** are accepted — a first and last name (apostrophes allowed, e.g. *Y'shtola Rhul*); anything else is rejected and the box is flagged until you fix it. The **server is ignored** for grouping: enter just the player's name and it matches them whether they're on your world or another, so the old `Name [Server]` suffix is no longer needed (names already saved with one still work).
- **Premade groups are now separate from custom groups** on the *Groups* page. The 7 built-in FFXIV friend-list symbol groups (★●▲♦♥♠♣) are shown as a locked reference numbered 1-7, while your own **custom groups** are listed separately — numbered 1, 2, 3 … and **reorderable** with up/down arrow buttons (replacing the old drag-to-reorder). `/e gc group <n>` and the on-page numbering now address your custom groups only (premade groups are never touched by the command), so `/e gc group 1 add …` edits your *first custom group*, and a new **`/e gc group list`** prints your custom groups with their numbers. The group number and the action can be given in either order — `/e gc group add 1 …` now works as well as `/e gc group 1 add …`. When a player is in both a custom group and a premade (symbol) group, the **custom group's colour now takes precedence**.
- Rebranded to **GobchatEx**, a fork of [Gobchat](https://github.com/MarbleBag/Gobchat) by MarbleBag (AGPL-3.0)
- Migrated to .NET 10 (Windows, x64)
- Updated to the upstream Sharlayan 9.0.39 memory library
- User data now lives in `%AppData%\GobchatEx`. On first launch the new setup screen offers to import an existing `%AppData%\Gobchat` install; when you accept, the original folder is copied over and left untouched.
- Auto-updates now come from the GobchatEx repository (github.com/Shuro/GobchatEx)
- The overlay now renders through the OS **Microsoft Edge WebView2** runtime instead of a bundled Chromium (CEF). This removes the one-time ~250 MB browser-engine download on first start and keeps the browser engine patched by Windows. WebView2 ships with current Windows 10/11; if it is missing GobchatEx points you to the installer on startup.
- Click-through is now a **lock/unlock toggle** in the tray menu ("Click-through"). WebView2 has no per-pixel hit-testing, so the whole overlay is either interactive (catches the mouse) or passive (clicks pass through to the game), rather than the old automatic passthrough of transparent areas.
- The chat overlay's toolbar **pin** button now **locks/unlocks the overlay for moving and resizing** (replacing the old hold-*Ctrl* drag): unlock to drag it by the toolbar — the cog/search/pin icons stay clickable — or resize from the edges; the new position and size are saved automatically. The old "keep the overlay visible while logged out" function of the pin now lives only on the tray icon / tray "Pin" menu.
- **Clicking the tray icon now opens settings** — a single left-click on the tray icon opens the settings window (its default action) instead of toggling the overlay pin. Right-click still opens the tray menu, where *Pin overlay* and the other actions remain.
- GobchatEx no longer always asks for administrator rights on launch, so there is no UAC prompt during normal startup. Administrator rights are only needed when FFXIV itself runs as administrator; in that case GobchatEx now detects it and offers to restart elevated with a single click.
- The settings window now opens **already rendered** instead of flashing an empty frame first, and clicking the overlay's cog while settings is already open **brings that window to the front** (restoring it if it was minimized) rather than doing nothing.
- Closing, cancelling or switching the active profile in settings now only warns about losing changes when there actually **are** unsaved changes — no more nagging when you only looked.
- The *Channels* page is now **Channel-Colors** and has a **Classic / Modern** text-colour switch. Picking a scheme recolours every channel's text in one click (sender and background colours are left alone) and is remembered in your profile; a per-field reset then returns that field to the selected scheme's colour. Empty colour fields now read *Default* instead of looking blank.
- The chat **font** on the *Formatting* page is now a dropdown of curated choices; the default is **IBM Plex Sans** (matching the Modern theme). A custom font from an older profile is kept and shown as *Custom*.
- The default chat **text size** is now **14px** (the chat **tabs** stay 16px). Existing profiles keep whatever size you have set.
- The chat **background colour** now comes from the selected theme (so *FFXIV Modern* and *FFXIV Modern Light* each look right out of the box). The *Chat background color* field on the *App* page is now an optional **override** — leave it empty to use the theme's colour. Transparency moved out of the colour field into its own **Background opacity** slider (default 90%). Existing profiles are migrated automatically: any saved chat-background colour is cleared and the opacity is set to 90%.
- The *About* page was redesigned to match the new settings look, with **GitHub** and **Licence** buttons that open in your default browser.
- The *App* settings page is now **application-global**: Language, Theme, the behaviour toggles (auto-hide, update checks, character locations), the show/hide **hotkey** and the chat/actor **update intervals** are no longer stored per profile — they apply to GobchatEx as a whole, take effect **immediately** (no *Save* needed) and live in a separate `appsettings.json`. Your existing values are migrated automatically on first start. The **Chat Overlay Window** box (chat position/size + background colour/opacity) and the **Search** highlight colours — which stay per-profile — moved to the top of the *Formatting* page.
- The settings navigation is now grouped into labelled sections — **General** (App, Profiles, Logs), **Appearance** (Formatting, Colors) and **Chat** (Tabs, Mentions, Groups, Range filter) — instead of one flat list. The *Chatlog* page is renamed **Logs** and the *Chat tabs* page **Tabs**.
- The *Mentions* page boxes are reordered to **General settings → Global Mentions → Player Name Mentions**, the *NEW* badge moved from the player toggle up onto the *Player Name Mentions* heading, and *Play audio* now shows a short description.
- The chat-log **format** box on the *Chatlog* page is now greyed out unless **Custom format** is selected in the dropdown, so a preset can't be edited by accident.
- Only **one GobchatEx can run at a time**. Launching it again while it is already running now shows a short *"GobchatEx is already running."* notice and closes the second copy, instead of starting a second overlay that would fight the first for the screen and the game's chat.
- Turning **Auto-hide off** now brings a pinned or logged-in overlay back on screen, instead of leaving it stuck hidden until the next visibility change.
- The update screen's **patch notes** now render as readable plain text. The Keep-a-Changelog markdown is converted to clean headings, line breaks and bullets, instead of the raw `##` / `**` / `-` markers collapsing into one run-on paragraph.

### Fixed
- The search **results background** colour field on the *App* page wouldn't open the colour picker; it works now (the highlight still overrides channel colours as before).
- Dragging the chat overlay mostly **off-screen** (for example into the top-left corner) could crash GobchatEx; window-procedure faults are now caught and logged instead of terminating the app.
- The **Tab style** and **Chat density** buttons on the *Formatting* page sometimes showed nothing selected when the page opened; the active choice is now always highlighted.
- The two **Hotkeys** fields on the *App* page no longer misalign when one field's revert button is hidden.
- Settings sub-pages tall enough to need a scrollbar no longer shift their content sideways compared to shorter pages.
- The **Export profile** button on the *Profiles* page now shows a tooltip.
- **Overwriting a profile** with another (the *Copy* action) no longer lists the profile being overwritten as a possible copy source.
- The first-time setup screen's **import your existing Gobchat profiles** option had no effect — the box was read after the screen had already closed, so the old `%AppData%\Gobchat` profiles were never copied. They are now imported as intended.
- The tray menu showed a **doubled separator line** above *Close* in release builds; it now shows a single divider.
- The first-time setup screen's **GobchatEx** wordmark now reads as one word (it had a gap between *Gobchat* and *Ex*).
- The chat overlay no longer ends up **hidden behind FFXIV after you alt-tab back** into the game. It now re-asserts itself as topmost whenever the game returns to the foreground, so the chat stays in front.

### Removed
- The legacy **FFXIV Dark** and **FFXIV Light** themes. They are superseded by FFXIV Modern / FFXIV Modern Light and no longer fit the theme-driven overlay background, so they have been retired. A profile still set to one of them is moved automatically — *FFXIV Dark → FFXIV Modern*, *FFXIV Light → FFXIV Modern Light* — on first start.
- The unused *Config font size* control on the *App* page (it had no effect on the redesigned settings window).
- The **Restore defaults** and **Override this profile with another** buttons on the *Profiles* page. Reordering, cloning and importing now cover the same needs without the risk of silently overwriting a profile's settings.

## [1.12.4] - 2025.08.07
### Fixed
- Rangefilter
- Mention checkbox in `Config / Chat tabs`

## [1.12.3] - 2025.04.03
### Fixed
- `Check own messages for mentions` in party chat
- Rangefilter should work again with 7.2

### Removed
- Exclude Mentions from Rangefilter checkbox in `Config / Rangefilter`
  - This was the same checkbox as in `Config / Mentions` 

## [1.12.2] - 2023.07.09
### Fixed
- Blink animation on Tabs

- Broken chat if you have an empty group

## [1.12.1] - 2023.05.04
### Fixed
- chat formatting for say and emote

## [1.12.0] - 2023.05.03
### Added
- Font size
  - There is now a wider range of sizes to choose from - or just enter whatever number you like


- Customizable gap between chat messages


- Customizable chat UI size
  - UI scales with menu font size, independently of your font size for chat messages


- Config
  - UI scales automatically with the width of the dialog


- Previous messages are updated if group settings are changed


- Tabs can be filtered by groups


- Color pickers for channel specific senders


### Changed
- Some settings were split into their own tabs
  - Chat log
  - Range filter
  - Formatting

- Chat log
  - by default enabled

### Fixed
- Chat position and size
  - The values in config will now update if you move your chat around (hover with your mouse over the chat and press ctrl-key, you can drag and resize it now!)
  - It's now possible to enter negative values for multi-monitor setups

- Gobchat will now recognize more FF14 auto translated terms

## [1.11.4] - 2023.01.13
### Fixed
With 6.3 Gobchat was unable to identify player characters from the game. While no error message was shown, features like rangefilter and `Check own messages for mentions` were not working previously.

- Rangefilter

- Mentions


## [1.11.3] - 2022.08.03
### Added
- Chat log
  - A new keyword {sender-cha} which writes the sender name as seen in game. For example; tells will have their '>>', emote won't use colons, etc
  - Added a new predefined log format which will use {sender-cha}
  - {sender-cha} not supported by log converter


### Fixed
- Gobchat was unable to identify a character as the sender of a chat message, if said character had one or more hyphens in its name.


- Rangefilter
  - The filter should no longer ignore names with hyphens


- Mentions
  - `Check own messages for mentions` should work as expected for a character with hyphens in their name


## [1.11.2] - 2022.04.15
### Fixed
- Rangefilter
  - memory signature updated

## [1.11.1] - 2021.09.30
### Fixed
- An issue where the order of elements, if changed without adding or removing, was not saved
  - tabs, groups, etc.


- An issue where the chat log format was not saved to a new log file


- An issue where the reset button for channel colors didn't work

##  [1.11.0] - 2021.09.09
### Added
- Chat log
  - Change which channels are logged in `Config / Channel`
  - Customizable chat log format in `Config / App`
    - Details in readme under `Chat Log`


- LogConverter
  - Supports customizable chat log format `CCLv1`


- Message culling for 'outdated' messages is back
  - It uses FFXIV's time now


## [1.10.0] - 2021.04.14
### Added
- Chat command: `config open`
  - Opens the `Config` dialog


- Chat command: `config reset frame`
  - Resets the overlay position and size to default


- More localization (`Config`, `Chat commands`)


- Off-screen protection
  - If moved off-screen, Gobchat reverts back to its last position
  - If it's still off-screen, Gobchat resets position and size


- Tray icon
  - Button to reset overlay position and size to default

### Removed
- Message culling for 'outdated' messages
  - Gobchat no longer removes messages with an outdated timestamp (if your system clock goes ahead, every message was outdated, until the game caught up)
  - Needs to be tested

### Changed
- Chat command: `profile` changed to `profile load`


- `Config / App / Chat position and size`
  - Fields `X & Y` allow negative values

### Fixed
- Rangefilter
  - Now utilizes the whole opacity range from start to end

## [1.9.0] - 2020.09.08
### Added
- Hotkeys
  - Button to remove hotkeys


- Support for multi-boxing
  - By default Gobchat shows the chat of the first process of FFXIV it can find
  - It's possible to select a specific process of FFXIV in `Config / App / Multi-boxing`


- Chat log
  - The output folder of chat logs can be changed on a profile level in `Config / App`


- Config
  - Button to close Gobchat
    - Gobchat can now be closed via config, tray icon (right click) or chat command (/e gc close)


### Removed
- Limit of 10 chat entries per update
  - It no longer chokes on battle logs

### Fixed
- LogConverter

- Newly created profiles were not saved without changes to the config

## [1.8.0] - 2020.08.17
### Added
- Chat tabs
  - Configuration can be found in `Config / Chat tabs`
  - Supports any number of tabs.
    - To navigate all tabs either use the buttons on the side of the tab bar or a scroll wheel while hovering it (Remember: Gobchat needs focus to detect a scroll wheel)
  - A yellow dot marks the active tab
  - Inactive tabs change their appearance on new messages or mentions
     - Can be changed in `Config / Chat tabs`  


- Chat
  - Color selector for chat frame background color in `Config / App`


- More font sizes
  - Smaller, larger and very large


- More localization

### Changed
- Profile selection drop down shows its content sorted


- Rangefilter
  - Removed check box for activation in `Config / App`


- Config / chat tabs
  - Rows in the tab table can be clicked directly to open the tab config      


- Javascript dialogs
  - Replaced by proper html dialogs

### Fixed
- Roleplay formatting
  - In some cases colors weren't applied correctly


- FFXIV error messages
  - Will no longer show up with an empty colon at the front


- Chat position (X, Y) in `Config / App` will now accept 0 as a value


## [1.7.1] - 2020.07.18
### Fixed
- Groups
  - Delete button for custom groups did not work


- Updater
  - Download status text will now show the correct number of downloaded bytes
  - Archive will be deleted if an error occurs while unpacking to avoid the same error on restart due to a corrupted archive

## [1.7.0] - 2020.07.16
### Added
- Theme selection in `Config / App`
  - Available  themes are ffxiv dark / light and a kind of Gobchat legacy


- A checkbox in `Config / app` to (de)activate mention scanning in your own messages
  - If turned on, Gobchat will mark any mention it can find in your message
  - If turned off, Gobchat will not mark them, unless another user uses them.


- Chat commands: `info off`, `info on`
  - `info off` will suppress any Gobchat info until turned back on via `info on` or restart of Gobchat


- Chat commands: `error off`, `error on`
  - `error off` will suppress any Gobchat errors until turned back on via `error on` or restart of Gobchat


- Language selection dropdown in `Config / App`
  - Most of Gobchat is translated into german


- Tons of new tooltips

### Changed
- Merged channels which are used by NPCs to talk into a single channel

### Fixed
- An error on `Config / Roleplay` where `reset entries` stops working after one use
  - This bug also affected some other reset buttons

## [1.6.3] - 2020.06.05
### Added
- Color selection to channels: NPC dialog, animated emote, echo

### Fixed
- groups
  - color reset buttons didn't work
  - ffxiv specific groups had their colors wrong


- hotkey: hide & show
  - Previously set hotkey could become stuck on profile reset


- profiles
  - A newly created profile could not be immediately activated without pressing save in between
  - Profiles which were created and deleted in the same session were not correctly deleted from disk and were loaded again at the next start

## [1.6.2] - 2020.05.26
### Fixed
- Messages not shown
  - A message which ends on one or more letters of a multi-letter token for roleplay, like '((', could cause an exception which leads to Gobchat not displaying the message

## [1.6.1] - 2020.05.23
### Fixed
- Profiles were not loaded
  - Player who used 1.5.2 but not any beta version of 1.6.0 experienced problems with their profiles

## [1.6.0] - 2020.05.21
### Added
- `Character location updates`
  - Gobchat tries to get information about your character and nearby players.
  - This feature is optional and can be deactivated in `Config / App`


- Chat command: `player count`
  - Counts nearby players.
  - Requires `Character location updates`


- Chat command: `player list`
  - Lists nearby players and their distance to you.
  - Requires `Character location updates`


- Chat command: `player distance`
  - Shows distance (in yalms) to a player.
  - Requires `Character location updates`


- Range filter
  - Applies (or hides) messages from players depending on their distance to you. The affected channels can be changed in `Config / Channels`. Will only work for players which are nearby. By default, deactivated and can be activated in `Config / App`
  - Requires `Character location updates`


- ChatLog converter
  - Can be found in the root directory and can be used to convert old chatlogs into the new format

### Changed
- Chatlogs are now easier to read by splitting message header from content


- Your message will no longer be scanned for any mentions
  - Requires: `Character location updates`

## [1.5.2] - 2020.03.22
### Added
- A small border to the left side of the chat
- Profile import & export

### Fixed
- Some grammatical errors in labels and tool-tips
- Command 'group': A Name containing '-' will now be saved correctly
- Error on download of beta versions

## [1.5.1] - 2020.03.02
### Added
- Another button `Add new group` in `Config / Groups` at the bottom, which will attach a new group at the end
- `Textsearch`, it's now possible to search through the chat. Gobchat will highlight all entries which fit the search term and allows stepping through them.
- A new button to open `textsearch` at the top of the chat
- Input boxes to set position and size of the chat in `Config / App`
- A `Save & Exit` button in `Config`
- Color picker for `textsearch` in `Config / App`

### Changed
- `Add new group` in `Config / Groups` will now insert a new group at the beginning
- `Save` in `Config` will no longer close `Config`

### Fixed
- Gobchat can now distinguish between different beta releases by parsing the pre-release version

## [1.4.1] - 2020.02.19
### Fixed
- Gobchat should work again - FFXIV uses new control characters since patch 5.2

## [1.4.0] - 2020.02.03
### Added
- Chat command to close gobchat. Use: /e gc close
- Message on config save
- Profiles will be saved directly to disk on save now
- Gobchat now uses a wider array of characters to detect say and emote, they can be configured in the roleplay tab

### Fixed
- Hotkeys were not applied on change, only after restarting Gobchat
- Config dialog can only be opened once at the same time

## [1.3.1] - 2020.01.20
### Fixed
- Some typos
- Command group add / remove will now tell the user the result of the command


## [1.3.0] - 2020.01.10
### Added
- Sound on mention! The mention tab was extended and now includes settings to play a sound file
- Gobchat can now hide itself when FFXIV gets minimized

### Fixed
- On profile delete the profile file will also be deleted from the filesystem, so it won't be loaded again
- Profile names can be changed

## [1.2.1] - 2020.01.05
### Fixed
- Changed settings will be applied immediately again, just how it should be!

## [1.2.0] - 2020.01.05
### Bug
- It's currently necessary to start Gobchat with admin rights, otherwise it can't read FF chatlog.

### Added
- Profiles! It's now possible to have more settings per settings, so you can twink your twink
- Chat command to switch between profiles without open the settings. Try /e gc profile
- A checkbox which allows gobchat to be updated with pre-releases / beta-releases
- Gobchat will now communicate some of its problems with the user. Be nice
- Two new channels for Gobchat to report errors and inform the user about stuff
- Color selector for FFXIV ECHO and ERROR channel
- On using '/e gc' gobchat will now tell you which commands are available

## [1.1.0] - 2019.12.06
### Added
- Auto-Updater
- Field to set the frequency of chat updates in milliseconds (Experimental)
- Additional sections to the german readme

### Changed
- Chatlogs names are changed from yyyy_MM_dd_HH_mm to yyyy-MM-dd_HH-mm

### Fixed
- Lost messages when a high amount of messages from other players are received

## [1.0.0] - 2019.11.27
### Changed
- Gobchat is now a stand-alone application

### Added
- Checks for a new version of gobchat on start-up
- Tray Icon! It even can be clicked. Watch out!
- Support for ffxiv's autotranslate. (English)
- Chat History to file

## [0.2.2] - 2019.10.27
### Added
- Channel for npc dialogue

### Fixed
- Players didn't show up, when their last name contained an apostrophe/additional uppercase letter and gobchat didn't know the datacenter yet

## [0.2.1] - 2019.10.22
### Added
- Tooltips to some interactable fields
- Party 'Random / Roll / Dice' channel

### Changed
- Group delete requires a confirmation
- Delete buttons have now a proper icon
- Reset buttons have now a proper icon
- Merged all 'Random / Roll / Dice' channels into one

## [0.2.0] - 2019.10.18
### Added
- Player groups. It is now possible to make own groups, which get activated on certain players to style their message further
- Player groups are sorted. Only the first group that matches will be applied.
- Server name. If someone comes from another server, the overlay tries to display their server separate from their last name
- A chat command manager. To start a chat command use '/e gc'
- chat command 'group'. Allows to add, remove and clear player groups. Example: /e gc group 1 add firstname lastname [server] (Server is only needed if the player comes from a different server than you!)

### Fixed
- Checkboxes on the plugin control form have now the correct size

## [0.1.5] - 2019.10.11
### Added
- Channels for random rolls

### Fixed
- Automatic Stylesheet generation was not triggered on a fresh start with no settings
- No messages if mentions are empty
- Party chat should work again

## [0.1.4] - 2019.10.10
### Added
- Color can be changed for each (cross-world)-linkshell separately
- Friendlist Groups can now be colorized too!

## [0.1.3] - 2019.10.09
### Added
- Config button to overlay. On click it will open a config dialog
- Detection for 'ooc' comments. A ooc-comment starts with (( and ends with ))
- Detection for 'emote' in say. When quotation marks are used to mark speech in say, everything that's not enclosed in quotation marks will be seen as emote
- Mention config to config dialog
- Channel color selection to config dialog
- Channel visibility to config dialog
- Channel roleplay formatting to config dialog
- Basic font selection

### Changed
- Moved mention config from plugin to overlay

### Fixed
- Multiple whitespaces in chat are not removed anymore

## [0.1.2] - 2019.09.28
### Fixed
- Error on mentions, because of typo in javascript
- Autoscroll

## [0.1.1] - 2019.09.27
### Fixed
- Mentions should work better, even while enclosed by non-alphanumeric letters

## [0.1.0] - 2019.09.25
### Added
- A fixed roleplay format which will be applied to roleplay channels
- A fixed set of roleplay channels: Say (/s), Emote (/em), Yell (/y)
- A fixed color encoding for channels
- A scrollbar to overlay
- Ability to resize overlay
- Ability to set overlay to visible/hidden
- Ability to set overlay to activated/deactivated (If deactivated, the plugin does not process any input)
- Ability to set mentions
