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
        private const string IndexUrl = "https://gobchat.local/gobchat.html";

        private IDIContext _container;
        private IConfigManager _configManager;
        private IBrowserAPIManager _browserAPIManager;
        private CefOverlayForm _cefOverlay;
        private string _uiRoot;
#if DEBUG
        private bool _testHarnessInjected;
#endif

        /// <summary>
        /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// Requires: <see cref="IConfigManager"/> <br></br>
        /// <br></br>
        /// Adds to UI element: <see cref="CefOverlayForm"/> <br></br>
        /// </summary>
        public AppModuleLoadUI()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _configManager = _container.Resolve<IConfigManager>();
            _browserAPIManager = _container.Resolve<IBrowserAPIManager>();

            _uiRoot = Path.GetFullPath(Path.Combine(GobchatContext.ResourceLocation, "ui"));

            var uiManager = _container.Resolve<IUIManager>();
            _cefOverlay = uiManager.GetUIElement<CefOverlayForm>(AppModuleChatOverlay.OverlayUIId);

            _cefOverlay.Browser.ResourceRootFolder = _uiRoot;
            _cefOverlay.Browser.ResourceResolver = ResolveResource;

            _cefOverlay.Browser.OnBrowserLoadPageDone += Browser_BrowserLoadPageDone;
            _cefOverlay.Browser.OnBrowserInitialized += Browser_BrowserInitialized;
        }

        public void Dispose()
        {
            _cefOverlay.Browser.OnBrowserInitialized -= Browser_BrowserInitialized;
            _cefOverlay.Browser.OnBrowserLoadPageDone -= Browser_BrowserLoadPageDone;

            _configManager = null;
            _cefOverlay = null;
            _container = null;
        }

        /// <summary>
        /// Maps a virtual-host request path to a local file, applying the two layout rules the
        /// flat folder mapping cannot: the <c>module</c>&#8594;<c>modules</c> rename and the
        /// <c>.min</c>/extensionless preference. Returns <c>null</c> to let the folder mapping serve
        /// the literal path.
        /// </summary>
        private string ResolveResource(string requestPath)
        {
            if (string.IsNullOrEmpty(requestPath))
                return null;

            var rel = requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            if (rel.Length == 0)
                return null;

            var modulePrefix = "module" + Path.DirectorySeparatorChar;
            if (rel.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase))
                rel = "modules" + Path.DirectorySeparatorChar + rel.Substring(modulePrefix.Length);

            var basePath = Path.GetFullPath(Path.Combine(_uiRoot, rel));
            if (!basePath.StartsWith(_uiRoot, StringComparison.OrdinalIgnoreCase))
                return null; // outside the UI folder

            var ext = Path.GetExtension(basePath);
            var candidates = new List<string>();
            if (ext.Length == 0)
            {
                candidates.Add(basePath + ".min.js");
                candidates.Add(basePath + ".js");
            }
            else if (ext.Equals(".js", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.ChangeExtension(basePath, ".min.js"));
                candidates.Add(basePath);
            }
            else if (ext.Equals(".css", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.ChangeExtension(basePath, ".min.css"));
                candidates.Add(basePath);
            }
            else
            {
                candidates.Add(basePath);
            }

            return candidates.FirstOrDefault(File.Exists);
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

            _cefOverlay.Browser.Load(IndexUrl);
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
            if (!_cefOverlay.Visible)
                _cefOverlay.InvokeSyncOnUI((overlay) => overlay.Visible = true);
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
