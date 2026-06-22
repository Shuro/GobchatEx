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
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Gobchat.Core.Runtime;
using Velopack;

namespace Gobchat
{
    public static class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // Held for the whole process lifetime so a second launch can detect us. Kept in a static
        // field so it isn't garbage-collected; the OS releases it automatically on process exit,
        // so no explicit teardown is needed.
        private static Mutex? _instanceMutex;

        [System.STAThread]
        private static void Main(string[] args)
        {
            // Must run before anything else: handles Velopack install/update/uninstall hooks (which
            // run briefly and exit the process) when launched by Setup.exe or the updater. In a normal
            // launch and in non-Velopack builds (dev/portable) it returns immediately. See AppModuleUpdater.
            VelopackApp.Build().Run();

            // Legacy code pages aren't enabled by default; register the provider so Sharlayan
            // can decode Shift-JIS (932). The assembly ships in the .NET 10 framework (no package needed).
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");

            // Last-resort logging for any exception that slips past a handler. Window-procedure faults are
            // already contained in OverlayForm.WndProc; this records the rest instead of silently
            // fail-fasting.
#if DEBUG
            // In debug builds, let UI-thread exceptions surface (default ThrowException mode) so real
            // handler bugs aren't masked while developing.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
#else
            // In release, route UI-thread exceptions to ThreadException and log-and-continue so a single
            // transient fault doesn't take the whole overlay down.
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
                logger.Error(e.Exception, "Unhandled exception on the UI thread");
#endif
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                logger.Fatal(e.ExceptionObject as Exception, "Unhandled exception (terminating: {0})", e.IsTerminating);

            // Generated from the Application* MSBuild properties in Gobchat.csproj
            // (visual styles, text rendering, high-DPI mode).
            // --dry-run: developer/debug mode that does NOT attach to FFXIV. A fake memory manager
            // simulates a connected game and the settings dialog auto-opens on the Debug page, whose
            // Dry Run section injects characters/logins/chat by hand. See StartupOptions.DryRun.
            var dryRun = args.Any(a => string.Equals(a, "--dry-run", StringComparison.OrdinalIgnoreCase));

            // Only one GobchatEx may run at a time: two overlays would fight for the same topmost
            // composition space, both poll the FFXIV process, and both write the same %AppData% files.
            // --dry-run is exempt so a developer can run a no-game dry-run instance alongside a real one.
            // (This sits after VelopackApp.Build().Run(), so the install/update hook runs it handles -
            // which exit the process before returning here - never trip the guard.)
            if (!dryRun && !TryAcquireSingleInstanceLock())
            {
                // Tray/overlay app: the running instance has no obvious window, so tell the user
                // instead of exiting silently.
                MessageBox.Show("GobchatEx is already running.", "GobchatEx", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.Run(new GobchatApplicationContext(new StartupOptions(dryRun)));
        }

        // Returns true if this process is the first instance (lock acquired), false if another
        // GobchatEx already holds it. The Local\ prefix scopes the mutex to the current interactive
        // logon session, which is the right granularity for a single-user desktop overlay and avoids
        // the cross-session ACL setup a Global\ mutex would need. The GUID keeps the name unique to
        // GobchatEx so it can't collide with another application's mutex.
        private static bool TryAcquireSingleInstanceLock()
        {
            _instanceMutex = new Mutex(initiallyOwned: false, @"Local\GobchatEx-SingleInstance-8F2C4A6E-1B3D-4E5F-9A7C-2D6E8F0A1B3C");
            try
            {
                return _instanceMutex.WaitOne(TimeSpan.Zero, exitContext: false);
            }
            catch (AbandonedMutexException)
            {
                // A previous instance crashed without releasing the mutex; WaitOne hands us ownership
                // anyway. Treat it as a successful acquire so a crash doesn't wedge every later launch.
                return true;
            }
        }
    }
}