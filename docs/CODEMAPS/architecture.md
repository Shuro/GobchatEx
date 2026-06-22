<!-- Generated: 2026-06-22 | Files scanned: ~260 | Token estimate: ~780 -->

# Architecture

GobchatEx — Windows-only FFXIV chat overlay for roleplayers. .NET 10 WinForms tray app
that reads chat/actor data from the game's process memory and renders chat in a WebView2
composition overlay (TypeScript/HTML/CSS UI). Fork of MarbleBag/Gobchat (AGPL-3.0);
rebrand is identity-only (namespaces stay `Gobchat.*`).

## Assemblies / project boundaries

C# projects live under `src/`; test projects under `tests/`.

```
Gobchat.App      (exe GobchatEx)      main app: modules, config, chat model, UI adapters
Gobchat.Memory                        FFXIV process attach + memory polling (on Sharlayan)
Gobchat.WebRenderer (asm Gobchat.UI)  WebView2 host: overlay forms, bridge, DirectComposition
Gobchat.LogConverter                  standalone chat-log converter tool
tests/Gobchat.App.Tests, .Memory.Tests (xUnit) ; tests/ui (Vitest)
```

Auto-update is in-app (no separate updater exe): `AppModuleUpdater` runs Velopack's
`UpdateManager`/`GithubSource` against `Shuro/GobchatEx` releases (the old `GobUpdater`
helper was removed in the Velopack migration).

## Runtime shape

```
MainEntry → GobchatApplicationContext.ApplicationStartupProcess
  → MigrateLegacyAppData (%AppData%\Gobchat → \GobchatEx)
  → ordered IApplicationModule pipeline (init in order, dispose in reverse)
Modules wired via DIContext (TinyIoC): each Requires interfaces + Provides one.
```

## Data flow (memory → screen)

```
FFXIV process memory
  └─ Gobchat.Memory (Sharlayan) → FFXIVMemoryReader (chatlog, actors, focus)
      └─ AppModuleMemoryReader  [IMemoryReaderManager]
          ├─ AppModuleActorManager [IActorManager]  (dedup, names, distance)
          └─ AppModuleChatManager  [IChatManager]   (format, mentions, range-filter, trigger groups)
              └─ AppModule*ToUI connectors  →  IBrowserAPIManager
                  └─ WebView2 (gobchat.localhost)  →  TS UI renders chat
```

## C# ↔ JS bridge (transport only; see backend.md)

- JS→C#: page calls `GobchatAPI.*` → postMessage JSON → `ManagedWebBrowser` reflects onto `GobchatBrowserAPI`.
- C#→JS: WebEvents serialized to JSON, dispatched as DOM CustomEvents via `ExecuteScriptAsync`.
- UI served from `https://gobchat.localhost` virtual host via `WebResourceRequested` handler.

## Overlays (WebView2 forms)

```
OverlayForm           composition-hosted, click-through, topmost  → chat overlay + system overlay
SettingsOverlayForm   borderless windowed WebView2 (native <select> works) → config dialog
```

## Config (see data.md)

JSON profiles via `GobchatConfigManager`. Two stores: per-profile (`default_profile.json`)
+ app-global instant settings (`default_appsettings.json`). Schema v20008; `ConfigUpgrader`
migrates old profiles. C# modules react via `AddPropertyChangeListener("dotted.path", …)`.

## Related codemaps
[backend.md](backend.md) · [frontend.md](frontend.md) · [data.md](data.md) · [dependencies.md](dependencies.md)
