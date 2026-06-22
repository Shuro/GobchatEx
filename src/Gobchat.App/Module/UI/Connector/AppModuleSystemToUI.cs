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
using Gobchat.Module.Overlay;
using Gobchat.UI.Forms;
using Gobchat.UI.Web;
using System;
using System.Threading.Tasks;

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Bridges the system overlay's notification toasts to the JS api: registers an
    /// <see cref="IBrowserSystemHandler"/> on the <see cref="IBrowserAPIManager"/> that pushes a
    /// <see cref="ShowNotificationWebEvent"/> to the system overlay. Must initialize after
    /// <see cref="AppModuleBrowserAPIManager"/> (the overlay itself sits before the manager and so
    /// can't register the handler).
    ///
    /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
    /// Requires: <see cref="IUIManager"/> <br></br>
    /// </summary>
    public sealed class AppModuleSystemToUI : IApplicationModule
    {
        private IBrowserAPIManager _browserAPIManager = null!;

        public AppModuleSystemToUI()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));
            _browserAPIManager = container.Resolve<IBrowserAPIManager>();
            _browserAPIManager.SystemHandler = new SystemHandler(container.Resolve<IUIManager>());
        }

        public void Dispose()
        {
            if (_browserAPIManager != null)
                _browserAPIManager.SystemHandler = null;
            _browserAPIManager = null!;
        }

        private sealed class SystemHandler : IBrowserSystemHandler
        {
            private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            private readonly JavascriptBuilder _jsBuilder = new JavascriptBuilder();
            private readonly IUIManager _uiManager;

            public SystemHandler(IUIManager uiManager)
            {
                _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            }

            public Task ShowNotification(string message)
            {
                try
                {
                    // Guard: the system overlay may not exist; only push when present.
                    if (_uiManager.TryGetUIElement<OverlayForm>(AppModuleSystemOverlay.SystemOverlayUIId, out var overlay) && overlay != null)
                    {
                        var script = _jsBuilder.BuildCustomEventDispatcher(new ShowNotificationWebEvent(message));
                        overlay.InvokeAsyncOnUI(o => o.Browser.ExecuteScript(script));
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to push notification to the system overlay");
                }
                return Task.CompletedTask;
            }

            public Task ToggleGreeter()
            {
                try
                {
                    if (_uiManager.TryGetUIElement<OverlayForm>(AppModuleSystemOverlay.SystemOverlayUIId, out var overlay) && overlay != null)
                    {
                        var script = _jsBuilder.BuildCustomEventDispatcher(new ToggleGreeterWebEvent());
                        overlay.InvokeAsyncOnUI(o => o.Browser.ExecuteScript(script));
                    }
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to toggle the greeter on the system overlay");
                }
                return Task.CompletedTask;
            }
        }
    }
}
