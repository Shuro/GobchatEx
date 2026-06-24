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

using Gobchat.Module.UI.Internal;
using Gobchat.UI.Web;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace Gobchat.App.Tests.Module.UI
{
    /// <summary>
    /// The page calls the C# bridge through the untyped postMessage transport, so the only thing that
    /// keeps the two sides in sync is the hand-written <c>GobchatAPI</c> block in <c>globals.d.ts</c>.
    /// WHY this matters: if a bridge method is renamed/added/re-signatured in C# but the declaration
    /// drifts (as several had: Item1/Item2 tuples, attachToFFXIVProcose typed <c>void</c>,
    /// synchronizeConfig's wrong param), TS compiles clean while the call silently breaks at runtime.
    /// This test fails the build instead, so the declaration can't rot away from the implementation.
    /// </summary>
    public sealed class BridgeGlobalsContractTests
    {
        [Fact]
        public void EveryBridgeMethodIsDeclaredInGlobalsDts()
        {
            var declared = ParseGlobalsApiFunctions();

            var implementations = BridgeImplementations();
            Assert.NotEmpty(implementations);

            var failures = new List<string>();
            foreach (var impl in implementations)
            {
                foreach (var method in BridgeMethods(impl))
                {
                    var name = CamelCase(method.Name);
                    if (!declared.TryGetValue(name, out var declaredArgCount))
                    {
                        failures.Add($"{impl.Name}.{method.Name} -> '{name}' has no declaration in globals.d.ts (GobchatAPI)");
                        continue;
                    }

                    var parameters = method.GetParameters();
                    var required = parameters.Count(p => !p.HasDefaultValue);
                    var total = parameters.Length;
                    // The bridge can be called with anywhere from 'required' to 'total' args, so accept
                    // any declared arity in that range; flag a genuine count mismatch.
                    if (declaredArgCount < required || declaredArgCount > total)
                        failures.Add($"{impl.Name}.{method.Name} declares {total} param(s) ({required} required) but globals.d.ts '{name}' declares {declaredArgCount}");
                }
            }

            Assert.True(failures.Count == 0,
                "Bridge/globals.d.ts contract drift:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        /// <summary>
        /// ARC-4: the same contract in reverse. The bridge dispatches by reflecting on method names
        /// (<c>ManagedWebBrowser.FindMethod</c>), so a declaration in <c>globals.d.ts</c> that no longer
        /// maps to any C# bridge method is a dead entry: TS keeps compiling and the page keeps calling
        /// <c>GobchatAPI.thatMethod(...)</c>, but the dispatch fails at runtime with no method found. The
        /// forward test catches an implementation with no declaration; this catches a declaration with no
        /// implementation, plus an arity that drifted out of the method's [required..total] range.
        /// </summary>
        [Fact]
        public void EveryDeclaredGobchatApiFunctionMapsToABridgeMethod()
        {
            var declared = ParseGlobalsApiFunctions();

            // camelCase bridge name -> (required, total) parameter counts, aggregated over every impl.
            var implemented = new Dictionary<string, (int Required, int Total)>(StringComparer.OrdinalIgnoreCase);
            foreach (var impl in BridgeImplementations())
            {
                foreach (var method in BridgeMethods(impl))
                {
                    var parameters = method.GetParameters();
                    implemented[CamelCase(method.Name)] =
                        (parameters.Count(p => !p.HasDefaultValue), parameters.Length);
                }
            }

            Assert.NotEmpty(implemented);

            var failures = new List<string>();
            foreach (var (name, declaredArgCount) in declared)
            {
                if (!implemented.TryGetValue(name, out var arity))
                {
                    failures.Add($"globals.d.ts declares GobchatAPI.{name}(...) but no bridge method maps to it (dead declaration)");
                    continue;
                }

                if (declaredArgCount < arity.Required || declaredArgCount > arity.Total)
                    failures.Add($"globals.d.ts '{name}' declares {declaredArgCount} param(s) but the bridge method takes {arity.Total} ({arity.Required} required)");
            }

            Assert.True(failures.Count == 0,
                "globals.d.ts declares bridge functions with no matching C# method:" + Environment.NewLine + string.Join(Environment.NewLine, failures));
        }

        // Concrete classes exposed to the page through the postMessage bridge.
        private static List<Type> BridgeImplementations()
        {
            return typeof(GobchatBrowserAPI).Assembly.GetTypes()
                .Where(t => typeof(IBrowserAPI).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
                .ToList();
        }

        // Public instance methods callable from the page: object overrides and property accessors
        // (e.g. get_APIName) are not part of the bridge surface and are excluded.
        private static IEnumerable<MethodInfo> BridgeMethods(Type implementation)
        {
            return implementation.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.DeclaringType != typeof(object))
                .Where(m => !m.IsSpecialName);
        }

        private static string CamelCase(string name)
        {
            if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
                return name;
            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        // Parses the `declare namespace GobchatAPI { ... }` block of globals.d.ts into a
        // function-name -> declared-parameter-count map.
        private static Dictionary<string, int> ParseGlobalsApiFunctions()
        {
            var path = Path.Combine(FindRepoRoot(), "src", "Gobchat.App", "resources", "ui", "globals.d.ts");
            Assert.True(File.Exists(path), $"globals.d.ts not found at {path}");

            var content = File.ReadAllText(path);
            var block = ExtractNamespaceBlock(content, "GobchatAPI");

            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(block, @"function\s+(\w+)\s*\(([^)]*)\)"))
            {
                var name = match.Groups[1].Value;
                var paramList = match.Groups[2].Value.Trim();
                var count = paramList.Length == 0 ? 0 : paramList.Split(',').Length;
                result[name] = count;
            }

            Assert.NotEmpty(result);
            return result;
        }

        private static string ExtractNamespaceBlock(string content, string namespaceName)
        {
            var marker = "namespace " + namespaceName;
            var start = content.IndexOf(marker, StringComparison.Ordinal);
            Assert.True(start >= 0, $"'{marker}' not found in globals.d.ts");

            var open = content.IndexOf('{', start);
            Assert.True(open >= 0, $"opening brace for '{marker}' not found");

            var depth = 0;
            for (var i = open; i < content.Length; ++i)
            {
                if (content[i] == '{') depth++;
                else if (content[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return content.Substring(open, i - open + 1);
                }
            }

            throw new InvalidOperationException($"Unbalanced braces in '{marker}' block");
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Gobchat.sln")))
                dir = dir.Parent;
            if (dir == null)
                throw new FileNotFoundException("Could not locate Gobchat.sln above the test output directory");
            return dir.FullName;
        }
    }
}
