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

using Gobchat.Core.Chat;
using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Module.Actor;
using Gobchat.Module.Chat;
using Gobchat.Module.Misc.Chatlogger.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gobchat.Module.Misc.Chatlogger
{
    public sealed class AppModuleChatLogger : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IDIContext _container = null!;
        private IConfigManager _configManager = null!;

        private CustomChatLogger _chatLogger = null!;
        private IChatManager _chatManager = null!;
        private IActorManager _actorManager = null!;

        /// <summary>
        ///
        /// Requires: <see cref="IConfigManager"/> <br></br>
        /// Requires: <see cref="IChatManager"/> <br></br>
        /// Requires: <see cref="IActorManager"/> <br></br>
        /// <br></br>
        /// </summary>
        public AppModuleChatLogger()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));

            _chatLogger = new CustomChatLogger();

            _configManager = _container.Resolve<IConfigManager>();
            _configManager.AddPropertyChangeListener("behaviour.chatlog.active", true, true, ConfigManager_UpdateWriteLog);
            _configManager.AddPropertyChangeListener("behaviour.chatlog.path", true, true, ConfigManager_UpdateLogPath);
            _configManager.AddPropertyChangeListener("behaviour.chatlog.format", true, true, ConfigManager_UpdateLogFormat);
            _configManager.AddPropertyChangeListener("behaviour.chatlog.characterfolders", true, true, ConfigManager_UpdateCharacterFolders);
            _configManager.AddPropertyChangeListener("behaviour.channel.log", true, true, ConfigManager_UpdateLogChannels);

            _chatManager = _container.Resolve<IChatManager>();
            _chatManager.OnChatMessage += ChatManager_ChatMessageEvent;

            // Gate logging on the character session: each login/switch starts a new file, logout pauses.
            _actorManager = _container.Resolve<IActorManager>();
            _actorManager.OnCurrentPlayerChanged += ActorManager_OnCurrentPlayerChanged;
            _chatLogger.SetCurrentCharacter(_actorManager.GetActivePlayerName()); // seed if already logged in
        }

        public void Dispose()
        {
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateWriteLog);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateLogPath);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateLogChannels);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateLogFormat);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateCharacterFolders);
            _configManager = null!;

            _chatManager.OnChatMessage -= ChatManager_ChatMessageEvent;
            _chatManager = null!;

            _actorManager.OnCurrentPlayerChanged -= ActorManager_OnCurrentPlayerChanged;
            _actorManager = null!;

            _chatLogger?.Dispose();
            _chatLogger = null!;

            _container = null!;
        }

        private void ActorManager_OnCurrentPlayerChanged(object? sender, CurrentPlayerChangedEventArgs e)
        {
            try
            {
                _chatLogger.SetCurrentCharacter(e.CurrentPlayerName);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void ConfigManager_UpdateWriteLog(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatLogger.Active = sender.GetProperty<bool>("behaviour.chatlog.active");
            }
            catch (Exception ex)
            {
                logger.Warn(ex);
            }
        }

        private void ConfigManager_UpdateLogPath(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var path = sender.GetProperty<string>("behaviour.chatlog.path");

                if (path == null || path.Length == 0)
                    path = Path.Combine(GobchatContext.AppDataLocation, "log");

                if (!Path.IsPathRooted(path))
                    path = Path.Combine(GobchatContext.AppDataLocation, path);

                _chatLogger.SetLogFolder(path);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void ConfigManager_UpdateLogFormat(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatLogger.SetLogFormat(sender.GetProperty<string>("behaviour.chatlog.format"));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void ConfigManager_UpdateCharacterFolders(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatLogger.SetUseCharacterFolders(sender.GetProperty<bool>("behaviour.chatlog.characterfolders"));
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void ConfigManager_UpdateLogChannels(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var logChannels = Enum.GetValues(typeof(ChatChannel)).Cast<ChatChannel>();
                var excludeFromLogging = sender.GetProperty<List<long>>("behaviour.channel.log").Select(i => (ChatChannel)i);
                //the profile defines which channels shouldn't be logged
                _chatLogger.LogChannels = logChannels.Except(excludeFromLogging);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }

        private void ChatManager_ChatMessageEvent(object? sender, ChatMessageEventArgs e)
        {
            foreach (var message in e.Messages)
            {
                try
                {
                    _chatLogger.Log(message);
                }
                catch (Exception ex)
                {
                    logger.Fatal(ex);
                }
            }

            try
            {
                _chatLogger.Flush();
            }
            catch (Exception ex)
            {
                logger.Fatal(ex);
            }
        }
    }
}