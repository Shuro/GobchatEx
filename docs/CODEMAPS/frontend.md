<!-- Generated: 2026-06-22 | Files scanned: ~30 | Token estimate: ~880 -->

# Frontend (TypeScript UI)

Lives in [src/Gobchat.App/resources/ui/](../../src/Gobchat.App/resources/ui/). Compiled by
`Microsoft.TypeScript.MSBuild` (ES2021, strict); `.js` emitted next to `.ts`. Served from
`https://gobchat.localhost`. Pre-TS layer in `gobchat/*.js`. jQuery + lodash + Coloris globals.

## Pages (HTML + paired TS entry)

```
gobchat.html  + gobchat.ts   — the chat overlay (sets up globals, ChatControl)
system.html?  + system.ts    — system overlay: greeter + login/logout notifications
config/config_app.html + config/config.ts — settings dialog shell (windowed WebView2)
```

## Config sub-pages ([config/](../../src/Gobchat.App/resources/ui/config/))

```
config.ts            dialog bootstrap: tabs, save/synchronize, unsaved-change control
config_app.ts        App page — language, theme, toggles, hotkey, intervals (→ app settings store)
config_formatting.ts Formatting — fonts, colours, chat-overlay window, search box
config_channel.ts    Channels — channel→colour scheme (classic/modern)
config_mentions.ts    Player Mentions — per-character list; whole-word + opt-in fuzzy / partial-name / Miqo'te switches
config_groups.ts     Trigger groups editor (native HTML5 drag-reorder)
config_rangefilter.ts Range filter — distance visibility fade
config_chatlog.ts    Chat log writing options
config_tabs.ts       Chat tab definitions
config_profiles.ts   Profile create/switch/import/export
```

## Shared modules ([modules/](../../src/Gobchat.App/resources/ui/modules/))

```
Chat.ts          ChatControl: render messages, scroll, search, mention/segment styling
Config.ts        GobchatConfig: per-profile config proxy, property listeners, sync to C#
AppConfig.ts     AppConfig: app-global instant settings (gobAppConfig, setAppSetting)
Databinding.ts   two-way DOM↔config binding helpers
Style.ts         StyleLoader (gobStyles): theme CSS load/activate (activateStyles)
Locale.ts        LocaleManager (gobLocale): localized strings via getLocalizedStrings
EventDispatcher.ts dispatch/subscribe layer for incoming C# DOM CustomEvents
Components.ts / WebComponents.ts  reusable UI widgets / custom elements
Dialog.ts        modal/dialog helpers
MenuNavigationComponent.ts  config page nav menu
ProfileControl.ts  profile management UI logic
CommonUtility.ts / Constants.ts / Command.ts / GobModule.ts / JQueryExtensions.ts  helpers
```

## Globals / state ([globals.d.ts](../../src/Gobchat.App/resources/ui/globals.d.ts))

State lives on backend-injected globals, set up in `gobchat.ts`:
```
gobConfig       GobchatConfig    per-profile config (synced JSON blob ↔ C#)
gobAppConfig    AppConfig        app-global instant settings
gobChatManager  ChatControl      overlay chat rendering
gobStyles       StyleLoader      theme registry/activation
gobLocale       LocaleManager    i18n
Gobchat.*       injected enums/data: Channels, ChannelEnum, DefaultProfileConfig,
                AppConfig, FFXIVModernColors, KeyCodeToKeyEnum
GobchatAPI.*    JS→C# bridge namespace (see backend.md)
```

## C# → JS event reception

C# dispatches DOM `CustomEvent`s on `window`; UI listens (via EventDispatcher.ts / direct
`addEventListener`). Typed in `globals.d.ts` `WindowEventMap`:
```
ChatMessagesEvent      → { messages: ChatMessage[] }       (gobChatManager renders)
OverlayStateUpdateEvent→ { isLocked }                       (lock/move overlay state)
SynchronizeConfigEvent → resync config blob from C#
ConnectionStateEvent   → { state, player, greeterText, notify* }  (system overlay)
```

## Styles / themes ([styles/](../../src/Gobchat.App/resources/ui/styles/))

`styles.json` registers themes → CSS files. SCSS compiled by DartSassBuilder on build.
```
FFXIV Modern / FFXIV Modern Light → ffxiv_modern_chat.css  (default; ffxiv_modern_chat.scss)
```
`gobStyles.activateStyles(theme)` swaps the active theme CSS; overlay reads
`style.chat-frame.tab-style` / `density` into `data-*` attributes.

## Related
[architecture.md](architecture.md) · [backend.md](backend.md) · [data.md](data.md)
