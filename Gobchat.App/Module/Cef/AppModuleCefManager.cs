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

using Gobchat.UI.Web;
using Gobchat.Core.Runtime;

namespace Gobchat.Module.Cef
{
    public sealed class AppModuleCefManager : IApplicationModule
    {
        private IUISynchronizer _synchronizer;

        /// <summary>
        /// Requires: <see cref="IUISynchronizer"/> <br></br>
        /// <br></br>
        /// Controls lifetime of <see cref="CEFManager"/>
        /// </summary>
        public AppModuleCefManager()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            // WebView2 needs a writable folder for its cache/state; the application folder can be
            // Program Files, so keep it under the user's AppData next to the rest of the profile.
            var userDataFolder = System.IO.Path.Combine(GobchatContext.AppDataLocation, "WebView2");
            System.IO.Directory.CreateDirectory(userDataFolder);
            CEFManager.UserDataFolder = userDataFolder;

            _synchronizer = container.Resolve<IUISynchronizer>();
            _synchronizer.RunSync(() => global::Gobchat.UI.Web.CEFManager.Initialize());
        }

        public void Dispose()
        {
            _synchronizer?.RunSync(() => global::Gobchat.UI.Web.CEFManager.Dispose());
            _synchronizer = null;
        }
    }
}