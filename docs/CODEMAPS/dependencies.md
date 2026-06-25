<!-- Generated: 2026-06-26 | Files scanned: ~12 | Token estimate: ~630 -->

# Dependencies

.NET 10 (`net10.0-windows`, x64), SDK-style csprojs, central package management via
[Directory.Packages.props](../../Directory.Packages.props). TS toolchain via MSBuild.

## NuGet (runtime)

```
Sharlayan               9.0.39      FFXIV process memory reading (chat, actors, focus)
Microsoft.Web.WebView2  1.0.4022.49 overlay render via OS Evergreen runtime (replaced CefSharp/CEF)
Velopack                1.2.0       in-app self-update (UpdateManager/GithubSource); packaged with vpk
Newtonsoft.Json         13.0.4      config + bridge JSON
NLog                    6.1.3       logging (NLog.config dev / NLog-Release.config ships)
Hjson                   3.0.0       autotranslate hjson data
```
(SharpCompress + the dead `ArchiveUnpacker`/download helpers were removed — updates are Velopack-atomic.)

## NuGet (build-time / test)

```
Microsoft.TypeScript.MSBuild  5.9.3   compiles resources/ui TS during App build
DartSassBuilder               1.1.0   compiles styles/*.scss → .css on build
Microsoft.NET.Test.Sdk 17.12.0 · xunit 2.9.2 · xunit.runner.visualstudio 2.8.2 · coverlet.collector 6.0.2
```

## TS / Vitest ([tests/ui/](../../tests/ui/))

Vitest unit suite (self-contained Node project, outside `resources/ui`).
`resources/ui/tsconfig.json`: target ES2021, strict.

## External services / integrations

```
GitHub (Shuro/GobchatEx)   auto-update: Velopack UpdateManager + GithubSource read the release
                           manifest + .nupkg, download and apply in-place atomically (self-restart);
                           non-installed (portable/dev) builds open the releases page for manual install
Sharlayan signature JSON   downloaded at runtime → resources/sharlayan (dev copies in repo)
WebView2 Evergreen runtime OS-serviced Edge/Chromium; only thin SDK + WebView2Loader.dll ship
gobchat.localhost          in-process https virtual host (WebResourceRequested) serving the UI
```

## Bundled web libs (resources/ui/lib)

jQuery, lodash, Coloris (color picker) — loaded as page globals (see frontend.md).

## Related
[architecture.md](architecture.md) · [data.md](data.md) · [backend.md](backend.md)
