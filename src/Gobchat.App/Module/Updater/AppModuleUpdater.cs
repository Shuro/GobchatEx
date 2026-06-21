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

using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Module.Updater.Internal;
using System;

namespace Gobchat.Module.Updater
{
    /// <summary>
    /// Startup update flow. Builds the shared <see cref="UpdateService"/> (the actual check/download/apply
    /// logic lives there, so the on-demand About-page button can reuse it) and, when the "check on start"
    /// preference is enabled, runs it once during startup. The service is registered in the DI context
    /// <b>unconditionally</b> so the manual trigger keeps working even when this auto-check is disabled.
    ///
    /// Runs on the application worker thread (see AbstractGobchatApplicationContext); the service creates UI
    /// through IUIManager so it lives on the main UI thread and keeps rendering while this thread blocks on
    /// the download.
    ///
    /// Provides: <see cref="UpdateService"/>
    /// </summary>
    public sealed class AppModuleUpdater : IApplicationModule
    {
        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (container == null) throw new ArgumentNullException(nameof(container));

            var configManager = container.Resolve<IConfigManager>();
            var uiManager = container.Resolve<IUIManager>();

            var updateService = new UpdateService(uiManager);
            container.Register<UpdateService>(updateService);

            if (!configManager.GetProperty<bool>("behaviour.appUpdate.checkOnline"))
                return;

            var allowBeta = configManager.GetProperty<bool>("behaviour.appUpdate.acceptBeta");
            var outcome = updateService.RunUpdateCheck(allowBeta);

            // A non-installed (portable/dev) build can't self-update: it sent the user to the release page
            // for a manual install, so stop the rest of startup to let them replace the app. Every other
            // outcome lets startup continue — including a Velopack apply (it restarts the process itself)
            // and a failed apply/download (the current version keeps running rather than exiting).
            if (outcome == UpdateOutcome.NeedsManualInstall)
                handler.StopStartup = true;
        }

        public void Dispose()
        {
        }
    }
}
