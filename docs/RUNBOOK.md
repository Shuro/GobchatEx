# GobchatEx Release & Operations Runbook

> **Scope note.** GobchatEx is a **desktop WinForms tray application**, not a server.
> The generic runbook concepts of *deploy to environment*, *health-check endpoints*,
> *monitoring/alerting*, and *escalation paths* do not apply. This runbook is adapted
> to the real operational surface of a shipped desktop app: cutting a release, how the
> in-app auto-updater delivers it, how to roll back, and how to diagnose runtime
> failures on a user's machine. It is generated from the release scripts
> ([pack-release.ps1](../build/pack-release.ps1),
> [RELEASING.md](RELEASING.md)), the in-app auto-updater
> (Velopack's `UpdateManager`), and the user-facing troubleshooting in [README.md](README.md).

## 1. Versioning

The version is the single source of truth in
[src/Gobchat.App/Properties/AssemblyInfo.cs](../src/Gobchat.App/Properties/AssemblyInfo.cs)
(`AssemblyVersion` / `AssemblyFileVersion`). Format:

```
{Major}.{Minor}.{Patch}.{PreRelease}
```

- The 4th component (`PreRelease`) **> 0 marks a beta/prerelease**, producing the
  display/asset version `{Major}.{Minor}.{Patch}-{PreRelease}`.
- `0` in the 4th component is a normal release: `{Major}.{Minor}.{Patch}`.

`pack-release.ps1` (and CI) reads this value directly and derives the SemVer pack
version, so the Velopack assets in `.\Releases\` always match AssemblyInfo.
(Current: `2.0.0.0` → `2.0.0`.)

## 2. Cutting a release (deployment procedure)

<!-- AUTO-GENERATED: from RELEASING.md + build/pack-release.ps1 + .github/workflows/release.yml. Regenerate, don't hand-edit. -->

**Automated (preferred).** CI ([release.yml](../.github/workflows/release.yml)) packs and
publishes for you:

- Push to a **`dev/X.Y.Z`** branch → a **prerelease** `X.Y.Z-N` (N auto-increments per push).
- Push a **`vX.Y.Z`** tag → a **stable** release `X.Y.Z`.

CI injects the version (no AssemblyInfo edit needed), runs the same `vpk pack` flow, and
uploads every file from `.\Releases\` to the GitHub release. The manual steps below remain
valid for packing locally.

1. **Bump the version** in
   [AssemblyInfo.cs](../src/Gobchat.App/Properties/AssemblyInfo.cs) (local packs only).
2. **Update docs & changelog** for any user-facing change — both
   [README.md](README.md) and [README_de.md](README_de.md), plus a new
   [CHANGELOG.md](CHANGELOG.md) section.
3. **(Optional) Convert docs to PDF:** export the relevant `/docs` markdown (`README`,
   `README_de`, `CHANGELOG`) to PDF (e.g. the VS Code *Markdown PDF* extension). The packer
   bundles them if present and skips missing ones with a warning.
4. **Pack:** run `build/pack-release.bat` (or `build/pack-release.ps1`) from the repo root. It:
   - `dotnet publish`es the app (Release, **self-contained** win-x64) to `publish/`,
   - swaps `NLog-Release.config` in as `NLog.config` (info-level logging),
   - archives the `.pdb`s to `gobchatex-debug-{version}.zip` with the built-in
     `Compress-Archive` (no external archiver; vpk keeps symbols out of the package), and
   - restores the pinned `vpk` tool (`.config/dotnet-tools.json`) and runs `vpk pack` into
     `.\Releases\`, producing `GobchatEx-win-Setup.exe`, `GobchatEx-{version}-full.nupkg`
     (+ a `delta` when a prior local release exists), `GobchatEx-win-Portable.zip`, and the
     manifest (`RELEASES` / `assets.win.json` / `releases.win.json`).
5. **Publish on GitHub** at [github.com/Shuro/GobchatEx](https://github.com/Shuro/GobchatEx):
   - Release **title** `v{version}`; description = patch notes.
   - Tick **pre-release** if the version has a `-{PreRelease}` suffix.
   - **Upload every file from `.\Releases\`** — the in-app updater reads the manifest +
     `.nupkg`; `Setup.exe` is for new users and `Portable.zip` for no-install users.

<!-- END AUTO-GENERATED -->

> **Builds are unsigned** until an Authenticode certificate is wired into the `vpk pack`
> call (`--signTemplate` / `--azureTrustedSignFile`, no app-code change), so Windows
> SmartScreen / antivirus warn on install and update until then.

## 3. How updates reach users (the "deploy target")

On startup GobchatEx checks the GitHub releases of `Shuro/GobchatEx` for a newer
version. Users can update two ways:

- **Automatic:** click the install button on the update screen; Velopack's
  `UpdateManager` downloads the latest `.nupkg` from the release and applies it
  atomically (side-by-side), then restarts.
- **Manual:** download `GobchatEx-win-Setup.exe` (installer) or
  `GobchatEx-win-Portable.zip` (no-install) from the releases page.

There is **no staged rollout** — publishing a non-prerelease GitHub release makes it
the update for everyone on next launch. Use the **pre-release** flag to ship a build
to opt-in testers without offering it as the auto-update to everyone.

## 4. Rollback

There is no server to roll back; rollback means **stop offering the bad version and
restore a known-good install**.

1. **Stop the bad auto-update:** on the GitHub release, either delete/unpublish the
   bad release or publish a higher-versioned fixed build. As long as the bad release
   is the latest non-prerelease, clients will keep offering it.
2. **Restore a user install:** download the previous good release's
   `GobchatEx-win-Setup.exe` (or `GobchatEx-win-Portable.zip`) from the
   [releases page](https://github.com/Shuro/GobchatEx/releases) and reinstall. User data in
   `%AppData%\GobchatEx` (profiles, `appsettings.json`, logs) is **untouched** by
   reinstalling, so settings survive a downgrade.
3. **If a bad profile/schema is the cause:** the config is versioned and migrated by
   `ConfigUpgrader`; a corrupt profile can be removed from `%AppData%\GobchatEx` so a
   default profile is recreated on next start.

## 5. Runtime troubleshooting (user machines)

<!-- AUTO-GENERATED: from README.md troubleshooting + startup behavior. Regenerate, don't hand-edit. -->

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| GobchatEx doesn't start | WebView2 runtime missing/out of date (look for a `WebView2` error in `gobchatex_debug.log`) | Install the [Evergreen WebView2 runtime](https://developer.microsoft.com/microsoft-edge/webview2/). |
| Tray icon stays in the "looking for FFXIV" state | FFXIV not running, or memory signatures not yet loaded | Ensure FFXIV (DX11, 64-bit) is running; give it a moment on first launch. |
| Range filter / player info empty; red warning in Config → App | Sharlayan can't read player data from the running game | Confirm FFXIV is running; reopen the config dialog; or close GobchatEx, delete `resources\sharlayan`, and restart to re-download signatures. |
| GobchatEx can't read chat; offers to restart as admin | FFXIV itself is running **as administrator** | Accept the elevation prompt (run both elevated, or neither). |
| Overlay invisible over the game | FFXIV is in exclusive **full-screen** mode | Use borderless/windowed full-screen. |

<!-- END AUTO-GENERATED -->

## 6. Logs & diagnostics

- **App log:** `gobchatex_debug.log` next to the executable (level set by the shipped
  `NLog.config`, which is `NLog-Release.config` renamed at pack time). This is the
  first thing to check for startup/runtime faults.
- **User data & chat logs:** `%AppData%\Roaming\GobchatEx` — profiles,
  `appsettings.json`, and (if chat-logging is enabled) per-session chat log files. A
  new chat log file is created on each start.
- **Memory signatures:** `resources\sharlayan` (downloaded at runtime; safe to delete
  to force a re-download).
- **Debug symbols** for a given release ship separately as
  `gobchatex-debug-{version}.zip` for symbolicating crash reports.
