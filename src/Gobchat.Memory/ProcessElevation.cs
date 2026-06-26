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
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace Gobchat.Memory
{
    /// <summary>
    /// Helpers to tell apart "FFXIV is not running" from "FFXIV is running, but we cannot attach to
    /// it because it runs at a higher integrity level (e.g. as administrator) than we do".
    /// A non-elevated process can enumerate an elevated process by name, but cannot open it with the
    /// access Sharlayan's attach needs - the <see cref="OpenProcess"/> call fails with ERROR_ACCESS_DENIED.
    /// </summary>
    internal static class ProcessElevation
    {
        // The mask Sharlayan's attach actually requires: its MemoryHandler constructor sets
        // Process.EnableRaisingEvents = true, which makes .NET re-open the process via
        // GetOrOpenProcessHandle() -> GetProcessHandle(PROCESS_ALL_ACCESS). Probing only the weaker
        // read mask (PROCESS_VM_READ | PROCESS_QUERY_INFORMATION) lets a process that is readable but
        // not fully openable slip through as "not blocked", so the attach then fails with
        // ERROR_ACCESS_DENIED and the user never gets the restart-as-administrator prompt. Probe with
        // the same full mask the attach uses so the prediction is honest.
        private const uint PROCESS_ALL_ACCESS = 0x1FFFFF;
        private const int ERROR_ACCESS_DENIED = 5;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        /// <summary>
        /// True if the current process runs with an elevated (administrator) token. When we are
        /// already elevated, suggesting "restart as administrator" is pointless, so callers use this
        /// to gate that suggestion.
        /// </summary>
        public static bool IsCurrentProcessElevated()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        /// <summary>
        /// True if opening the given process with the access Sharlayan's attach needs is denied (the
        /// definitive sign of an integrity-level mismatch). Returns false if the process can be opened,
        /// or if it is gone / fails for any other reason - we only want to react to a genuine access denial.
        /// </summary>
        public static bool IsAttachAccessDenied(int processId)
        {
            var handle = OpenProcess(PROCESS_ALL_ACCESS, false, processId);
            if (handle != IntPtr.Zero)
            {
                CloseHandle(handle);
                return false;
            }
            return Marshal.GetLastWin32Error() == ERROR_ACCESS_DENIED;
        }
    }
}
