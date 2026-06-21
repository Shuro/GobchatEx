# Contributing to GobchatEx

GobchatEx is a Windows-only FFXIV chat overlay for roleplayers — a .NET 10 WinForms
tray app that renders its chat UI (TypeScript/HTML/CSS) in a WebView2 composition
overlay. It is a fork of [MarbleBag/Gobchat](https://github.com/MarbleBag/Gobchat)
(AGPL-3.0). For the architecture, start with [CODEMAPS/architecture.md](CODEMAPS/architecture.md)
and the repository [CLAUDE.md](../CLAUDE.md).

> This guide is generated from the project's real source-of-truth files (the solution
> and `.csproj` files, `Directory.Build.props` / `Directory.Packages.props`, the
> TypeScript config, the test projects, and the release scripts). Sections marked
> `AUTO-GENERATED` are derived from those files — update the source, then regenerate
> rather than editing the table by hand.

## Prerequisites

| Tool | Required for | Notes |
|------|--------------|-------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (x64) | Building & running everything | All projects target `net10.0-windows`, `PlatformTarget=x64`. |
| Windows 10 / 11 (x64) | Building & running | WinForms + WebView2; the app is Windows-only. |
| [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (Evergreen) | Running the overlay | Ships with current Win10/11; the build only carries the small wrappers + `WebView2Loader.dll`. |
| Node.js (LTS) | Running the TypeScript UI unit tests only | Not needed to build the app — `Microsoft.TypeScript.MSBuild` compiles the UI during the build. |
| Final Fantasy XIV (DX11, 64-bit) | Manual / integration testing only | Memory-reading features can only be exercised against a running game. |
| Visual Studio 2022 (17.2+) | Optional | The solution also builds from the CLI; VS is not required. |
| [Velopack `vpk`](https://github.com/velopack/velopack) | Packing a release only | Pinned in `.config/dotnet-tools.json` and auto-restored by the pack script (`dotnet tool restore`) — no manual install. See [Release packaging](#release-packaging). |

## Getting started

```sh
git clone https://github.com/Shuro/GobchatEx.git
cd GobchatEx
dotnet build Gobchat.sln -c Debug
```

Running the app requires the WebView2 runtime (above); memory features additionally
require FFXIV to be running. User data is written to `%AppData%\GobchatEx` (migrated
once from a legacy `%AppData%\Gobchat` folder on first start).

## Build & test commands

<!-- AUTO-GENERATED: from Gobchat.sln, *.bat, tests/ui/package.json. Regenerate, don't hand-edit. -->

| Command | Description |
|---------|-------------|
| `dotnet build Gobchat.sln -c Debug` | Build the whole solution (Debug). Also runs the TypeScript compile (emits `.js` next to each `.ts`) and the SCSS compile. |
| `dotnet build Gobchat.sln -c Release` | Release build (what `pack-release` packages). |
| `build/build-debug.bat` | Convenience wrapper for the Debug build. |
| `build/build-release.bat` | Convenience wrapper for the Release build. |
| `dotnet test Gobchat.sln` | Run the C# unit suites (xUnit): `Gobchat.App.Tests` and `Gobchat.Memory.Tests`. |
| `cd tests/ui && npm install && npm test` | Run the TypeScript UI unit suite (Vitest). `npm test` maps to `vitest run`. |
| `build/pack-release.bat` / `build/pack-release.ps1` | Pack a Velopack release (Setup.exe, `.nupkg`, Portable.zip, manifest) into `.\Releases\` (see [Release packaging](#release-packaging)). |

<!-- END AUTO-GENERATED -->

**CI** runs the build + both unit suites on every PR and on `master`
([ci.yml](../.github/workflows/ci.yml) → the reusable `build-test.yml`);
[release.yml](../.github/workflows/release.yml) reruns that gate and then packs a Velopack
release on `dev/X.Y.Z` pushes and `vX.Y.Z` tags. Beyond the unit suites, broader
verification is manual — run `GobchatEx.exe` (memory features need FFXIV).

### How the TypeScript and SCSS get built

You do not invoke `tsc` or a Sass CLI directly. `Microsoft.TypeScript.MSBuild`
compiles the UI in `src/Gobchat.App/resources/ui` (config:
[tsconfig.json](../src/Gobchat.App/resources/ui/tsconfig.json), target ES2021, `strict`)
as part of the `Gobchat.App` build, emitting each `.js` next to its `.ts` source.
`DartSassBuilder` compiles the SCSS partials on build. The UI unit tests live in
`tests/ui` (a self-contained Node project deliberately **outside** `resources/ui`
so its `node_modules` is not swept into the app's `resources\**` content copy).

## Testing

Two suites live under `tests/` (the C# ones are part of `Gobchat.sln`):

- **C# (xUnit):** `tests/Gobchat.App.Tests`, `tests/Gobchat.Memory.Tests` — run with `dotnet test Gobchat.sln`.
- **TypeScript (Vitest):** `tests/ui` — run with `cd tests/ui && npm install && npm test`.

Scope is the **unit-testable business logic only**: chat formatting/mentions, the
range-filter visibility fade, channel mapping, `ConfigUpgrader`, `GobVersion`, actor
dedup/name-normalization, the `Gobchat.Memory` chatlog byte-tokenizer and distance
projection, and the pure TS UI helpers. The WebView2/WinForms UI, live FFXIV memory
reading, and process/OS code are **out of scope** (manual/integration only).

When writing tests:

- Internal types are reached via `[assembly: InternalsVisibleTo(...)]` added **manually**
  in each project's `Properties/AssemblyInfo.cs` — the `<InternalsVisibleTo>` MSBuild
  item is ignored because `Directory.Build.props` sets `GenerateAssemblyInfo=false`.
- Test package versions are pinned centrally in
  [Directory.Packages.props](../Directory.Packages.props); test `.csproj`s reference
  them versionless (central package management).
- Sharlayan exposes no mock/dump seam, so the memory layer is tested by feeding
  hand-built Sharlayan output DTOs (`ActorItem`, `ChatLogItem`, `Coordinate`) into the
  consuming code; tiny hand-rolled fakes stand in for interfaces (no mocking library).
- Tests should encode **why** a behavior matters, not just what it does — use
  Arrange-Act-Assert structure and descriptive names that state the behavior under test.

## Code style & conventions

- **License header:** every C#/TS source file carries the AGPL-3.0-only header
  (MarbleBag copyright, preserved in the fork). New files must include it.
- **Identity vs. namespaces:** the rebrand is identity-only. The product/exe is
  `GobchatEx`, but C# namespaces, the JS `Gobchat`/`GobchatAPI` bridge, and the
  project/solution file names deliberately stay `Gobchat.*`. Do not rename these.
- **Central package management:** add/upgrade dependencies via `<PackageVersion>` in
  [Directory.Packages.props](../Directory.Packages.props); `.csproj`s use versionless
  `<PackageReference>`. Shared MSBuild settings live in
  [Directory.Build.props](../Directory.Build.props) (`x64`, `LangVersion=latest`,
  `EnableNETAnalyzers=true`, `GenerateAssemblyInfo=false`).
- **Logging:** via NLog (`NLog.config` for dev; `NLog-Release.config` ships and is
  swapped in by the release script). No stray `Console.WriteLine`/debug prints.
- **Config schema changes** require a `ConfigUpgrader` step for old profiles
  (see [CODEMAPS/data.md](CODEMAPS/data.md)).
- **User-facing changes** must be reflected in the docs **in both languages**
  ([README.md](README.md) / [README_de.md](README_de.md)) and in
  [CHANGELOG.md](CHANGELOG.md).
- General coding standards apply: KISS/DRY/YAGNI, many small focused files (≤800 lines),
  explicit error handling (never silently swallow), and descriptive naming.

## Commit & PR workflow

Commit messages follow Conventional Commits:

```
<type>: <description>

<optional body>
```

Types: `feat`, `fix`, `refactor`, `docs`, `test`, `chore`, `perf`, `ci`.
Work on a feature branch off the default branch (`master`); the active dev branch is
`dev/2.0.0`.

### PR checklist

- [ ] `dotnet build Gobchat.sln -c Debug` succeeds (no new analyzer warnings).
- [ ] `dotnet test Gobchat.sln` is green.
- [ ] `cd tests/ui && npm test` is green (if you touched `resources/ui`).
- [ ] New files carry the AGPL-3.0-only header.
- [ ] New dependencies added via `Directory.Packages.props` (central management).
- [ ] Config schema bumped + `ConfigUpgrader` added (if profile shape changed).
- [ ] Docs updated in **both** languages + CHANGELOG entry (for user-facing changes).
- [ ] No secrets, no stray debug logging.

## Release packaging

Releasing uses **Velopack** (`vpk`): `build/pack-release.ps1` publishes a self-contained
Release build and packs it into `.\Releases\` — `GobchatEx-win-Setup.exe`,
`GobchatEx-{version}-full.nupkg` (+ delta), `GobchatEx-win-Portable.zip`, and the update
manifest. The in-app updater (Velopack `UpdateManager` + `GithubSource`) reads the manifest
and `.nupkg` from the GitHub release. `vpk` is pinned in `.config/dotnet-tools.json` and
auto-restored by the pack script — no external archiver (7-Zip/NanaZip) is needed anymore.
The canonical step list is [RELEASING.md](RELEASING.md); operational and rollback notes are
in [RUNBOOK.md](RUNBOOK.md). CI ([release.yml](../.github/workflows/release.yml)) runs this
same flow on `dev/X.Y.Z` pushes and `vX.Y.Z` tags.

## License

GobchatEx is free software under the **GNU Affero General Public License v3.0 (only)**.
See [LICENSE.md](LICENSE.md). By contributing you agree your contributions are licensed
under the same terms, preserving the existing MarbleBag copyright headers.
