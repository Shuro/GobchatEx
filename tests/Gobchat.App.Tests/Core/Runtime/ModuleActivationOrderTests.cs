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
using System.Linq;
using Gobchat.Core.Runtime;
using Xunit;

namespace Gobchat.App.Tests.Core.Runtime
{
    /// <summary>
    /// ARC-1: the module activation order in <see cref="GobchatApplicationContext"/> is a hand-maintained
    /// list, and each module resolves its dependencies from the DIContext during Initialize. If a module is
    /// ordered before a module that Provides a service it Requires, startup fails at runtime with a generic
    /// DIException ("app won't start") - never at build or test time.
    ///
    /// This contract test closes that gap. It walks the REAL activation sequence and asserts that, at each
    /// position, every service a module Requires has already been Provided by an earlier module (or by one of
    /// the bootstrap registrations). The Requires/Provides map below is the declared contract of record,
    /// derived from each module's actual <c>container.Resolve&lt;T&gt;()</c> / <c>Register&lt;T&gt;()</c>
    /// calls (the doc-comments drift, so the calls are authoritative). A reorder that breaks a dependency,
    /// or a newly added module with no contract entry, fails here instead of only on a user's machine.
    /// </summary>
    public sealed class ModuleActivationOrderTests
    {
        private sealed record ModuleContract(string[] Requires, string[] Provides);

        // Services registered before the module loop runs (see GobchatApplicationContext.ApplicationStartupProcess).
        private static readonly string[] BootstrapProvides = { "IUISynchronizer", "IUIManager", "StartupOptions" };

        // Declared dependency contract per module, keyed by the module's simple type name. Requires = the
        // services it resolves from the DIContext; Provides = the services it registers. Keep in sync with
        // each AppModule*'s Initialize.
        private static readonly Dictionary<string, ModuleContract> Contracts = new()
        {
            ["AppModuleConfig"] = new(Requires: new string[0], Provides: new[] { "IConfigManager" }),
            ["AppModuleLanguage"] = new(new[] { "IConfigManager", "IUISynchronizer" }, new[] { "ILocaleManager" }),
            ["AppModuleUpdater"] = new(new[] { "IConfigManager", "IUIManager" }, new[] { "UpdateService" }),
            ["AppModuleNotifyIcon"] = new(new[] { "ILocaleManager", "IUIManager" }, new string[0]),
            ["AppModuleHotkeyManager"] = new(new[] { "IUISynchronizer" }, new[] { "IHotkeyManager" }),
            ["AppModuleMemoryReader"] = new(new[] { "StartupOptions" }, new[] { "IDryRunController", "IMemoryReaderManager" }),
            ["AppModuleActorManager"] = new(new[] { "IConfigManager", "IMemoryReaderManager" }, new[] { "IActorManager" }),
            ["AppModuleChatManager"] = new(new[] { "IActorManager", "IConfigManager", "ILocaleManager", "IMemoryReaderManager" }, new[] { "IChatManager" }),
            ["AppModuleWebViewManager"] = new(new[] { "IUISynchronizer" }, new string[0]),
            ["AppModuleChatOverlay"] = new(new[] { "IActorManager", "IConfigManager", "ILocaleManager", "IMemoryReaderManager", "IUIManager" }, new string[0]),
            ["AppModuleSystemOverlay"] = new(new[] { "IActorManager", "IMemoryReaderManager", "IUIManager" }, new string[0]),
            ["AppModuleBrowserAPIManager"] = new(new[] { "IUIManager" }, new[] { "IBrowserAPIManager" }),
            ["AppModuleShowConnectionOnTrayIcon"] = new(new[] { "IMemoryReaderManager", "IUIManager" }, new string[0]),
            ["AppModuleChatLogger"] = new(new[] { "IActorManager", "IChatManager", "IConfigManager" }, new string[0]),
            ["AppModuleInformUserAboutMemoryState"] = new(new[] { "IChatManager", "IMemoryReaderManager", "IUISynchronizer" }, new string[0]),
            ["AppModuleShowHideHotkey"] = new(new[] { "IChatManager", "IConfigManager", "IHotkeyManager", "IUIManager" }, new string[0]),
            ["AppModuleSearchHotkey"] = new(new[] { "IChatManager", "IConfigManager", "IHotkeyManager", "IUIManager" }, new string[0]),
            ["AppModuleChatToUI"] = new(new[] { "IBrowserAPIManager", "IChatManager" }, new string[0]),
            ["AppModuleChatCommandManager"] = new(new[] { "IActorManager", "IBrowserAPIManager", "IChatManager", "IConfigManager" }, new string[0]),
            ["AppModuleConfigToUI"] = new(new[] { "IBrowserAPIManager", "IChatManager", "IConfigManager" }, new string[0]),
            ["AppModuleActorToUI"] = new(new[] { "IActorManager", "IBrowserAPIManager", "IMemoryReaderManager" }, new string[0]),
            ["AppModuleMemoryToUI"] = new(new[] { "IActorManager", "IBrowserAPIManager", "IMemoryReaderManager" }, new string[0]),
            ["AppModuleSystemToUI"] = new(new[] { "IBrowserAPIManager", "IUIManager" }, new string[0]),
            ["AppModuleDryRunToUI"] = new(new[] { "IBrowserAPIManager", "IChatManager", "IDryRunController", "StartupOptions" }, new string[0]),
            ["AppModuleUpdaterToUI"] = new(new[] { "IBrowserAPIManager", "IConfigManager", "UpdateService" }, new string[0]),
            ["AppModuleLoadUI"] = new(new[] { "IBrowserAPIManager", "IConfigManager", "IUIManager", "StartupOptions" }, new string[0]),
        };

        [Fact]
        public void EveryModuleInTheActivationOrderHasADeclaredContract()
        {
            var order = GobchatApplicationContext.BuildModuleActivationSequence()
                .Select(m => m.GetType().Name)
                .ToList();

            var missing = order.Where(name => !Contracts.ContainsKey(name)).Distinct().ToList();
            Assert.True(missing.Count == 0,
                $"These modules are in the activation order but have no declared Requires/Provides contract " +
                $"(add them to ModuleActivationOrderTests.Contracts): {string.Join(", ", missing)}");
        }

        [Fact]
        public void EveryModuleDependencyIsProvidedByAnEarlierModule()
        {
            var order = GobchatApplicationContext.BuildModuleActivationSequence()
                .Select(m => m.GetType().Name)
                .ToList();

            var available = new HashSet<string>(BootstrapProvides);

            foreach (var moduleName in order)
            {
                Assert.True(Contracts.TryGetValue(moduleName, out var contract),
                    $"No declared contract for module '{moduleName}'.");

                var unmet = contract!.Requires.Where(r => !available.Contains(r)).ToList();
                Assert.True(unmet.Count == 0,
                    $"Module '{moduleName}' requires {string.Join(", ", unmet)} but no earlier module (or " +
                    $"bootstrap registration) provides it. Its dependencies must be activated before it.");

                foreach (var provided in contract.Provides)
                    available.Add(provided);
            }
        }

        [Fact]
        public void ContractDoesNotDeclareModulesThatAreNotInTheActivationOrder()
        {
            // Guards the other direction: a module removed from the order should have its (now stale) contract
            // entry removed too, so the map stays an honest description of what actually starts.
            var order = GobchatApplicationContext.BuildModuleActivationSequence()
                .Select(m => m.GetType().Name)
                .ToHashSet();

            var stale = Contracts.Keys.Where(name => !order.Contains(name)).ToList();
            Assert.True(stale.Count == 0,
                $"These modules have a contract entry but are not in the activation order (remove the stale " +
                $"entries): {string.Join(", ", stale)}");
        }
    }
}
