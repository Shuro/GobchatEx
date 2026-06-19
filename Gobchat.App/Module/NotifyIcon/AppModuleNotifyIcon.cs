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

using Gobchat.Core.Runtime;
using Gobchat.Core.UI;
using Gobchat.Module.Language;
using NLog;
using System;
using System.Windows.Forms;

namespace Gobchat.Module.NotifyIcon
{
    public sealed class AppModuleNotifyIcon : IApplicationModule
    {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();

        public const string NotifyIconManagerId = "Gobchat.NotifyIconManager";

        private IUIManager _manager;
        private ILocaleManager _localeManager;
        private ToolStripMenuItem _closeMenuItem;

        /// <summary>
        /// Requires: <see cref="IUIManager"/> <br></br>
        /// Requires: <see cref="ILocaleManager"/> <br></br>
        /// <br></br>
        /// Installs UI element: <see cref="INotifyIconManager"/> <br></br>
        /// </summary>
        public AppModuleNotifyIcon()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _manager = container.Resolve<IUIManager>();
            _localeManager = container.Resolve<ILocaleManager>();

            _manager.CreateUIElement(NotifyIconManagerId, () =>
            {
                var notifyIconManager = new NotifyIconManager(new[] { "app", "debug", "close" }, "app")
                {
                    Text = "GobchatEx",
                    Icon = Gobchat.Resources.GobIcon,
                    Visible = true
                };

                _closeMenuItem = new ToolStripMenuItem(Resources.Module_NotifyIcon_UI_Close);
                _closeMenuItem.Click += OnEvent_MenuItem_Close;
                notifyIconManager.AddMenuToGroup("close", "close", _closeMenuItem);

                return notifyIconManager;
            });

            // The .resx label is set once above; re-apply it on language change (fires once on subscribe
            // too, per the ILocaleManager contract).
            _localeManager.OnLocaleChange += OnEvent_LocaleManager_LocaleChange;
        }

        private void OnEvent_LocaleManager_LocaleChange(object sender, LocaleEventArgs e)
        {
            _manager?.UISynchronizer.RunSync(() =>
            {
                if (_closeMenuItem != null)
                    _closeMenuItem.Text = Resources.Module_NotifyIcon_UI_Close;
            });
        }

        private void OnEvent_MenuItem_Close(object sender, EventArgs e)
        {
            logger.Info("User requests shutdown");
            GobchatApplicationContext.ExitGobchat();
        }

        public void Dispose()
        {
            if (_localeManager != null)
            {
                _localeManager.OnLocaleChange -= OnEvent_LocaleManager_LocaleChange;
                _localeManager = null;
            }
            _closeMenuItem = null;

            if (_manager == null)
                return;
            _manager.DisposeUIElement(NotifyIconManagerId);
            _manager = null;
        }
    }
}