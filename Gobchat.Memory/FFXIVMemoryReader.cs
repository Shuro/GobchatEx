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

using NLog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Gobchat.Memory
{
    public sealed class FFXIVMemoryReader : IDisposable
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly ProcessConnector _processConnector;

        private readonly Chat.ChatlogMemoryReader _chatlogProcessor;
        private readonly Actor.PlayerLocationMemoryReader _locationProcessor;

        private readonly Window.WindowObserver _windowScanner = new Window.WindowObserver();
        private bool _windowVisible = true;

        public bool ObserveGameWindow
        {
            get { return _windowScanner.Enabled; }
            set
            {
                if (value)
                    _windowScanner.StartObserving();
                else
                    _windowScanner.StopObserving();
            }
        }

        /// <summary>
        /// Fired when the currently tracked FFXIV process changes
        /// </summary>
        public event EventHandler<ProcessChangeEventArgs>? OnProcessChanged;

        /// <summary>
        /// Fired when the currently tracked FFXIV window is moved into the foreground or into the background
        /// </summary>
        public event EventHandler<WindowFocusChangedEventArgs>? OnWindowFocusChanged;

        /// <summary>
        /// Needs to be disposed on the same thread it was created
        /// </summary>
        public FFXIVMemoryReader()
        {
            _processConnector = new ProcessConnector();
            _chatlogProcessor = new Chat.ChatlogMemoryReader(_processConnector);
            _locationProcessor = new Actor.PlayerLocationMemoryReader(_processConnector);

            _processConnector.OnConnectionLost += ProcessConnector_OnConnectionLost;
        }

        public void Initialize()
        {
            _processConnector.OnMemoryException += ProcessConnector_OnMemoryException;
            _windowScanner.ActiveWindowChangedEvent += OnEvent_ActiveWindowChangedEvent;
        }

        private void ProcessConnector_OnMemoryException(object sender, NLog.Logger log, Exception e)
        {
            // Upstream Sharlayan no longer reports a severity, so everything is logged as a warning.
            logger.Warn(e, () => $"Memory error in {sender}");
        }

        public void Dispose()
        {
            _processConnector.Disconnect();
            _processConnector.OnMemoryException -= ProcessConnector_OnMemoryException;
            _windowScanner.ActiveWindowChangedEvent -= OnEvent_ActiveWindowChangedEvent;
            _windowScanner.Dispose();
        }

        private void OnEvent_ActiveWindowChangedEvent(object? sender, Window.WindowObserver.ActiveWindowChangedEventArgs e)
        {
            if (!FFXIVProcessValid || e.ProcessId != FFXIVProcessId)
                return;

            logger.Debug(() => e.ToString());

            switch (e.EventType)
            {
                case Window.WindowObserver.EventTypeEnum.Maximized:
                case Window.WindowObserver.EventTypeEnum.Minimized:
                    var isVisible = e.EventType == Window.WindowObserver.EventTypeEnum.Maximized;
                    if (isVisible != _windowVisible)
                    {
                        _windowVisible = !_windowVisible;
                        OnWindowFocusChanged?.Invoke(this, new WindowFocusChangedEventArgs(_windowVisible));
                    }
                    break;
            }
        }

        #region process handling

        public bool FFXIVProcessValid { get { return _processConnector.FFXIVProcessValid; } }

        public int FFXIVProcessId { get { return _processConnector.FFXIVProcessId; } }

        /// <summary>
        /// True if the most recent attach found every memory signature Gobchat needs. False when
        /// attached to a process whose signatures are missing (outdated signature data), so callers
        /// can report the connection as unusable instead of "connected". Only meaningful while
        /// <see cref="FFXIVProcessValid"/> is true.
        /// </summary>
        public bool SignaturesValid { get; private set; }

        public List<int> GetFFXIVProcesses()
        {
            return _processConnector.GetFFXIVProcesses();
        }

        /// <summary>
        /// True if an FFXIV process is running but cannot be read because it is more elevated than we are.
        /// </summary>
        public bool IsBlockedByElevation()
        {
            return _processConnector.IsBlockedByElevation();
        }

        public bool IsConnectedTo(int processId = -1)
        {
            return FFXIVProcessValid && (processId <= 0 || processId == FFXIVProcessId);
        }

        public bool TryConnectingToFFXIV(int processId = -1)
        {
            if (IsConnectedTo(processId))
                return true; //do nothing if it's already connected to the correct process or if any process is valid

            if (processId <= 0)
                processId = _processConnector.GetFFXIVProcesses().FirstOrDefault();

            if (processId <= 0) // no process available
            {
                if (_processConnector.Disconnect()) //ensure it's disconnected
                    OnProcessChanged?.Invoke(this, new ProcessChangeEventArgs(FFXIVProcessValid, FFXIVProcessId));
                return false;
            }

            if (ConnectToFFXIV(processId))
                OnProcessChanged?.Invoke(this, new ProcessChangeEventArgs(FFXIVProcessValid, FFXIVProcessId));
            return FFXIVProcessValid; // either connected or not
        }

        public bool DisconnectFromFFXIV()
        {
            if (_processConnector.Disconnect())
                OnProcessChanged?.Invoke(this, new ProcessChangeEventArgs(FFXIVProcessValid, FFXIVProcessId));
            return !FFXIVProcessValid;
        }

        private bool ConnectToFFXIV(int processId)
        {
            SignaturesValid = false;

            if (!_processConnector.ConnectToProcess(processId))
                return false;

            var handler = _processConnector.ActiveHandler;
            if (handler == null)
                return false;

            var signaturesOfInterest = new string[] { Sharlayan.Signatures.CHATLOG_KEY, Sharlayan.Signatures.CHARMAP_KEY };
            var availableSignatures = handler.Scanner.Locations.Keys;
            var foundSignatures = signaturesOfInterest.Intersect(availableSignatures).ToArray();
            var missingSignatures = signaturesOfInterest.Except(foundSignatures).ToArray();
            logger.Info($"Signatures found: {string.Join(", ", foundSignatures)}");
            if (missingSignatures.Length > 0)
                logger.Error($"Signatures not found: {string.Join(", ", missingSignatures)}");
            // Attached, but record whether the signatures are actually usable so the connection can be
            // reported honestly (OutdatedSignatures) rather than as a working connection.
            SignaturesValid = missingSignatures.Length == 0;
            return true;
        }

        private void ProcessConnector_OnConnectionLost(object? sender, EventArgs e)
        {
            OnProcessChanged?.Invoke(this, new ProcessChangeEventArgs(FFXIVProcessValid, FFXIVProcessId));
        }

        #endregion process handling

        #region feature - chat log

        public bool ChatLogAvailable { get { return _chatlogProcessor.ChatLogAvailable; } }

        public List<Chat.ChatlogItem> GetNewestChatlog()
        {
            if (!FFXIVProcessValid)
                return new List<Chat.ChatlogItem>();
            return _chatlogProcessor.GetNewestChatlog();
        }

        #endregion feature - chat log

        #region feature - character data

        public bool PlayerCharactersAvailable { get { return _locationProcessor.LocationAvailable; } }

        public List<Actor.PlayerCharacter> GetPlayerCharacters()
        {
            if (!FFXIVProcessValid)
                return new List<Actor.PlayerCharacter>();
            return _locationProcessor.GetPlayerCharacters();
        }

        /// <summary>
        /// The locally logged-in character, or <c>null</c> when disconnected or at the title /
        /// character-select screen.
        /// </summary>
        public Actor.CurrentPlayer? GetCurrentPlayer()
        {
            if (!FFXIVProcessValid)
                return null;
            return _locationProcessor.GetCurrentPlayer();
        }

        #endregion feature - character data
    }
}