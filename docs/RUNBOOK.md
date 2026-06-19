# GobchatEx Release & Operations Runbook

> **Scope note.** GobchatEx is a **desktop WinForms tray application**, not a server.
> The generic runbook concepts of *deploy to environment*, *health-check endpoints*,
> *monitoring/alerting*, and *escalation paths* do not apply. This runbook is adapted
> to the real operational surface of a shipped desktop app: cutting a release, how the
> in-app auto-updater delivers it, how to roll back, and how to diagnose runtime
> failures on a user's machine. It is generated from the release scripts
> ([pack-release.ps1](../pack-release.ps1),
> [StepsToPackARelease.txt](../Gobchat.App/StepsToPackARelease.txt)), the auto-updater
> (`GobUpdater`), and the user-facing troubleshooting in [README.md](README.md).

## 1. Versioning

The version is the single source of truth in
[Gobchat.App/Properties/AssemblyInfo.cs](../Gobchat.App/Properties/AssemblyInfo.cs)
(`AssemblyVersion` / `AssemblyFileVersion`). Format:

```
{Major}.{Minor}.{Patch}.{PreRelease}
```

- The 4th component (`PreRelease`) **> 0 marks a beta/prerelease**, producing the
  display/asset version `{Major}.{Minor}.{Patch}-{PreRelease}`.
- `0` in the 4th component is a normal release: `{Major}.{Minor}.{Patch}`.

`pack-release.ps1` reads this value directly and names the asset accordingly, so the
zip name always matches AssemblyInfo. (Current: `2.0.0.0` → `2.0.0`.)

## 2. Cutting a release (deployment procedure)

<!-- AUTO-GENERATED: from StepsToPackARelease.txt + pack-release.ps1. Regenerate, don't hand-edit. -->

1. **Bump the version** in
   [AssemblyInfo.cs](../Gobchat.App/Properties/AssemblyInfo.cs).
2. **Update docs & changelog** for any user-facing change — both
   [README.md](README.md) and [README_de.md](README_de.md), plus a new
   [CHANGELOG.md](CHANGELOG.md) section.
3. **Build Release:** `dotnet build Gobchat.sln -c Release` (or `build-release.bat`).
4. **Convert docs to PDF** (optional, but the packer copies them if present): export
   the relevant `/docs` markdown (`README`, `README_de`, `CHANGELOG`) to PDF — e.g.
   via the VS Code *Markdown PDF* extension. Missing PDFs are skipped with a warning.
5. **Pack:** run `pack-release.bat` (or `pack-release.ps1`) from the repo root. It:
   - locates the built `GobchatEx.exe` under `Gobchat.App/bin/Release/<TFM>/<RID>`,
   - prunes the output to the shippable set (keeps `resources`, `de`, `en`; strips
     non-`.css/.json` style sources, `.log` files; moves `.pdb` into a debug bundle),
   - swaps `NLog-Release.config` in as `NLog.config` (info-level logging),
   - archives with 7-Zip/NanaZip at max compression, and
   - drops `gobchatex-{version}.zip` (+ `gobchatex-debug-{version}.zip`) in the repo root.
6. **Publish on GitHub** at [github.com/Shuro/GobchatEx](https://github.com/Shuro/GobchatEx):
   - Release **title** `v{version}`; description = patch notes.
   - Tick **pre-release** if the version has a `-{PreRelease}` suffix.
   - **Attach `gobchatex-{version}.zip`** — the in-app updater downloads this asset.

<!-- END AUTO-GENERATED -->

**Archiver requirement.** The packer needs a 7-Zip-compatible console archiver. It
checks, in order: `$GOBCHAT_7ZIP`, `C:\Program Files\7-Zip\7z.exe`, then `7z.exe` /
`NanaZipC.exe` on `PATH`. If none is found it aborts with a clear error — install
7-Zip/NanaZip or set `GOBCHAT_7ZIP` to a console exe.

## 3. How updates reach users (the "deploy target")

On startup GobchatEx checks the GitHub releases of `Shuro/GobchatEx` for a newer
version. Users can update two ways:

- **Automatic:** click the install button on the patch-note screen; `GobUpdater`
  downloads the latest `gobchatex-{version}.zip` asset and replaces the files.
- **Manual:** download the zip, *Unblock* it (file → Properties → Unblock), and
  unzip over the existing install.

There is **no staged rollout** — publishing a non-prerelease GitHub release makes it
the update for everyone on next launch. Use the **pre-release** flag to ship a build
to opt-in testers without offering it as the auto-update to everyone.

## 4. Rollback

There is no server to roll back; rollback means **stop offering the bad version and
restore a known-good install**.

1. **Stop the bad auto-update:** on the GitHub release, either delete/unpublish the
   bad release or publish a higher-versioned fixed build. As long as the bad release
   is the latest non-prerelease, clients will keep offering it.
2. **Restore a user install:** download the previous good `gobchatex-{version}.zip`
   from the [releases page](https://github.com/Shuro/GobchatEx/releases), Unblock,
   and unzip over the install folder (replace all files). User data in
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

> Note: [docs/LogGuide.md](LogGuide.md) is a **vendored cactbot reference** about ACT
> network/log-line formats for trigger authors — it is *not* about GobchatEx's own
> logging and is intentionally left at its upstream version.
