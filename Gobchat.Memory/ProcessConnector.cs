/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
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

using Sharlayan;
using Sharlayan.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Gobchat.Memory
{
    /// <summary>
    /// Provides functionality to search, connect and monitor FFXIV processes for the Sharlayan framework.
    /// Owns the <see cref="MemoryHandler"/> of the currently connected process; the chat and actor readers
    /// access it through <see cref="ActiveHandler"/>. Only one FFXIV process is tracked per app process.
    /// </summary>
    internal sealed class ProcessConnector
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly TimeSpan ScanTimeout = TimeSpan.FromSeconds(120);

        public bool FFXIVProcessValid { get; private set; }

        public int FFXIVProcessId { get; private set; } = 0;

        /// <summary>
        /// The Sharlayan handler for the currently connected process, or <c>null</c> while disconnected.
        /// </summary>
        public MemoryHandler? ActiveHandler { get; private set; }

        public event EventHandler? OnConnectionLost;

        /// <summary>
        /// Forwards <see cref="MemoryHandler.OnException"/> of the currently active handler.
        /// </summary>
        public event Action<object, NLog.Logger, Exception>? OnMemoryException;

        private Process? _connectedTo = null;

        public ProcessConnector()
        {
        }

        public List<int> GetFFXIVProcesses()
        {
            var processes = Process.GetProcessesByName("ffxiv_dx11");
            return processes.Select(p => p.Id).ToList();
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="processId"></param>
        /// <returns>true if the connection to the given process id is still valid or was successful created</returns>
        public bool ConnectToProcess(int processId)
        {
            if (FFXIVProcessValid)
            {
                if (FFXIVProcessId == processId)
                    return true;
                else
                    Disconnect();
            }

            try
            {
                var process = GetProcessById(processId);
                if (process == null)
                    return false;

                ConnectTo(process);

                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;

                _connectedTo = process;
                FFXIVProcessValid = true;
            }
            catch (Exception e)
            {
                logger.Error(e);
                Disconnect(); // ensure a partially added handler is removed again
                return false;
            }

            return FFXIVProcessValid;
        }

        private Process? GetProcessById(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process != null && process.ProcessName.Equals("ffxiv_dx11"))
                    return process;
                return null;
            }
            catch (ArgumentException) // fires if there is no process with the given id
            {
                return null;
            }
        }

        /// <summary>
        ///
        /// </summary>
        /// <returns>true if the previous process was valid before disconnecting</returns>
        public bool Disconnect()
        {
            var wasConnected = FFXIVProcessValid;

            if (ActiveHandler != null)
            {
                ActiveHandler.OnException -= Handler_OnException;
                try
                {
                    SharlayanMemoryManager.Instance.RemoveHandler(FFXIVProcessId);
                }
                catch (Exception e)
                {
                    logger.Warn(e, "Error while removing Sharlayan handler");
                }
                ActiveHandler = null;
            }

            if (_connectedTo != null)
                _connectedTo.Exited -= Process_Exited;
            _connectedTo = null;

            FFXIVProcessValid = false;
            return wasConnected;
        }

        private void ConnectTo(Process process)
        {
            var configuration = new SharlayanConfiguration
            {
                ProcessModel = new ProcessModel
                {
                    Process = process
                },
                GameInstallPath = TryGetGameInstallPath(process),
            };

            FFXIVProcessId = process.Id;

            var handler = SharlayanMemoryManager.Instance.AddHandler(configuration);
            handler.OnException += Handler_OnException;
            ActiveHandler = handler;

            WaitForScan(handler);
        }

        /// <summary>
        /// Best-effort: the directory containing ffxiv_dx11.exe (which holds the sqpack data).
        /// Optional - Sharlayan falls back to the attached process' main module when this is null.
        /// </summary>
        private static string? TryGetGameInstallPath(Process process)
        {
            try
            {
                var fileName = process.MainModule?.FileName;
                return string.IsNullOrEmpty(fileName) ? null : System.IO.Path.GetDirectoryName(fileName);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void WaitForScan(MemoryHandler handler)
        {
            // AddHandler kicks off the signature scan asynchronously. There is a brief window right
            // after it returns where the scan task has not started yet and IsScanning is still false,
            // so we cannot rely on IsScanning alone. Instead we wait for the signatures Gobchat needs
            // to actually appear (or for a completed scan that genuinely lacks them, or a timeout).
            var deadline = DateTimeOffset.Now.Add(ScanTimeout);
            while (DateTimeOffset.Now < deadline)
            {
                var locations = handler.Scanner.Locations.Keys;
                if (locations.Contains(Signatures.CHATLOG_KEY) && locations.Contains(Signatures.CHARMAP_KEY))
                    return;

                // Scan finished at least once but the keys are not present -> genuine miss, stop waiting.
                if (!handler.Scanner.IsScanning && handler.ScanCount > 0)
                    return;

                logger.Debug("Scanning for FFXIV signatures...");
                Thread.Sleep(100);
            }

            logger.Warn($"FFXIV signature scan did not finish within {ScanTimeout.TotalSeconds} seconds");
        }

        private void Handler_OnException(object sender, NLog.Logger log, Exception ex)
        {
            OnMemoryException?.Invoke(sender, log, ex);
        }

        private void Process_Exited(object? sender, EventArgs e)
        {
            if (sender is Process process)
            {
                if (process.Id == FFXIVProcessId)
                {
                    Disconnect();
                    OnConnectionLost?.Invoke(this, new EventArgs());
                }
            }
        }
    }
}
