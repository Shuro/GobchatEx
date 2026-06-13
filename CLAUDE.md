# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

GobchatEx is an FFXIV chat overlay for roleplayers (Windows-only) — a fork of [MarbleBag/Gobchat](https://github.com/MarbleBag/Gobchat) (AGPL-3.0). It is a .NET 10 (Windows) WinForms tray application that reads chat and actor data from the running FFXIV process's memory (via the upstream [Sharlayan](https://www.nuget.org/packages/Sharlayan) NuGet package) and renders the chat in a CEF (CefSharp) offscreen browser overlay whose UI is written in TypeScript/HTML/CSS.

The rebrand is **identity-only**: the product and executable are `GobchatEx`, but C# namespaces, the JS `Gobchat`/`GobchatAPI` bridge, and the project/solution file names deliberately remain `Gobchat.*`. Auto-update and the CEF download point at `Shuro/GobchatEx`; user data lives under `%AppData%\GobchatEx`.

## Build

- SDK-style csproj files on `net10.0-windows` (x64) using `PackageReference`. Build with the .NET 10 SDK:

  ```sh
  dotnet build Gobchat.sln -c Debug
  ```

  Visual Studio works too. (This replaced the old non-SDK / `packages.config` / `msbuild` setup; that path is gone.)
- TypeScript in `Gobchat.App/resources/ui` is compiled by `Microsoft.TypeScript.MSBuild` as part of the Gobchat.App build (config: [tsconfig.json](Gobchat.App/resources/ui/tsconfig.json), target ES2021, strict). Emitted `.js` files sit next to their `.ts` sources.
- There are no automated tests and no CI; verification is manual (run `GobchatEx.exe`, which requires FFXIV for memory features). CEF binaries are not in the repo — they are downloaded on first app start by `AppModuleCefInstaller`.

## Release packaging

See [StepsToPackARelease.txt](Gobchat.App/StepsToPackARelease.txt). In short: set the version in `Gobchat.App/Properties/AssemblyInfo.cs` (the 4th version component > 0 marks a prerelease, producing `{major}.{minor}.{patch}-{n}`), build Release, then run [pack-release.ps1](Gobchat.App/pack-release.ps1) (requires 7-Zip at `C:\Program Files\7-Zip\7z.exe`). The script also swaps `NLog-Release.config` in as `NLog.config`. GitHub releases are titled `v{version}`; the in-app updater downloads the `gobchatex-{version}.zip` release asset.

## Solution layout

- **Gobchat.App** (exe `GobchatEx`) — main application; everything below lives here unless noted.
- **Gobchat.Memory** — FFXIV process attachment and memory polling (chat events, actors, window focus) on top of the upstream **Sharlayan** NuGet package (`9.0.39`; the previously vendored Sharlayan fork/project was removed). Memory signatures/structures are JSON files downloaded at runtime into `resources/sharlayan` (repo copies exist for dev).
- **Gobchat.WebRenderer** (assembly `Gobchat.UI`) — CefSharp offscreen browser, click-through overlay form, and the C#↔JS bridge plumbing (`IBrowserAPI`, `IManagedWebBrowser`, `JavascriptBuilder`).
- **GobUpdater** / **Gobchat.LogConverter** — auto-update helper and a standalone WinForms tool that converts written chat logs.

## Architecture

### Module system (C# side)

Startup is a linear module pipeline: `MainEntry` → [GobchatApplicationContext.cs](Gobchat.App/Core/Runtime/GobchatApplicationContext.cs), which initializes an ordered list of `IApplicationModule`s and disposes them in reverse order on shutdown. Modules live in `Gobchat.App/Module/*`; shared/core code (chat message model, config manager, runtime, util) lives in `Gobchat.App/Core/*`.

Modules communicate only through a `DIContext` (TinyIoC wrapper): each module resolves what it *Requires* and registers what it *Provides* — these contracts are documented in each module's class-level doc comment. Order in the activation list matters; a module's dependencies must be initialized before it.

Data flow: `AppModuleMemoryReader` (polls FFXIV memory) → `AppModuleActorManager` / `AppModuleChatManager` (builds `ChatMessage`s on a background worker, applies formatting/mentions/range-filter/trigger groups from config) → `AppModuleChatToUI` and the other `Module/UI/Connector/AppModule*ToUI` adapters, which push web events into the browser.

### C# ↔ JS bridge

- JS→C#: [GobchatBrowserAPI](Gobchat.App/Module/UI/BrowserAPI.cs) is exposed to the page as the global `GobchatAPI` object (registered through `AppModuleBrowserAPIManager`).
- C#→JS: events are serialized to JSON and dispatched as DOM custom events (`ChatMessagesWebEvent` etc., see `Module/UI/WebEvents` and `Gobchat.WebRenderer/Web/JavascriptEvents`).

### Web UI (TypeScript)

Lives in `Gobchat.App/resources/ui`. Entry points: `gobchat.html`/`gobchat.ts` (the overlay) and `config/` (the settings dialog). Shared code is in `modules/` (`Chat.ts`, `Config.ts`, `Databinding.ts`, `Style.ts`, `Locale.ts`, …); `gobchat/*.js` is the older pre-TypeScript layer. Globals (`gobConfig`, `gobLocale`, `gobStyles`, `gobChatManager`) are declared in `globals.d.ts` and set up in `gobchat.ts`. Themes are CSS in `resources/ui/styles`.

### Configuration

JSON-based profiles managed by `Core/Config/GobchatConfigManager`. `resources/default_profile.json` defines defaults; user profiles live in `AppData\Roaming\GobchatEx` (migrated once from the legacy `AppData\Roaming\Gobchat` folder on first start — see `GobchatContext.MigrateLegacyAppData`). The full config is synchronized as a JSON blob between C# and the JS `GobchatConfig`, and C# modules react via `AddPropertyChangeListener("dotted.property.path", ...)`. Schema changes require a `ConfigUpgrader` for old profiles.

### Localization

C# strings use `.resx` (`Resources.de.resx`, `WebUIResources.*.resx`); web UI strings load through the `Locale` module; FFXIV autotranslate data is in `resources/lang/autotranslate_*.hjson`.

## Conventions

- Every C#/TS source file carries the AGPL-3.0-only license header (MarbleBag copyright, preserved in the fork).
- Logging via NLog (`NLog.config` for dev, `NLog-Release.config` ships).
- User docs are `docs/README.md` / `docs/README_de.md` / `docs/CHANGELOG.md`; user-facing feature changes should be reflected there (both languages) and in the changelog.
