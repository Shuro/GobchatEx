#
# Builds a GobchatEx release with Velopack.
#
# Flow: dotnet publish (Release, self-contained win-x64) -> vpk pack.
# Produces, under .\Releases:
#   - GobchatEx-win-Setup.exe        the installer (per-user, %LocalAppData%\GobchatEx)
#   - GobchatEx-<version>-full.nupkg the full package
#   - GobchatEx-<version>-delta.nupkg the delta against the previous release (when one exists)
#   - RELEASES / assets.win.json     the manifest the in-app updater reads from GitHub
# plus a separate gobchatex-debug-<version>.zip with the .pdb symbols (vpk keeps them out of
# the shipped package; they are archived only for diagnosing release stack traces).
#
# Unsigned. Signing is a later step (Phase 4): add --signTemplate / --azureTrustedSignFile to
# the vpk invocation below once a certificate exists. No app-code change is needed.
#
# Patch notes: unless -ReleaseNotes is passed, this defaults to the newest version section of
# docs\CHANGELOG.md, embedded via VelopackAsset.NotesMarkdown and shown in the in-app update dialog.
# Pass -ReleaseNotes <path-to-markdown> to override, or -ReleaseNotes "" to pack without any notes.
#
param(
    [string]$ReleaseNotes
)

$ErrorActionPreference = "Stop"

function DeleteIfExists([string] $Path){
	if(-Not (Test-Path $Path)){ return }
	Write-Host "Deleting: $Path"
	# Freshly-built exes (Setup.exe) are often briefly locked by antivirus/Explorer right after a
	# previous pack, so retry rather than failing the whole release on a transient handle.
	for($attempt = 1; $attempt -le 5; $attempt++){
		try{
			Remove-Item -Recurse -Force $Path -ErrorAction Stop
			return
		}catch{
			if($attempt -eq 5){ throw }
			Write-Host "  locked, retrying ($attempt/5) ..."
			Start-Sleep -Milliseconds 800
		}
	}
}

function ClearDirectoryContents([string] $Path){
	if(-Not (Test-Path $Path)){
		$null = New-Item -Path $Path -ItemType directory
		return
	}
	# Remove the *contents* but keep the directory itself. On Windows the Releases folder often keeps a
	# lingering directory handle (Explorer, the search indexer, a build server) that blocks deleting the
	# folder, while the files inside still delete fine - and vpk only needs no conflicting release here.
	Write-Host "Clearing: $Path"
	for($attempt = 1; $attempt -le 5; $attempt++){
		try{
			Get-ChildItem -Path $Path -Force | Remove-Item -Recurse -Force -ErrorAction Stop
			return
		}catch{
			if($attempt -eq 5){ throw }
			Write-Host "  locked, retrying ($attempt/5) ..."
			Start-Sleep -Milliseconds 800
		}
	}
}

function MakeDirectory([string] $Path){
	if( -Not (Test-Path -Path $Path )){
		$null = New-Item -Path $Path -ItemType directory
	}
}

function MakeAndDeleteDirectory([string] $Path){
	DeleteIfExists $Path
	$null = New-Item -Path $Path -ItemType directory
}

$scriptPath = split-path -parent $MyInvocation.MyCommand.Definition
# These scripts live in the repository's build\ folder; the app project, metadata and docs are
# addressed relative to the repository root (the app project is src\Gobchat.App).
$repoRoot = Split-Path -Parent $scriptPath
$appFolder = Join-Path $repoRoot "src\Gobchat.App"
$appProject = Join-Path $appFolder "Gobchat.csproj"

function GetApplicationVersion(){
	$text = [System.IO.File]::ReadAllText("$appFolder\Properties\AssemblyInfo.cs");
	$hasMatch = $text -match '\[assembly: AssemblyVersion\("([0-9]+\.[0-9]+\.[0-9]+)\.([0-9]+)"\)'
	if(-Not $hasMatch){
		Write-Error "Unable to find app version"
		exit 1
		return $null
	}

	$appVersion = $Matches[1]
	$appPrerelease = $Matches[2]
	if( [int]::Parse($appPrerelease) -gt 0 ){
		# 4th component > 0 marks a prerelease -> {major}.{minor}.{patch}-{n} (valid SemVer)
		$appVersion = "$appVersion-$appPrerelease"
	}

	return $appVersion
}

$appVersion = GetApplicationVersion
if (!$appVersion) {
	Write-Error "Unable to find app version"
	exit 1
}
Write-Host "Packing GobchatEx $appVersion"

# 1. Publish the app (Release). The RID (win-x64) and self-contained shape come from the csproj
#    (SelfContained is true for Release); TypeScript/Sass compile during build and the
#    IncludeGeneratedWebAssetsInPublish target feeds the emitted .js/.css into publish. The Release
#    build also drops the dev-only gobchat-test.js (see DevOnlyWebAssets in Gobchat.csproj).
#    NOTE: self-contained -> the .NET 10 runtime is bundled, so the target machine needs no separate
#    .NET install (only the Evergreen WebView2 runtime, which Setup.exe provisions).
$publishDir = Join-Path $repoRoot "publish"
MakeAndDeleteDirectory $publishDir

Write-Host "Publishing $appProject (Release) -> $publishDir ..."
dotnet publish $appProject -c Release -o $publishDir
if($LASTEXITCODE -ne 0){
	Write-Error "dotnet publish failed"
	exit 1
}

# 2. Swap in the release NLog config (info-level file logging) for the dev one.
Write-Host "Setting log config to NLog-Release.config ..."
Remove-Item -Force "$publishDir\NLog.config" -ErrorAction SilentlyContinue
if(Test-Path "$publishDir\NLog-Release.config"){
	Rename-Item -Path "$publishDir\NLog-Release.config" -NewName "NLog.config" -Force
}

# 3. Ship the license + docs alongside the app (optional PDFs are skipped if not generated).
Write-Host "Copying docs ..."
$docs = @(
	@{src="$repoRoot\docs\LICENSE.md";			dst="$publishDir\docs\LICENSE.md"},
	@{src="$repoRoot\docs\SHARLAYAN_LICENSE.md";	dst="$publishDir\docs\SHARLAYAN_LICENSE.md"},
	@{src="$repoRoot\docs\CHANGELOG.pdf";			dst="$publishDir\docs\CHANGELOG.pdf"},
	@{src="$repoRoot\docs\README.pdf";			dst="$publishDir\docs\README.pdf"},
	@{src="$repoRoot\docs\README_de.pdf";			dst="$publishDir\docs\README_de.pdf"}
)
foreach($entry in $docs){
	if( -Not (Test-Path -Path $entry.src) ){
		Write-Warning "$($entry.src) not found - skipping (optional, e.g. a PDF that was not generated)"
		continue
	}
	MakeDirectory (Split-Path -Path $entry.dst)
	Copy-Item -Path $entry.src -Destination $entry.dst -Force
}

# 4. Archive the debug symbols separately. vpk excludes *.pdb from the package by default, so they
#    never ship; this keeps them around for reading release stack traces. (No external archiver
#    needed - Compress-Archive is built in, so the old 7-Zip dependency is gone.)
$debugSymbols = Get-ChildItem -Path $publishDir -Recurse -Filter *.pdb
if($debugSymbols){
	$archiveDebug = Join-Path $repoRoot "gobchatex-debug-$appVersion.zip"
	DeleteIfExists $archiveDebug
	Write-Host "Archiving debug symbols -> $archiveDebug ..."
	Compress-Archive -Path $debugSymbols.FullName -DestinationPath $archiveDebug -Force
}

# 5. Restore the pinned vpk tool (.config/dotnet-tools.json) and pack the Velopack release.
Write-Host "Restoring vpk ..."
dotnet tool restore
if($LASTEXITCODE -ne 0){
	Write-Error "dotnet tool restore (vpk) failed"
	exit 1
}

# vpk refuses to pack a version <= an existing release in the same channel, and we want each run to
# emit exactly the current version's uploadable assets, so start from a clean Releases folder.
# (Generating a delta against the previously *published* release would mean seeding this folder via
# `vpk download github ...` here first - a later enhancement; releases are full-only until then.)
$releasesDir = Join-Path $repoRoot "Releases"
ClearDirectoryContents $releasesDir

# Patch notes: unless -ReleaseNotes was passed explicitly, default to the newest version section of
# the changelog so the in-app updater's notes pane is populated. It was blank before because vpk only
# embeds notes when given --releaseNotes, and the script never supplied one by default.
if(-not $PSBoundParameters.ContainsKey('ReleaseNotes')){
	$changelogPath = Join-Path $repoRoot "docs\CHANGELOG.md"
	if(Test-Path $changelogPath){
		$changelog = Get-Content -Raw $changelogPath
		# From the first "## [version]" heading up to (not including) the next one, or end of file.
		$section = [regex]::Match($changelog, '(?ms)^## \[.*?(?=^## \[|\z)')
		if($section.Success){
			$notesFile = Join-Path ([System.IO.Path]::GetTempPath()) "gobchatex-relnotes-$appVersion.md"
			Set-Content -Path $notesFile -Value ($section.Value.TrimEnd()) -Encoding UTF8
			$ReleaseNotes = $notesFile
			Write-Host "Using latest changelog section as release notes -> $notesFile"
		}else{
			Write-Warning "No '## [version]' section found in $changelogPath - packing without notes"
		}
	}else{
		Write-Warning "$changelogPath not found - packing without notes"
	}
}

$vpkArgs = @(
	"pack",
	"--packId", "GobchatEx",
	"--packTitle", "GobchatEx",
	"--packAuthors", "Shuro",
	"--packVersion", $appVersion,
	"--packDir", $publishDir,
	"--mainExe", "GobchatEx.exe",
	"--icon", (Join-Path $appFolder "resources\GobIcon.ico"),
	# Setup.exe checks for the Evergreen WebView2 runtime and installs it if missing. It is
	# preinstalled on Windows 11 and on most up-to-date Windows 10, but not guaranteed on Win10 -
	# without this the app would install fine and then fail to launch ("WebView2 runtime missing").
	# (The .NET runtime is bundled self-contained, so it needs no framework entry here.)
	"--framework", "webview2",
	"--outputDir", $releasesDir
)
if($ReleaseNotes){
	if(Test-Path $ReleaseNotes){
		$vpkArgs += @("--releaseNotes", $ReleaseNotes)
	}else{
		Write-Warning "Release notes file '$ReleaseNotes' not found - packing without notes"
	}
}

Write-Host "Running vpk pack ..."
dotnet vpk @vpkArgs
if($LASTEXITCODE -ne 0){
	Write-Error "vpk pack failed"
	exit 1
}

Write-Host ""
Write-Host "Release assets ready in $releasesDir"
Write-Host "  Upload every file in that folder to the GitHub release - the in-app updater reads"
Write-Host "  the manifest + .nupkg from there, and Setup.exe is the installer for new users."
