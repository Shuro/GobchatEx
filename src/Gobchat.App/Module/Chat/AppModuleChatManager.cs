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
using System.Linq;
using Gobchat.Core.Chat;
using Gobchat.Core.Config;
using Gobchat.Core.Runtime;
using Gobchat.Core.Util;
using System.Threading;
using Gobchat.Module.Chat.Internal;
using Newtonsoft.Json.Linq;
using Gobchat.Module.Actor;
using Gobchat.Module.MemoryReader;
using Gobchat.Module.Language;

namespace Gobchat.Module.Chat
{
    public sealed class AppModuleChatManager : IApplicationModule
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private IDIContext _container;
        private IConfigManager _configManager;
        private IMemoryReaderManager _memoryManager;
        private IActorManager _actorManager;
        private ChatManager _chatManager;

        private IndependentBackgroundWorker _updater;

        private long _updateInterval;

        // Effective mentions are the union of the global trigger words and the words derived from the
        // currently logged-in character (player mentions). They arrive from two different threads
        // (config dispatch vs. actor poll), so guard the combine with a lock.
        private readonly object _mentionLock = new object();
        private string[] _globalMentions = Array.Empty<string>();
        private string[] _playerMentions = Array.Empty<string>();
        // The active character's words to match as substrings instead of whole words (the "partial
        // first/last name" switches). Player-only; global trigger words always match whole-word.
        private string[] _playerPartialMentions = Array.Empty<string>();
        // The active character's words to also match fuzzily (typos), and how forgiving to be. Only
        // populated when that character has fuzzy matching turned on; empty means fuzzy is off.
        private string[] _fuzzyPlayerMentions = Array.Empty<string>();
        private FuzzyMatchLevel _fuzzyLevel = FuzzyMatchLevel.Conservative;

        /// <summary>
        ///
        /// Requires: <see cref="IGobchatConfig"/> <br></br>
        /// Requires: <see cref="IMemoryReaderManager"/> <br></br>
        /// Requires: <see cref="IActorManager"/> <br></br>
        /// Provides: <see cref="IChatManager"/> <br></br>
        /// <br></br>
        /// </summary>
        public AppModuleChatManager()
        {
        }

        public void Initialize(ApplicationStartupHandler handler, IDIContext container)
        {
            _container = container ?? throw new ArgumentNullException(nameof(container));
            _configManager = _container.Resolve<IConfigManager>();
            _memoryManager = _container.Resolve<IMemoryReaderManager>();
            _actorManager = _container.Resolve<IActorManager>();

            var resourceBundle = _container.Resolve<ILocaleManager>().GetResourceBundle("autotranslate");
            var autotranslateProvider = new AutotranslateProvider(resourceBundle);

            _chatManager = new ChatManager(autotranslateProvider, _actorManager);

            _configManager.AddPropertyChangeListener("behaviour.chat.updateInterval", true, true, ConfigManager_UpdateChatInterval);
            _configManager.AddPropertyChangeListener("behaviour.channel", true, true, ConfigManager_UpdateChannelProperties);
            _configManager.AddPropertyChangeListener("behaviour.segment", true, true, ConfigManager_UpdateFormaterProperties);
            _configManager.AddPropertyChangeListener("behaviour.groups", true, true, ConfigManager_UpdateTriggerGroupProperties);
            _configManager.AddPropertyChangeListener("behaviour.chat.autodetectEmoteInSay", true, true, ConfigManager_UpdateAutodetectProperties);
            _configManager.AddPropertyChangeListener("behaviour.chat.autodetectEmoteInParty", true, true, ConfigManager_UpdateAutodetectProperties);
            _configManager.AddPropertyChangeListener("behaviour.language", true, true, ConfigManager_UpdateLanguage);
            _configManager.AddPropertyChangeListener("behaviour.rangefilter", true, true, ConfigManager_UpdateRangeFilter);

            _configManager.AddPropertyChangeListener("behaviour.mentions.trigger", true, true, ConfigManager_UpdateMentions);
            _configManager.AddPropertyChangeListener("behaviour.mentions.userCanTriggerMention", true, true, ConfigManager_UpdateUserMentionProperties);
            _configManager.AddPropertyChangeListener("behaviour.mentions.player", true, true, ConfigManager_UpdatePlayerMentions);

            _actorManager.OnCurrentPlayerChanged += ActorManager_OnCurrentPlayerChanged;

            _configManager.AddPropertyChangeListener("behaviour.chattabs.data", true, true, ConfigManager_UpdateVisibleChannel);
            _configManager.AddPropertyChangeListener("behaviour.chattabs.data", true, true, ConfigManager_UpdateUpdateRangeFilterActive);

            _container.Register<IChatManager>((c, p) => _chatManager);

            _updater = new IndependentBackgroundWorker();
            _updater.Start(UpdateJob);
        }

        public void Dispose()
        {
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateUpdateRangeFilterActive);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateVisibleChannel);

            _actorManager.OnCurrentPlayerChanged -= ActorManager_OnCurrentPlayerChanged;

            _configManager.RemovePropertyChangeListener(ConfigManager_UpdatePlayerMentions);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateUserMentionProperties);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateRangeFilter);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateLanguage);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateAutodetectProperties);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateMentions);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateTriggerGroupProperties);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateFormaterProperties);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateChannelProperties);
            _configManager.RemovePropertyChangeListener(ConfigManager_UpdateChatInterval);
            
            _updater.Dispose();

            _updater = null;
            _chatManager = null;
            _container = null;
            _configManager = null;
            _memoryManager = null;
            _actorManager = null;
        }

        private void UpdateJob(CancellationToken cancellationToken)
        {
            //TODO some start up logging
            try
            {
                var timer = new System.Diagnostics.Stopwatch();
                while (!cancellationToken.IsCancellationRequested)
                {
                    timer.Restart();

                    UpdateChatManager();

                    timer.Stop();
                    var timeSpend = timer.Elapsed;

                    if (cancellationToken.IsCancellationRequested)
                        break;

                    int waitTime = (int)Math.Max(0, _updateInterval - timeSpend.Milliseconds);
                    if (waitTime > 0)
                        Thread.Sleep(waitTime);
                }
            }
            finally
            {
                logger.Info("Chat updates concluded");
            }
        }

        private void UpdateChatManager()
        {
            try
            {
                if (_memoryManager.IsConnected)
                {
                    var chatlogs = _memoryManager.GetNewestChatlog();
                    foreach (var chatlog in chatlogs)
                        _chatManager.EnqueueMessage(chatlog);
                }

                _chatManager.UpdateManager();
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateChatInterval(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _updateInterval = config.GetProperty<long>("behaviour.chat.updateInterval");
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateChannelProperties(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatManager.Config.FormatChannels = config.GetProperty<List<long>>("behaviour.channel.roleplay").Select(i => (ChatChannel)i).ToArray();
                _chatManager.Config.MentionChannels = config.GetProperty<List<long>>("behaviour.channel.mention").Select(i => (ChatChannel)i).ToArray();
                _chatManager.Config.CutOffChannels = config.GetProperty<List<long>>("behaviour.channel.rangefilter").Select(i => (ChatChannel)i).ToArray();
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateVisibleChannel(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var jTabs = GetVisibleChatTabs(config);
                var visibleChannels = jTabs
                    .Select(tab => tab["channel"]["visible"].ToObject<List<long>>())
                    .Select(channel => channel.Select(i => (ChatChannel)i))
                    .SelectMany(channel => channel)
                    .ToArray();

                _chatManager.Config.VisibleChannels = visibleChannels;
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateUpdateRangeFilterActive(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatManager.Config.EnableCutOff = RangeFilterConfig.IsActiveForVisibleTabs(config);
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private static List<JToken> GetVisibleChatTabs(IConfigManager config)
        {
            return config.GetProperty<JObject>("behaviour.chattabs.data")
               .Properties()
               .Select(p => p.Value)
               .Where(tab => tab.Value<bool>("visible"))
               .ToList();
        }

        private void ConfigManager_UpdateAutodetectProperties(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatManager.Config.DetectEmoteInSayChannel = config.GetProperty<bool>("behaviour.chat.autodetectEmoteInSay");
                _chatManager.Config.DetectEmoteInPartyChannel = config.GetProperty<bool>("behaviour.chat.autodetectEmoteInParty");
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateUserMentionProperties(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatManager.Config.ExcludeUserMention = !config.GetProperty<bool>("behaviour.mentions.userCanTriggerMention");
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateFormaterProperties(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var ids = config.GetProperty<List<string>>("behaviour.segment.order");
                var list = config.GetProperty<JToken>("behaviour.segment.data");
                var newValues = new List<FormatConfig>();
                foreach (var id in ids)
                {
                    var data = list[id];
                    var format = data.ToObject<FormatConfig>();
                    newValues.Add(format);
                }
                _chatManager.Config.Formats = newValues.ToArray();
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateTriggerGroupProperties(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var ids = config.GetProperty<List<string>>("behaviour.groups.sorting");
                var list = config.GetProperty<JToken>("behaviour.groups.data");
                var newValues = new List<TriggerGroup>();
                foreach (var id in ids)
                {
                    var data = list[id];
                    var format = data.ToObject<TriggerGroup>();
                    newValues.Add(format);
                }
                _chatManager.Config.TriggerGroups = newValues.ToArray();
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateMentions(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var triggers = config.GetProperty<List<string>>("behaviour.mentions.trigger");
                lock (_mentionLock)
                {
                    _globalMentions = triggers.ToArray();
                    ApplyEffectiveMentions();
                }
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdatePlayerMentions(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                RecomputePlayerMentions(config, _actorManager?.GetActivePlayerName());
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ActorManager_OnCurrentPlayerChanged(object sender, CurrentPlayerChangedEventArgs e)
        {
            try
            {
                var playerName = e.CurrentPlayerName;
                if (!string.IsNullOrWhiteSpace(playerName))
                    RememberCharacter(playerName);
                RecomputePlayerMentions(_configManager, playerName);
            }
            catch (Exception e1)
            {
                // Runs on the actor poll thread; a player-mention hiccup must not kill polling.
                logger.Error(e1);
            }
        }

        // Adds a freshly logged-in character to the profile so it shows up in the Player Mentions list.
        // No-op when a character with the same name (case-insensitive) is already remembered.
        private void RememberCharacter(string playerName)
        {
            var name = playerName.Trim();
            if (name.Length == 0)
                return;

            var data = _configManager.GetProperty<JObject>("behaviour.mentions.player.data", null);
            if (data != null)
            {
                foreach (var property in data.Properties())
                {
                    var existing = property.Value?["name"]?.ToObject<string>();
                    if (existing != null && string.Equals(existing, name, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            var template = _configManager.GetProperty<JObject>("behaviour.mentions.player.data-template", null);
            var entry = template != null ? (JObject)template.DeepClone() : new JObject();
            entry["name"] = name;
            // Auto-remembered characters are always added disabled, regardless of the template default
            // (older profiles seeded it as active before this was changed). They only start mentioning
            // once the user activates them in settings.
            entry["active"] = false;

            var existingKeys = data != null ? data.Properties().Select(p => p.Name) : Enumerable.Empty<string>();
            var id = GeneratePlayerId(existingKeys);
            _configManager.SetProperty($"behaviour.mentions.player.data.{id}", entry);

            var sorting = _configManager.GetProperty<List<string>>("behaviour.mentions.player.sorting", new List<string>());
            sorting.Add(id);
            _configManager.SetProperty("behaviour.mentions.player.sorting", sorting);

            // SetProperty only queues change events; flush them now (as every config write does) so the
            // new character actually reaches the UI — the "*" listener dispatches a config-sync to the
            // overlay, and the settings window snapshots the overlay config when it opens.
            _configManager.DispatchChangeEvents();

            logger.Debug(() => $"Remembered new character for player mentions: {name} ({id})");
        }

        private void RecomputePlayerMentions(IConfigManager config, string playerName)
        {
            var words = Array.Empty<string>();
            var partialWords = Array.Empty<string>();
            var fuzzyWords = Array.Empty<string>();
            var fuzzyLevel = FuzzyMatchLevel.Conservative;

            var enabled = config.GetProperty<bool>("behaviour.mentions.player.enabled", false);
            if (enabled && !string.IsNullOrWhiteSpace(playerName))
            {
                var entry = FindPlayerEntry(config, playerName);
                if (entry != null && (entry["active"]?.ToObject<bool>() ?? false))
                {
                    var custom = entry["mentions"]?.ToObject<List<string>>() ?? new List<string>();
                    var resolved = PlayerMentionResolver.ResolveWords(
                        playerName,
                        entry["matchFullName"]?.ToObject<bool>() ?? false,
                        entry["matchFirstName"]?.ToObject<bool>() ?? false,
                        entry["matchLastName"]?.ToObject<bool>() ?? false,
                        entry["matchFirstNamePartial"]?.ToObject<bool>() ?? false,
                        entry["matchLastNamePartial"]?.ToObject<bool>() ?? false,
                        entry["matchMiqote"]?.ToObject<bool>() ?? false,
                        custom);
                    words = resolved.WholeWords.ToArray();
                    partialWords = resolved.PartialWords.ToArray();

                    // Fuzzy matching covers every name the character wants matched (whole-word and
                    // partial alike, see PlayerMentionResolver.FuzzyCandidates), so exact hits stay exact
                    // and only near-misses (typos) are added on top.
                    if (entry["matchFuzzy"]?.ToObject<bool>() ?? false)
                    {
                        fuzzyWords = PlayerMentionResolver.FuzzyCandidates(resolved).ToArray();
                        fuzzyLevel = StringSimilarity.ParseLevel(entry["fuzzyLevel"]?.ToObject<string>());
                    }
                }
            }

            lock (_mentionLock)
            {
                _playerMentions = words;
                _playerPartialMentions = partialWords;
                _fuzzyPlayerMentions = fuzzyWords;
                _fuzzyLevel = fuzzyLevel;
                ApplyEffectiveMentions();
            }
        }

        private static JObject FindPlayerEntry(IConfigManager config, string playerName)
        {
            var data = config.GetProperty<JObject>("behaviour.mentions.player.data", null);
            if (data == null)
                return null;

            foreach (var property in data.Properties())
            {
                if (property.Value is JObject entry)
                {
                    var name = entry["name"]?.ToObject<string>();
                    if (name != null && string.Equals(name, playerName, StringComparison.OrdinalIgnoreCase))
                        return entry;
                }
            }
            return null;
        }

        private static string GeneratePlayerId(IEnumerable<string> existingKeys)
        {
            var keys = new HashSet<string>(existingKeys ?? Enumerable.Empty<string>());
            string id;
            do
            {
                id = "char-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            } while (keys.Contains(id));
            return id;
        }

        // Caller must hold _mentionLock.
        private void ApplyEffectiveMentions()
        {
            var effective = _globalMentions
                .Concat(_playerMentions)
                .Where(w => !string.IsNullOrEmpty(w))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            _chatManager.Config.Mentions = effective;
            _chatManager.Config.PartialMentions = _playerPartialMentions;
            _chatManager.Config.FuzzyMentions = _fuzzyPlayerMentions;
            _chatManager.Config.FuzzyMentionLevel = _fuzzyLevel;
            logger.Debug(() => $"Set effective mentions to: {string.Join(", ", effective)} (partial: {string.Join(", ", _playerPartialMentions)}) (fuzzy [{_fuzzyLevel}]: {string.Join(", ", _fuzzyPlayerMentions)})");
        }

        private void ConfigManager_UpdateLanguage(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                var selectedLanguage = config.GetProperty<string>("behaviour.language");
                var autotranslateProvider = _chatManager.Config.AutotranslateProvider as AutotranslateProvider;
                autotranslateProvider?.SetLocale(selectedLanguage);
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }

        private void ConfigManager_UpdateRangeFilter(IConfigManager config, ProfilePropertyChangedCollectionEventArgs evt)
        {
            try
            {
                _chatManager.Config.CutOffDistance = config.GetProperty<long>("behaviour.rangefilter.cutoff");
                _chatManager.Config.FadeOutDistance = config.GetProperty<long>("behaviour.rangefilter.fadeout");
            }
            catch (Exception e1)
            {
                logger.Error(e1);
                throw;
            }
        }
    }
}