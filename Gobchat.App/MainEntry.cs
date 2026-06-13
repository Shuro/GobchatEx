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

using System.Globalization;
using System.Text;

namespace Gobchat
{
    public static class App
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
            ApplicationConfiguration.Initialize();
            System.Windows.Forms.Application.Run(new global::Gobchat.Core.Runtime.GobchatApplicationContext());
        }
    }
}