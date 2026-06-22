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
using Gobchat.Core.Runtime;
using Gobchat.Module.Chat;
using Gobchat.Module.MemoryReader;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Bridges the developer dry-run control panel (Debug settings page) to the JS api: in dry-run
    /// mode it registers an <see cref="IBrowserDryRunHandler"/> on the <see cref="IBrowserAPIManager"/>
    /// that drives the fake memory manager's roster/connection and injects chat through the normal
    /// chat pipeline. Outside dry-run the handler stays null (the page greys the panel out). Must
    /// initialize after <see cref="AppModuleBrowserAPIManager"/>.
    ///
    /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
    /// Requires: <see cref="IDryRunController"/> (dry-run only) <br></br>
    /// Requires: <see cref="IChatManager"/> (dry-run only) <br></br>
    /// </summary>
    public sealed class AppModuleDryRunToUI : IApplicationModule
    {
        private IBrowserAPIManager _browserAPIManager = null!;

        public AppModuleDryRunToUI()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            _browserAPIManager = container.Resolve<IBrowserAPIManager>();

            // The dry-run control surface and its DI registration only exist when --dry-run is active;
            // outside dry-run the handler stays null and the Debug page greys out the panel.
            if (!container.Resolve<StartupOptions>().DryRun)
                return;

            var controller = container.Resolve<IDryRunController>();
            var chatManager = container.Resolve<IChatManager>();
            _browserAPIManager.DryRunHandler = new DryRunHandler(controller, chatManager);
        }

        public void Dispose()
        {
            if (_browserAPIManager != null)
                _browserAPIManager.DryRunHandler = null;
            _browserAPIManager = null!;
        }

        private sealed class DryRunHandler : IBrowserDryRunHandler
        {
            private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

            // Folder under resources/ holding the bundled mock chatlogs the "inject scenario" tool replays.
            private const string ScenarioFolder = "dryrun_scenarios";

            private readonly IDryRunController _controller;
            private readonly IChatManager _chatManager;

            public DryRunHandler(IDryRunController controller, IChatManager chatManager)
            {
                _controller = controller ?? throw new ArgumentNullException(nameof(controller));
                _chatManager = chatManager ?? throw new ArgumentNullException(nameof(chatManager));
            }

            // The user-provided character-name list (resources/dryrun_characters.json) that backs every
            // Characters dropdown on the Debug page. A JSON string array; on any error returns empty.
            public Task<string[]> GetCharacters()
            {
                try
                {
                    var path = Path.Combine(GobchatContext.ResourceLocation, "dryrun_characters.json");
                    var json = File.ReadAllText(path);
                    var names = JArray.Parse(json).ToObject<string[]>() ?? Array.Empty<string>();
                    return Task.FromResult(names);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to read dry-run character list");
                    return Task.FromResult(Array.Empty<string>());
                }
            }

            // The bundled mock chatlogs (resources/dryrun_scenarios/*.log) that back the scenario dropdown.
            // Returns the bare file names, sorted; on any error returns empty.
            public Task<string[]> GetScenarios()
            {
                try
                {
                    var folder = Path.Combine(GobchatContext.ResourceLocation, ScenarioFolder);
                    if (!Directory.Exists(folder))
                        return Task.FromResult(Array.Empty<string>());

                    var names = Directory.EnumerateFiles(folder, "*.log")
                        .Select(f => Path.GetFileName(f)!)
                        .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    return Task.FromResult(names);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to list dry-run scenarios");
                    return Task.FromResult(Array.Empty<string>());
                }
            }

            // Replays a bundled scenario through the normal chat pipeline (same path as SendMessage), so
            // formatting/mentions/range-filter apply identically. Every parsed line is injected at once,
            // timestamped now; malformed and header lines are skipped by the parser.
            public Task InjectScenario(string name)
            {
                try
                {
                    var folder = Path.GetFullPath(Path.Combine(GobchatContext.ResourceLocation, ScenarioFolder));
                    // Path.GetFileName strips any directory parts from the (UI-supplied) name; the prefix
                    // check then guarantees we never read outside the scenarios folder.
                    var safeName = Path.GetFileName(name ?? string.Empty);
                    if (string.IsNullOrEmpty(safeName))
                        return Task.CompletedTask;

                    var path = Path.GetFullPath(Path.Combine(folder, safeName));
                    if (!path.StartsWith(folder, StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                    {
                        logger.Warn("Dry-run scenario not found: {0}", safeName);
                        return Task.CompletedTask;
                    }

                    var lines = ChatlogReplayParser.Parse(File.ReadAllLines(path));
                    foreach (var line in lines)
                        _chatManager.EnqueueMessage(DateTime.Now, line.Channel, line.Source, line.Message);

                    logger.Debug("Injected dry-run scenario {0} ({1} lines)", safeName, lines.Count);
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to inject dry-run scenario");
                }
                return Task.CompletedTask;
            }

            public Task<DryRunCharacter[]> GetRoster()
            {
                return Task.FromResult(_controller.GetRoster().ToArray());
            }

            public Task Connect(string name)
            {
                _controller.Connect(name);
                return Task.CompletedTask;
            }

            public Task Disconnect()
            {
                _controller.Disconnect();
                return Task.CompletedTask;
            }

            public Task AddCharacter(string name, double distance)
            {
                _controller.AddCharacter(name, (float)distance);
                return Task.CompletedTask;
            }

            public Task RemoveCharacter(string name)
            {
                _controller.RemoveCharacter(name);
                return Task.CompletedTask;
            }

            public Task SendMessage(int channel, string source, string message)
            {
                // Same path as the real JS SendChatMessage bridge (AppModuleChatToUI), so formatting,
                // mentions, and the range-filter fade apply to dry-run messages identically.
                _chatManager.EnqueueMessage(DateTime.Now, (Gobchat.Core.Chat.ChatChannel)channel, source, message);
                return Task.CompletedTask;
            }
        }
    }
}
