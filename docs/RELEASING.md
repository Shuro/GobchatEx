Releases are built with Velopack (vpk). The pack script publishes the app and produces an
installer (Setup.exe), the full/delta packages, and the manifest the in-app updater reads.

AUTOMATED (preferred): CI publishes releases for you (.github/workflows/release.yml):
	- Push to a dev/X.Y.Z branch -> a prerelease X.Y.Z-N (first push -1, then -2, ... auto-incrementing).
	- Push a tag vX.Y.Z          -> a stable release X.Y.Z.
	CI injects the version (no AssemblyInfo edit needed), runs this same pack flow, and uploads every
	asset to a GitHub release. The manual steps below remain valid for packing/publishing locally.

1. Set the version in src/Gobchat.App/Properties/AssemblyInfo.cs
	- Pattern: {Major}.{Minor}.{Patch}.{PreRelease}
	- A 4th component > 0 marks a beta -> the release version becomes {Major}.{Minor}.{Patch}-{PreRelease}

2. (Optional) Use VS Code to convert the markdown docs (.md) under /docs into PDFs
	1. Markdown PDF Extension
	2. Open the markdown file, right click, export to PDF
	- CHANGELOG.pdf / README.pdf / README_de.pdf are bundled with the app if present (skipped if not).

3. Run build/pack-release.bat (or build/pack-release.ps1 directly). It will:
	1. dotnet publish the app (Release)
	2. swap NLog-Release.config in as NLog.config
	3. archive the .pdb debug symbols to gobchatex-debug-{version}.zip (kept out of the package)
	4. restore the pinned vpk tool (.config/dotnet-tools.json) and run `vpk pack`
	- To embed patch notes, pass a markdown file: build/pack-release.ps1 -ReleaseNotes path\to\notes.md
	  (shown to the user in the update prompt via the release's NotesMarkdown).

4. The generated assets land in .\Releases:
	- GobchatEx-win-Setup.exe         the installer (per-user, installs under %LocalAppData%\GobchatEx)
	- GobchatEx-{version}-full.nupkg  the full package
	- GobchatEx-{version}-delta.nupkg the delta vs. the previous release (only when a previous release was packed locally)
	- GobchatEx-win-Portable.zip      a no-install portable build
	- RELEASES / assets.win.json / releases.win.json  the manifest the updater reads from GitHub

5. Create a new release on github.com/Shuro/GobchatEx
	1. Release title is v{version}
	2. Description is used for patch notes
	3. Beta release? Check 'pre-release' (the in-app updater only offers betas when "accept beta" is on)
	4. Upload EVERY file from .\Releases as release assets
	   - The in-app updater reads the manifest + .nupkg from the release to download and apply updates.
	   - Setup.exe is for new users; Portable.zip is for users who do not want an installer.

Note: releases are published on the fork github.com/Shuro/GobchatEx.

Note: builds are currently UNSIGNED. Both the automated CI releases and local packs ship unsigned
      until an Authenticode certificate is wired in, so Windows SmartScreen / antivirus will warn on
      install and update until then. Signing is a one-line addition to the vpk invocation in
      build/pack-release.ps1 (--signTemplate / --azureTrustedSignFile) with no app-code change.

Note: the Release build ships self-contained - it bundles the .NET 10 runtime, so the target
      machine needs no separate .NET install. (Debug stays framework-dependent for fast iteration.)

Note: the overlay renders through the OS Microsoft Edge WebView2 runtime, so there is no
      bundled-Chromium (CEF) payload to ship or download. The packaged build only carries the small
      WebView2 wrappers + WebView2Loader.dll (next to the exe). End users need the Evergreen WebView2
      runtime, which ships with current Win10/11.
