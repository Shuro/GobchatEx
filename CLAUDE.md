# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

GobchatEx is an FFXIV chat overlay for roleplayers (Windows-only) — a fork of [MarbleBag/Gobchat](https://github.com/MarbleBag/Gobchat) (AGPL-3.0). It is a .NET 10 (Windows) WinForms tray application that reads chat and actor data from the running FFXIV process's memory (via the upstream [Sharlayan](https://www.nuget.org/packages/Sharlayan) NuGet package) and renders the chat in a **WebView2** (Microsoft Edge/Chromium) overlay whose UI is written in TypeScript/HTML/CSS. The overlay uses WebView2 *composition hosting* (a transparent, topmost, `WS_EX_NOREDIRECTIONBITMAP` window with a DirectComposition tree) to float per-pixel-alpha chat over the game. (It previously bundled CefSharp/CEF; that was replaced to drop a ~250 MB download and to render through the OS-serviced runtime.)

The rebrand is **identity-only**: the product and executable are `GobchatEx`, but C# namespaces, the JS `Gobchat`/`GobchatAPI` bridge, and the project/solution file names deliberately remain `Gobchat.*`. (The WebView2 host types were renamed away from their CefSharp-era `Cef*`/`CEF*` names to neutral ones — `OverlayForm`, `WebViewManager`, `AppModuleWebViewManager` under `Module/Web/` — so nothing in the live code still says `Cef`.) Auto-update points at `Shuro/GobchatEx`; user data lives under `%AppData%\GobchatEx`.

## Build

- SDK-style csproj files on `net10.0-windows` (x64) using `PackageReference`. Build with the .NET 10 SDK:

  ```sh
  dotnet build Gobchat.sln -c Debug
  ```

  Visual Studio works too. (This replaced the old non-SDK / `packages.config` / `msbuild` setup; that path is gone.)
- TypeScript in `Gobchat.App/resources/ui` is compiled by `Microsoft.TypeScript.MSBuild` as part of the Gobchat.App build (config: [tsconfig.json](Gobchat.App/resources/ui/tsconfig.json), target ES2021, strict). Emitted `.js` files sit next to their `.ts` sources.
- A unit test suite covers the unit-testable logic (see **Testing** below); there is no CI yet, and broader verification is still manual (run `GobchatEx.exe`, which requires FFXIV for memory features). The overlay renders through the OS WebView2 Evergreen runtime (the build only ships the small `Microsoft.Web.WebView2.*` wrappers + `WebView2Loader.dll`); nothing is downloaded on first start.

## Testing

Two suites live under `tests/` (the C# ones are in `Gobchat.sln`). The suite is CI-ready (deterministic, no FFXIV/desktop dependency), but no CI pipeline exists yet.

- **C# (xUnit):** `tests/Gobchat.App.Tests` and `tests/Gobchat.Memory.Tests`. Run with `dotnet test Gobchat.sln`.
- **TypeScript (Vitest):** `tests/ui` (a self-contained Node project, deliberately **outside** `resources/ui` so its `node_modules` isn't swept into the app's `resources\**` content copy). Run with `cd tests/ui && npm install && npm test`.

Scope is the unit-testable business logic only — chat formatting/mentions, the range-filter visibility fade, channel mapping, `ConfigUpgrader`, `GobVersion`, actor dedup/name-normalization, the `Gobchat.Memory` chatlog byte-tokenizer and distance projection, and the pure TS UI helpers. The WebView2/WinForms UI, live FFXIV memory reading, and process/OS code are out of scope (manual/integration only).

- Internal types are reached via `[assembly: InternalsVisibleTo(...)]` added **manually** in each project's `Properties/AssemblyInfo.cs` — the `<InternalsVisibleTo>` MSBuild item is ignored because `Directory.Build.props` sets `GenerateAssemblyInfo=false`.
- Test package versions are pinned centrally in `Directory.Packages.props` (the repo uses central package management; test csprojs reference them versionless).
- Sharlayan exposes no mock/dump seam, so the memory layer is tested by feeding hand-built Sharlayan output DTOs (`ActorItem`, `ChatLogItem`, `Coordinate`) into the consuming code; tiny hand-rolled fakes stand in for interfaces (no mocking library).

## Release packaging

Releases are built with **Velopack** (`vpk`), pinned via the local tool manifest [.config/dotnet-tools.json](.config/dotnet-tools.json) to match the `Velopack` library version. See [StepsToPackARelease.txt](Gobchat.App/StepsToPackARelease.txt). In short: set the version in `Gobchat.App/Properties/AssemblyInfo.cs` (the 4th version component > 0 marks a prerelease, producing `{major}.{minor}.{patch}-{n}`), then run [pack-release.bat](pack-release.bat) (or [pack-release.ps1](pack-release.ps1)) from the repository root. The script `dotnet publish`es the app (Release, framework-dependent win-x64), swaps `NLog-Release.config` in as `NLog.config`, archives the `.pdb`s to `gobchatex-debug-{version}.zip` (vpk keeps symbols out of the package), then runs `vpk pack` into `.\Releases\`: `GobchatEx-win-Setup.exe` (the installer), `GobchatEx-{version}-full.nupkg` (+ a `delta` when a prior release was packed locally), `GobchatEx-win-Portable.zip`, and the manifest (`RELEASES` / `assets.win.json` / `releases.win.json`). GitHub releases are titled `v{version}`; **upload every file from `.\Releases\`** — the in-app updater (`AppModuleUpdater` via Velopack's `UpdateManager` + `GithubSource`) reads the manifest + `.nupkg` to download and atomically apply updates, and `Setup.exe` is for new users. Builds are currently **unsigned** (signing is a deferred one-line `--signTemplate`/`--azureTrustedSignFile` addition to the `vpk pack` call), so keep unsigned builds to local testing.

## Solution layout

- **Gobchat.App** (exe `GobchatEx`) — main application; everything below lives here unless noted.
- **Gobchat.Memory** — FFXIV process attachment and memory polling (chat events, actors, window focus) on top of the upstream **Sharlayan** NuGet package (`9.0.39`; the previously vendored Sharlayan fork/project was removed). Memory signatures/structures are JSON files downloaded at runtime into `resources/sharlayan` (repo copies exist for dev).
- **Gobchat.WebRenderer** (assembly `Gobchat.UI`) — WebView2 host: the composition-hosted, click-through overlay form (`OverlayForm`, used for both the chat overlay and the fullscreen greeter/notifications "system" overlay), the `CoreWebView2` content wrapper + postMessage bridge (`ManagedWebBrowser`), the shared WebView2 environment (`WebViewManager`), the settings dialog host (`SettingsOverlayForm` — a borderless, ctrl-movable *windowed* WebView2 so the config UI's native `<select>` popups work), DirectComposition interop, and the C#↔JS bridge plumbing (`IBrowserAPI`, `IManagedWebBrowser`, `JavascriptBuilder`).
- **GobUpdater** / **Gobchat.LogConverter** — auto-update helper and a standalone WinForms tool that converts written chat logs.

## Architecture

### Module system (C# side)

Startup is a linear module pipeline: `MainEntry` → [GobchatApplicationContext.cs](Gobchat.App/Core/Runtime/GobchatApplicationContext.cs), which initializes an ordered list of `IApplicationModule`s and disposes them in reverse order on shutdown. Modules live in `Gobchat.App/Module/*`; shared/core code (chat message model, config manager, runtime, util) lives in `Gobchat.App/Core/*`.

Modules communicate only through a `DIContext` (TinyIoC wrapper): each module resolves what it *Requires* and registers what it *Provides* — these contracts are documented in each module's class-level doc comment. Order in the activation list matters; a module's dependencies must be initialized before it.

Data flow: `AppModuleMemoryReader` (polls FFXIV memory) → `AppModuleActorManager` / `AppModuleChatManager` (builds `ChatMessage`s on a background worker, applies formatting/mentions/range-filter/trigger groups from config) → `AppModuleChatToUI` and the other `Module/UI/Connector/AppModule*ToUI` adapters, which push web events into the browser.

### C# ↔ JS bridge

- JS→C#: [GobchatBrowserAPI](Gobchat.App/Module/UI/BrowserAPI.cs) is exposed to the page as the global `GobchatAPI` object via a **postMessage JSON bridge** (not WebView2's `AddHostObjectToScript`). A bridge shim is injected with `AddScriptToExecuteOnDocumentCreatedAsync`; the page calls `GobchatAPI.someMethod(...)`, the shim posts `{api,method,id,args}` via `chrome.webview.postMessage`, and `ManagedWebBrowser` dispatches by reflection to the existing `GobchatBrowserAPI` methods (case-insensitive camelCase→PascalCase), serializing the result with Newtonsoft and posting it back. So `GobchatBrowserAPI`'s method bodies and the `await GobchatAPI.*` call sites are unchanged from the CefSharp era — only the transport differs. The injected `Gobchat.*` enums/config are registered the same way (document-creation init scripts) by `AppModuleLoadUI`.
- Resource loading: the UI is served from the `https://gobchat.localhost` virtual host (an `https` origin is required because the UI loads as ES modules which Chromium blocks over `file://`; the `.localhost` suffix resolves to loopback instantly, whereas a `.local` host triggers a ~2s mDNS stall). Every request is answered by a `WebResourceRequested` handler — `SetVirtualHostNameToFolderMapping` is deliberately *not* used, because a folder mapping serves files by exact name and would bypass the handler's `module`→`modules` rename and `.min` preference (`AppModuleLoadUI.ResolveResource`). The settings dialog (`window.open`) is backed by a second WebView2 on the same environment/origin via `NewWindowRequested`, so the page's `window.opener` sharing keeps working.
- C#→JS: events are serialized to JSON and dispatched as DOM custom events (`ChatMessagesWebEvent` etc., see `Module/UI/WebEvents` and `Gobchat.WebRenderer/Web/JavascriptEvents`) through `CoreWebView2.ExecuteScriptAsync`.

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
