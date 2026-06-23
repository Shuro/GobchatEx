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

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using NLog;

namespace Gobchat.Core.Config
{
    public sealed class GobchatConfigManager : IConfigManager
    {
        private readonly static Logger logger = LogManager.GetCurrentClassLogger();

        private readonly object _synchronizationLock = new object();

        private readonly string _defaultProfilePath;
        private IGobchatConfigProfile _globalProfile = null!; // set by InitializeManager->LoadGlobalProfile

        // Application-global preferences live outside any profile, in a separate appsettings.json that
        // applies instantly (no profile Save). _appConfig holds the user's values and falls back to
        // _appDefaults (bundled default_appsettings.json). These roots are routed to _appConfig on read
        // and stripped from every profile so a profile can never shadow them. A *leaf* root
        // (behaviour.chat.updateInterval) keeps its sibling (autodetectEmoteInSay) in the profile.
        private GobchatConfigProfile _appDefaults = null!; // set by InitializeManager->LoadAppSettings
        private GobchatConfigProfile _appConfig = null!; // set by InitializeManager->LoadAppSettings
        private bool _appSettingsExisted;
        // The user's stored app-settings JSON (without the bundled-defaults fallback). Used by the
        // migration to tell whether a root is genuinely already migrated vs. only present via defaults -
        // _appConfig.HasProperty would falsely report the latter as present.
        private JObject _storedAppSettings = new JObject();

        private static readonly string[] AppSettingRoots =
        {
            "behaviour.language",
            "behaviour.hideOnMinimize",
            "behaviour.appUpdate",
            "behaviour.actor",
            "behaviour.hotkeys",
            "behaviour.chat.updateInterval",
            "behaviour.frame.chat.pinned",
            "style.theme",
        };

        private readonly Dictionary<string, IGobchatConfigProfile> _profiles = new Dictionary<string, IGobchatConfigProfile>();
        private readonly ISet<string> _changedProfiles = new HashSet<string>();
        private readonly ISet<string> _deletedProfiles = new HashSet<string>();

        private GobchatConfigProfile _defaultConfig = null!; // set by InitializeManager->LoadDefaultProfile

        private string _activeProfileId = null!; // set by InitializeManager->LoadAppConfig (always non-null post-init)

        private readonly Dictionary<string, IList<PropertyChangedListener>> _allPropertyChangedListener = new Dictionary<string, IList<PropertyChangedListener>>();
        private readonly Dictionary<string, IList<PropertyChangedListener>> _activePropertyChangedListener = new Dictionary<string, IList<PropertyChangedListener>>();
        private readonly Dictionary<string, ISet<string>> _pendingPropertyChanges = new Dictionary<string, ISet<string>>();

        #region public properties

        public string ConfigFolderPath { get; set; }

        public IGobchatConfigProfile ActiveProfile => GetProfile(ActiveProfileId);

        public IList<string> Profiles => _profiles.Keys.ToArray();

        public string ActiveProfileId
        {
            get => _activeProfileId;
            set
            {
                if (value == null || value.Length == 0)
                    throw new ArgumentNullException(nameof(ActiveProfileId));
                if (!_profiles.ContainsKey(value))
                    throw new InvalidProfileIdException(value);
                if (_activeProfileId == value)
                    return;

                string oldProfileId;
                lock (_synchronizationLock)
                {
                    oldProfileId = _activeProfileId;
                    _activeProfileId = value;
                }

                OnActiveProfileChange?.Invoke(this, new ActiveProfileChangedEventArgs(oldProfileId, _activeProfileId, false));
            }
        }

        public IGobchatConfigProfile DefaultProfile => _defaultConfig;

        #endregion public properties

        public GobchatConfigManager(string defaultProfilePath, string configFolderPath)
        {
            _defaultProfilePath = defaultProfilePath ?? throw new ArgumentNullException(nameof(defaultProfilePath));
            ConfigFolderPath = configFolderPath ?? throw new ArgumentNullException(nameof(configFolderPath));

            OnActiveProfileChange += UpdateActiveListenerOnActiveProfileChange;
        }

        #region load

        public void InitializeManager()
        {
            LoadDefaultProfile();
            LoadGlobalProfile();
            LoadAppSettings();
            LoadUserProfiles();
            LoadAppConfig();
            MigrateAppSettings();
            logger.Info("Config manager loaded");
        }

        private static JsonConfigLoader GetProfileLoader()
        {
            var loader = new JsonConfigLoader();
            loader.AddFunction(new JsonValidateIsProfile());
            loader.AddFunction(new JsonConfigUpgrader(new ConfigUpgrader()));
            loader.AddFunction(new JsonValueToEnum());
            return loader;
        }

        private void LoadDefaultProfile()
        {
            var loader = new JsonConfigLoader();
            loader.AddFunction(new JsonValueToEnum());
            var defaultConfig = loader.LoadConfig(_defaultProfilePath);
            defaultConfig["profile"]!["id"] = null;
            _defaultConfig = new GobchatConfigProfile(defaultConfig, false);
        }

        private void LoadGlobalProfile()
        {
            _globalProfile = new GobchatConfigProfile(new JObject(), true, _defaultConfig);
        }

        private void LoadUserProfiles()
        {
            var userProfileFolderPath = Path.Combine(ConfigFolderPath, "profiles");
            if (!Directory.Exists(userProfileFolderPath))
                return;

            var userProfileFiles = Directory.EnumerateFiles(userProfileFolderPath, "profile_*.json", SearchOption.TopDirectoryOnly);

            foreach (var userProfileFile in userProfileFiles)
            {
                var userProfile = ParseProfile(userProfileFile);
                if (userProfile != null)
                    StoreNewProfile(userProfile, false, false);
            }
        }

        private string EnsureProfileId(JObject profile)
        {
            var profileId = profile["profile"]!["id"]!.Value<string>();
            if (profileId == null || profileId.Length == 0 || _profiles.ContainsKey(profileId))
            {
                profileId = GenerateProfileId();
                profile["profile"]!["id"] = profileId;
            }
            return profileId;
        }

        public JObject? ParseProfile(string path)
        {
            var loader = GetProfileLoader();

            JObject userProfile;
            try
            {
                userProfile = loader.LoadConfig(path);
                EnsureProfileId(userProfile);
                return userProfile;
            }
            catch (Exception ex)
            {
                logger.Error(ex, $"Invalid profile {path}");
                return null;
            }
        }

        private void LoadAppConfig()
        {
            var appConfigPath = Path.Combine(ConfigFolderPath, "gobconfig.json");

            string? activeProfile;
            if (File.Exists(appConfigPath))
            {
                var loader = new JsonConfigLoader();
                var appConfig = loader.LoadConfig(appConfigPath);

                if (appConfig["activeProfile"] != null)
                {
                    activeProfile = appConfig["activeProfile"]!.Value<string>();
                    if (activeProfile != null && _profiles.ContainsKey(activeProfile))
                    {
                        ActiveProfileId = activeProfile;
                        return;
                    }
                }
            }

            if (ActiveProfileId != null)
                return;

            activeProfile = _profiles.Keys.FirstOrDefault();
            if (activeProfile != null)
            {
                ActiveProfileId = activeProfile;
                return;
            }
            ActiveProfileId = CreateNewProfile();
        }

        private void LoadAppSettings()
        {
            var defaultAppSettingsPath = Path.Combine(Path.GetDirectoryName(_defaultProfilePath)!, "default_appsettings.json");
            JObject defaults;
            try
            {
                defaults = new JsonConfigLoader().LoadConfig(defaultAppSettingsPath);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to load default app settings; using empty defaults");
                defaults = new JObject();
            }
            _appDefaults = new GobchatConfigProfile(defaults, false);

            var appSettingsPath = Path.Combine(ConfigFolderPath, "appsettings.json");
            JObject stored = new JObject();
            _appSettingsExisted = File.Exists(appSettingsPath);
            if (_appSettingsExisted)
            {
                try
                {
                    stored = new JsonConfigLoader().LoadConfig(appSettingsPath);
                }
                catch (Exception ex)
                {
                    logger.Error(ex, "Failed to load app settings; starting from defaults");
                    stored = new JObject();
                    _appSettingsExisted = false;
                }
            }
            _appConfig = new GobchatConfigProfile(stored, true, _appDefaults);
            _storedAppSettings = stored;
        }

        private void SaveAppSettings()
        {
            var appSettingsPath = Path.Combine(ConfigFolderPath, "appsettings.json");
            Directory.CreateDirectory(ConfigFolderPath);

            var json = _appConfig.ToJson();
            var tempPath = appSettingsPath + ".tmp";
            try
            {
                // Write to a temp file and atomically move it into place: a crash mid-write can't then
                // leave a truncated appsettings.json, which (being the only copy after migration) would
                // silently reset the user's app-global settings to defaults on the next start.
                using (StreamWriter file = File.CreateText(tempPath))
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    writer.Formatting = Formatting.Indented;
                    json.WriteTo(writer);
                }
                File.Move(tempPath, appSettingsPath, true);
                _appSettingsExisted = true;
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Failed to save app settings");
                try { if (File.Exists(tempPath)) File.Delete(tempPath); }
                catch (Exception cleanupEx) { logger.Warn(cleanupEx, "Failed to clean up temp app settings file"); }
            }
        }

        // One-time migration to the app-settings schema: lift the active profile's current values into the
        // app store (so users keep their language/theme/hotkey/etc.), then strip the app-setting roots from
        // every profile so a profile can never shadow the app store. Idempotent on later runs.
        private void MigrateAppSettings()
        {
            // Lift any app-setting root not yet present in the app store from the active profile (the
            // source of the user's current value). This covers the first migration (no appsettings.json)
            // and, just as importantly, a later version adding a new root to AppSettingRoots: in both
            // cases the value still lives in the profile and would be silently lost when stripped below.
            // Roots already in the app store are left untouched, so this stays idempotent.
            var active = ActiveProfile;
            var liftedAny = false;
            foreach (var root in AppSettingRoots)
            {
                // Check the user's stored settings only (not the defaults fallback): a root present solely
                // via _appDefaults still needs lifting from the profile to keep the user's real value.
                if (_storedAppSettings.SelectToken(root) != null)
                    continue;
                var value = active.GetProperty<JToken>(root, null!);
                if (value != null)
                {
                    _appConfig.SetProperty(root, value.DeepClone()); // clone: don't reparent the profile's live node
                    liftedAny = true;
                }
            }
            if (liftedAny || !_appSettingsExisted)
                SaveAppSettings();

            // Only strip the migrated roots from the profiles once the app store is confirmed on disk;
            // if the save failed, leave them in place so they remain the recoverable source next start.
            if (!_appSettingsExisted)
                return;

            var strippedAny = false;
            foreach (var profile in _profiles.Values)
            {
                foreach (var root in AppSettingRoots)
                {
                    if (profile.HasProperty(root))
                    {
                        profile.DeleteProperty(root);
                        strippedAny = true;
                    }
                }
            }

            if (strippedAny)
            {
                // Drop the change events the strip queued (no listeners exist this early) so a later
                // Synchronize doesn't trip its pending-changes guard, then persist the cleaned profiles.
                GetPendingEvents();
                SaveUserProfiles();
            }
        }

        #endregion load

        #region save

        public void SaveProfiles()
        {
            SaveUserProfiles();
            SaveAppConfig();
            SaveAppSettings();

            logger.Info("Config manager saved");
        }

        private void SaveUserProfiles()
        {
            var outputFolder = Path.Combine(ConfigFolderPath, "profiles");
            Directory.CreateDirectory(outputFolder);

            var finalizer = new JsonEnumToString();

            ISet<string>? changedProfiles = null;
            lock (_synchronizationLock)
            {
                changedProfiles = new HashSet<string>(_changedProfiles);
                _changedProfiles.Clear();
            }

            foreach (var profile in _profiles.Values)
            {
                if (!changedProfiles.Contains(profile.ProfileId))
                    continue;

                var profilePath = Path.Combine(outputFolder, $"profile_{profile.ProfileId}.json");

                var json = profile.ToJson();
                json = finalizer.Apply(json);
                json.Remove("appdata"); //don't save those

                try
                {
                    using (StreamWriter file = File.CreateText(profilePath))
                    using (JsonTextWriter writer = new JsonTextWriter(file))
                    {
                        writer.Formatting = Formatting.Indented;
                        json.WriteTo(writer);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.Fatal(ex);
                    lock (_synchronizationLock)
                    {
                        _changedProfiles.Add(profile.ProfileId); // in case of an error write the copy back
                    }
                }
            }

            foreach (var profileId in _deletedProfiles)
            {
                if (_profiles.ContainsKey(profileId))
                    continue;

                try
                {
                    var file = Path.Combine(outputFolder, $"profile_{profileId}.json");
                    if (File.Exists(file))
                        File.Delete(file);
                }
                catch (UnauthorizedAccessException ex)
                {
                    logger.Warn(ex);
                }
            }
            _deletedProfiles.Clear();
        }

        private void SaveAppConfig()
        {
            var appConfigPath = Path.Combine(ConfigFolderPath, "gobconfig.json");
            Directory.CreateDirectory(ConfigFolderPath);

            var appConfigJson = new JObject
            {
                ["version"] = 2,
                ["activeProfile"] = ActiveProfileId
            };

            try
            {
                using (StreamWriter file = File.CreateText(appConfigPath))
                using (JsonTextWriter writer = new JsonTextWriter(file))
                {
                    writer.Formatting = Formatting.Indented;
                    appConfigJson.WriteTo(writer);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                logger.Fatal(ex);
            }
        }

        #endregion save

        #region profile managment

        private string GenerateProfileId()
        {
            return Util.IdGenerator.GenerateNewId(8, _profiles.Keys);
        }

        public IGobchatConfigProfile GetProfile(string profileId)
        {
            if (_profiles.TryGetValue(profileId, out var profile))
                return profile;
            throw new InvalidProfileIdException(profileId);
        }

        public void DeleteProfile(string profileId)
        {
            DeleteProfile(profileId, false);
        }

        private void DeleteProfile(string profileId, bool synchronizing)
        {
            if (!_profiles.ContainsKey(profileId))
                return;

            if (_profiles.Count == 1)
                throw new ConfigException("Unable to delete last profile");

            var config = _profiles[profileId];
            config.OnPropertyChange -= OnEvent_Config_OnPropertyChange;

            _profiles.Remove(profileId);
            _deletedProfiles.Add(profileId);

            if (ActiveProfileId == profileId)
                ActiveProfileId = _profiles.Keys.First();

            OnProfileChange?.Invoke(this, new ProfileChangedEventArgs(profileId, ProfileChangedEventArgs.Type.Delete, synchronizing));
        }

        public string CreateNewProfile()
        {
            var profileId = GenerateProfileId();

            var newConfig = new JObject
            {
                ["version"] = _defaultConfig.ProfileVersion,
                ["profile"] = new JObject
                {
                    ["id"] = profileId,
                    ["name"] = $"Profile {this.Profiles.Count + 1}"
                }
            };

            StoreNewProfile(newConfig, false, true);
            return profileId;
        }

        private void StoreNewProfile(JObject profile, bool synchronizing, bool save)
        {
            var config = new GobchatConfigProfile(profile, true, _globalProfile);

            var configVersion = config.ProfileVersion;
            var defaultVersion = _defaultConfig.ProfileVersion;
            if (configVersion < defaultVersion)
            {
                logger.Warn($"Profile {config.ProfileId} with version {configVersion} is outdated. Expected version is {defaultVersion}. Profile not stored.");
                return;
            }
            else if (configVersion > defaultVersion)
            {
                logger.Warn($"Profile {config.ProfileId} with version {configVersion} is too new. Expected version is {defaultVersion}. Profile not stored.");
                return;
            }

            config.OnPropertyChange += OnEvent_Config_OnPropertyChange;
            _profiles.Add(config.ProfileId, config);
            _deletedProfiles.Remove(config.ProfileId);
            if (save)
                _changedProfiles.Add(config.ProfileId);
            OnProfileChange?.Invoke(this, new ProfileChangedEventArgs(config.ProfileId, ProfileChangedEventArgs.Type.New, synchronizing));
        }

        public void CopyProfile(string srcProfileId, string dstProfileId)
        {
            var srcProfile = GetProfile(srcProfileId);
            var dstProfile = GetProfile(dstProfileId);
            dstProfile.SetProperties(srcProfile.ToJson());
        }

        #endregion profile managment

        public JObject AsJson()
        {
            var root = new JObject
            {
                ["activeProfile"] = this.ActiveProfileId,
                ["profiles"] = new JObject()
            };

            var profiles = this.Profiles;
            var profileStore = root["profiles"]!;
            foreach (var profileId in profiles)
            {
                profileStore[profileId] = this.GetProfile(profileId).ToJson();
            }

            return root;
        }

        public void Synchronize(JToken configJson)
        {
            var activeProfile = configJson["activeProfile"]!.ToString();
            var profileIds = (configJson["profiles"] as JObject)!.Properties().Select(p => p.Name);

            lock (_synchronizationLock)
            {
                if (_pendingPropertyChanges.Count != 0)
                    throw new SynchronizationException("Pending property change events detected");

                var storedProfiles = this.Profiles;
                var availableProfiles = profileIds.Where(p => storedProfiles.Contains(p));
                var newProfiles = profileIds.Where(p => !storedProfiles.Contains(p));
                var removedProfiles = storedProfiles.Where(p => !profileIds.Contains(p));

                foreach (var profileId in newProfiles)
                    StoreNewProfile((configJson["profiles"]![profileId] as JObject)!, true, true);

                foreach (var profileId in availableProfiles)
                {
                    var profileData = configJson["profiles"]![profileId] as JObject;
                    var profile = GetProfile(profileId);
                    profile.Synchronize(profileData!);
                }

                ActiveProfileChangedEventArgs? profileEvent = null;
                if (_activeProfileId != activeProfile && activeProfile != null && _profiles.ContainsKey(activeProfile))
                {
                    var oldProfileId = _activeProfileId;
                    _activeProfileId = activeProfile;
                    profileEvent = new ActiveProfileChangedEventArgs(oldProfileId, _activeProfileId, true);
                }

                foreach (var profileId in removedProfiles)
                    DeleteProfile(profileId, true);

                var pendingEvents = GetPendingEvents();
                System.Threading.Tasks.Task.Run(() =>
                {
                    if (profileEvent != null)
                        OnActiveProfileChange?.Invoke(this, profileEvent);
                    DispatchEvents(pendingEvents, true);
                });
            }

            logger.Info("Config manager synchronized");
        }

        #region properties

        // Reads take the same lock the writes (SetProperty/DeleteProperty/SetGlobalProperty/SetAppSetting)
        // use: the underlying Newtonsoft JObject tree is not safe for concurrent read+mutate, and the actor
        // poll thread reads here while the config/UI thread writes. The lock is reentrant and listener
        // callbacks are dispatched outside it, so this adds no deadlock or reentrancy hazard.
        public T GetProperty<T>(string key)
        {
            lock (_synchronizationLock)
            {
                if (IsAppSettingKey(key))
                    return _appConfig.GetProperty<T>(key);
                return ActiveProfile.GetProperty<T>(key);
            }
        }

        public T GetProperty<T>(string key, T defaultValue)
        {
            lock (_synchronizationLock)
            {
                if (IsAppSettingKey(key))
                    return _appConfig.GetProperty<T>(key, defaultValue);
                return ActiveProfile.GetProperty<T>(key, defaultValue);
            }
        }

        public bool HasProperty(string key)
        {
            lock (_synchronizationLock)
            {
                if (IsAppSettingKey(key))
                    return _appConfig.HasProperty(key);
                return ActiveProfile.HasProperty(key);
            }
        }

        public void SetProperty(string key, object value)
        {
            // App settings live outside profiles; route a stray write so it can't re-create a profile shadow.
            if (IsAppSettingKey(key))
            {
                SetAppSetting(key, value);
                return;
            }
            lock (_synchronizationLock)
            {
                ActiveProfile.SetProperty(key, value);
            }
        }

        #region app settings

        /// <summary>True if <paramref name="key"/> targets an application-global setting (one of the
        /// <see cref="AppSettingRoots"/> or a descendant of one).</summary>
        public static bool IsAppSettingKey(string key)
        {
            if (key == null)
                return false;
            foreach (var root in AppSettingRoots)
                if (key.Equals(root, StringComparison.Ordinal) || key.StartsWith(root + ".", StringComparison.Ordinal))
                    return true;
            return false;
        }

        public T GetAppSetting<T>(string key) => _appConfig.GetProperty<T>(key);

        public T GetAppSetting<T>(string key, T defaultValue) => _appConfig.GetProperty<T>(key, defaultValue);

        // Effective values: bundled defaults overlaid with the user's stored settings.
        public JObject AppSettingsAsJson()
        {
            var effective = _appDefaults.ToJson();
            // Pass an explicit (never-ignore) predicate: the 2-arg Overwrite overload passes ignorePath=null,
            // and `!ignorePath?.Invoke(...) ?? false` is false when null (lifted `!` on a null bool?), which
            // silently skips every overwrite - leaving only the defaults.
            JsonUtil.Overwrite(_appConfig.ToJson(), effective, _ => false);
            return effective;
        }

        public void SetAppSetting(string key, object value)
        {
            if (!IsAppSettingKey(key))
                throw new ConfigException($"'{key}' is not an application setting");

            // Notify the same property-change listeners the (now-routed) profile keys used, so live modules
            // react instantly. Dispatch the key plus its parent paths (modules register on exact keys);
            // deliberately not the "*" topic, so this doesn't trigger a full profile resync to the UI.
            var events = new Dictionary<string, ISet<string>>();
            lock (_synchronizationLock)
            {
                _appConfig.SetProperty(key, value);
                SaveAppSettings();

                // Capture the active-profile tag under the same lock as the write so a concurrent
                // profile switch can't tag these events with a stale/mismatched profile id.
                var activeProfileId = this.ActiveProfileId;
                foreach (var path in ExpandKeyAndParents(key))
                    events[path] = new HashSet<string>() { activeProfileId };
            }
            DispatchEvents(events, false);

            OnAppSettingChange?.Invoke(this, EventArgs.Empty);
        }

        private static IEnumerable<string> ExpandKeyAndParents(string key)
        {
            yield return key;
            var index = key.LastIndexOf('.');
            while (index > 0)
            {
                key = key.Substring(0, index);
                yield return key;
                index = key.LastIndexOf('.');
            }
        }

        #endregion app settings

        public void DeleteProperty(string key)
        {
            lock (_synchronizationLock)
            {
                ActiveProfile.DeleteProperty(key);
            }
        }

        public void SetGlobalProperty(string key, object value)
        {
            lock (_synchronizationLock)
            {
                _globalProfile.SetProperty(key, value);
            }
        }

        #endregion properties

        #region event handling

        private void OnEvent_Config_OnPropertyChange(object? sender, PropertyChangedEventArgs e)
        {
            lock (_synchronizationLock)
            {
                var profileId = (sender as IGobchatConfigProfile)!.ProfileId;
                if (_pendingPropertyChanges.TryGetValue(e.PropertyKey, out var profiles))
                    profiles.Add(profileId);
                else
                    _pendingPropertyChanges.Add(e.PropertyKey, new HashSet<string>() { profileId });
                _changedProfiles.Add(profileId);
            }
        }

        public event EventHandler<ProfileChangedEventArgs>? OnProfileChange;

        public event EventHandler<ActiveProfileChangedEventArgs>? OnActiveProfileChange;

        /// <summary>Raised after an application-global setting changes (instant apply, already persisted).</summary>
        public event EventHandler? OnAppSettingChange;

        public bool AddPropertyChangeListener(string path, PropertyChangedListener listener)
        {
            return AddPropertyChangeListener(path, false, false, listener);
        }

        public bool AddPropertyChangeListener(string path, bool activeProfile, PropertyChangedListener listener)
        {
            return AddPropertyChangeListener(path, activeProfile, false, listener);
        }

        public bool AddPropertyChangeListener(string path, bool activeProfile, bool initialize, PropertyChangedListener listener)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (listener == null) throw new ArgumentNullException(nameof(listener));

            // Mutate the listener buckets under the same lock the dispatch path reads them through
            // (GetListenersFor / UpdateActiveListenerOnActiveProfileChange), since the poll thread can
            // dispatch while the UI thread registers/unregisters on a profile switch. The initialize
            // Invoke runs outside the lock, matching how DispatchEvents invokes listeners unlocked.
            lock (_synchronizationLock)
            {
                //ensure listener is not already registered

                if (_activePropertyChangedListener.TryGetValue(path, out var activeListeners))
                    if (activeListeners.Contains(listener))
                        return false;

                if (_allPropertyChangedListener.TryGetValue(path, out var passiveListeners))
                    if (passiveListeners.Contains(listener))
                        return false;

                //put them in the correct bucket

                if (activeProfile)
                {
                    if (activeListeners != null)
                        activeListeners.Add(listener);
                    else
                        _activePropertyChangedListener.Add(path, new List<PropertyChangedListener>() { listener });
                }
                else
                {
                    if (passiveListeners != null)
                        passiveListeners.Add(listener);
                    else
                        _allPropertyChangedListener.Add(path, new List<PropertyChangedListener>() { listener });
                }
            }

            if (initialize)
            {
                var activeEvent = new ProfilePropertyChangedEventArgs(path, ActiveProfileId, true, false);
                var collection = new List<ProfilePropertyChangedEventArgs>(1) { activeEvent };
                listener.Invoke(this, new ProfilePropertyChangedCollectionEventArgs(collection));
            }

            return true;
        }

        public void RemovePropertyChangeListener(string path, PropertyChangedListener listener)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (listener == null) return;

            bool RemoveListener(Dictionary<string, IList<PropertyChangedListener>> store)
            {
                var removed = false;
                if (store.TryGetValue(path, out var listeners))
                {
                    removed = listeners.Remove(listener);
                    if (listeners.Count == 0)
                        store.Remove(path);
                }
                return removed;
            }

            lock (_synchronizationLock)
            {
                if (!RemoveListener(_allPropertyChangedListener))
                    RemoveListener(_activePropertyChangedListener);
            }
        }

        public void RemovePropertyChangeListener(PropertyChangedListener listener)
        {
            if (listener == null) return;

            bool RemoveListener(Dictionary<string, IList<PropertyChangedListener>> store)
            {
                var removed = false;
                foreach (var key in store.Keys.ToArray())
                {
                    var listeners = store[key];
                    removed = listeners.Remove(listener);
                    if (listeners.Count == 0)
                        store.Remove(key);
                }
                return removed;
            }

            lock (_synchronizationLock)
            {
                if (!RemoveListener(_allPropertyChangedListener))
                    RemoveListener(_activePropertyChangedListener);
            }
        }

        private IDictionary<string, ISet<string>> GetPendingEvents()
        {
            lock (_synchronizationLock)
            {
                var pendingPropertyChanges = new Dictionary<string, ISet<string>>(_pendingPropertyChanges);
                _pendingPropertyChanges.Clear();
                return pendingPropertyChanges;
            }
        }

        private IList<PropertyChangedListener> GetListenersFor(string path)
        {
            // Snapshot under the lock (registration can run concurrently); the returned copy is then
            // safe for the caller to iterate and invoke unlocked.
            var result = new List<PropertyChangedListener>();
            lock (_synchronizationLock)
            {
                if (_allPropertyChangedListener.TryGetValue(path, out var passiveListeners))
                    result.AddRange(passiveListeners);

                if (_activePropertyChangedListener.TryGetValue(path, out var activeListeners))
                    result.AddRange(activeListeners);
            }
            return result;
        }

        private void DispatchEvents(IDictionary<string, ISet<string>> events, bool synchronizeEvents)
        {
            var activeProfileId = this.ActiveProfileId;
            var dispatchableChanges = new Dictionary<PropertyChangedListener, List<ProfilePropertyChangedEventArgs>>();
            foreach (var entry in events)
            {
                var listeners = GetListenersFor(entry.Key);
                if (listeners.Count == 0)
                    continue;

                var eventArgs = new List<ProfilePropertyChangedEventArgs>();
                foreach (var profileId in entry.Value)
                    eventArgs.Add(new ProfilePropertyChangedEventArgs(entry.Key, profileId, profileId == activeProfileId, synchronizeEvents));

                foreach (var listener in listeners)
                {
                    if (dispatchableChanges.TryGetValue(listener, out var pendingEvents))
                    {
                        pendingEvents.AddRange(eventArgs);
                    }
                    else
                    {
                        pendingEvents = new List<ProfilePropertyChangedEventArgs>();
                        pendingEvents.AddRange(eventArgs);
                        dispatchableChanges.Add(listener, pendingEvents);
                    }
                }
            }

            foreach (var entry in dispatchableChanges)
                entry.Key.Invoke(this, new ProfilePropertyChangedCollectionEventArgs(entry.Value));
        }

        private void UpdateActiveListenerOnActiveProfileChange(object? sender, ActiveProfileChangedEventArgs e)
        {
            var activeProfileId = this.ActiveProfileId;
            var dispatchableChanges = new Dictionary<PropertyChangedListener, List<ProfilePropertyChangedEventArgs>>();

            // Build the dispatch snapshot under the lock (registration can run concurrently); listeners
            // are invoked afterwards, outside the lock.
            lock (_synchronizationLock)
            {
                foreach (var path in _activePropertyChangedListener.Keys.ToArray())
                {
                    if (_activePropertyChangedListener.TryGetValue(path, out var listeners))
                    {
                        var activeEvent = new ProfilePropertyChangedEventArgs(path, activeProfileId, true, e.Synchronizing);
                        foreach (var listener in listeners.ToArray())
                        {
                            if (dispatchableChanges.TryGetValue(listener, out var list))
                                list.Add(activeEvent);
                            else
                                dispatchableChanges.Add(listener, new List<ProfilePropertyChangedEventArgs>() { activeEvent });
                        }
                    }
                }
            }

            foreach (var entry in dispatchableChanges)
                entry.Key.Invoke(this, new ProfilePropertyChangedCollectionEventArgs(entry.Value));
        }

        public void DispatchChangeEvents()
        {
            var events = GetPendingEvents();
            DispatchEvents(events, false);
        }

        #endregion event handling
    }
}