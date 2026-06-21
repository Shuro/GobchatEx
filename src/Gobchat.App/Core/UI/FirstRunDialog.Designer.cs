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

using System.Drawing;
using System.Windows.Forms;

namespace Gobchat.Core.UI
{
    // Built entirely in code (no per-form .resx); text is set/localized from the code-behind. Layout mirrors
    // the FFXIV Modern look of the web greeter: a layered dark surface, IBM-Plex-like sans (Segoe UI here,
    // since IBM Plex is web-only) and a single gold accent on the "Ex" wordmark and the primary button.
    partial class FirstRunDialog
    {
        private System.ComponentModel.IContainer components = null;

        // FFXIV Modern tokens (kept in sync with system.html / styles/config.scss).
        private static readonly Color ColorSurface = Color.FromArgb(23, 26, 32);
        private static readonly Color ColorControl = Color.FromArgb(35, 40, 48);
        private static readonly Color ColorInk = Color.FromArgb(232, 234, 238);
        private static readonly Color ColorInkMuted = Color.FromArgb(160, 167, 180);
        private static readonly Color ColorGold = Color.FromArgb(224, 164, 78);

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.layout = new TableLayoutPanel();
            this.lblWordmark = new Label();
            this.lblSubtitle = new Label();
            this.lblLanguage = new Label();
            this.cmbLanguage = new ComboBox();
            this.lblTheme = new Label();
            this.cmbTheme = new ComboBox();
            this.chkAutoUpdate = new CheckBox();
            this.chkMigrate = new CheckBox();
            this.lblMigrateNote = new Label();
            this.btnStart = new Button();
            this.layout.SuspendLayout();
            this.SuspendLayout();
            //
            // layout
            //
            this.layout.Dock = DockStyle.Fill;
            this.layout.AutoSize = true;
            this.layout.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.layout.ColumnCount = 2;
            this.layout.Padding = new Padding(28, 24, 28, 24);
            this.layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            this.layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            for (int i = 0; i < 8; ++i)
                this.layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            this.layout.Controls.Add(this.lblWordmark, 0, 0);
            this.layout.Controls.Add(this.lblSubtitle, 0, 1);
            this.layout.Controls.Add(this.lblLanguage, 0, 2);
            this.layout.Controls.Add(this.cmbLanguage, 1, 2);
            this.layout.Controls.Add(this.lblTheme, 0, 3);
            this.layout.Controls.Add(this.cmbTheme, 1, 3);
            this.layout.Controls.Add(this.chkAutoUpdate, 0, 4);
            this.layout.Controls.Add(this.chkMigrate, 0, 5);
            this.layout.Controls.Add(this.lblMigrateNote, 0, 6);
            this.layout.Controls.Add(this.btnStart, 0, 7);
            this.layout.SetColumnSpan(this.lblWordmark, 2);
            this.layout.SetColumnSpan(this.lblSubtitle, 2);
            this.layout.SetColumnSpan(this.chkAutoUpdate, 2);
            this.layout.SetColumnSpan(this.chkMigrate, 2);
            this.layout.SetColumnSpan(this.lblMigrateNote, 2);
            this.layout.SetColumnSpan(this.btnStart, 2);
            //
            // lblWordmark
            //
            // Owner-drawn in code (FirstRunDialog.cs) so "Gobchat" (ink) and "Ex" (gold) butt together as a
            // single word - two AutoSize labels each add glyph-overhang padding and never touch. Size is set
            // there too, from the measured text. AutoSize stays false and Text empty (the base label draws
            // nothing) so only the Paint handler renders the wordmark.
            this.lblWordmark.AutoSize = false;
            this.lblWordmark.Anchor = AnchorStyles.None;
            this.lblWordmark.Font = new Font("Segoe UI", 26F, FontStyle.Bold);
            this.lblWordmark.Margin = new Padding(0, 0, 0, 6);
            this.lblWordmark.Text = "";
            //
            // lblSubtitle
            //
            this.lblSubtitle.AutoSize = true;
            this.lblSubtitle.Anchor = AnchorStyles.None;
            this.lblSubtitle.ForeColor = ColorInkMuted;
            this.lblSubtitle.MaximumSize = new Size(380, 0);
            this.lblSubtitle.Margin = new Padding(0, 0, 0, 22);
            //
            // lblLanguage
            //
            this.lblLanguage.AutoSize = true;
            this.lblLanguage.Dock = DockStyle.Fill;
            this.lblLanguage.TextAlign = ContentAlignment.MiddleLeft;
            this.lblLanguage.ForeColor = ColorInk;
            this.lblLanguage.Margin = new Padding(0, 6, 16, 6);
            //
            // cmbLanguage
            //
            this.cmbLanguage.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.cmbLanguage.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbLanguage.FlatStyle = FlatStyle.Flat;
            this.cmbLanguage.BackColor = ColorControl;
            this.cmbLanguage.ForeColor = ColorInk;
            this.cmbLanguage.Margin = new Padding(0, 4, 0, 4);
            //
            // lblTheme
            //
            this.lblTheme.AutoSize = true;
            this.lblTheme.Dock = DockStyle.Fill;
            this.lblTheme.TextAlign = ContentAlignment.MiddleLeft;
            this.lblTheme.ForeColor = ColorInk;
            this.lblTheme.Margin = new Padding(0, 6, 16, 6);
            //
            // cmbTheme
            //
            this.cmbTheme.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            this.cmbTheme.DropDownStyle = ComboBoxStyle.DropDownList;
            this.cmbTheme.FlatStyle = FlatStyle.Flat;
            this.cmbTheme.BackColor = ColorControl;
            this.cmbTheme.ForeColor = ColorInk;
            this.cmbTheme.Margin = new Padding(0, 4, 0, 4);
            //
            // chkAutoUpdate
            //
            this.chkAutoUpdate.AutoSize = true;
            this.chkAutoUpdate.ForeColor = ColorInk;
            this.chkAutoUpdate.Margin = new Padding(0, 14, 0, 4);
            //
            // chkMigrate
            //
            this.chkMigrate.AutoSize = true;
            this.chkMigrate.ForeColor = ColorInk;
            this.chkMigrate.Margin = new Padding(0, 8, 0, 2);
            //
            // lblMigrateNote
            //
            this.lblMigrateNote.AutoSize = true;
            this.lblMigrateNote.ForeColor = ColorInkMuted;
            this.lblMigrateNote.Font = new Font("Segoe UI", 8.25F);
            this.lblMigrateNote.MaximumSize = new Size(380, 0);
            this.lblMigrateNote.Margin = new Padding(22, 0, 0, 4);
            //
            // btnStart
            //
            this.btnStart.Anchor = AnchorStyles.Right;
            this.btnStart.AutoSize = false;
            this.btnStart.Size = new Size(150, 36);
            this.btnStart.FlatStyle = FlatStyle.Flat;
            this.btnStart.FlatAppearance.BorderSize = 0;
            this.btnStart.BackColor = ColorGold;
            this.btnStart.ForeColor = ColorSurface;
            this.btnStart.Font = new Font("Segoe UI", 9.75F, FontStyle.Bold);
            this.btnStart.Cursor = Cursors.Hand;
            this.btnStart.UseVisualStyleBackColor = false;
            this.btnStart.Margin = new Padding(0, 16, 0, 0);
            //
            // FirstRunDialog
            //
            this.AcceptButton = this.btnStart;
            this.AutoScaleMode = AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.BackColor = ColorSurface;
            this.ForeColor = ColorInk;
            this.Font = new Font("Segoe UI", 9F);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new Size(440, 0);
            this.Name = "FirstRunDialog";
            this.ShowInTaskbar = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "GobchatEx";
            this.Controls.Add(this.layout);
            this.layout.ResumeLayout(false);
            this.layout.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private TableLayoutPanel layout;
        private Label lblWordmark;
        private Label lblSubtitle;
        private Label lblLanguage;
        private ComboBox cmbLanguage;
        private Label lblTheme;
        private ComboBox cmbTheme;
        private CheckBox chkAutoUpdate;
        private CheckBox chkMigrate;
        private Label lblMigrateNote;
        private Button btnStart;
    }
}
