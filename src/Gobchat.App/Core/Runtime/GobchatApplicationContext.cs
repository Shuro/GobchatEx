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

using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using System.Linq;
using NLog;
using System;
using Gobchat.Core.UI;

namespace Gobchat.Core.Runtime
{
    public sealed class GobchatApplicationContext : AbstractGobchatApplicationContext
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        public new Form? MainForm { get { return null; } }

        //TODO later
        // private readonly TinyIoC.TinyIoCContainer _applicationDIContextF = new TinyIoC.TinyIoCContainer();
        //  private readonly TinyMessengerHub _messageHub = new TinyMessenger.TinyMessengerHub();

        private DIContext _applicationDIContext = null!; // set in ApplicationStartupProcess
        private UIManager _uiManager = null!; // set in ApplicationStartupProcess
        // Initialised empty (not null!) so ApplicationShutdownProcess can run safely even if the OS kills the
        // app before ApplicationStartupProcess assigns the populated list. Startup replaces it with a fresh one.
        private List<IApplicationModule> _activeApplicationModules = new List<IApplicationModule>();
        private readonly StartupOptions _options;

        public GobchatApplicationContext()
            : this(new StartupOptions(false))
        {
        }

        public GobchatApplicationContext(StartupOptions options)
        {
            _options = options ?? new StartupOptions(false);

            //TODO
            // Turn this into the application core
            // Start other parts of the app as components
            // Initialize components on start up
            // Dispose components on shut down
            // Provide a type of UIManager
            // UIManager allows access to UI widgets via ID
            // UIManager allows to run tasks on the UI thread
            // Look for a simple DI framework which supports a context tree for injection
        }

        internal override void ApplicationStartupProcess(CancellationToken token)
        {
            // On the very first run, show the welcome screen (language/theme/auto-update + optional legacy
            // migration) before any module reads the config, so the choices take effect this launch. On every
            // later run this is a no-op. Must never block startup, so failures are only logged.
            try
            {
                FirstRunSetup.RunFirstTimeSetup();
            }
            catch (System.Exception ex)
            {
                logger.Warn(ex, "First-run setup failed");
            }

            _activeApplicationModules = new List<IApplicationModule>();
            _applicationDIContext = new DIContext();
            _uiManager = new UIManager(GobchatApplicationContext.UISynchronizer);

            //_applicationDIContext.Register<string>((c, _) => GobchatApplicationContext.ResourceLocation, nameof(ResourceLocation));
            //_applicationDIContext.Register<string>((c, _) => GobchatApplicationContext.UserConfigLocation, nameof(UserConfigLocation));
            //_applicationDIContext.Register<string>((c, _) => GobchatApplicationContext.ApplicationLocation, nameof(ApplicationLocation));
            //_applicationDIContext.Register<GobVersion>((c, _) => GobchatApplicationContext.ApplicationVersion, nameof(ApplicationVersion));

            _applicationDIContext.Register<IUISynchronizer>((c, _) => GobchatApplicationContext.UISynchronizer);
            _applicationDIContext.Register<IUIManager>((c, _) => _uiManager);
            _applicationDIContext.Register<StartupOptions>((c, _) => _options);

            var moduleActivationSequence = BuildModuleActivationSequence();

            logger.Info(() => $"Initialize GobchatEx v{GobchatContext.ApplicationVersion} on {(Environment.Is64BitProcess ? "x64" : "x86")}");

            var startupHandler = new ApplicationStartupHandler();
            foreach (var module in moduleActivationSequence)
            {
                try
                {
                    _activeApplicationModules.Add(module);
                    logger.Info($"Starting: {module}");
                    module.Initialize(startupHandler, _applicationDIContext);
                }
                catch (System.Exception ex1)
                {
                    logger.Fatal($"Initialization error in {module}");
                    logger.Fatal(ex1);
                    startupHandler.StopStartup = true;

                    try
                    {
                        MessageBox.Show($"An error prevents GobchatEx from starting. For more details please check gobchatex_debug.log.\nError:\n{ex1.GetType()}: {ex1.Message}", "Error on start", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    catch (System.Exception)
                    {
                    }
                }

                if (startupHandler.StopStartup)
                {
                    logger.Fatal("Shutdown in initialization phase");
                    GobchatApplicationContext.ExitGobchat();
                    return;
                }
            }

            logger.Info("Initialization complete");
        }

        // The ordered module activation list. Order is load-bearing: each module resolves its dependencies
        // from the DIContext during Initialize, so every service a module Requires must be Provided by an
        // earlier module (or one of the bootstrap registrations above). The order is hand-maintained, so a
        // startup contract test (ModuleActivationOrderTests) validates it against each module's declared
        // Requires/Provides - a reorder that breaks a dependency fails the test instead of only failing at
        // runtime with a generic DIException (ARC-1). Module constructors are side-effect-free; the work
        // happens in Initialize, so building this list is cheap and safe to call from that test.
        internal static List<IApplicationModule> BuildModuleActivationSequence()
        {
            return new List<IApplicationModule>()
            {
                //config
                new global::Gobchat.Module.Config.AppModuleConfig(),
                new global::Gobchat.Module.Language.AppModuleLanguage(),

                //updater (the overlay now renders through the OS WebView2 runtime, so there is
                //no bundled-Chromium download/dependency check to run here anymore)
                new global::Gobchat.Module.Updater.AppModuleUpdater(),

                //base managers
                new global::Gobchat.Module.NotifyIcon.AppModuleNotifyIcon(),
                new global::Gobchat.Module.Hotkey.AppModuleHotkeyManager(),
                new global::Gobchat.Module.MemoryReader.AppModuleMemoryReader(),
                new global::Gobchat.Module.Actor.AppModuleActorManager(),
                new global::Gobchat.Module.Chat.AppModuleChatManager(),

                // WebView2 overlay and javascript api
                new global::Gobchat.Module.Web.AppModuleWebViewManager(),
                new global::Gobchat.Module.Overlay.AppModuleChatOverlay(),
                new global::Gobchat.Module.Overlay.AppModuleSystemOverlay(),
                new global::Gobchat.Module.UI.AppModuleBrowserAPIManager(),

                // Misc
                new global::Gobchat.Module.Misc.AppModuleShowConnectionOnTrayIcon(),
                new global::Gobchat.Module.Misc.Chatlogger.AppModuleChatLogger(),
                new global::Gobchat.Module.Misc.AppModuleInformUserAboutMemoryState(),
                new global::Gobchat.Module.Misc.AppModuleShowHideHotkey(),
                new global::Gobchat.Module.Misc.AppModuleSearchHotkey(),

                //UI Adapter
                new global::Gobchat.Module.UI.AppModuleChatToUI(),
                // Detects/dispatches `/e gc` chat commands C#-side; needs IBrowserAPIManager (above) to
                // forward the few UI-side commands, plus IChatManager/IActorManager/IConfigManager.
                new global::Gobchat.Module.Chat.AppModuleChatCommandManager(),
                new global::Gobchat.Module.UI.AppModuleConfigToUI(),
                new global::Gobchat.Module.UI.AppModuleActorToUI(),
                new global::Gobchat.Module.UI.AppModuleMemoryToUI(),
                new global::Gobchat.Module.UI.AppModuleSystemToUI(),
                new global::Gobchat.Module.UI.AppModuleDryRunToUI(),
                new global::Gobchat.Module.UI.AppModuleUpdaterToUI(),

                //Start UI
                new global::Gobchat.Module.UI.AppModuleLoadUI(),
            };
        }

        internal override void ApplicationShutdownProcess()
        {
            logger.Info("GobchatEx shutdown");

            //components are deactivated in reverse
            var moduleDeactivationSequence = _activeApplicationModules.Reverse<IApplicationModule>().ToList();
            _activeApplicationModules.Clear();

            foreach (var module in moduleDeactivationSequence)
            {
                try
                {
                    logger.Info($"Shutdown: {module}");
                    module.Dispose();
                }
                catch (System.Exception e)
                {
                    //that's the best you get, no one cares for you.
                    logger.Warn(e, $"Shutdown error in {module}");
                }
            }

            _uiManager?.Dispose();
            _applicationDIContext?.Dispose();
        }
    }
}