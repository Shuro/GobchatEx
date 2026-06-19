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
using System.Windows.Forms;
using Gobchat.Core.Runtime;

namespace Gobchat
{
    public static class Program
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        [System.STAThread]
        private static void Main(string[] args)
        {
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
            // --settings: developer/debug mode that keeps the chat overlay hidden and opens the
            // settings dialog automatically, for quick access to the config UI while developing.
            var settingsOnly = args.Any(a => string.Equals(a, "--settings", StringComparison.OrdinalIgnoreCase));

            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.Run(new GobchatApplicationContext(new StartupOptions(settingsOnly)));
        }
    }
}