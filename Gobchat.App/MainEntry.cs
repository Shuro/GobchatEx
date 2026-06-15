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
using Gobchat.Core.Runtime;

namespace Gobchat
{
    public static class Program
    {
        [System.STAThread]
        private static void Main(string[] args)
        {
            // .NET (Core) does not ship legacy code pages in-box; Sharlayan needs Shift-JIS (932)
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("en");
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("en");

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