<!-- Generated: 2026-06-21 | Files scanned: ~170 | Token estimate: ~990 -->

# Backend (C#)

No HTTP routes. The "routes" of this app are the **module activation pipeline** and the
**JS↔C# bridge**. Modules live in `src/Gobchat.App/Module/*`; core model in `src/Gobchat.App/Core/*`.

## Module pipeline (init order = this list; dispose in reverse)

Source: [GobchatApplicationContext.cs](../../src/Gobchat.App/Core/Runtime/GobchatApplicationContext.cs#L86)
Format: `Module — Requires | Provides`

```
1  AppModuleConfig          — | IGobchatConfigManager
2  AppModuleLanguage        — | ILocaleManager
3  AppModuleUpdater         IConfigManager, IUIManager | UpdateService (Velopack self-update; registered unconditionally for the on-demand check)
4  AppModuleNotifyIcon      IUIManager, ILocaleManager | installs INotifyIconManager (tray)
5  AppModuleHotkeyManager   — | IHotkeyManager
6  AppModuleMemoryReader    IUISynchronizer | IMemoryReaderManager
7  AppModuleActorManager    IGobchatConfig, IMemoryReaderManager | IActorManager
8  AppModuleChatManager     IGobchatConfig, IMemoryReaderManager, IActorManager | IChatManager
9  AppModuleWebViewManager  IUISynchronizer | (owns WebViewManager / WebView2 env)
10 AppModuleChatOverlay     IUIManager, IConfigManager, IMemoryReaderManager, IActorManager, ILocaleManager | OverlayForm
11 AppModuleSystemOverlay   IUIManager, IMemoryReaderManager, IActorManager | OverlayForm (greeter/notify)
12 AppModuleBrowserAPIManager IUIManager | IBrowserAPIManager
13 AppModuleShowConnectionOnTrayIcon  IMemoryReaderManager, IUIManager |
14 AppModuleHideOnMinimize  IUIManager, IGobchatConfig, IMemoryReaderManager |
15 AppModuleChatLogger      IConfigManager, IChatManager, IActorManager | (writes chat logs)
16 AppModuleInformUserAboutMemoryState  IChatManager, IMemoryReaderManager |
17 AppModuleShowHideHotkey  IConfigManager, IHotkeyManager, IUIManager, IChatManager |
18 AppModuleSearchHotkey    IConfigManager, IHotkeyManager, IUIManager, IChatManager | (focus-search global hotkey)
19 AppModuleChatToUI        IBrowserAPIManager, IChatManager |
20 AppModuleConfigToUI      IBrowserAPIManager, IConfigManager, IChatManager |
21 AppModuleActorToUI       IBrowserAPIManager, IMemoryReaderManager, IActorManager |
22 AppModuleMemoryToUI      IBrowserAPIManager, IMemoryReaderManager, IActorManager |
23 AppModuleSystemToUI      IBrowserAPIManager, IUIManager |
24 AppModuleDryRunToUI      IBrowserAPIManager, IDryRunController, IChatManager (--dry-run only) |
25 AppModuleUpdaterToUI     IBrowserAPIManager, IConfigManager, UpdateService |
26 AppModuleLoadUI          IBrowserAPIManager, IUIManager, IConfigManager | (loads UI into OverlayForm)
```

## Chat pipeline internals

```
Gobchat.Memory/FFXIVMemoryReader → ChatlogReader/ChatlogBuilder (byte-tokenizer → IChatlogToken)
Core/Chat/ChatMessage  built by ChatManager applying, in order:
  ChatMessageActorDataSetter → GobchatChannelMapping → range-filter (distance fade)
  → ChatMessageSegmentFormatter (IReplacer: ReplaceTypeByText/ByToken) → mentions
  → ChatMessageTriggerGroupSetter → AutotranslateProvider (hjson)
```

## JS → C# bridge (postMessage, reflection)

[BrowserAPI.cs](../../src/Gobchat.App/Module/UI/BrowserAPI.cs) `GobchatBrowserAPI` dispatched by
[BrowserAPIManager.cs](../../src/Gobchat.App/Module/UI/Internal/BrowserAPIManager.cs) /
WebRenderer `ManagedWebBrowser` (camelCase→PascalCase, Newtonsoft result). Method groups:
- chat: `sendChatMessage`, `sendInfo/ErrorChatMessage`
- player/memory: `getPlayersNearby`, `getPlayerDistance`, `getCurrentPlayer`, `getAttachable/AttachedFFXIVProcess`, `attachToFFXIVProcess`
- config: `getConfigAsJson`, `synchronizeConfig`, `setConfigActiveProfile`, `importProfile`
- app settings: `getAppSettingsAsJson`, `setAppSetting` (instant store)
- window/overlay: `toggleOverlayLock`, `beginWindowDrag`, `reveal/focus/minimizeSettings`, `setSettingsAlwaysOnTop`
- updater: `checkForUpdates` (on-demand About-page check via `UpdateService`)
- dry-run (--dry-run only): `injectTestHarness`, `dryRunConnect/Disconnect`, `dryRunAdd/RemoveCharacter`, `dryRunSendMessage`
- files/misc: `open*Dialog`, `read/writeTextToFile`, `getSoundFiles`, `getLocalizedStrings`, `openExternalLink`, `closeGobchat`

## C# → JS web events (DOM CustomEvents)

`Module/UI/WebEvents/*` + `Core/Chat/Events/*` + WebRenderer `Web/JavascriptEvents/*`:
`ChatMessagesWebEvent`, `SynchronizeConfigWebEvent`, `SynchronizeAppConfigWebEvent`,
`ConnectionStateWebEvent`, `ShowNotificationWebEvent`, `ToggleGreeterWebEvent`,
`FocusSearchWebEvent`, `OverlayStateUpdateEvent`.

## WebRenderer host types ([src/Gobchat.WebRenderer/](../../src/Gobchat.WebRenderer/))

```
OverlayForm            transparent topmost click-through composition window (chat + system)
SettingsOverlayForm    borderless windowed WebView2 (config dialog, ctrl-movable)
ManagedWebBrowser      CoreWebView2 wrapper + postMessage bridge dispatch
WebViewManager         shared WebView2 environment/origin
JavascriptBuilder      builds JS event/init scripts
DirectComposition / CompositionMouseInput  DComp interop + click-through hit-testing
IBrowserAPI / IManagedWebBrowser           bridge contracts
```

## Related
[architecture.md](architecture.md) · [frontend.md](frontend.md) · [data.md](data.md)
