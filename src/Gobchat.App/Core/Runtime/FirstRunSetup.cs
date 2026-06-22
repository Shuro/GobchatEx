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
using System.IO;
using System.Linq;
using Gobchat.Core.Config;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;

namespace Gobchat.Core.Runtime
{
    /// <summary>
    /// First-time launch flow. On the very first run (before any module loads) this shows a small native
    /// welcome screen that lets the user pick language, theme and auto-update, and - when a legacy
    /// <c>%AppData%\Gobchat</c> folder is present - whether to migrate the old profiles. The chosen values
    /// are written into <c>appsettings.json</c> before the config module reads it, so language, theme and the
    /// auto-update preference all take effect on this first launch (the updater reads its flag once at init).
    /// Migration reuses the existing non-destructive copy in <see cref="GobchatContext.MigrateLegacyAppData"/>,
    /// then the chosen settings are layered on top.
    /// </summary>
    internal static class FirstRunSetup
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private const string AppSettingsFileName = "appsettings.json";

        // App-setting paths (must match resources/default_appsettings.json and the config UI in config_app.html).
        private const string LanguagePath = "behaviour.language";
        private const string ThemePath = "style.theme";
        private const string AutoUpdatePath = "behaviour.appUpdate.checkOnline";

        // Fallbacks used only if resources/default_appsettings.json can't be read.
        private const string FallbackLanguage = "en";
        private const string FallbackTheme = "FFXIV Modern";

        /// <summary>
        /// First run = no <c>appsettings.json</c> has been written yet. Anyone who has completed startup once
        /// (or whose folder was migrated) already has the file, so existing users are never prompted.
        /// </summary>
        public static bool IsFirstRun(string appConfigLocation)
            => !File.Exists(Path.Combine(appConfigLocation, AppSettingsFileName));

        /// <summary>
        /// Reads the theme labels from <c>ui/styles/styles.json</c> (the same source the settings page uses to
        /// populate its theme dropdown). Falls back to a single default on any error.
        /// </summary>
        public static IReadOnlyList<string> ReadThemeLabels(string resourceLocation)
        {
            try
            {
                var path = Path.Combine(resourceLocation, "ui", "styles", "styles.json");
                var labels = JArray.Parse(File.ReadAllText(path))
                    .Select(t => (string?)t["label"])
                    .Where(label => !string.IsNullOrEmpty(label))
                    .Select(label => label!)
                    .ToList();
                if (labels.Count > 0)
                    return labels;
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not read theme list for the first-run screen; using the default");
            }
            return new[] { FallbackTheme };
        }

        /// <summary>
        /// Returns a copy of <paramref name="existing"/> (or a fresh object) with the three first-run choices
        /// applied. All other keys are preserved untouched - so a migrated config keeps its old settings while
        /// the explicit first-run choices win. Pure; this is the unit-tested seam.
        /// </summary>
        public static JObject BuildAppSettings(JObject? existing, string language, string theme, bool autoUpdate)
        {
            var result = existing != null ? (JObject)existing.DeepClone() : new JObject();
            SetPath(result, LanguagePath, language);
            SetPath(result, ThemePath, theme);
            SetPath(result, AutoUpdatePath, autoUpdate);
            return result;
        }

        /// <summary>
        /// Detects a first run and, if so, shows the welcome screen and applies the result (optional migration
        /// + writing appsettings.json). Called from the background startup worker (see
        /// <see cref="AbstractGobchatApplicationContext"/>) before the module pipeline starts; the dialog itself
        /// is marshalled onto the UI thread via <see cref="AbstractGobchatApplicationContext.UISynchronizer"/>
        /// so WinForms runs on its supported STA thread. Never throws; any failure leaves startup to continue
        /// with defaults.
        /// </summary>
        public static void RunFirstTimeSetup()
        {
            var appConfigLocation = GobchatContext.AppConfigLocation;
            if (!IsFirstRun(appConfigLocation))
                return;

            var resourceLocation = GobchatContext.ResourceLocation;
            var themeLabels = ReadThemeLabels(resourceLocation);
            var (defaultLanguage, defaultTheme, defaultAutoUpdate) = ReadDefaultSelections(resourceLocation);
            var hasLegacy = GobchatContext.HasLegacyAppData;

            // Show the welcome screen on the UI (STA) thread - WinForms is only supported there, and this
            // method runs on the background startup worker. The ShowDialog() result is deliberately ignored:
            // there is no close box, but even a forced close (Alt+F4) should still persist the current
            // selections so the first run completes and the screen never reappears.
            var (language, theme, autoUpdate, migrate) = AbstractGobchatApplicationContext.UISynchronizer.RunSync(() =>
            {
                using (var dialog = new global::Gobchat.Core.UI.FirstRunDialog(themeLabels, defaultLanguage, defaultTheme, defaultAutoUpdate, hasLegacy))
                {
                    dialog.ShowDialog();
                    return (dialog.SelectedLanguage, dialog.SelectedTheme, dialog.AutoUpdate, dialog.MigrateLegacy);
                }
            });

            if (migrate && hasLegacy)
            {
                try
                {
                    logger.Info("First-run: migrating legacy app data from %AppData%\\Gobchat");
                    GobchatContext.MigrateLegacyAppData();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Failed to migrate legacy app data during first-run setup");
                }
            }

            WriteAppSettings(appConfigLocation, language, theme, autoUpdate);
        }

        // Loads any (possibly just-migrated) appsettings.json, layers the chosen values on top, and writes it
        // back. Writing the file is also what marks the first run as complete (see IsFirstRun). Internal so the
        // migrate-then-layer file behavior is unit-testable without standing up the WinForms dialog.
        internal static void WriteAppSettings(string appConfigLocation, string language, string theme, bool autoUpdate)
        {
            var appSettingsPath = Path.Combine(appConfigLocation, AppSettingsFileName);

            JObject? existing = null;
            if (File.Exists(appSettingsPath))
            {
                try
                {
                    existing = JObject.Parse(File.ReadAllText(appSettingsPath));
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Could not read migrated appsettings.json; writing a fresh one");
                }
            }

            try
            {
                var appSettings = BuildAppSettings(existing, language, theme, autoUpdate);
                Directory.CreateDirectory(appConfigLocation);
                File.WriteAllText(appSettingsPath, appSettings.ToString(Formatting.Indented));
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to write appsettings.json during first-run setup");
            }
        }

        private static (string Language, string Theme, bool AutoUpdate) ReadDefaultSelections(string resourceLocation)
        {
            try
            {
                var defaults = JObject.Parse(File.ReadAllText(Path.Combine(resourceLocation, "default_appsettings.json")));
                var language = (string?)defaults.SelectToken(LanguagePath) ?? FallbackLanguage;
                var theme = (string?)defaults.SelectToken(ThemePath) ?? FallbackTheme;
                var autoUpdateToken = defaults.SelectToken(AutoUpdatePath);
                var autoUpdate = autoUpdateToken == null || (bool)autoUpdateToken;
                return (language, theme, autoUpdate);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not read default_appsettings.json for the first-run screen; using fallbacks");
                return (FallbackLanguage, FallbackTheme, true);
            }
        }

        private static void SetPath(JObject root, string path, object value)
            => JsonUtil.WalkJson(root, path, JsonUtil.MissingElementHandling.Create,
                (node, key) => node[key] = value == null ? null : JToken.FromObject(value));
    }
}
