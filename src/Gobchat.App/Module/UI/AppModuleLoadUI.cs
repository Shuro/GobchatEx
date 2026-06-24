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

        private IDIContext _container = null!;
        private IConfigManager _configManager = null!;
        private IBrowserAPIManager _browserAPIManager = null!;
        private OverlayForm _overlay = null!;
        private string _uiRoot = null!;
        private bool _dryRun;
        private bool _settingsAutoOpened;

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
            _dryRun = _container.Resolve<StartupOptions>().DryRun;

            if (_dryRun)
                _browserAPIManager.OnUIReadyChanged += BrowserAPIManager_UIReadyChanged;

            _uiRoot = Path.GetFullPath(Path.Combine(GobchatContext.ResourceLocation, "ui"));

            var uiManager = _container.Resolve<IUIManager>();
            _overlay = uiManager.GetUIElement<OverlayForm>(AppModuleChatOverlay.OverlayUIId);

            _overlay.Browser.ResourceRootFolder = _uiRoot;
            _overlay.Browser.ResourceResolver = ResolveResource;
            _overlay.Browser.SettingsFramePersister = PersistSettingsFrame;
            _overlay.Browser.SettingsFrameProvider = ProvideSettingsFrame;

            _overlay.Browser.OnBrowserLoadPageDone += Browser_BrowserLoadPageDone;
            _overlay.Browser.OnBrowserInitialized += Browser_BrowserInitialized;
        }

        public void Dispose()
        {
            _overlay.Browser.OnBrowserInitialized -= Browser_BrowserInitialized;
            _overlay.Browser.OnBrowserLoadPageDone -= Browser_BrowserLoadPageDone;

            if (_dryRun)
                _browserAPIManager.OnUIReadyChanged -= BrowserAPIManager_UIReadyChanged;

            _configManager = null!;
            _overlay = null!;
            _container = null!;
        }

        private string? ResolveResource(string requestPath)
        {
            return UiResourceResolver.Resolve(_uiRoot, requestPath);
        }

        // The settings window's placement lives app-globally in window_state.json (next to
        // gobconfig.json), deliberately NOT in any profile: it's window chrome, so it must be the same
        // regardless of the active profile and must never surface as an unsaved profile change.
        private static string SettingsWindowStatePath
            => Path.Combine(GobchatContext.AppConfigLocation, "window_state.json");

        // Persists the settings overlay's frame (called on every settings-window close). Merges into the
        // file so any unrelated future window state survives.
        private void PersistSettingsFrame(System.Drawing.Rectangle frame)
        {
            try
            {
                Directory.CreateDirectory(GobchatContext.AppConfigLocation);
                var path = SettingsWindowStatePath;
                // Merge into the existing file to preserve any unrelated state, but if it's corrupt
                // start fresh and overwrite it — otherwise a bad file would block every future save.
                Newtonsoft.Json.Linq.JObject root;
                try
                {
                    root = File.Exists(path)
                        ? Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path))
                        : new Newtonsoft.Json.Linq.JObject();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Settings window state file was unreadable; overwriting it");
                    root = new Newtonsoft.Json.Linq.JObject();
                }
                root["configWindow"] = new Newtonsoft.Json.Linq.JObject
                {
                    ["x"] = frame.X,
                    ["y"] = frame.Y,
                    ["width"] = frame.Width,
                    ["height"] = frame.Height,
                };
                File.WriteAllText(path, root.ToString(Newtonsoft.Json.Formatting.Indented));
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to persist settings window placement");
            }
        }

        // Supplies the saved settings-window frame to restore on open, or null when there's none (or it
        // is unreadable) so the window opens centered. The form clamps it back on-screen.
        private System.Drawing.Rectangle? ProvideSettingsFrame()
        {
            try
            {
                var path = SettingsWindowStatePath;
                if (!File.Exists(path))
                    return null;
                var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                if (root["configWindow"] is not Newtonsoft.Json.Linq.JObject frame)
                    return null;
                return new System.Drawing.Rectangle(
                    frame.Value<int>("x"),
                    frame.Value<int>("y"),
                    frame.Value<int>("width"),
                    frame.Value<int>("height"));
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to read settings window placement");
                return null;
            }
        }

        private void Browser_BrowserInitialized(object? sender, Gobchat.UI.Web.BrowserInitializedEventArgs e)
        {
            logger.Info("Loading gobchat ui");
            try
            {
                // Registered as document-creation scripts so Gobchat.* exists before page scripts.
                Browser_InjectEnums();
                Browser_InjectDefaultConfig();
                Browser_InjectAppConfig();
                Browser_InjectModernColors();
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

        // Inject the current application-global settings (theme, language, …) so the page has them at
        // first paint. Live changes arrive via the SynchronizeAppConfigEvent (the page re-reads then).
        private void Browser_InjectAppConfig()
        {
            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                builder.Append("Gobchat.AppConfig = ");
                builder.AppendLine(_configManager.AppSettingsAsJson().ToString());
            });
        }

        // Single source of truth for the Channel-Colors page's "Modern" text-colour scheme: inject the
        // style.channel subtree of the bundled ffxiv_modern_colors profile as Gobchat.FFXIVModernColors,
        // so the page can map each channel's text key to its modern colour (and reset to it).
        private void Browser_InjectModernColors()
        {
            _browserAPIManager.AddInitializationGobchatJavascript(builder =>
            {
                var json = "{}";
                try
                {
                    var path = Path.Combine(GobchatContext.ResourceLocation, "profiles", "ffxiv_modern_colors.json");
                    var root = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
                    var channel = root.SelectToken("style.channel");
                    if (channel != null)
                        // BRG-10: serialize through JsonConvert (full escaping) rather than emitting a raw
                        // JToken literal into the init script.
                        json = Newtonsoft.Json.JsonConvert.SerializeObject(channel, Newtonsoft.Json.Formatting.None);
                    else
                        logger.Warn("ffxiv_modern_colors.json has no style.channel; Modern colour scheme will be empty");
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to load FFXIV Modern colour scheme; Modern colour scheme will be empty");
                }

                builder.Append("Gobchat.FFXIVModernColors = ");
                builder.AppendLine(json);
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

        private void Browser_BrowserLoadPageDone(object? sender, Gobchat.UI.Web.BrowserLoadPageEventArgs e)
        {
            // Overlay visibility is owned by AppModuleChatOverlay (driven by pin + login state); this
            // module only loads the page. In dry-run mode the dialog is opened from
            // BrowserAPIManager_UIReadyChanged once the page (its window.opener) has fully initialized.
            // (The manual chat test harness is no longer auto-injected here — it's triggered on demand
            // from the Debug settings page via GobchatAPI.injectTestHarness.)
        }

        // Dry-run mode: open the settings dialog only after the overlay page reports it has finished
        // initializing (setUIReady -> OnUIReadyChanged). The page loads gobConfig asynchronously, so
        // hooking the earlier page-load-done event opens the dialog before the opener's config is ready
        // and it renders blank. One-shot so a reload doesn't re-trigger it.
        private void BrowserAPIManager_UIReadyChanged(object? sender, UIReadyChangedEventArgs e)
        {
            if (!e.IsUIReady || _settingsAutoOpened)
                return;
            _settingsAutoOpened = true;
            logger.Info("Dry-run mode: UI ready, invoking window.openGobConfig");
            _overlay.InvokeAsyncOnUI((overlay) =>
                overlay.Browser.ExecuteScript("if (window.openGobConfig) { window.openGobConfig(); } else { console.error('openGobConfig is not defined on window'); }"));
        }
    }
}
