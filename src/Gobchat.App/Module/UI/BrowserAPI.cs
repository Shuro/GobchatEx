/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
 * Copyright (C) 2026 Shuro
 *
 * This program is free software: you can redistribute it and/or modify it under
 * the terms of the GNU Affero General Public License as published by the Free
 * Software Foundation, version 3.
 *
 * You should have received a copy of the GNU Affero General Public License
 * along with this program. If not, see <https://www.gnu.org/licenses/>
 *
 * SPDX-License-Identifier: AGPL-3.0-only
 *******************************************************************************/

using Gobchat.Core.Runtime;
using Gobchat.Core.Util;
using Gobchat.Module.MemoryReader;
using Gobchat.UI.Web;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gobchat.Module.UI.Internal
{
    /// <summary>
    /// Bridge DTO for <see cref="GobchatBrowserAPI.GetScreenDimensions"/>. The bridge response
    /// serializer preserves member names, so the page reads <c>.Width</c> / <c>.Height</c> instead of
    /// a ValueTuple's <c>Item1</c>/<c>Item2</c>.
    /// </summary>
    public sealed record ScreenDimensions(int Width, int Height);

    internal sealed class GobchatBrowserAPI : IBrowserAPI
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public string APIName => "GobchatAPI";

        private BrowserAPIManager _browserAPIManager;

        // Paths the user explicitly chose via a native file dialog this session. WriteTextToFile is
        // gated on this set (plus the app data folder) so the page - or an XSS reaching the bridge -
        // cannot overwrite arbitrary files; only locations the user picked through the OS dialog.
        // Written on the UI thread (inside RunFileDialog) and read on the bridge dispatch thread, so
        // every access is taken under this lock.
        private readonly HashSet<string> _dialogApprovedPaths = new(StringComparer.OrdinalIgnoreCase);

        public GobchatBrowserAPI(BrowserAPIManager browserAPIManager)
        {
            _browserAPIManager = browserAPIManager ?? throw new ArgumentNullException(nameof(browserAPIManager));
        }

        // ARC-5: the required IBrowser*Handler slots on BrowserAPIManager are wired post-construction by
        // their separate ToUI modules, so a bridge call can reach one before (or without) it being set.
        // The required handlers used a bare null-forgiving `!`, which turned a missing-wire bug into an
        // opaque NullReferenceException that named nothing. Resolve them through this accessor instead: it
        // fails loud with the handler's name (the bridge dispatcher catches the throw and posts it back as
        // an error response). The optional handlers (Update/System/DryRun) are deliberately NOT routed
        // here - their absence is a legitimate runtime state (non-installer build, no system overlay,
        // non-dry-run mode), so their own call sites null-check and degrade on purpose.
        private static T RequireHandler<T>(T? handler, string name) where T : class
        {
            if (handler != null)
                return handler;
            var message = $"Bridge call reached the '{name}' handler before it was registered by its ToUI module";
            logger.Error(message);
            throw new InvalidOperationException(message);
        }

        private void RememberDialogPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
            try
            {
                var fullPath = Path.GetFullPath(path);
                lock (_dialogApprovedPaths)
                    _dialogApprovedPaths.Add(fullPath);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not record dialog-approved path {0}", path);
            }
        }

        private bool IsDialogApproved(string fullPath)
        {
            lock (_dialogApprovedPaths)
                return _dialogApprovedPaths.Contains(fullPath);
        }

        // True when fullPath equals an absolute "soundPath" stored anywhere in the active config. Custom
        // alert sounds the user configured in a previous session live there (not in the dialog set), so
        // this keeps them playable while still refusing arbitrary page-supplied paths.
        private async Task<bool> IsConfiguredSoundPath(string fullPath)
        {
            var handler = _browserAPIManager.ConfigHandler;
            if (handler == null)
                return false;
            try
            {
                var config = await handler.GetConfigAsJson().ConfigureAwait(false);
                foreach (var token in config.SelectTokens("$..soundPath"))
                {
                    var value = token?.Value<string>();
                    if (string.IsNullOrEmpty(value) || !Path.IsPathRooted(value))
                        continue;
                    try
                    {
                        if (string.Equals(Path.GetFullPath(value), fullPath, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch (Exception ex)
                    {
                        logger.Debug(ex, "Skipping malformed configured sound path {0}", value);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not read configured sound paths");
            }
            return false;
        }

        public async Task SetUIReady(bool ready)
        {
            _browserAPIManager.IsUIReady = ready;
        }

        #region chat

        public async Task SendChatMessage(int channel, string source, string message)
        {
            await RequireHandler(_browserAPIManager.ChatHandler, nameof(IBrowserChatHandler)).SendChatMessage(channel, source, message).ConfigureAwait(false);
        }

        public async Task SendInfoChatMessage(string message)
        {
            await RequireHandler(_browserAPIManager.ChatHandler, nameof(IBrowserChatHandler)).SendInfoChatMessage(message).ConfigureAwait(false);
        }

        public async Task SendErrorChatMessage(string message)
        {
            await RequireHandler(_browserAPIManager.ChatHandler, nameof(IBrowserChatHandler)).SendErrorChatMessage(message).ConfigureAwait(false);
        }

        #endregion chat

        #region config

        public async Task<string> GetConfigAsJson()
        {
            var result = await RequireHandler(_browserAPIManager.ConfigHandler, nameof(IBrowserConfigHandler)).GetConfigAsJson().ConfigureAwait(false);
            return result.ToString();
        }

        public async Task SetConfigActiveProfile(string profileId)
        {
            await RequireHandler(_browserAPIManager.ConfigHandler, nameof(IBrowserConfigHandler)).SetActiveProfile(profileId).ConfigureAwait(false);
        }

        public async Task SynchronizeConfig(string configJson)
        {
            var jToken = JToken.Parse(configJson);
            await RequireHandler(_browserAPIManager.ConfigHandler, nameof(IBrowserConfigHandler)).SynchronizeConfig(jToken).ConfigureAwait(false);
        }

        public async Task<string?> ImportProfile()
        {
            var file = await OpenFileDialog("Json files (*.json)|*.json").ConfigureAwait(false);
            if (file == null || file.Trim().Length == 0)
                return null;
            var result = await RequireHandler(_browserAPIManager.ConfigHandler, nameof(IBrowserConfigHandler)).ParseProfile(file).ConfigureAwait(false);
            return result?.ToString();
        }

        // Application-global settings (separate store): the App settings page reads these and writes them
        // through SetAppSetting, which persists + applies instantly (no profile Save).
        public async Task<string> GetAppSettingsAsJson()
        {
            var result = await RequireHandler(_browserAPIManager.ConfigHandler, nameof(IBrowserConfigHandler)).GetAppSettingsAsJson().ConfigureAwait(false);
            return result.ToString();
        }

        public async Task SetAppSetting(string key, string valueJson)
        {
            var value = JToken.Parse(valueJson);
            await RequireHandler(_browserAPIManager.ConfigHandler, nameof(IBrowserConfigHandler)).SetAppSetting(key, value).ConfigureAwait(false);
        }

        #endregion config

        #region files

        // A native modal dialog (ShowDialog) pumps a nested message loop. Showing it via RunSync (Send)
        // would run that loop *inline* on the UI thread, inside the WebView2 WebMessageReceived callback
        // that drove this bridge call - WebView2 forbids re-entering its message loop and FailFasts
        // (STATUS_BREAKPOINT) on the next dialog. ShowDialogAsync posts the dialog instead (Post), so the
        // bridge handler yields back to WebView2 first and the modal opens on a later pump turn.
        private Task<string> ShowDialogAsync(Func<string> showDialog)
        {
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _browserAPIManager.UISynchronizer.RunAsync(() =>
            {
                try { tcs.SetResult(showDialog()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task<string> OpenDirectoryDialog(string? path = null)
        {
            return ShowDialogAsync(() =>
            {
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.SelectedPath = path == null || path.Length == 0 ? GobchatContext.ResourceLocation : path;

                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        return dialog.SelectedPath;
                }
                return "";
            });
        }

        public Task<string> OpenFileDialog(string filter)
        {
            return ShowDialogAsync(() =>
            {
                using (var dialog = new OpenFileDialog())
                {
                    return RunFileDialog(dialog, filter, null);
                }
            });
        }

        public Task<string> SaveFileDialog(string filter, string fileName)
        {
            return ShowDialogAsync(() =>
            {
                using (var dialog = new SaveFileDialog())
                {
                    return RunFileDialog(dialog, filter, fileName);
                }
            });
        }

        private string RunFileDialog(FileDialog dialog, string filter, string? fileName)
        {
            dialog.InitialDirectory = GobchatContext.ResourceLocation;
            dialog.RestoreDirectory = true;
            dialog.Filter = filter ?? "Json files (*.json)|*.json";
            dialog.FileName = fileName ?? "";
            var dialogResult = dialog.ShowDialog();
            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                // The user explicitly picked this path, so subsequent bridge reads/writes to it are
                // authorized (see WriteTextToFile). Without this the page could only write inside AppData.
                RememberDialogPath(dialog.FileName);
                return dialog.FileName;
            }
            return ""; // cancelled: callers treat empty the same as "no file" (file == null || Length == 0)
        }

        public async Task WriteTextToFile(string file, string content)
        {
            if (string.IsNullOrEmpty(file))
                throw new ArgumentNullException(nameof(file));

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(file);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Rejected write to malformed path {0}", file);
                throw new IOException($"Could not write file: {file}", ex);
            }

            // Only allow writes the user authorized through a native save dialog this session, or
            // writes contained under the app's own data directory. An arbitrary absolute path supplied
            // by the page (e.g. via an XSS reaching the bridge) is refused.
            if (!IsDialogApproved(fullPath)
                && !PathSecurityUtil.IsContainedIn(GobchatContext.AppDataLocation, fullPath))
            {
                logger.Warn("Rejected write to non-approved path {0}", fullPath);
                throw new UnauthorizedAccessException($"Writing to '{file}' is not allowed.");
            }

            try
            {
                System.IO.File.WriteAllText(fullPath, content);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to write text to file {0}", fullPath);
                // Surface a user-facing failure to the page instead of letting the bridge swallow it.
                throw new IOException($"Could not write file: {file}", ex);
            }
        }

        public async Task<string> ReadTextFromFile(string file)
        {
            if (String.IsNullOrEmpty(file))
                throw new ArgumentNullException(nameof(file));

            // Relative paths resolve under resources/; absolute paths must still stay within it. The
            // only caller passes the fixed "ui/styles/styles.json", so a page cannot read out of bounds.
            string fullPath = PathSecurityUtil.ResolveWithin(GobchatContext.ResourceLocation, file);
            try
            {
                return System.IO.File.ReadAllText(fullPath);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read text from file {0}", fullPath);
                throw new IOException($"Could not read file: {file}", ex);
            }
        }
        public async Task<string> GetAbsoluteChatLogPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            return Path.Combine(GobchatContext.AppDataLocation, path);
        }

        public async Task<string> GetRelativeChatLogPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                // BRG-11: normalise then compare case-insensitively; on Windows a case difference
                // (SHURO vs Shuro) otherwise leaves the absolute path un-stripped and stored as "relative".
                var fullPath = Path.GetFullPath(path);
                if (fullPath.StartsWith(GobchatContext.AppDataLocation, StringComparison.OrdinalIgnoreCase))
                    path = fullPath.Substring(GobchatContext.AppDataLocation.Length);
            }

            while (path.StartsWith("" + Path.DirectorySeparatorChar))
                path = path.Substring(1);

            return path;
        }

        // Lists the alert sounds shipped under resources/sounds so the Mentions page can offer them in a
        // dropdown. Paths are returned in the same "../sounds/<file>" form the config stores and the
        // page plays, so a returned entry can be selected/used verbatim.
        public async Task<string[]> GetSoundFiles()
        {
            try
            {
                var soundsDir = Path.Combine(GobchatContext.ResourceLocation, "sounds");
                if (!Directory.Exists(soundsDir))
                    return Array.Empty<string>();

                var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp3", ".ogg", ".wav" };
                return Directory.EnumerateFiles(soundsDir)
                    .Where(f => extensions.Contains(Path.GetExtension(f)))
                    .Select(f => "../sounds/" + Path.GetFileName(f))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                // Don't mask a real failure (permissions, AV lock) as "no sounds": an empty dropdown would
                // look identical to a legitimately empty folder. Surface it so the page's promise rejects
                // and the failure is both logged and visible to the caller. (A missing folder is handled
                // above and still returns an empty list, which is the genuine "no sounds" case.)
                logger.Warn(ex, "Failed to enumerate sound files");
                throw new IOException("Could not list sound files", ex);
            }
        }

        // Reads a local audio file and returns it as a data: URL. The virtual host can only serve files
        // under resources/, so a custom sound the user picked from an arbitrary location (e.g. another
        // drive) is delivered this way instead. A bundled relative path ("../sounds/X.mp3") is resolved
        // against resources/ui (the page root); an absolute path is used as-is. Returns null on any
        // failure (missing, oversized, or not an allowed audio type).
        public async Task<string?> GetSoundDataUrl(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path))
                    return null;

                var fullPath = Path.IsPathRooted(path)
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(Path.Combine(GobchatContext.ResourceLocation, "ui", path));

                // Only serve files the user is entitled to: a bundled resource, a sound picked via a
                // native dialog this session, or the sound path currently stored in config. An arbitrary
                // absolute path from the page (e.g. an XSS reaching the bridge) is refused - the
                // extension/size guards below only limit the damage, they don't authorize the read.
                if (!PathSecurityUtil.IsContainedIn(GobchatContext.ResourceLocation, fullPath)
                    && !IsDialogApproved(fullPath)
                    && !await IsConfiguredSoundPath(fullPath).ConfigureAwait(false))
                {
                    logger.Warn("Rejected sound read from non-approved path {0}", fullPath);
                    return null;
                }

                if (!File.Exists(fullPath))
                    return null;

                string mime;
                switch (Path.GetExtension(fullPath).ToLowerInvariant())
                {
                    case ".mp3": mime = "audio/mpeg"; break;
                    case ".ogg": mime = "audio/ogg"; break;
                    case ".wav": mime = "audio/wav"; break;
                    default: return null;
                }

                var info = new FileInfo(fullPath);
                if (info.Length > 25 * 1024 * 1024) // guard against accidentally huge files
                    return null;

                var bytes = File.ReadAllBytes(fullPath);
                return $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
            }
            catch (UnauthorizedAccessException ex)
            {
                // SIF-5: surface an access-denied distinctly from a benign not-found so a real permission
                // problem (vs the file simply being absent) is visible in the log.
                logger.Warn(ex, "Access denied reading sound file {0}", path);
                return null;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read sound file");
                return null;
            }
        }

        #endregion files

        #region player data

        public async Task<bool> IsFeaturePlayerLocationAvailable()
        {
            return await RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).IsFeatureAvailable().ConfigureAwait(false);
        }

        public async Task<int> GetPlayerCount()
        {
            return await RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).GetPlayerNearbyCount().ConfigureAwait(false);
        }

        public async Task<string[]> GetPlayersNearby()
        {
            return await RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).GetPlayersNearby().ConfigureAwait(false);
        }

        public async Task<float> GetPlayerDistance(string playerName)
        {
            var distance = await RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).GetDistanceToPlayer(playerName).ConfigureAwait(false);
            return distance;
        }

        public async Task<string?> GetCurrentPlayer()
        {
            return await RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).GetCurrentPlayerName().ConfigureAwait(false);
        }

        public async Task<string[]> GetPlayersAndDistance()
        {
            var players = await RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).GetPlayersNearby().ConfigureAwait(false);
            if (players.Length == 0)
                return Array.Empty<string>();

            // BRG-13: start all distance lookups, then await once, instead of serialising N awaits on this
            // hot polling path (up to ~200 actors per zone).
            var distanceTasks = new Task<float>[players.Length];
            for (var i = 0; i < players.Length; ++i)
                distanceTasks[i] = RequireHandler(_browserAPIManager.ActorHandler, nameof(IBrowserActorHandler)).GetDistanceToPlayer(players[i]);
            var distances = await Task.WhenAll(distanceTasks).ConfigureAwait(false);

            var result = new List<(float Distance, string Name)>(players.Length);
            for (var i = 0; i < players.Length; ++i)
                result.Add((distances[i], players[i]));

            result.Sort((a, b) => a.Distance.CompareTo(b.Distance));
            return result.Select(e => $"{e.Name}: {e.Distance.ToString("0.00", CultureInfo.InvariantCulture)}").ToArray();
        }

        #endregion player data

        #region process functions

        public async Task<int[]> GetAttachableFFXIVProcesses()
        {
            return await RequireHandler(_browserAPIManager.MemoryHandler, nameof(IBrowserMemoryHandler)).GetAttachableFFXIVProcesses().ConfigureAwait(false);
        }

        public async Task<AttachedProcessInfo> GetAttachedFFXIVProcess()
        {
            return await RequireHandler(_browserAPIManager.MemoryHandler, nameof(IBrowserMemoryHandler)).GetAttachedFFXIVProcess().ConfigureAwait(false);
        }

        public async Task<bool> AttachToFFXIVProcess(int id)
        {
            return await RequireHandler(_browserAPIManager.MemoryHandler, nameof(IBrowserMemoryHandler)).AttachToFFXIVProcess(id).ConfigureAwait(false);
        }

        #endregion process functions

        #region localization

        public async Task<Dictionary<string, string>> GetLocalizedStrings(string locale, string[] requestedIds)
        {
            if (requestedIds == null)
                requestedIds = Array.Empty<string>();

            if (locale == null)
                throw new ArgumentNullException(nameof(locale));

            // The page supplies the locale tag directly; an unknown/invalid tag makes GetCultureInfo throw
            // CultureNotFoundException, which would propagate as a bridge error (and let the page probe for
            // valid culture names). Fall back to the invariant culture so the resource manager just serves
            // the default (neutral) strings instead.
            CultureInfo selectedCulture;
            try
            {
                selectedCulture = CultureInfo.GetCultureInfo(locale);
            }
            catch (CultureNotFoundException ex)
            {
                logger.Warn(ex, "Unknown locale '{0}' requested; falling back to invariant culture", locale);
                selectedCulture = CultureInfo.InvariantCulture;
            }
            var manager = WebUIResources.ResourceManager;

            var result = new Dictionary<string, string>();
            foreach (var requestedId in requestedIds)
            {
                if (result.ContainsKey(requestedId))
                    continue;

                var translation = manager.GetString(requestedId, selectedCulture);
                if (translation == null)
                    result.Add(requestedId, StringFormat.Format(WebUIResources.localization_key_missing, requestedId));
                else
                    result.Add(requestedId, translation);
            }

            return result;
        }

        #endregion

        public async Task<string> GetAppVersion()
        {
            return GobchatContext.ApplicationVersion.ToString();
        }

        // About-page "Check for updates" button: runs the same flow as the startup check (for an installer
        // build with an update available: confirm -> download -> apply-and-restart). The handler runs the
        // blocking routine off the UI thread; returns a page-facing outcome code (see IBrowserUpdateHandler).
        // Returns "Unavailable" when the handler isn't wired (e.g. before the UI adapter initializes).
        public async Task<string> CheckForUpdates()
        {
            var handler = _browserAPIManager.UpdateHandler;
            if (handler == null)
                return "Unavailable";
            return await handler.CheckForUpdates().ConfigureAwait(false);
        }

        public async Task<ScreenDimensions> GetScreenDimensions()
        {
            var bounds = Screen.PrimaryScreen!.Bounds;
            return new ScreenDimensions(bounds.Width, bounds.Height);
        }

        public async Task CloseGobchat()
        {
            logger.Info("User requests shutdown");
            GobchatApplicationContext.ExitGobchat();
        }

        #region overlay window

        // Toolbar pin button: lock/unlock the overlay for moving + resizing.
        public async Task ToggleOverlayLock()
        {
            _browserAPIManager.ToggleOverlayLock();
        }

        // Begins a native window move; the page calls this on mousedown over the toolbar/grip while the
        // overlay is unlocked. Position/size are persisted when the overlay is pinned (see AppModuleChatOverlay).
        public async Task BeginWindowDrag()
        {
            _browserAPIManager.BeginOverlayDrag();
        }

        // Settings-window title bar: the settings page shares this overlay's GobchatAPI via
        // window.opener. Minimize sends the settings window to the taskbar.
        public async Task MinimizeSettings()
        {
            _browserAPIManager.MinimizeSettings();
        }

        // Reveal-when-ready: the settings page calls this (via window.opener's GobchatAPI) at the end of
        // its setup so the initially hidden settings window appears already rendered, with no flash.
        public async Task RevealSettings()
        {
            _browserAPIManager.RevealSettings();
        }

        // Second cog click from the overlay: bring an already-open settings window to the front.
        // Returns false when no settings window is open so the overlay opens a fresh one instead.
        public async Task<bool> FocusSettings()
        {
            return _browserAPIManager.FocusSettings();
        }

        #endregion overlay window

        #region external

        // Opens an https URL in the user's default browser (About-page GitHub/Licence links). Restricted
        // to https so the bridge can't be coaxed into launching arbitrary executables or file/custom-scheme
        // handlers. Uses the OS shell (mirrors AppModuleInformUserAboutMemoryState).
        public async Task OpenExternalLink(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("A url is required", nameof(url));
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
                throw new ArgumentException($"Refusing to open non-https url: {url}", nameof(url));

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true,
                });
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to open external link {0}", url);
                throw;
            }
        }

        #endregion external

        #region debug

        public async Task<bool> IsDebugBuild()
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }

        // Shows a test toast on the system overlay (Debug settings page). No-ops if the overlay is absent.
        public async Task ShowTestNotification()
        {
            var handler = _browserAPIManager.SystemHandler;
            if (handler != null)
                await handler.ShowNotification($"Test notification — {DateTime.Now:HH:mm:ss}").ConfigureAwait(false);
        }

        // Toggles the greeter splash on the system overlay (Debug settings page) so it can be previewed
        // after FFXIV is connected. No-ops if the overlay is absent.
        public async Task ToggleGreeter()
        {
            var handler = _browserAPIManager.SystemHandler;
            if (handler != null)
                await handler.ToggleGreeter().ConfigureAwait(false);
        }

#if DEBUG
        // Loads the manual chat test harness (resources/ui/gobchat-test.js) into the chat overlay on
        // demand. It is excluded from Release output (see Gobchat.csproj), so this stays Debug-only and
        // the Debug settings page that calls it is hidden in Release (GobchatAPI.isDebugBuild()).
        public async Task InjectTestHarness()
        {
            _browserAPIManager.ExecuteGobchatJavascript(builder =>
            {
                builder.AppendLine("(function(){");
                builder.AppendLine("    const script = document.createElement('script');");
                builder.AppendLine("    script.src = 'gobchat-test.js';");
                builder.AppendLine("    document.head.appendChild(script);");
                builder.AppendLine("})();");
            });
        }
#endif

        #endregion debug

        #region dry run

        // Developer dry-run control panel (Debug settings page). The handler is only set when the app
        // runs with --dry-run; outside dry-run every method null-guards into an empty/no-op result so the
        // page can grey the panel out. Channel int == ChatChannel ordinal (same convention as SendChatMessage).

        public async Task<bool> IsDryRun()
        {
            return _browserAPIManager.DryRunHandler != null;
        }

        public async Task<string[]> GetDryRunCharacters()
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler == null)
                return Array.Empty<string>();
            return await handler.GetCharacters().ConfigureAwait(false);
        }

        public async Task<string[]> GetDryRunScenarios()
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler == null)
                return Array.Empty<string>();
            return await handler.GetScenarios().ConfigureAwait(false);
        }

        public async Task DryRunInjectScenario(string name)
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler != null)
                await handler.InjectScenario(name).ConfigureAwait(false);
        }

        public async Task<DryRunCharacter[]> GetDryRunRoster()
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler == null)
                return Array.Empty<DryRunCharacter>();
            return await handler.GetRoster().ConfigureAwait(false);
        }

        public async Task DryRunConnect(string name)
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler != null)
                await handler.Connect(name).ConfigureAwait(false);
        }

        public async Task DryRunDisconnect()
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler != null)
                await handler.Disconnect().ConfigureAwait(false);
        }

        public async Task DryRunAddCharacter(string name, double distance)
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler != null)
                await handler.AddCharacter(name, distance).ConfigureAwait(false);
        }

        public async Task DryRunRemoveCharacter(string name)
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler != null)
                await handler.RemoveCharacter(name).ConfigureAwait(false);
        }

        public async Task DryRunSendMessage(int channel, string source, string message)
        {
            var handler = _browserAPIManager.DryRunHandler;
            if (handler != null)
                await handler.SendMessage(channel, source, message).ConfigureAwait(false);
        }

        #endregion dry run
    }
}
