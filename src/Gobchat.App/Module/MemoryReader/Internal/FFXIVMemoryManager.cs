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
using Gobchat.Memory;
using Gobchat.Memory.Actor;
using Gobchat.Memory.Chat;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Gobchat.Module.MemoryReader.Internal
{
    internal sealed class FFXIVMemoryManager : IMemoryReaderManager, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Code Quality", "IDE0069:Disposable fields should be disposed", Justification = "Disposed in dispose")]
        private FFXIVMemoryReader _memoryReader;

        private IDIContext _container;
        private IndependentBackgroundWorker _worker;
        // Serialises connect-worker restarts so only one connect loop (one Sharlayan attach) is ever live.
        private readonly object _restartLock = new object();
        private volatile ConnectionState _connectionState = ConnectionState.NotInitialized;
        private volatile int _preferredFFXIVProcess = -1;

        public FFXIVMemoryManager(IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _worker = new IndependentBackgroundWorker();

            //needs to be done on the same thread as dispose, anchore it to ui thread, because that one never changes
            var synchronizer = container.Resolve<IUISynchronizer>();
            _memoryReader = synchronizer.RunSync(() => new FFXIVMemoryReader());
            _memoryReader.Initialize();

            _memoryReader.OnProcessChanged += MemoryReader_OnProcessChanged;
            _memoryReader.OnWindowFocusChanged += MemoryReader_OnWindowFocusChanged;
            _worker.Start(Task_ConnectMemoryReader);
        }

        public void Dispose()
        {
            // Hold _restartLock so a queued RestartConnectWorker can't revive the worker after teardown;
            // once _worker is null it returns early.
            lock (_restartLock)
            {
                _worker?.Dispose();
                _worker = null!;
            }

            var synchronizer = _container.Resolve<IUISynchronizer>();
            synchronizer.RunSync(() => _memoryReader.Dispose());

            _memoryReader = null!;
            _container = null!;
        }

        #region event handler

        private void MemoryReader_OnProcessChanged(object? sender, ProcessChangeEventArgs e)
        {
            if (e.IsProcessValid)
            {
                logger.Info("FFXIV process detected");
            }
            else
            {
                logger.Info("No FFXIV process detected");
                SetConnectionState(ConnectionState.NotFound);
                RestartConnectWorker();
            }
        }

        private void MemoryReader_OnWindowFocusChanged(object? sender, WindowFocusChangedEventArgs e)
        {
            OnWindowFocusChanged?.Invoke(this, e);
        }

        #endregion event handler

        // Consecutive elevation-blocked attempts to tolerate before declaring NoAccess. A freshly
        // launched FFXIV briefly refuses to be opened (looks like an access denial) before it is
        // readable, so we retry a few times and only treat a persistent block as a real elevation issue.
        private const int ElevationRetryThreshold = 4;

        // Restarts the connect loop off the calling thread. Stop(true) blocks until
        // Task_ConnectMemoryReader returns, and that task raises OnConnectionStateChanged which
        // subscribers marshal back onto the UI thread (RunSync). Restarting inline from the UI thread
        // (e.g. ConnectTo from the settings dialog) would therefore deadlock. The lock serialises
        // restarts so the previous loop fully stops before a new one starts - one Sharlayan attach.
        private void RestartConnectWorker()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    lock (_restartLock)
                    {
                        var worker = _worker;
                        if (worker == null)
                            return; // disposed while this restart was queued

                        try
                        {
                            worker.Stop(true);
                        }
                        catch (Exception ex)
                        {
                            // A faulted connect loop must not block the restart, and (being fire-and-forget)
                            // would otherwise vanish as an unobserved task exception.
                            logger.Warn(ex, "Previous connect worker faulted while stopping; restarting anyway");
                        }

                        worker.Start(Task_ConnectMemoryReader);
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to restart the connect worker");
                }
            });
        }

        private void Task_ConnectMemoryReader(CancellationToken cancellationToken)
        {
            long specificProcessTimeout = DateTimeOffset.Now.Ticks + TimeSpan.FromSeconds(20).Ticks;
            var elevationBlockedAttempts = 0;

            while (!cancellationToken.IsCancellationRequested && !_memoryReader.IsConnectedTo(_preferredFFXIVProcess))
            {
                _memoryReader.TryConnectingToFFXIV(_preferredFFXIVProcess);
                if (_memoryReader.FFXIVProcessValid)
                    break;

                if (_preferredFFXIVProcess > 0)
                    if (specificProcessTimeout <= DateTimeOffset.Now.Ticks)
                        _preferredFFXIVProcess = -1;

                // A found-but-unreadable elevated process makes Sharlayan fail to attach, so the loop
                // never connects. Only report NoAccess after it stays blocked for several attempts, so
                // a transient access denial during game startup does not trigger the elevation prompt.
                if (_memoryReader.IsBlockedByElevation())
                {
                    if (++elevationBlockedAttempts >= ElevationRetryThreshold)
                        SetConnectionState(ConnectionState.NoAccess);
                    else
                        SetConnectionState(ConnectionState.Searching);
                }
                else
                {
                    elevationBlockedAttempts = 0;
                    SetConnectionState(ConnectionState.Searching);
                }
                Thread.Sleep(1000);
            }

            if (_memoryReader.FFXIVProcessValid)
            {
                // Attached, but if we still cannot read the chatlog and a found process is locked
                // behind a higher integrity level, this is an elevation problem - not a real connect.
                if (!_memoryReader.ChatLogAvailable && _memoryReader.IsBlockedByElevation())
                    SetConnectionState(ConnectionState.NoAccess);
                // Attached but the signatures Gobchat needs are missing: the connection is unusable,
                // so report it honestly instead of as Connected.
                else if (!_memoryReader.SignaturesValid)
                    SetConnectionState(ConnectionState.OutdatedSignatures);
                else
                    SetConnectionState(ConnectionState.Connected);
            }
            else if (_memoryReader.IsBlockedByElevation())
            {
                SetConnectionState(ConnectionState.NoAccess);
            }
            else
            {
                SetConnectionState(ConnectionState.NotFound);
            }
        }

        private void SetConnectionState(ConnectionState state)
        {
            if (_connectionState == state)
                return;
            _connectionState = state;
            OnConnectionStateChanged?.Invoke(this, new ConnectionEventArgs(state));
        }

        #region interface

        public ConnectionState ConnectionState => _connectionState;

        public bool IsConnected => _connectionState == ConnectionState.Connected;

        public int ConnectedProcessId => _memoryReader.FFXIVProcessId;

        public bool ChatLogAvailable => _memoryReader.ChatLogAvailable;

        public bool PlayerCharactersAvailable => _memoryReader.PlayerCharactersAvailable;

        public bool IsBlockedByElevation => _memoryReader.IsBlockedByElevation();

        public bool ObserveGameWindow
        {
            get => _memoryReader.ObserveGameWindow;
            set => _memoryReader.ObserveGameWindow = value;
        }

        public void FocusGameWindow()
        {
            _memoryReader.FocusGameWindow();
        }

        public event EventHandler<ConnectionEventArgs>? OnConnectionStateChanged;

        public event EventHandler<WindowFocusChangedEventArgs>? OnWindowFocusChanged;

        public List<int> GetProcessIds()
        {
            return _memoryReader.GetFFXIVProcesses();
        }

        public void ConnectTo(int processId)
        {
            if (_memoryReader.IsConnectedTo(processId))
                return;

            // Set the target, then restart off-thread so a UI-thread caller never blocks on Stop(true).
            _preferredFFXIVProcess = processId;
            RestartConnectWorker();
        }

        public List<PlayerCharacter> GetPlayerCharacters()
        {
            return _memoryReader.GetPlayerCharacters();
        }

        public CurrentPlayer? GetCurrentPlayer()
        {
            return _memoryReader.GetCurrentPlayer();
        }

        public List<ChatlogItem> GetNewestChatlog()
        {
            return _memoryReader.GetNewestChatlog();
        }

        #endregion interface
    }
}