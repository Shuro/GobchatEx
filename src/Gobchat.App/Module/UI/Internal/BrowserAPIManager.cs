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
using System.Collections.Generic;
using System.Threading.Tasks;
using Gobchat.Core.Runtime;
using Gobchat.UI.Forms;
using Gobchat.UI.Web;
using Gobchat.UI.Web.JavascriptEvents;
using Newtonsoft.Json.Linq;

namespace Gobchat.Module.UI.Internal
{
    internal sealed partial class BrowserAPIManager : IBrowserAPIManager, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private event EventHandler<UIReadyChangedEventArgs>? _onUIReadyChanged;

        private readonly JavascriptBuilder _jsBuilder = new JavascriptBuilder();
        private readonly List<IBrowserAPI> _apis = new List<IBrowserAPI>();
        private IUISynchronizer _synchronizer;
        private OverlayForm _overlay;
        private readonly object _uiReadyLock = new object();
        private bool _isUIReady;

        public BrowserAPIManager(
                OverlayForm overlay,
                IUISynchronizer uiSynchronizer
            )
        {
            _overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            _synchronizer = uiSynchronizer ?? throw new ArgumentNullException(nameof(uiSynchronizer));

            RegisterAPI(new GobchatBrowserAPI(this));
        }

        public bool IsUIReady
        {
            // The setter runs on the WebView2 thread (via SetUIReady) while getters run on background
            // module threads; without synchronisation the compound read-test-write tore. Guard the state
            // with a lock, capture whether it actually changed, then fire the notification OUTSIDE the lock
            // so a listener can't deadlock by re-entering the property.
            get { lock (_uiReadyLock) return _isUIReady; }
            set
            {
                bool changed;
                lock (_uiReadyLock)
                {
                    changed = _isUIReady != value;
                    if (changed)
                        _isUIReady = value;
                }
                if (changed)
                    _onUIReadyChanged?.Invoke(this, new UIReadyChangedEventArgs(value));
            }
        }
        public IUISynchronizer UISynchronizer { get { return _synchronizer; } }
        public IBrowserChatHandler? ChatHandler { get; set; }
        public IBrowserConfigHandler? ConfigHandler { get; set; }
        public IBrowserActorHandler? ActorHandler { get; set; }
        public IBrowserMemoryHandler? MemoryHandler { get; set; }
        public IBrowserSystemHandler? SystemHandler { get; set; }
        public IBrowserDryRunHandler? DryRunHandler { get; set; }
        public IBrowserUpdateHandler? UpdateHandler { get; set; }

        public event EventHandler<UIReadyChangedEventArgs> OnUIReadyChanged
        {
            add
            {
                _onUIReadyChanged += value;
                _onUIReadyChanged?.Invoke(this, new UIReadyChangedEventArgs(IsUIReady));
            }
            remove => _onUIReadyChanged -= value;
        }

        public void Dispose()
        {
            lock (_apis)
            {
                foreach (var api in _apis)
                {
                    try
                    {
                        _overlay.Browser.UnbindBrowserAPI(api);
                    }
                    catch (Exception ex)
                    {
                        logger.Warn(ex);
                    }
                }

                _overlay = null!;
            }

            _synchronizer = null!;
            _onUIReadyChanged = null;

            ChatHandler = null;
            ConfigHandler = null;
        }

        public void DispatchEventToBrowser(JSEvent jsEvent)
        {
            if (jsEvent == null)
                return;
            var script = _jsBuilder.BuildCustomEventDispatcher(jsEvent);
            ExecuteJavascript(script);
        }

        public void ExecuteGobchatJavascript(Action<System.Text.StringBuilder> content)
        {
            ExecuteJavascript(BuildGobchatJavascript(content));
        }

        private static string BuildGobchatJavascript(Action<System.Text.StringBuilder> content)
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine("'use strict'");
            builder.AppendLine("var Gobchat = function(Gobchat){");
            builder.AppendLine();
            content(builder);
            builder.AppendLine();
            builder.AppendLine("return Gobchat");
            builder.AppendLine("}(Gobchat || {});");
            return builder.ToString();
        }

        // Registers a Gobchat.* bootstrap (enums/config) to run in every page before its own
        // scripts. WebView2 runs these at document creation, replacing the old per-load injection.
        public void AddInitializationGobchatJavascript(Action<System.Text.StringBuilder> content)
        {
            AddInitializationScript(BuildGobchatJavascript(content));
        }

        public void AddInitializationScript(string script)
        {
            _synchronizer.RunSync(() =>
            {
                try
                {
                    _overlay.Browser.AddInitializationScript(script);
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, $"On init-script registration: {script}");
                }
            });
        }

        public void ExecuteJavascript(string script)
        {
            _synchronizer.RunSync(() =>
            {
                try
                {
                    _overlay.Browser.ExecuteScript(script);
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex, $"On script execution: {script}");
                }
            });
        }

        public async Task<IJavascriptResponse> EvaluateJavascript(string script, TimeSpan? timeout = null)
        {
            return await _synchronizer.RunSync(() =>
             {
                 try
                 {
                     return _overlay.Browser.EvaluateScript(script: script, timeout: timeout);
                 }
                 catch (Exception ex)
                 {
                     // Return a failed response, not null: callers await this and read .Success /
                     // dereference .Result. null made eval failures invisible (and NRE'd on deref);
                     // a failed JavascriptResponse mirrors ManagedWebBrowser.EvaluateScript's own catch.
                     logger.Fatal(ex, $"On script execution: {script}");
                     return Task.FromResult<IJavascriptResponse>(new JavascriptResponse(false, null, ex.Message));
                 }
             })!.ConfigureAwait(false);
        }

        // Toolbar pin: toggle the overlay between locked (frozen) and unlocked (movable + resizable).
        public void ToggleOverlayLock()
        {
            _synchronizer.RunSync(() => _overlay?.ToggleLock());
        }

        // Page-driven window move: the overlay page calls this on mousedown over the toolbar/grip while
        // unlocked, handing the drag to the OS move loop (so the toolbar icons stay clickable).
        public void BeginOverlayDrag()
        {
            _synchronizer.RunSync(() => _overlay?.BeginWindowDrag());
        }

        // Settings-window title-bar controls (minimize). The settings window is opened from this overlay
        // browser's window.open, so this routes through it.
        public void MinimizeSettings()
        {
            _synchronizer.RunSync(() => _overlay?.Browser?.MinimizeSettings());
        }

        // Reveal-when-ready: the config page signals (via its window.opener's GobchatAPI) that it has
        // rendered, so the initially hidden settings window can show without an empty-frame flash.
        public void RevealSettings()
        {
            _synchronizer.RunSync(() => _overlay?.Browser?.RevealSettings());
        }

        // Second cog click: focus the open settings window. Returns false when none is open.
        public bool FocusSettings()
        {
            return _synchronizer.RunSync(() => _overlay?.Browser?.FocusSettings() ?? false);
        }

        public void RegisterAPI(IBrowserAPI api)
        {
            lock (_apis)
            {
                if (_overlay.Browser.BindBrowserAPI(api, true))
                    _apis.Add(api);
            }
        }

        public void UnregisterAPI(IBrowserAPI api)
        {
            lock (_apis)
            {
                if (_overlay.Browser.UnbindBrowserAPI(api))
                    _apis.Remove(api);
            }
        }
    }
}