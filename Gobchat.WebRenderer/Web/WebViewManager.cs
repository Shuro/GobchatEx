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
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;

namespace Gobchat.UI.Web
{
    /// <summary>
    /// Owns the process-wide WebView2 <see cref="CoreWebView2Environment"/> that the overlay
    /// browser shares.
    ///
    /// This replaces the former CefSharp.OffScreen bootstrap. There is no bundled Chromium to
    /// locate, no assembly-resolve hook and no <c>Cef.Initialize</c>: WebView2 renders through
    /// the OS Evergreen runtime (serviced by Windows), so all this does is pin a writable
    /// user-data folder and create one environment lazily, on demand.
    /// </summary>
    public static class WebViewManager
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private static readonly object _lock = new object();
        private static Task<CoreWebView2Environment> _environmentTask;
        private static bool _isDisposed;

        /// <summary>
        /// Folder WebView2 may write its cache/state into. Must be writable (the application
        /// folder can be Program Files), so the App points this at
        /// <c>%AppData%\GobchatEx\WebView2</c>. When empty, WebView2 picks its default next to
        /// the executable.
        /// </summary>
        public static string UserDataFolder { get; set; } = string.Empty;

        /// <summary>Version of the installed Evergreen runtime, or <c>null</c> until initialized.</summary>
        public static string RuntimeVersion { get; private set; }

        public static void Initialize()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(WebViewManager));
                if (_environmentTask != null)
                    return;

                try
                {
                    // Throws WebView2RuntimeNotFoundException when the Evergreen runtime is absent.
                    RuntimeVersion = CoreWebView2Environment.GetAvailableBrowserVersionString();
                    logger.Info(() => $"WebView2 runtime {RuntimeVersion} detected");
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "WebView2 runtime not found");
                    throw new InvalidOperationException(
                        "The Microsoft Edge WebView2 runtime is required but was not found. It ships " +
                        "with current Windows 10/11; otherwise install it from " +
                        "https://developer.microsoft.com/microsoft-edge/webview2/.", ex);
                }

                // The overlay only ever loads its own resources from the gobchat.localhost virtual
                // host; it never reaches the network. Disable Chromium's proxy stack so it doesn't
                // run WPAD proxy auto-detection on every navigation, which otherwise stalls even
                // loopback requests by a second or more.
                var options = new CoreWebView2EnvironmentOptions
                {
                    AdditionalBrowserArguments = "--no-proxy-server"
                };

                // CreateAsync is kicked off but not awaited here: it completes on the UI message
                // loop, so blocking the UI thread on it would deadlock. The browser awaits the
                // stored task instead (see GetEnvironmentAsync).
                var userData = string.IsNullOrWhiteSpace(UserDataFolder) ? null : UserDataFolder;
                _environmentTask = CoreWebView2Environment.CreateAsync(
                    browserExecutableFolder: null, userDataFolder: userData, options: options);
            }
        }

        public static Task<CoreWebView2Environment> GetEnvironmentAsync()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(nameof(WebViewManager));
                if (_environmentTask == null)
                    Initialize();
                return _environmentTask;
            }
        }

        public static void Dispose()
        {
            lock (_lock)
            {
                if (_isDisposed)
                    return;
                _isDisposed = true;
                _environmentTask = null;
            }
        }
    }
}
