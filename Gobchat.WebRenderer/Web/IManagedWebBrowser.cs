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

using System;
using System.Drawing;
using System.Threading.Tasks;

namespace Gobchat.UI.Web
{
    /// <summary>
    /// Abstraction over the WebView2 content (navigation, scripting and the JS&#8596;C# bridge)
    /// that backs the overlay. The owning form handles window compositing, input forwarding and
    /// click-through; this interface deliberately exposes none of that.
    /// </summary>
    public interface IManagedWebBrowser
    {
        event EventHandler<BrowserConsoleLogEventArgs> OnBrowserConsoleLog;

        event EventHandler<BrowserErrorEventArgs> OnBrowserError;

        /// <summary>
        /// Fires once the underlying WebView2 is ready. Listeners added after the browser is
        /// already initialized fire immediately and are not stored.
        /// </summary>
        event EventHandler<BrowserInitializedEventArgs> OnBrowserInitialized;

        event EventHandler<BrowserLoadPageEventArgs> OnBrowserLoadPage;

        event EventHandler<BrowserLoadPageEventArgs> OnBrowserLoadPageDone;

        bool IsBrowserInitialized { get; }

        Size Size { get; set; }

        /// <summary>
        /// Local folder that backs the overlay's virtual host (<c>https://gobchat.local/</c>).
        /// Set before the browser initializes; an <c>https</c> origin is required because the UI
        /// loads as ES modules, which Chromium blocks over <c>file://</c>.
        /// </summary>
        string ResourceRootFolder { get; set; }

        /// <summary>
        /// Resolves a requested resource path (e.g. <c>/module/Chat.js</c>) served from the
        /// virtual host to an absolute local file path, or <c>null</c> to fall through to the
        /// virtual-host folder mapping. Set by the App so resource-layout rules (the
        /// <c>module</c>&#8594;<c>modules</c> rename, <c>.min</c> preference) stay on its side.
        /// </summary>
        Func<string, string> ResourceResolver { get; set; }

        bool BindBrowserAPI(IBrowserAPI api, bool isApiAsync);

        bool UnbindBrowserAPI(IBrowserAPI api);

        /// <summary>
        /// Registers a script to run in every page before its own scripts (WebView2
        /// <c>AddScriptToExecuteOnDocumentCreated</c>). Used for the bridge shim and the injected
        /// <c>Gobchat.*</c> enums/config. Order of registration is preserved.
        /// </summary>
        void AddInitializationScript(string script);

        void ShowDevTools();

        void CloseBrowser(bool forceClose);

        void Dispose();

        void ExecuteScript(string script);

        Task<IJavascriptResponse> EvaluateScript(string script, TimeSpan? timeout);

        void Load(string url);

        void Reload();
    }

    #region EventArgs

    public class BrowserLoadPageEventArgs : EventArgs
    {
        public int HttpStatusCode { get; }
        public string Url { get; }

        public BrowserLoadPageEventArgs(int httpStatusCode, string url)
        {
            HttpStatusCode = httpStatusCode;
            Url = url;
        }
    }

    public class BrowserInitializedEventArgs : EventArgs
    {
    }

    public class BrowserErrorEventArgs : EventArgs
    {
        public string ErrorCode { get; }
        public string ErrorText { get; }
        public string Url { get; }

        public BrowserErrorEventArgs(string errorCode, string errorText, string url)
        {
            ErrorCode = errorCode;
            ErrorText = errorText;
            Url = url;
        }
    }

    public class BrowserConsoleLogEventArgs : EventArgs
    {
        public string Message { get; }
        public string Source { get; }
        public int Line { get; }

        public BrowserConsoleLogEventArgs(string message, string source, int line)
        {
            Message = message;
            Source = source;
            Line = line;
        }
    }

    #endregion EventArgs
}
