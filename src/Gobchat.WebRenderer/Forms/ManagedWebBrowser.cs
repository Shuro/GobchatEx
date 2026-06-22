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

using Gobchat.UI.Web;
using Microsoft.Web.WebView2.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Gobchat.UI.Forms
{
    /// <summary>
    /// Wraps the overlay's <see cref="CoreWebView2"/> content: navigation, scripting, resource
    /// serving from the <c>gobchat.localhost</c> virtual host, and the postMessage JSON bridge that
    /// exposes the C# <see cref="IBrowserAPI"/> objects to the page.
    ///
    /// The owning <see cref="OverlayForm"/> creates the WebView2 environment and composition
    /// controller (it owns the HWND + DirectComposition tree) and hands the controller here via
    /// <see cref="Attach"/>. Window compositing, input forwarding and click-through stay on the
    /// form; this type knows nothing about them.
    /// </summary>
    internal sealed class ManagedWebBrowser : IManagedWebBrowser, IDisposable
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        // Host must NOT end in ".local": that triggers Windows mDNS resolution, which blocks ~2s
        // before failing and stalled every page load. ".localhost" is special-cased by Chromium to
        // resolve to loopback instantly (the bytes are still supplied by WebResourceRequested below).
        public const string VirtualHost = "gobchat.localhost";
        private const string VirtualHostPrefix = "https://gobchat.localhost/";

        // Flip on (Debug builds) to trace per-resource serve timing into the log.
        private static readonly bool TraceResources =
#if DEBUG
            true;
#else
            false;
#endif

        // Injected before any page script. Builds window.GobchatAPI-style facades that turn each
        // method call into a postMessage round-trip, and forwards console output to the host.
        private const string BridgeCoreScript = @"
(function () {
  if (window.__gobchatBridge) return;
  if (!window.chrome || !window.chrome.webview) return;
  var pending = new Map();
  var nextId = 1;
  window.chrome.webview.addEventListener('message', function (e) {
    var msg = e.data;
    if (!msg || msg.__gobapi !== true) return;
    var entry = pending.get(msg.id);
    if (!entry) return;
    pending.delete(msg.id);
    if (msg.error) entry.reject(new Error(msg.error));
    else entry.resolve(msg.result);
  });
  function makeApi(apiName) {
    return new Proxy({}, {
      get: function (target, prop) {
        if (typeof prop !== 'string') return undefined;
        return function () {
          var args = Array.prototype.slice.call(arguments);
          var id = nextId++;
          return new Promise(function (resolve, reject) {
            pending.set(id, { resolve: resolve, reject: reject });
            window.chrome.webview.postMessage({ __gobapi: true, api: apiName, method: prop, id: id, args: args });
          });
        };
      }
    });
  }
  function forwardConsole(level) {
    var original = console[level] ? console[level].bind(console) : function () {};
    console[level] = function () {
      try {
        var parts = Array.prototype.map.call(arguments, function (a) {
          try { return typeof a === 'string' ? a : JSON.stringify(a); } catch (e) { return String(a); }
        });
        window.chrome.webview.postMessage({ __gobconsole: true, level: level, message: parts.join(' ') });
      } catch (e) { /* never let logging break the page */ }
      original.apply(console, arguments);
    };
  }
  ['log', 'info', 'warn', 'error'].forEach(forwardConsole);
  window.__gobchatBridge = { makeApi: makeApi };
})();";

        private readonly object _lock = new object();
        private readonly List<IBrowserAPI> _apis = new List<IBrowserAPI>();
        private readonly List<string> _pendingInitScripts = new List<string>();
        private readonly List<Task> _initScriptTasks = new List<Task>();
        private readonly JsonSerializer _argSerializer = JsonSerializer.CreateDefault();

        // Assigned in Attach (two-phase init) and only ever touched while _initialized/!_disposed,
        // which the nullable analyzer can't correlate; null! keeps those gated call sites un-littered.
        private CoreWebView2CompositionController _controller = null!;
        private CoreWebView2 _webview = null!;
        private bool _initialized;
        private bool _disposed;
        private Size _size = new Size(800, 450);
        private string? _pendingNavigation;
        private string _lastNavigationUri = string.Empty;
        private SettingsOverlayForm? _settingsForm;

        public event EventHandler<BrowserConsoleLogEventArgs>? OnBrowserConsoleLog;

        public event EventHandler<BrowserErrorEventArgs>? OnBrowserError;

        public event EventHandler<BrowserLoadPageEventArgs>? OnBrowserLoadPage;

        public event EventHandler<BrowserLoadPageEventArgs>? OnBrowserLoadPageDone;

        public event EventHandler? SettingsWindowClosed;

        private event EventHandler<BrowserInitializedEventArgs>? _browserInitialized;

        // Late subscribers fire immediately if the browser is already up (mirrors the old
        // CefSharp behaviour the App relies on).
        event EventHandler<BrowserInitializedEventArgs> IManagedWebBrowser.OnBrowserInitialized
        {
            add
            {
                lock (_lock)
                {
                    if (_initialized)
                        value?.Invoke(this, new BrowserInitializedEventArgs());
                    else
                        _browserInitialized += value;
                }
            }
            remove { _browserInitialized -= value; }
        }

        public bool IsBrowserInitialized => _initialized;

        // Kept for the popup wiring; resource resolution itself goes entirely through
        // ResourceResolver + WebResourceRequested (no virtual-host folder mapping — see Attach).
        public string? ResourceRootFolder { get; set; }

        public Func<string, string>? ResourceResolver { get; set; }

        public Action<Rectangle>? SettingsFramePersister { get; set; }

        public Func<Rectangle?>? SettingsFrameProvider { get; set; }

        public Size Size
        {
            get => _size;
            set
            {
                _size = value;
                if (_controller != null)
                    _controller.Bounds = new Rectangle(Point.Empty, value);
            }
        }

        /// <summary>
        /// Wires up the WebView2 once the form has created its composition controller. Runs on the
        /// UI thread. Registers the bridge + queued init scripts before firing
        /// <c>OnBrowserInitialized</c>, so a navigation triggered from that event already has them.
        /// </summary>
        internal async Task Attach(CoreWebView2CompositionController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _webview = controller.CoreWebView2;
            _controller.Bounds = new Rectangle(Point.Empty, _size);

            var settings = _webview.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.AreBrowserAcceleratorKeysEnabled = false;
            settings.IsZoomControlEnabled = false;
            settings.IsPasswordAutosaveEnabled = false;
            settings.IsGeneralAutofillEnabled = false;
#if DEBUG
            settings.AreDevToolsEnabled = true;
#else
            settings.AreDevToolsEnabled = false;
#endif

            // Serve every gobchat.localhost request through WebResourceRequested. We deliberately do
            // NOT use SetVirtualHostNameToFolderMapping: a folder mapping owns the host and serves
            // files by exact name, bypassing WebResourceRequested — which would defeat the
            // module->modules / ".min" rewrites (the UI requests e.g. /lib/jquery-4.0.0.js but only
            // jquery-4.0.0.min.js ships).
            _webview.AddWebResourceRequestedFilter(VirtualHostPrefix + "*", CoreWebView2WebResourceContext.All);
            _webview.WebResourceRequested += OnWebResourceRequested;
            _webview.NavigationStarting += OnNavigationStarting;
            _webview.NavigationCompleted += OnNavigationCompleted;
            _webview.WebMessageReceived += OnWebMessageReceived;
            _webview.NewWindowRequested += OnNewWindowRequested;

            // Bridge core must run before any per-API facade or page script.
            await RegisterScriptAsync(BridgeCoreScript).ConfigureAwait(true);
            List<string> queued;
            lock (_lock)
            {
                queued = new List<string>(_pendingInitScripts);
                _pendingInitScripts.Clear();
            }
            foreach (var script in queued)
                await RegisterScriptAsync(script).ConfigureAwait(true);

            _initialized = true;

            var handler = _browserInitialized;
            _browserInitialized = null;
            handler?.Invoke(this, new BrowserInitializedEventArgs());

            if (_pendingNavigation != null)
            {
                var url = _pendingNavigation;
                _pendingNavigation = null;
                Load(url);
            }
        }

        private Task RegisterScriptAsync(string script)
        {
            var task = _webview.AddScriptToExecuteOnDocumentCreatedAsync(script);
            lock (_lock)
                _initScriptTasks.Add(task);
            return task;
        }

        public void AddInitializationScript(string script)
        {
            if (string.IsNullOrEmpty(script))
                return;
            lock (_lock)
            {
                if (!_initialized)
                {
                    _pendingInitScripts.Add(script);
                    return;
                }
            }
            _ = RegisterScriptAsync(script);
        }

        #region navigation / events

        public void Load(string url)
        {
            if (string.IsNullOrEmpty(url))
                return;
            if (!_initialized)
            {
                _pendingNavigation = url;
                return;
            }
            _ = NavigateAsync(url);
        }

        private async Task NavigateAsync(string url)
        {
            try
            {
                Task[] outstanding;
                lock (_lock)
                    outstanding = _initScriptTasks.ToArray();
                await Task.WhenAll(outstanding).ConfigureAwait(true);
                _webview.Navigate(url);
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Navigation to {url} failed");
            }
        }

        public void Reload()
        {
            if (_initialized)
                _webview.Reload();
        }

        private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
        {
            _lastNavigationUri = e.Uri;
            OnBrowserLoadPage?.Invoke(this, new BrowserLoadPageEventArgs(0, e.Uri));
        }

        private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            int status = 0;
            try { status = e.HttpStatusCode; } catch { /* not available on older runtimes */ }

            if (!e.IsSuccess)
                OnBrowserError?.Invoke(this,
                    new BrowserErrorEventArgs(e.WebErrorStatus.ToString(), e.WebErrorStatus.ToString(), _lastNavigationUri));

            OnBrowserLoadPageDone?.Invoke(this, new BrowserLoadPageEventArgs(status, _lastNavigationUri));
        }

        #endregion navigation / events

        #region resource serving

        private void OnWebResourceRequested(object? sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            ServeResource(_webview.Environment, ResourceResolver, e);
        }

        // Shared by the overlay and the config popup (each WebView2 owns its own virtual-host
        // mapping, but they apply the same resolution rules).
        internal static void ServeResource(CoreWebView2Environment environment, Func<string, string>? resolver, CoreWebView2WebResourceRequestedEventArgs e)
        {
            var sw = TraceResources ? System.Diagnostics.Stopwatch.StartNew() : null;
            try
            {
                if (resolver == null)
                    return;
                if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out var uri))
                    return;

                var filePath = resolver(uri.AbsolutePath);
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    // Browsers auto-request /favicon.ico; there is none, so answer with an empty
                    // 200 instead of letting it fail (gobchat.localhost does not resolve) and log a 404.
                    if (uri.AbsolutePath.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                        e.Response = environment.CreateWebResourceResponse(
                            new MemoryStream(), 200, "OK", "Content-Type: image/x-icon");
                    else
                        logger.Warn($"[res] UNRESOLVED -> network fallthrough: {uri.AbsolutePath}");
                    return;
                }

                var bytes = File.ReadAllBytes(filePath);
                var stream = new MemoryStream(bytes);
                // Content-Length is required: without it Chromium waits (~2s) for the response body to
                // "complete" before parsing the document, which stalled the whole UI load. (CEF's
                // scheme handler supplied the length, so it was instant.)
                var headers = $"Content-Type: {GuessContentType(filePath)}\r\nContent-Length: {bytes.Length}";
                e.Response = environment.CreateWebResourceResponse(stream, 200, "OK", headers);
                if (sw != null)
                {
                    sw.Stop();
                    logger.Info($"[res] {sw.ElapsedMilliseconds,4}ms {bytes.Length,8}B {uri.AbsolutePath}");
                }
            }
            catch (Exception ex)
            {
                logger.Warn(ex, $"Failed to serve resource {e.Request.Uri}");
            }
        }

        // window.open (the settings dialog) is backed by a second WebView2 on the same environment
        // and origin, so the page's window.opener sharing (GobchatAPI, gobConfig, Gobchat) keeps
        // working without per-window re-binding. It is hosted in a borderless, ctrl-movable window
        // (SettingsOverlayForm) — windowed hosting, so the config UI's native <select> popups work.
        private async void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            logger.Info(() => $"NewWindowRequested for '{e.Uri}'");
            var deferral = e.GetDeferral();
            try
            {
                var settings = new SettingsOverlayForm(_webview.Environment, ResourceResolver, SettingsFramePersister, SettingsFrameProvider);
                // Track the live settings window so the title-bar controls (minimize / always-on-top
                // pin) can act on it via the overlay bridge; clear the reference when it closes.
                _settingsForm = settings;
                settings.FormClosed += (s, args) =>
                {
                    if (ReferenceEquals(_settingsForm, settings))
                        _settingsForm = null;
                    SettingsWindowClosed?.Invoke(this, EventArgs.Empty);
                };
                settings.ApplyWindowFeatures(e.WindowFeatures);
                // Deliberately not Show()n here: the window starts hidden and reveals itself once the
                // config page has rendered (RevealSettings), so the user never sees an empty frame.
                await settings.InitializeAsync().ConfigureAwait(true);
                e.NewWindow = settings.CoreWebView2;
                e.Handled = true;
                logger.Info("NewWindowRequested: settings overlay opened");
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to open settings overlay");
            }
            finally
            {
                deferral.Complete();
            }
        }

        // Title-bar controls for the settings window opened above (no-op if none is open). Called on the
        // UI thread via BrowserAPIManager.RunSync.
        public void MinimizeSettings()
        {
            _settingsForm?.MinimizeToTaskbar();
        }

        public void SetSettingsAlwaysOnTop(bool value)
        {
            _settingsForm?.SetAlwaysOnTop(value);
        }

        // Reveal-when-ready: the config page calls this once it has finished rendering so the
        // (initially hidden) settings window appears already built, with no empty-frame flash.
        public void RevealSettings()
        {
            _settingsForm?.RevealNow();
        }

        // Second cog click: bring the already-open settings window to the foreground. Returns false
        // when no settings window is open, so the overlay knows to fall through and open a fresh one.
        public bool FocusSettings()
        {
            if (_settingsForm == null)
                return false;
            _settingsForm.FocusNow();
            return true;
        }

        private static string GuessContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".js": case ".mjs": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".html": case ".htm": return "text/html; charset=utf-8";
                case ".json": case ".hjson": return "application/json; charset=utf-8";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".gif": return "image/gif";
                case ".webp": return "image/webp";
                case ".woff": return "font/woff";
                case ".woff2": return "font/woff2";
                case ".ttf": return "font/ttf";
                case ".eot": return "application/vnd.ms-fontobject";
                case ".mp3": return "audio/mpeg";
                case ".ogg": return "audio/ogg";
                case ".wav": return "audio/wav";
                default: return "application/octet-stream";
            }
        }

        #endregion resource serving

        #region bridge (JS -> C#)

        public bool BindBrowserAPI(IBrowserAPI api, bool isApiAsync)
        {
            if (api == null)
                throw new ArgumentNullException(nameof(api));

            lock (_lock)
            {
                if (_apis.Any(a => a.APIName.Equals(api.APIName)))
                    return false;
                _apis.Add(api);
            }

            // The page-side facade is generic; one Proxy per bound API name.
            AddInitializationScript($"window[{ToJsString(api.APIName)}] = window.__gobchatBridge.makeApi({ToJsString(api.APIName)});");
            return true;
        }

        public bool UnbindBrowserAPI(IBrowserAPI api)
        {
            if (api == null)
                return false;

            lock (_lock)
            {
                if (_apis.RemoveAll(a => a.APIName.Equals(api.APIName) && a == api) == 0)
                    return false;
            }

            if (_initialized)
                ExecuteScript($"try {{ delete window[{ToJsString(api.APIName)}]; }} catch (e) {{}}");
            return true;
        }

        private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            JObject msg;
            try
            {
                msg = JObject.Parse(e.WebMessageAsJson);
            }
            catch (JsonException ex)
            {
                // A malformed bridge message can't be answered (no id to respond to), but it must not
                // disappear silently: without this the page's awaiting promise hangs forever and nothing
                // in the log explains the frozen UI.
                logger.Warn(ex, "Dropped malformed bridge message");
                return;
            }

            if (msg.Value<bool?>("__gobconsole") == true)
            {
                OnBrowserConsoleLog?.Invoke(this, new BrowserConsoleLogEventArgs(msg.Value<string>("message") ?? "", "", 0));
                return;
            }

            if (msg.Value<bool?>("__gobapi") != true)
                return;

            var id = msg["id"];
            object? result = null;
            string? error = null;
            try
            {
                result = await DispatchAsync(msg.Value<string>("api"), msg.Value<string>("method"), msg["args"] as JArray);
            }
            catch (Exception ex)
            {
                var baseEx = ex.GetBaseException();
                error = baseEx.Message;
                logger.Warn(baseEx, $"Bridge call {msg.Value<string>("api")}.{msg.Value<string>("method")} failed");
            }

            if (_disposed || _webview == null)
                return;

            try
            {
                var response = JsonConvert.SerializeObject(new BridgeResponse { id = id, result = result, error = error });
                _webview.PostWebMessageAsJson(response);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to post bridge response");
            }
        }

        private async Task<object?> DispatchAsync(string? apiName, string? methodName, JArray? args)
        {
            if (apiName == null || methodName == null)
                throw new ArgumentException("Malformed bridge request");

            IBrowserAPI? api;
            lock (_lock)
                api = _apis.FirstOrDefault(a => a.APIName.Equals(apiName));
            if (api == null)
                throw new MissingMemberException($"No bound API named '{apiName}'");

            args ??= new JArray();
            var method = FindMethod(api.GetType(), methodName, args.Count);
            if (method == null)
                throw new MissingMethodException($"{apiName}.{methodName}({args.Count} args)");

            var parameters = method.GetParameters();
            var callArgs = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; ++i)
            {
                if (i < args.Count && args[i].Type != JTokenType.Undefined)
                    callArgs[i] = args[i].ToObject(parameters[i].ParameterType, _argSerializer);
                else if (parameters[i].HasDefaultValue)
                    callArgs[i] = parameters[i].DefaultValue;
                else
                    // No arg supplied and no default: fail loudly instead of silently defaulting (which
                    // would e.g. turn attachToFFXIVProcess() into a connect to PID 0). The error is
                    // posted back to the awaiting promise.
                    throw new ArgumentException(
                        $"Bridge call {apiName}.{methodName} is missing required argument '{parameters[i].Name}' (position {i})");
            }

            var returned = method.Invoke(api, callArgs);
            if (returned is Task task)
            {
                await task.ConfigureAwait(true);
                var resultProperty = task.GetType().GetProperty("Result");
                if (resultProperty != null && resultProperty.PropertyType.Name != "VoidTaskResult")
                    return resultProperty.GetValue(task);
                return null;
            }

            return returned;
        }

        private static MethodInfo? FindMethod(Type type, string methodName, int argCount)
        {
            var candidates = type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 1)
                return candidates[0];

            // Disambiguate overloads by how many parameters the call can satisfy.
            return candidates.FirstOrDefault(m =>
            {
                var ps = m.GetParameters();
                var required = ps.Count(p => !p.HasDefaultValue);
                return argCount >= required && argCount <= ps.Length;
            }) ?? candidates.FirstOrDefault();
        }

        private static string ToJsString(string value)
        {
            return JsonConvert.ToString(value);
        }

        private sealed class BridgeResponse
        {
            public bool __gobapi => true;
            public JToken? id { get; set; }
            public object? result { get; set; }
            public string? error { get; set; }
        }

        #endregion bridge (JS -> C#)

        #region scripting (C# -> JS)

        public void ExecuteScript(string script)
        {
            if (!_initialized || string.IsNullOrEmpty(script))
                return;
            _ = _webview.ExecuteScriptAsync(script);
        }

        public async Task<IJavascriptResponse> EvaluateScript(string script, TimeSpan? timeout = null)
        {
            if (!_initialized)
                return new JavascriptResponse(false, null, "browser not initialized");
            try
            {
                var json = await _webview.ExecuteScriptAsync(script).ConfigureAwait(true);
                object? result = json == null ? null : JsonConvert.DeserializeObject(json);
                return new JavascriptResponse(true, result, null);
            }
            catch (Exception ex)
            {
                return new JavascriptResponse(false, null, ex.Message);
            }
        }

        #endregion scripting (C# -> JS)

        public void ShowDevTools()
        {
            if (_initialized)
                _webview.OpenDevToolsWindow();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            logger.Debug($"Disposing {nameof(ManagedWebBrowser)}");
            try
            {
                if (_webview != null)
                {
                    _webview.WebResourceRequested -= OnWebResourceRequested;
                    _webview.NavigationStarting -= OnNavigationStarting;
                    _webview.NavigationCompleted -= OnNavigationCompleted;
                    _webview.WebMessageReceived -= OnWebMessageReceived;
                    _webview.NewWindowRequested -= OnNewWindowRequested;
                }
                _controller?.Close();
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Error while disposing browser");
            }
            _controller = null!;
            _webview = null!;
        }
    }
}
