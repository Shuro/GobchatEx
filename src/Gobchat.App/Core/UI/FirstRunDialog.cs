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
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Gobchat.Core.Runtime;
using NLog;

namespace Gobchat.Core.UI
{
    /// <summary>
    /// First-time launch screen (see <see cref="FirstRunSetup"/>). A native, dark-themed welcome dialog that
    /// echoes the web greeter's wordmark and lets the user pick language, theme, auto-update and whether to
    /// migrate legacy profiles. Strings are pulled from <c>Resources.resx</c>/<c>Resources.de.resx</c> and
    /// re-applied live when the language selection changes (this runs before the language module sets the
    /// culture). The only way out is "Get Started" (there is no close box), so a completed run always persists.
    /// </summary>
    public partial class FirstRunDialog : Form
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Mirrors the language options on the settings page (config_app.html). Endonyms are not localized.
        private static readonly (string Code, string DisplayName)[] Languages =
        {
            ("en", "English"),
            ("de", "Deutsch"),
        };

        // Captured at construction: chkMigrate.Visible can't be read after the dialog closes (Control.Visible
        // returns the effective visibility, which is false once the parent form is hidden), and MigrateLegacy
        // is read by the caller after ShowDialog() returns. Reading .Visible there silently disabled migration.
        private readonly bool _showMigrate;

        public string SelectedLanguage => Languages[Math.Max(0, cmbLanguage.SelectedIndex)].Code;
        public string SelectedTheme => cmbTheme.SelectedItem as string ?? cmbTheme.Text;
        public bool AutoUpdate => chkAutoUpdate.Checked;
        public bool MigrateLegacy => _showMigrate && chkMigrate.Checked;

        public FirstRunDialog(IReadOnlyList<string> themeLabels, string defaultLanguage, string defaultTheme, bool defaultAutoUpdate, bool showMigrate)
        {
            InitializeComponent();
            TryLoadAppIcon();
            LayoutWordmark();

            cmbLanguage.Items.AddRange(Languages.Select(l => (object)l.DisplayName).ToArray());
            var languageIndex = Array.FindIndex(Languages, l => string.Equals(l.Code, defaultLanguage, StringComparison.OrdinalIgnoreCase));
            cmbLanguage.SelectedIndex = languageIndex >= 0 ? languageIndex : 0;

            cmbTheme.Items.AddRange(themeLabels.Cast<object>().ToArray());
            var themeIndex = themeLabels.ToList().FindIndex(t => string.Equals(t, defaultTheme, StringComparison.OrdinalIgnoreCase));
            cmbTheme.SelectedIndex = cmbTheme.Items.Count == 0 ? -1 : (themeIndex >= 0 ? themeIndex : 0);

            chkAutoUpdate.Checked = defaultAutoUpdate;

            _showMigrate = showMigrate;
            chkMigrate.Visible = showMigrate;
            lblMigrateNote.Visible = showMigrate;
            chkMigrate.Checked = showMigrate;

            cmbLanguage.SelectedIndexChanged += (s, e) => ApplyTexts();
            btnStart.Click += (s, e) => { DialogResult = DialogResult.OK; Close(); };

            ApplyTexts();
        }

        private void ApplyTexts()
        {
            var culture = CultureInfo.GetCultureInfo(SelectedLanguage);
            lblSubtitle.Text = Loc("firstrun.welcome", culture);
            lblLanguage.Text = Loc("firstrun.language.label", culture);
            lblTheme.Text = Loc("firstrun.theme.label", culture);
            chkAutoUpdate.Text = Loc("firstrun.autoupdate.label", culture);
            chkMigrate.Text = Loc("firstrun.migrate.label", culture);
            lblMigrateNote.Text = Loc("firstrun.migrate.note", culture);
            btnStart.Text = Loc("firstrun.start.button", culture);
        }

        // Draws the wordmark as a single word: "Gobchat" in ink directly followed by "Ex" in gold, matching
        // the web greeter (system.html: Gobchat<span>Ex</span>). NoPadding rendering makes the two segments
        // butt together; measuring from the live (DPI-scaled) Font keeps the size correct under
        // AutoScaleMode.Font. (The label itself draws no text - see Designer: AutoSize=false, Text="".)
        private void LayoutWordmark()
        {
            const TextFormatFlags flags = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;

            var gobSize = TextRenderer.MeasureText("Gobchat", lblWordmark.Font, Size.Empty, flags);
            var exSize = TextRenderer.MeasureText("Ex", lblWordmark.Font, Size.Empty, flags);
            lblWordmark.Size = new Size(gobSize.Width + exSize.Width, Math.Max(gobSize.Height, exSize.Height));
            lblWordmark.Paint += (s, e) =>
            {
                TextRenderer.DrawText(e.Graphics, "Gobchat", lblWordmark.Font, new Point(0, 0), ColorInk, flags);
                TextRenderer.DrawText(e.Graphics, "Ex", lblWordmark.Font, new Point(gobSize.Width, 0), ColorGold, flags);
            };
        }

        private static string Loc(string key, CultureInfo culture)
            => Resources.ResourceManager.GetString(key, culture) ?? key;

        private void TryLoadAppIcon()
        {
            try
            {
                var iconPath = Path.Combine(GobchatContext.ResourceLocation, "GobIcon.ico");
                if (File.Exists(iconPath))
                    Icon = new System.Drawing.Icon(iconPath);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Could not load the application icon for the first-run screen");
            }
        }
    }
}
