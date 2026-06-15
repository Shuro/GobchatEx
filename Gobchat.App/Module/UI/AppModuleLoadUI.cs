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

using Gobchat.Core.Chat;
using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Core.Util.Extension;
using Gobchat.Module.Overlay;
using Gobchat.UI.Forms;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Gobchat.Module.UI
{

    public sealed class AppModuleLoadUI : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // The overlay UI is served from this virtual host (an https origin is required because the
        // page loads as ES modules, which Chromium blocks over file://).
        private const string IndexUrl = "https://gobchat.localhost/gobchat.html";

        private IDIContext _container;
        private IConfigManager _configManager;
        private IBrowserAPIManager _browserAPIManager;
        private OverlayForm _overlay;
        private string _uiRoot;
        private bool _settingsOnly;
        private bool _settingsAutoOpened;
#if DEBUG
        private bool _testHarnessInjected;
#endif

        /// <summary>
        /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// Requires: <see cref="IConfigManager"/> <br></br>
        /// <br></br>
        /// Adds to UI element: <see cref="OverlayForm"/> <br></br>
        /// </summary>
        public AppModuleLoadUI()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _configManager = _container.Resolve<IConfigManager>();
            _browserAPIManager = _container.Resolve<IBrowserAPIManager>();
            _settingsOnly = _container.Resolve<StartupOptions>().SettingsOnly;

            if (_settingsOnly)
                _browserAPIManager.OnUIReadyChanged += BrowserAPIManager_UIReadyChanged;

            _uiRoot = Path.GetFullPath(Path.Combine(GobchatContext.ResourceLocation, "ui"));

            var uiManager = _container.Resolve<IUIManager>();
            _overlay = uiManager.GetUIElement<OverlayForm>(AppModuleChatOverlay.OverlayUIId);

            _overlay.Browser.ResourceRootFolder = _uiRoot;
            _overlay.Browser.ResourceResolver = ResolveResource;
            _overlay.Browser.SettingsFramePersister = PersistSettingsFrame;

            _overlay.Browser.OnBrowserLoadPageDone += Browser_BrowserLoadPageDone;
            _overlay.Browser.OnBrowserInitialized += Browser_BrowserInitialized;
        }

        public void Dispose()
        {
            _overlay.Browser.OnBrowserInitialized -= Browser_BrowserInitialized;
            _overlay.Browser.OnBrowserLoadPageDone -= Browser_BrowserLoadPageDone;

            if (_settingsOnly)
                _browserAPIManager.OnUIReadyChanged -= BrowserAPIManager_UIReadyChanged;

            _configManager = null;
            _overlay = null;
            _container = null;
        }

        private string ResolveResource(string requestPath)
        {
            return UiResourceResolver.Resolve(_uiRoot, requestPath);
        }

        // Persists the settings overlay's frame to config the same lightweight way the chat overlay
        // does (no profile save / "saved" message) — gobchat.ts reads it back as window.open features.
        private void PersistSettingsFrame(System.Drawing.Rectangle frame)
        {
            try
            {
                _configManager.SetProperty("behaviour.frame.config.position.x", frame.X);
                _configManager.SetProperty("behaviour.frame.config.position.y", frame.Y);
                _configManager.SetProperty("behaviour.frame.config.size.width", frame.Width);
                _configManager.SetProperty("behaviour.frame.config.size.height", frame.Height);
                _configManager.DispatchChangeEvents();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to persist settings frame");
            }
        }

        private void Browser_BrowserInitialized(object sender, Gobchat.UI.Web.BrowserInitializedEventArgs e)
        {
            logger.Info("Loading gobchat ui");
            try
            {
                // Registered as document-creation scripts so Gobchat.* exists before page scripts.
                Browser_InjectEnums();
                Browser_InjectDefaultConfig();
                Browser_InjectKeyCodes();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex, "Error while preparing browser bootstrap scripts");
            }

            _overlay.Browser.Load(IndexUrl);
        }

        private void Browser_InjectKeyCodes()
        {
            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                builder.Append("Gobchat.KeyCodeToKeyEnum = ");
                builder.AppendLine("function(keyCode){");
                {
                    var lookupTable = Enum.GetValues(typeof(Keys)).Cast<object>()
                                    .Where(enumValue => ((Keys)enumValue & Keys.Modifiers) == 0)
                                    .Distinct()
                                    .ToDictionary(enumValue => (int)enumValue, enumValue => enumValue.ToString());
                    var jsonObject = Newtonsoft.Json.JsonConvert.SerializeObject(lookupTable);

                    builder.Append("const lookup = ").AppendLine(jsonObject);
                    builder.AppendLine("const result = lookup[keyCode]");
                    builder.AppendLine("return result === undefined ? null : result");
                }
                builder.AppendLine("}");
            });
        }

        private void Browser_InjectDefaultConfig()
        {
            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                builder.Append("Gobchat.DefaultProfileConfig = ");
                builder.AppendLine(_configManager.DefaultProfile.ToJson().ToString());
            });
        }

        private void Browser_InjectEnums()
        {
            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                builder.Append("Gobchat.MessageSegmentEnum = ");
                builder.AppendLine(typeof(MessageSegmentType).EnumToJson(s => s.ToUpperInvariant()));
            });

            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                builder.Append("Gobchat.ChannelEnum = ");
                builder.AppendLine(typeof(ChatChannel).EnumToJson(s => s.ToUpperInvariant()));
            });

            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                var channels = GobchatChannelMapping.GetAllChannels();

                var settings = new Newtonsoft.Json.JsonSerializerSettings();
                settings.ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver();

                builder.AppendLine("Gobchat.Channels = {");

                for (var i = 0; i < channels.Count; ++i)
                {
                    var channel = channels[i];
                    var name = channel.ChatChannel.ToString().ToUpperInvariant();
                    var jsonObject = Newtonsoft.Json.JsonConvert.SerializeObject(channel, settings);
                    builder.Append("\"").Append(name).Append("\": ").Append(jsonObject);
                    if (i + 1 < channels.Count)
                        builder.AppendLine(",");
                    else
                        builder.AppendLine();
                }

                builder.AppendLine("}");
            });
        }

        private void Browser_BrowserLoadPageDone(object sender, Gobchat.UI.Web.BrowserLoadPageEventArgs e)
        {
#if DEBUG
            Browser_InjectTestHarness();
#endif
            // Overlay visibility is owned by AppModuleChatOverlay (driven by pin + login state); this
            // module only loads the page. In settings-only debug mode the dialog is opened from
            // BrowserAPIManager_UIReadyChanged once the page (its window.opener) has fully initialized.
        }

        // Settings-only debug mode: open the settings dialog only after the overlay page reports it
        // has finished initializing (setUIReady -> OnUIReadyChanged). The page loads gobConfig
        // asynchronously, so hooking the earlier page-load-done event opens the dialog before the
        // opener's config is ready and it renders blank. One-shot so a reload doesn't re-trigger it.
        private void BrowserAPIManager_UIReadyChanged(object sender, UIReadyChangedEventArgs e)
        {
            if (!e.IsUIReady || _settingsAutoOpened)
                return;
            _settingsAutoOpened = true;
            logger.Info("Settings-only mode: UI ready, invoking window.openGobConfig");
            _overlay.InvokeAsyncOnUI((overlay) =>
                overlay.Browser.ExecuteScript("if (window.openGobConfig) { window.openGobConfig(); } else { console.error('openGobConfig is not defined on window'); }"));
        }

#if DEBUG
        // Debug-only: load the manual chat test harness (resources/ui/gobchat-test.js). It is not
        // referenced by gobchat.html and is excluded from Release output (see Gobchat.csproj), so it
        // never ships in release builds. Injected once after the first page load.
        private void Browser_InjectTestHarness()
        {
            if (_testHarnessInjected)
                return;
            _testHarnessInjected = true;
            _browserAPIManager.ExecuteGobchatJavascript(builder =>
            {
                builder.AppendLine("(function(){");
                builder.AppendLine("    const script = document.createElement('script');");
                builder.AppendLine("    script.src = 'gobchat-test.js';");
                builder.AppendLine("    document.head.appendChild(script);");
                builder.AppendLine("})();");
            });
        }
#endif
    }
}
