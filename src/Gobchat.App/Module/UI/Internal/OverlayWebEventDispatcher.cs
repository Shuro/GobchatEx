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

using Gobchat.UI.Forms;
using Gobchat.UI.Web;
using Gobchat.UI.Web.JavascriptEvents;

namespace Gobchat.Module.UI.Internal
{
    /// <summary>
    /// ARC-6: the single primitive every UI adapter uses to push a <see cref="JSEvent"/> to an overlay.
    /// It builds the custom-event dispatcher script and runs it on the target overlay's own UI thread, so
    /// no adapter has to reach <c>overlay.Browser</c> off-thread by hand (a new adapter that hand-rolled
    /// the hop could touch CoreWebView2 on the wrong thread). Both the chat overlay (via
    /// <see cref="BrowserAPIManager.DispatchEventToBrowser"/>) and the separate system/notifications overlay
    /// (via <c>AppModuleSystemToUI</c>'s toast/greeter pushes) dispatch through here.
    /// </summary>
    internal static class OverlayWebEventDispatcher
    {
        // BuildCustomEventDispatcher locks its StringBuilder internally, so one shared builder is safe for
        // the chat-worker and bridge threads that call in concurrently.
        private static readonly JavascriptBuilder _jsBuilder = new JavascriptBuilder();

        /// <summary>
        /// Builds the dispatcher script for <paramref name="jsEvent"/> and runs it on
        /// <paramref name="overlay"/>'s UI thread. Fire-and-forget: marshalling is async
        /// (<see cref="UIExtensions.InvokeAsyncOnUI{T}"/>), which logs any failure (SIF-4) and keeps event
        /// order intact (the single UI-thread message queue is FIFO). No-ops when either argument is null,
        /// so a push after the overlay is gone is harmless rather than an NRE.
        /// </summary>
        public static void Dispatch(OverlayForm overlay, JSEvent jsEvent)
        {
            if (overlay == null || jsEvent == null)
                return;
            var script = _jsBuilder.BuildCustomEventDispatcher(jsEvent);
            overlay.InvokeAsyncOnUI(o => o.Browser.ExecuteScript(script));
        }
    }
}
