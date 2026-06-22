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
using Gobchat.Module.Actor;
using Gobchat.Module.MemoryReader;
using System;
using System.Threading.Tasks;

namespace Gobchat.Module.UI
{
    public sealed class AppModuleMemoryToUI : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        private IDIContext _container = null!;

        private IBrowserAPIManager _browserAPIManager = null!;
        private IMemoryReaderManager _memoryManager = null!;
        private IActorManager _actorManager = null!;

        /// <summary>
        /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// Requires: <see cref="IActorManager"/> <br></br>
        /// <br></br>
        /// </summary>
        public AppModuleMemoryToUI()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _browserAPIManager = _container.Resolve<IBrowserAPIManager>();
            _memoryManager = _container.Resolve<IMemoryReaderManager>();
            _actorManager = _container.Resolve<IActorManager>();

            _browserAPIManager.MemoryHandler = new BrowserMemoryHandler(this);

            // Keep the overlay's connection/waiting banner in sync with connection + login state.
            _memoryManager.OnConnectionStateChanged += MemoryManager_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged += ActorManager_OnCurrentPlayerChanged;
            _browserAPIManager.OnUIReadyChanged += BrowserAPIManager_OnUIReadyChanged;
        }

        public void Dispose()
        {
            _memoryManager.OnConnectionStateChanged -= MemoryManager_OnConnectionStateChanged;
            _actorManager.OnCurrentPlayerChanged -= ActorManager_OnCurrentPlayerChanged;
            _browserAPIManager.OnUIReadyChanged -= BrowserAPIManager_OnUIReadyChanged;

            _browserAPIManager.MemoryHandler = null;
            _browserAPIManager = null!;
            _memoryManager = null!;
            _actorManager = null!;
            _container = null!;
        }

        private void MemoryManager_OnConnectionStateChanged(object? sender, ConnectionEventArgs e) => PushConnectionState();

        private void ActorManager_OnCurrentPlayerChanged(object? sender, CurrentPlayerChangedEventArgs e) => PushConnectionState();

        private void BrowserAPIManager_OnUIReadyChanged(object? sender, UIReadyChangedEventArgs e)
        {
            // Send the current state once the overlay is ready, since state changes may predate it.
            if (e.IsUIReady)
                PushConnectionState();
        }

        private void PushConnectionState()
        {
            try
            {
                _browserAPIManager.DispatchEventToBrowser(
                    new ConnectionStateWebEvent((int)_memoryManager.ConnectionState, _actorManager.GetActivePlayerName()));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private sealed class BrowserMemoryHandler : IBrowserMemoryHandler
        {
            private AppModuleMemoryToUI _module;

            public BrowserMemoryHandler(AppModuleMemoryToUI module)
            {
                _module = module ?? throw new ArgumentNullException(nameof(module));
            }

            public async Task<bool> AttachToFFXIVProcess(int id)
            {
                // The real connect outcome is asynchronous and arrives via OnConnectionStateChanged;
                // the returned bool only means the request was accepted (a known FFXIV process id),
                // not that the attach succeeded. Reject unknown ids instead of firing ConnectTo(0).
                var attachable = _module._memoryManager.GetProcessIds();
                if (!attachable.Contains(id))
                {
                    logger.Warn($"Ignoring attach request for unknown FFXIV process id {id}");
                    return false;
                }
                _module._memoryManager.ConnectTo(id);
                return true;
            }

            public async Task<int[]> GetAttachableFFXIVProcesses()
            {
                return _module._memoryManager.GetProcessIds().ToArray();
            }

            public async Task<AttachedProcessInfo> GetAttachedFFXIVProcess()
            {
                var state = _module._memoryManager.ConnectionState;
                var processId = _module._memoryManager.ConnectedProcessId;
                return new AttachedProcessInfo(state, processId);
            }
        }
    }
}