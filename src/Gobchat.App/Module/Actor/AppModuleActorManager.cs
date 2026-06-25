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

using System;
using Gobchat.Core.Chat;
using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Core.Util;
using System.Threading;
using Gobchat.Module.Actor.Internal;
using Gobchat.Module.MemoryReader;

namespace Gobchat.Module.Actor
{
    public sealed class AppModuleActorManager : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IDIContext _container = null!;
        private IConfigManager _configManager = null!;
        private IMemoryReaderManager _memoryManager = null!;
        private ActorManager _actorManager = null!;
        private IndependentBackgroundWorker _updater = null!;
        private long _updateInterval;

        // Whether to scan nearby player positions each poll. Driven by the range filter (the only
        // consumer of those positions); the current player is always polled regardless. Read on the
        // background worker thread, written on the config thread -> volatile.
        private volatile bool _collectPositions;

        /// <summary>
        /// Adds an <see cref="IActorManager"/> to the app context and supplies it with constant updates by querying a <see cref="IMemoryReaderManager"/>
        /// <br></br>
        /// <br></br>
        /// Requires: <see cref="IGobchatConfig"/> <br></br>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// <br></br>
        /// Provides: <see cref="IActorManager"/> <br></br>
        /// </summary>
        public AppModuleActorManager()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _configManager = _container.Resolve<IConfigManager>();
            _memoryManager = _container.Resolve<IMemoryReaderManager>();

            _actorManager = new ActorManager();
            _updater = new IndependentBackgroundWorker();

            _configManager.AddPropertyChangeListener("behaviour.actor.updateInterval", true, true, ConfigManager_UpdateActorsUpdateInterval);
            _configManager.AddPropertyChangeListener("behaviour.chattabs.data", true, true, ConfigManager_UpdateCollectPositions);

            _container.Register<IActorManager>((c, p) => _actorManager);

            // The worker always runs: the current player (login/identity) is polled every tick. Whether
            // it also scans nearby player positions is gated per-poll by _collectPositions.
            _updater.Start(UpdateJob);
        }

        public void Dispose()
        {
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateActorsUpdateInterval);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateCollectPositions);

            _updater.Dispose();

            _updater = null!;
            _actorManager = null!;
            _container = null!;
            _configManager = null!;
            _memoryManager = null!;
        }

        private void UpdateJob(CancellationToken cancellationToken)
        {
            //TODO some start up logging
            try
            {
                logger.Info("Actor updates started");
                var timer = new System.Diagnostics.Stopwatch();
                while (!cancellationToken.IsCancellationRequested)
                {
                    timer.Restart();

                    UpdateManager();

                    timer.Stop();
                    var timeSpend = timer.Elapsed;

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    // CHT-2: use TotalMilliseconds via the shared helper (Milliseconds is only the 0-999
                    // sub-second part, which makes the worker oversleep once a cycle runs >=1s).
                    int waitTime = WorkerSchedule.RemainingWaitMs(_updateInterval, timeSpend);
                    if (waitTime > 0)
                        Thread.Sleep(waitTime);
                }
            }
            finally
            {
                _actorManager.UpdateManager();
                _actorManager.IsAvailable = false;
                logger.Info("Actor updates concluded");
            }
        }

        private void UpdateManager()
        {
            // Scan nearby positions when either a visible tab uses the range filter (_collectPositions) or
            // the settings window is previewing nearby players (keepalive). The latter lets the range-filter
            // preview / Debug nearby panel populate even with the filter off on every tab.
            var collectPositions = _collectPositions || _actorManager.PreviewKeepaliveActive;
            // "Available" reflects the range-filter data path: only meaningful while we actually scan
            // nearby players. Identity (current player) is handled separately below and is always polled.
            _actorManager.IsAvailable = collectPositions && _memoryManager.PlayerCharactersAvailable;

            Gobchat.Memory.Actor.CurrentPlayer? currentPlayer = null;
            if (_memoryManager.IsConnected)
            {
                if (collectPositions)
                {
                    var characterData = _memoryManager.GetPlayerCharacters();
                    _actorManager.AddUpdate(characterData);
                }
                currentPlayer = _memoryManager.GetCurrentPlayer();
            }

            // With no AddUpdate this clears the actor cache, so stale distances can't linger once the
            // range filter is switched off.
            _actorManager.UpdateManager();
            // null while disconnected / at the title screen -> treated as "logged out"
            _actorManager.SetCurrentPlayer(currentPlayer);
        }

        private void ConfigManager_UpdateActorsUpdateInterval(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            _updateInterval = config.GetProperty<long>("behaviour.actor.updateInterval");
        }

        private void ConfigManager_UpdateCollectPositions(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _collectPositions = RangeFilterConfig.IsActiveForVisibleTabs(config);
            }
            catch (Exception e)
            {
                // Non-critical: on a malformed/unreadable tab config, fall back to "don't scan" rather
                // than tearing down the always-on current-player poll.
                logger.Error(e);
                _collectPositions = false;
            }
        }
    }
}