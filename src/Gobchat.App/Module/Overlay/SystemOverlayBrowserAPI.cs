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
using Gobchat.UI.Web;
using System.Threading.Tasks;

namespace Gobchat.Module.Overlay
{
    /// <summary>
    /// The single, minimal JS&#8594;C# API bound to the otherwise GobchatAPI-free system overlay
    /// (see <see cref="AppModuleSystemOverlay"/>). It exists only so the greeter's close (X) button can
    /// quit the application; deliberately nothing else is exposed to that page.
    /// </summary>
    internal sealed class SystemOverlayBrowserAPI : IBrowserAPI
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public string APIName => "GobchatSystemAPI";

        // The greeter's X. Shutdown runs off the UI thread inside ExitGobchat, so this returns promptly.
        public async Task CloseGobchat()
        {
            logger.Info("User requests shutdown via greeter close button");
            GobchatApplicationContext.ExitGobchat();
        }
    }
}
