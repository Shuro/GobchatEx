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
using Gobchat.Core.Config;
using Newtonsoft.Json.Linq;

namespace Gobchat.App.Tests.Fakes
{
    /// <summary>One recorded write made through <see cref="FakeConfigManager"/>.</summary>
    internal readonly record struct ConfigWrite(string Operation, string Key, JToken? Value);

    /// <summary>
    /// Hand-rolled <see cref="IConfigManager"/> test double (repo convention: no mocking library). It
    /// serves seeded property values, records every write/delete in <see cref="Writes"/>, and tracks
    /// active-profile switches and persistence calls so tests can assert exactly which config effects a
    /// chat command produced (and, for invalid commands, that it produced none). Members the command
    /// handlers do not use throw <see cref="NotSupportedException"/>.
    /// </summary>
    internal sealed class FakeConfigManager : IConfigManager
    {
        private readonly Dictionary<string, JToken> _data = new();
        private readonly Dictionary<string, FakeConfigProfile> _profiles = new();
        private readonly List<string> _profileOrder = new();

        public List<ConfigWrite> Writes { get; } = new();
        public int DispatchChangeEventsCount { get; private set; }
        public int SaveProfilesCount { get; private set; }
        public int ActiveProfileSetCount { get; private set; }

        private string _activeProfileId = "";

        /// <summary>Seeds a readable property without recording it as a write.</summary>
        public FakeConfigManager Seed(string key, JToken value)
        {
            _data[key] = value;
            return this;
        }

        /// <summary>Seeds a profile id + display name (order is preserved for <see cref="Profiles"/>).</summary>
        public FakeConfigManager AddProfile(string profileId, string name)
        {
            _profiles[profileId] = new FakeConfigProfile(profileId, new JObject { ["profile"] = new JObject { ["name"] = name } });
            _profileOrder.Add(profileId);
            if (_activeProfileId.Length == 0)
                _activeProfileId = profileId;
            return this;
        }

        public string ActiveProfileId
        {
            get => _activeProfileId;
            set
            {
                _activeProfileId = value;
                ActiveProfileSetCount++;
            }
        }

        public IList<string> Profiles => _profileOrder;

        public IGobchatConfigProfile GetProfile(string profileId) => _profiles[profileId];

        public T GetProperty<T>(string key)
        {
            if (!_data.TryGetValue(key, out var token))
                throw new KeyNotFoundException(key);
            return token is T typed ? typed : token.ToObject<T>()!;
        }

        public T GetProperty<T>(string key, T defaultValue)
        {
            if (!_data.TryGetValue(key, out var token))
                return defaultValue;
            return token is T typed ? typed : token.ToObject<T>()!;
        }

        public bool HasProperty(string key) => _data.ContainsKey(key);

        public void SetProperty(string key, object value)
        {
            var token = value as JToken ?? JToken.FromObject(value);
            _data[key] = token;
            Writes.Add(new ConfigWrite("set", key, token));
        }

        public void DeleteProperty(string key)
        {
            _data.Remove(key);
            Writes.Add(new ConfigWrite("delete", key, null));
        }

        public void DispatchChangeEvents() => DispatchChangeEventsCount++;

        public void SaveProfiles() => SaveProfilesCount++;

        // ----- unused by the chat command handlers -----

        public event EventHandler<ActiveProfileChangedEventArgs>? OnActiveProfileChange { add { } remove { } }
        public event EventHandler<ProfileChangedEventArgs>? OnProfileChange { add { } remove { } }
        public event EventHandler? OnAppSettingChange { add { } remove { } }

        public IGobchatConfigProfile DefaultProfile => throw new NotSupportedException();

        public bool AddPropertyChangeListener(string path, PropertyChangedListener listener) => throw new NotSupportedException();
        public bool AddPropertyChangeListener(string path, bool onActiveProfile, PropertyChangedListener listener) => throw new NotSupportedException();
        public bool AddPropertyChangeListener(string path, bool onActiveProfile, bool initialize, PropertyChangedListener listener) => throw new NotSupportedException();
        public void RemovePropertyChangeListener(string path, PropertyChangedListener listener) => throw new NotSupportedException();
        public void RemovePropertyChangeListener(PropertyChangedListener listener) => throw new NotSupportedException();
        public void DeleteProfile(string profileId) => throw new NotSupportedException();
        public string CreateNewProfile() => throw new NotSupportedException();
        public void CopyProfile(string srcProfileId, string dstProfileId) => throw new NotSupportedException();
        public JObject? ParseProfile(string path) => throw new NotSupportedException();
        public void SetGlobalProperty(string key, object value) => throw new NotSupportedException();
        public T GetAppSetting<T>(string key) => throw new NotSupportedException();
        public T GetAppSetting<T>(string key, T defaultValue) => throw new NotSupportedException();
        public void SetAppSetting(string key, object value) => throw new NotSupportedException();
        public JObject AppSettingsAsJson() => throw new NotSupportedException();
        public JObject AsJson() => throw new NotSupportedException();
        public void Synchronize(JToken json) => throw new NotSupportedException();
    }

    /// <summary>Minimal <see cref="IGobchatConfigProfile"/> backing <see cref="FakeConfigManager.GetProfile"/>; serves seeded values only.</summary>
    internal sealed class FakeConfigProfile : IGobchatConfigProfile
    {
        private readonly JObject _data;

        public FakeConfigProfile(string profileId, JObject data)
        {
            ProfileId = profileId;
            _data = data;
        }

        public string ProfileId { get; }
        public bool IsWriteable => true;

        public event EventHandler<PropertyChangedEventArgs>? OnPropertyChange { add { } remove { } }

        public T GetProperty<T>(string key) => _data.SelectToken(key)!.ToObject<T>()!;

        public T GetProperty<T>(string key, T defaultValue)
        {
            var token = _data.SelectToken(key);
            if (token == null)
                return defaultValue;
            return token is T typed ? typed : token.ToObject<T>()!;
        }

        public bool HasProperty(string key) => _data.SelectToken(key) != null;
        public JObject ToJson() => _data;

        public void SetProperty(string key, object value) => throw new NotSupportedException();
        public void DeleteProperty(string key) => throw new NotSupportedException();
        public void SetProperties(JObject json) => throw new NotSupportedException();
        public void Synchronize(JObject root) => throw new NotSupportedException();
    }
}
