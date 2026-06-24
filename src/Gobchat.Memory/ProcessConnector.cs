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

        // MEM-1: Process_Exited fires on a thread-pool thread and disconnects, mutating the connection
        // state (FFXIVProcessValid/FFXIVProcessId/_connectedTo/ActiveHandler) concurrently with the
        // connection-management thread calling ConnectToProcess/Disconnect. Serialize all three mutators
        // on this lock so no thread can observe a half-disconnected state or re-validate a dead process.
        // (Monitor is recursive, so ConnectToProcess/Process_Exited calling Disconnect re-enters safely.)
        private readonly object _stateLock = new object();

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
            try
            {
                return processes.Select(p => p.Id).ToList();
            }
            finally
            {
                // GetProcessesByName hands back live Process objects (each a kernel handle); we only
                // need their ids, so release the handles instead of leaking one per call per second.
                foreach (var p in processes)
                    p.Dispose();
            }
        }

        /// <summary>
        /// True if a running FFXIV process exists that we cannot read because it runs at a higher
        /// integrity level than we do (typically FFXIV started as administrator while we did not).
        /// Always false when we are already elevated, since restarting as admin would not help then.
        /// </summary>
        public bool IsBlockedByElevation()
        {
            if (ProcessElevation.IsCurrentProcessElevated())
                return false;

            foreach (var processId in GetFFXIVProcesses())
                if (ProcessElevation.IsReadAccessDenied(processId))
                    return true;

            return false;
        }

        /// <summary>
        /// Connects to the given FFXIV process and blocks until the Sharlayan signature scan finds the
        /// signatures Gobchat needs (or the scan genuinely misses them, or <see cref="ScanTimeout"/>
        /// elapses).
        /// <para>
        /// MEM-5 — thread contract: this call may block the caller for up to <see cref="ScanTimeout"/>
        /// (signature scanning on a cold start). It MUST be invoked from a background/connection thread,
        /// never the UI/STA thread, or the tray app freezes for the duration. The app satisfies this by
        /// driving connect from the memory poll/connect worker (AppModuleMemoryReader), never the UI thread.
        /// </para>
        /// </summary>
        /// <param name="processId">The FFXIV process id to attach to.</param>
        /// <returns>true if the connection to the given process id is still valid or was successful created</returns>
        public bool ConnectToProcess(int processId)
        {
            // MEM-1: held across the whole connect (incl. WaitForScan) so a process exit cannot interleave
            // and disconnect a half-built connection or have ConnectToProcess re-validate it afterwards.
            lock (_stateLock)
            {
                if (FFXIVProcessValid)
                {
                    if (FFXIVProcessId == processId)
                        return true;
                    else
                        DisconnectLocked();
                }

                try
                {
                    var process = GetProcessById(processId);
                    if (process == null)
                        return false;

                    // Own the handle from here on, so a failure in ConnectTo still disposes it via Disconnect.
                    _connectedTo = process;

                    ConnectTo(process);

                    process.EnableRaisingEvents = true;
                    process.Exited += Process_Exited;

                    FFXIVProcessValid = true;
                }
                catch (Exception e)
                {
                    logger.Error(e);
                    DisconnectLocked(); // ensure a partially added handler is removed again
                    return false;
                }

                return FFXIVProcessValid;
            }
        }

        private Process? GetProcessById(int processId)
        {
            try
            {
                var process = Process.GetProcessById(processId);
                if (process != null && process.ProcessName.Equals("ffxiv_dx11"))
                    return process;
                // Wrong process name: dispose the handle we just opened before discarding it.
                process?.Dispose();
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
            lock (_stateLock)
            {
                return DisconnectLocked();
            }
        }

        private bool DisconnectLocked()
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
            {
                _connectedTo.Exited -= Process_Exited;
                _connectedTo.Dispose();
            }
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

        // MEM-5: bounded spin — blocks the calling (connection) thread up to ScanTimeout. See the thread
        // contract on ConnectToProcess: must not run on the UI/STA thread.
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
            if (sender is not Process process)
                return;

            bool connectionLost = false;
            lock (_stateLock)
            {
                if (process.Id == FFXIVProcessId)
                {
                    DisconnectLocked();
                    connectionLost = true;
                }
            }

            // Fire outside the lock so a subscriber that re-enters the connector cannot deadlock.
            if (connectionLost)
                OnConnectionLost?.Invoke(this, new EventArgs());
        }
    }
}
