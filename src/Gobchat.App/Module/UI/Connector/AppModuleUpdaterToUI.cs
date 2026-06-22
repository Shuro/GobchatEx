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
using System.Threading.Tasks;

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Bridges the About-page "Check for updates" button to the shared update flow: registers an
    /// <see cref="IBrowserUpdateHandler"/> on the <see cref="IBrowserAPIManager"/> that runs the same
    /// <see cref="UpdateService"/> the startup check uses. The flow blocks (GitHub check + download), so it
    /// runs on a background thread (<see cref="Task.Run"/>) to keep the WebView2 message thread responsive;
    /// the service creates its WinForms dialogs on the UI thread via IUIManager. The service's single-flight
    /// gate means a manual check can't collide with the startup check.
    ///
    /// Must initialize after <see cref="AppModuleBrowserAPIManager"/> (provides the manager) and after
    /// <see cref="Gobchat.Module.Updater.AppModuleUpdater"/> (provides the shared <see cref="UpdateService"/>).
    ///
    /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
    /// Requires: <see cref="IConfigManager"/> <br></br>
    /// Requires: <see cref="UpdateService"/> <br></br>
    /// </summary>
    public sealed class AppModuleUpdaterToUI : IApplicationModule
    {
        private IBrowserAPIManager _browserAPIManager = null!;

        public AppModuleUpdaterToUI()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            _browserAPIManager = container.Resolve<IBrowserAPIManager>();
            var configManager = container.Resolve<IConfigManager>();
            var updateService = container.Resolve<UpdateService>();
            _browserAPIManager.UpdateHandler = new UpdateHandler(updateService, configManager);
        }

        public void Dispose()
        {
            if (_browserAPIManager != null)
                _browserAPIManager.UpdateHandler = null;
            _browserAPIManager = null!;
        }

        private sealed class UpdateHandler : IBrowserUpdateHandler
        {
            private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            private readonly UpdateService _updateService;
            private readonly IConfigManager _configManager;

            public UpdateHandler(UpdateService updateService, IConfigManager configManager)
            {
                _updateService = updateService ?? throw new ArgumentNullException(nameof(updateService));
                _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
            }

            public async Task<string> CheckForUpdates()
            {
                // Honors the beta preference; deliberately does NOT consult "check on start" — an explicit
                // user action overrides it.
                var allowBeta = _configManager.GetProperty<bool>("behaviour.appUpdate.acceptBeta");

                // Off the WebView2 message thread (the flow blocks). This reproduces the startup context
                // (a thread-pool worker); the service marshals its dialogs to the UI thread itself.
                var outcome = await Task.Run(() => _updateService.RunUpdateCheck(allowBeta)).ConfigureAwait(false);
                logger.Info("On-demand update check outcome: {0}", outcome);
                return outcome.ToString();
            }
        }
    }
}
