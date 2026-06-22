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
using System.Globalization;
using System.Linq;
using Gobchat.Core.Chat;
using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Core.Util;
using Gobchat.Module.Actor;
using Gobchat.Module.Chat.Command;
using Gobchat.Module.UI;

namespace Gobchat.Module.Chat
{
    /// <summary>
    /// Detects and runs <c>/e gc</c> chat commands C#-side. Replaces the former TypeScript command system:
    /// it observes <see cref="IChatManager.OnChatMessage"/>, rebuilds each ECHO line's text from its
    /// segments, and hands it to <see cref="ChatCommandManager"/>. Data/lifecycle/config commands execute
    /// here (their state already lives in C#); the few genuinely UI-side ones are forwarded to the overlay
    /// via <see cref="ExecuteUiCommandWebEvent"/>.
    ///
    /// Requires: <see cref="IChatManager"/> <br></br>
    /// Requires: <see cref="IActorManager"/> <br></br>
    /// Requires: <see cref="IConfigManager"/> <br></br>
    /// Requires: <see cref="IBrowserAPIManager"/> <br></br>
    /// </summary>
    public sealed class AppModuleChatCommandManager : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IDIContext _container = null!;
        private IChatManager _chatManager = null!;
        private IConfigManager _configManager = null!;
        private ChatCommandManager _commandManager = null!;

        public AppModuleChatCommandManager()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _chatManager = _container.Resolve<IChatManager>();
            _configManager = _container.Resolve<IConfigManager>();
            var actorManager = _container.Resolve<IActorManager>();
            var browserAPIManager = _container.Resolve<IBrowserAPIManager>();

            var context = new ChatCommandContext(
                actorManager,
                _configManager,
                Localize,
                (type, message) => _chatManager.EnqueueMessage(type, message),
                Close,
                command => browserAPIManager.DispatchEventToBrowser(new ExecuteUiCommandWebEvent(command)));
            _commandManager = new ChatCommandManager(context);

            _chatManager.OnChatMessage += ChatManager_OnChatMessage;
        }

        public void Dispose()
        {
            _chatManager.OnChatMessage -= ChatManager_OnChatMessage;

            _commandManager = null!;
            _chatManager = null!;
            _configManager = null!;
            _container = null!;
        }

        // Runs on the chat worker thread. A command only reads in-memory actor/config state and enqueues a
        // reply (which is dispatched on the next poll, exactly like the old JS->bridge round-trip), so it is
        // safe and non-blocking here. A malformed command must never kill chat polling, so each line is
        // guarded independently.
        private void ChatManager_OnChatMessage(object? sender, ChatMessageEventArgs e)
        {
            foreach (var message in e.Messages)
            {
                if (message.Channel != ChatChannel.Echo)
                    continue;

                try
                {
                    // Mirror the old JS `content.map(e => e.text).join()` exactly, including JS Array.join()'s
                    // default "," separator, so a multi-segment echo rebuilds to the text the JS saw.
                    var text = string.Join(",", message.Content.Select(segment => segment.Text));
                    _commandManager.Process(text);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Error while processing chat command");
                }
            }
        }

        private static void Close()
        {
            logger.Info("User requests shutdown via chat command");
            GobchatApplicationContext.ExitGobchat();
        }

        // Resolves a WebUIResources string in the user's configured language (the same resx + lookup the
        // bridge uses for the page). Falls back to the invariant culture and finally to the "missing key"
        // placeholder, so a command always replies with something readable.
        private string Localize(string id)
        {
            CultureInfo culture;
            try
            {
                var locale = _configManager.GetProperty<string>("behaviour.language", "en");
                culture = CultureInfo.GetCultureInfo(locale);
            }
            catch (Exception)
            {
                culture = CultureInfo.InvariantCulture;
            }

            var text = WebUIResources.ResourceManager.GetString(id, culture);
            return text ?? StringFormat.Format(WebUIResources.localization_key_missing, id);
        }
    }
}
