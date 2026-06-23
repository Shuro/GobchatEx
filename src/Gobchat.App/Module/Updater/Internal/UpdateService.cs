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
using Gobchat.Core.UI;
using Gobchat.Core.Util;
using NLog;
using System;
using Velopack;
using Velopack.Sources;

namespace Gobchat.Module.Updater.Internal
{
    /// <summary>
    /// The result of a single run of <see cref="UpdateService.RunUpdateCheck"/>.
    /// </summary>
    internal enum UpdateOutcome
    {
        /// <summary>No newer release than the running version.</summary>
        UpToDate,

        /// <summary>The user dismissed the prompt (skip / "no" to the manual-install question).</summary>
        Declined,

        /// <summary>Velopack build, but the user chose the manual route: the release page was opened and the app keeps running.</summary>
        OpenedReleasePage,

        /// <summary>Non-installed (portable/developer) build: the release page was opened for a manual install.
        /// At startup this stops the rest of startup so the user can install the new version.</summary>
        NeedsManualInstall,

        /// <summary>The check or download failed (e.g. no network).</summary>
        Failed,

        /// <summary>Another check/download is already in flight; this call did nothing.</summary>
        Busy,
    }

    /// <summary>
    /// The shared update flow used by both the startup check (<see cref="AppModuleUpdater"/>) and the
    /// on-demand About-page button (<see cref="Gobchat.Module.UI.AppModuleUpdaterToUI"/>). When the app was
    /// installed through Velopack (Setup.exe) a newer release is downloaded and applied in-place atomically
    /// and the app restarts; developer builds and the legacy portable zip are not Velopack-installed, so they
    /// fall back to checking GitHub and pointing the user at the release page for a manual install.
    ///
    /// A <see cref="SingleFlightGate"/> ensures only one run happens at a time, so a manual check can't
    /// collide with the startup check. The (blocking) routine creates its WinForms dialogs through
    /// <see cref="IUIManager"/> so they live on the UI thread; callers run it off the UI thread (the startup
    /// worker is a thread-pool task; the on-demand path wraps it in <see cref="System.Threading.Tasks.Task.Run"/>).
    /// </summary>
    internal sealed class UpdateService
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string GitHubRepoUrl = "https://github.com/Shuro/GobchatEx";
        private const string GitHubReleasesUrl = GitHubRepoUrl + "/releases";

        private readonly IUIManager _uiManager;
        private readonly SingleFlightGate _gate = new SingleFlightGate();

        public UpdateService(IUIManager uiManager)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
        }

        /// <summary>True while a check/download is in flight.</summary>
        public bool IsBusy => _gate.IsBusy;

        /// <summary>
        /// Runs the update flow once. Returns <see cref="UpdateOutcome.Busy"/> immediately if a run is already
        /// in flight (single-flight). Does not consult the "check on start" preference — an explicit trigger
        /// overrides it; gating the automatic startup run is the caller's responsibility.
        /// </summary>
        public UpdateOutcome RunUpdateCheck(bool allowBeta)
        {
            if (!_gate.TryEnter())
                return UpdateOutcome.Busy;

            try
            {
                var updateManager = TryCreateUpdateManager(allowBeta);
                if (updateManager != null && updateManager.IsInstalled)
                    return HandleVelopackUpdate(updateManager);
                return HandleManualUpdate(allowBeta);
            }
            finally
            {
                _gate.Exit();
            }
        }

        private UpdateManager? TryCreateUpdateManager(bool allowBeta)
        {
            try
            {
                var source = new GithubSource(GitHubRepoUrl, accessToken: null, prerelease: allowBeta);
                return new UpdateManager(source);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to create the Velopack update manager");
                return null;
            }
        }

        private UpdateOutcome HandleVelopackUpdate(UpdateManager updateManager)
        {
            UpdateInfo? updateInfo;
            try
            {
                updateInfo = updateManager.CheckForUpdatesAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to check for updates");
                return UpdateOutcome.Failed;
            }

            if (updateInfo == null)
                return UpdateOutcome.UpToDate; // already up to date

            var asset = updateInfo.TargetFullRelease;
            switch (AskUser(asset.Version.ToString(), asset.NotesMarkdown ?? string.Empty))
            {
                case UpdateFormDialog.UpdateType.Skip:
                    return UpdateOutcome.Declined;
                case UpdateFormDialog.UpdateType.Manual:
                    return OpenReleasePage(GitHubReleasesUrl)
                        ? UpdateOutcome.OpenedReleasePage
                        : UpdateOutcome.Failed;
            }

            return DownloadAndApply(updateManager, updateInfo);
        }

        private UpdateOutcome DownloadAndApply(UpdateManager updateManager, UpdateInfo updateInfo)
        {
            var displayId = _uiManager.CreateUIElement(() =>
            {
                var f = new ProgressDisplayForm();
                f.Show();
                return f;
            });

            try
            {
                var progressDisplay = _uiManager.GetUIElement<ProgressDisplayForm>(displayId);
                using (var progressMonitor = new ProgressMonitorAdapter(progressDisplay))
                {
                    var cancellationToken = progressMonitor.GetCancellationToken();
                    progressMonitor.StatusText = Resources.Module_Updater_UI_Log_PrepareUpdates;

                    try
                    {
                        updateManager.DownloadUpdatesAsync(
                            updateInfo,
                            percent => progressMonitor.Progress = percent / 100d,
                            cancellationToken).GetAwaiter().GetResult();
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        logger.Info("Update download canceled by the user");
                        return UpdateOutcome.Declined;
                    }

                    progressMonitor.Progress = 1d;
                    progressMonitor.StatusText = Resources.Module_Updater_UI_Log_Done;

                    // Hands off to Velopack's helper, which waits for this process to exit, swaps in
                    // the new version atomically and relaunches. Does not return on success.
                    updateManager.ApplyUpdatesAndRestart(updateInfo);

                    // Reaching here means apply-and-restart returned without restarting: the update did
                    // not take. Report failure so the caller keeps the running (old) version alive instead
                    // of stopping startup and leaving the user with nothing running.
                    logger.Error("ApplyUpdatesAndRestart returned without restarting; update was not applied");
                    return UpdateOutcome.Failed;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Auto update failed");
                System.Windows.Forms.MessageBox.Show(
                    StringFormat.Format(Resources.Module_Updater_Dialog_DownloadFailed_Text, ex.Message),
                    Resources.Module_Updater_Dialog_DownloadFailed_Title,
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return UpdateOutcome.Failed;
            }
            finally
            {
                _uiManager.DisposeUIElement(displayId);
            }
        }

        private UpdateOutcome HandleManualUpdate(bool allowBeta)
        {
            IUpdateDescription? update;
            try
            {
                update = GetUpdate(GobchatContext.ApplicationVersion, allowBeta);
            }
            catch (Exception ex)
            {
                // A failed check (no network, GitHub 5xx, parse error) must report Failed, not UpToDate,
                // so the user isn't told they're current and left sitting on an old build.
                logger.Error(ex, "Manual update check failed");
                return UpdateOutcome.Failed;
            }

            if (update == null)
                return UpdateOutcome.UpToDate;

            if (AskUser(update.Version.ToString(), update.PatchNotes) == UpdateFormDialog.UpdateType.Skip)
                return UpdateOutcome.Declined;

            // Auto and Manual both route here: this build can't self-update.
            var dialogText = StringFormat.Format(Resources.Module_Updater_Dialog_ManualInstall_Text, update.Version);
            var dialogResult = System.Windows.Forms.MessageBox.Show(
                dialogText,
                Resources.Module_Updater_Dialog_ManualInstall_Title,
                System.Windows.Forms.MessageBoxButtons.YesNo,
                System.Windows.Forms.MessageBoxIcon.Information);

            if (System.Windows.Forms.DialogResult.Yes != dialogResult)
                return UpdateOutcome.Declined;

            return OpenReleasePage(update.PageUrl)
                ? UpdateOutcome.NeedsManualInstall
                : UpdateOutcome.Failed;
        }

        // Mirrors GobchatBrowserAPI.OpenExternalLink: .NET 10 Process.Start does not shell-launch a
        // URL unless UseShellExecute is set, so the raw Process.Start(url) used previously threw.
        private bool OpenReleasePage(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = uri.AbsoluteUri,
                        UseShellExecute = true,
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to open release page {0}", url);
                }
            }
            else
            {
                logger.Warn("Refusing to open non-https release url: {0}", url);
            }

            // The browser didn't open: don't fail silently. Show the user the address so they can reach
            // the download manually (the caller maps this to UpdateOutcome.Failed).
            System.Windows.Forms.MessageBox.Show(
                StringFormat.Format(Resources.Module_Updater_Dialog_OpenPageFailed_Text, url),
                Resources.Module_Updater_Dialog_OpenPageFailed_Title,
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Warning);
            return false;
        }

        private UpdateFormDialog.UpdateType AskUser(string newVersion, string patchNotes)
        {
            using (var notes = new UpdateFormDialog())
            {
                notes.UpdateHeadText = StringFormat.Format(notes.UpdateHeadText, newVersion, GobchatContext.ApplicationVersion);
                notes.UpdateNotes = patchNotes;
                notes.ShowDialog();
                return notes.UpdateRequest;
            }
        }

        private IUpdateDescription? GetUpdate(GobVersion appVersion, bool allowBetaUpdates = false)
        {
            var provider = new GitHubUpdateProvider(appVersion, userName: "Shuro", repoName: "GobchatEx");
            provider.AcceptBetaReleases = allowBetaUpdates;

            try
            {
                var updateDescription = provider.CheckForUpdate();
                if (!updateDescription.IsVersionAvailable || updateDescription.Version <= appVersion)
                    return null;
                return updateDescription;
            }
            catch (Exception e)
            {
                // Let the failure propagate so the caller can tell a genuine "up to date" (null) apart
                // from a failed check. Swallowing it here mapped a network/5xx/parse error to UpToDate -
                // "you are on the latest version" - while the Velopack path returns Failed on the same errors.
                logger.Error(e);
                throw;
            }
        }
    }
}
