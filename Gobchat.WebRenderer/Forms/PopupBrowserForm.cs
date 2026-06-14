/*******************************************************************************
 * Copyright (C) 2019-2025 MarbleBag
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

using Microsoft.Web.WebView2.Core;
using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Gobchat.UI.Forms
{
    /// <summary>
    /// A normal (windowed) host for a page opened via <c>window.open</c> — in practice the settings
    /// dialog. It uses the overlay's WebView2 environment and the same <c>gobchat.local</c> origin,
    /// which keeps the page's <c>window.opener</c> sharing (GobchatAPI, gobConfig, Gobchat) intact,
    /// so the existing config TypeScript runs unchanged. Created from
    /// <see cref="ManagedWebBrowser"/>'s NewWindowRequested handler.
    /// </summary>
    internal sealed class PopupBrowserForm : Form
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private readonly CoreWebView2Environment _environment;
        private readonly string _resourceRootFolder;
        private readonly Func<string, string> _resourceResolver;

        private CoreWebView2Controller _controller;

        public CoreWebView2 CoreWebView2 { get; private set; }

        public PopupBrowserForm(CoreWebView2Environment environment, string resourceRootFolder, Func<string, string> resourceResolver)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _resourceRootFolder = resourceRootFolder;
            _resourceResolver = resourceResolver;

            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            ShowInTaskbar = true;
            MinimizeBox = true;
            MaximizeBox = true;
            ShowIcon = false;
            Text = "GobchatEx";
            ClientSize = new Size(900, 650);
            MinimumSize = new Size(480, 360);
        }

        public void ApplyWindowFeatures(CoreWebView2WindowFeatures features)
        {
            if (features == null)
                return;
            if (features.HasSize && features.Width > 0 && features.Height > 0)
                ClientSize = new Size((int)features.Width, (int)features.Height);
        }

        public async Task InitializeAsync()
        {
            // Force the handle so the controller can bind to it.
            _ = Handle;

            _controller = await _environment.CreateCoreWebView2ControllerAsync(Handle).ConfigureAwait(true);
            CoreWebView2 = _controller.CoreWebView2;
            _controller.Bounds = new Rectangle(Point.Empty, ClientSize);

            var settings = CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;
#if DEBUG
            settings.AreDevToolsEnabled = true;
#else
            settings.AreDevToolsEnabled = false;
#endif

            // Resources are served through WebResourceRequested + the shared resolver (no
            // virtual-host folder mapping; see ManagedWebBrowser.Attach for why).
            CoreWebView2.AddWebResourceRequestedFilter("https://" + ManagedWebBrowser.VirtualHost + "/*", CoreWebView2WebResourceContext.All);
            CoreWebView2.WebResourceRequested += OnWebResourceRequested;
            CoreWebView2.WindowCloseRequested += OnWindowCloseRequested;

            this.Resize += OnFormResize;
        }

        private void OnFormResize(object sender, EventArgs e)
        {
            if (_controller != null)
                _controller.Bounds = new Rectangle(Point.Empty, ClientSize);
        }

        private void OnWebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            ManagedWebBrowser.ServeResource(_environment, _resourceResolver, e);
        }

        private void OnWindowCloseRequested(object sender, object e)
        {
            // Raised by window.close() in the page (the dialog's save/exit buttons).
            if (IsHandleCreated && !IsDisposed)
                BeginInvoke((Action)Close);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (CoreWebView2 != null)
                    {
                        CoreWebView2.WebResourceRequested -= OnWebResourceRequested;
                        CoreWebView2.WindowCloseRequested -= OnWindowCloseRequested;
                    }
                    _controller?.Close();
                }
                catch (Exception ex)
                {
                    logger.Warn(ex, "Error while disposing popup browser");
                }
                _controller = null;
                CoreWebView2 = null;
            }
            base.Dispose(disposing);
        }
    }
}
