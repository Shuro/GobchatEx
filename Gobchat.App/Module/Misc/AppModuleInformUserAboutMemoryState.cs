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

using Gobchat.Core.Chat;
using Gobchat.Core.Runtime;
using Gobchat.Memory;
using Gobchat.Module.Chat;
using Gobchat.Module.MemoryReader;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace Gobchat.Module.Misc
{
    public sealed class AppModuleInformUserAboutMemoryState : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IDIContext _container;
        private IChatManager _chatManager;
        private IMemoryReaderManager _memoryReader;

        private volatile bool _reportError;
        private bool _promptedForElevation;

        private readonly object _lock = new object();

        /// <summary>
        /// Requires: <see cref="IChatManager"/> <br></br>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// <br></br>
        /// </summary>
        public AppModuleInformUserAboutMemoryState()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _chatManager = _container.Resolve<IChatManager>();
            _memoryReader = _container.Resolve<IMemoryReaderManager>();

            _reportError = true; //report error on startup
            _memoryReader.OnConnectionStateChanged += MemoryReader_OnConnectionState;
            Report(_memoryReader.ConnectionState);
        }

        public void Dispose()
        {
            _memoryReader.OnConnectionStateChanged -= MemoryReader_OnConnectionState;

            _chatManager = null;
            _container = null;
        }

        private void MemoryReader_OnConnectionState(object sender, ConnectionEventArgs e)
        {
            Report(e.State);
        }

        private void Report(ConnectionState state)
        {
            var shouldPromptForElevation = false;

            lock (_lock)
            {
                switch (state)
                {
                    case ConnectionState.NotInitialized:
                        return;

                    case ConnectionState.Searching:
                    case ConnectionState.NotFound:
                        if (!_reportError)
                            return;

                        _reportError = false;
                        _chatManager.EnqueueMessage(SystemMessageType.Error, Resources.Module_Misc_Connection_NotFound);
                        break;

                    case ConnectionState.NoAccess:
                        // FFXIV is running but is more elevated than we are. Report once per episode
                        // and offer a one-click restart-as-administrator (handled outside the lock so
                        // the modal dialog does not block other state reports).
                        _reportError = true;
                        if (_promptedForElevation)
                            return;
                        _promptedForElevation = true;
                        _chatManager.EnqueueMessage(SystemMessageType.Error, Resources.Module_Misc_Connection_AdminRights);
                        shouldPromptForElevation = true;
                        break;

                    case ConnectionState.Connected:
                        _reportError = true;
                        _promptedForElevation = false;
                        if (_memoryReader.ChatLogAvailable)
                            _chatManager.EnqueueMessage(SystemMessageType.Info, Resources.Module_Misc_Connection_Found);
                        else
                            _chatManager.EnqueueMessage(SystemMessageType.Error, Resources.Module_Misc_Connection_AdminRights);
                        break;
                }
            }

            if (shouldPromptForElevation)
                PromptRestartAsAdministrator();
        }

        private void PromptRestartAsAdministrator()
        {
            var synchronizer = _container.Resolve<IUISynchronizer>();
            var choice = synchronizer.RunSync(() => MessageBox.Show(
                Resources.Module_Misc_Connection_ElevationPrompt_Text,
                Resources.Module_Misc_Connection_ElevationPrompt_Title,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning));

            if (choice != DialogResult.Yes)
                return;

            if (RestartAsAdministrator())
                GobchatApplicationContext.ExitGobchat();
        }

        /// <summary>
        /// Relaunches GobchatEx with a request for elevation (the only point where a UAC prompt
        /// appears). Returns false - leaving the current instance running - if the user dismisses the
        /// UAC prompt or the relaunch otherwise fails.
        /// </summary>
        private bool RestartAsAdministrator()
        {
            var exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
            {
                logger.Error("Could not determine the executable path for an elevated restart");
                return false;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = AppContext.BaseDirectory,
                });
                return true;
            }
            catch (Win32Exception e)
            {
                // Most commonly: the user clicked "No" on the UAC prompt.
                logger.Info(e, "Elevated restart was cancelled or denied");
                return false;
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to restart as administrator");
                return false;
            }
        }
    }
}