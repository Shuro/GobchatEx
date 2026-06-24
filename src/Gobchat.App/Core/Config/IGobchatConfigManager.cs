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
using Newtonsoft.Json.Linq;

namespace Gobchat.Core.Config
{
    public delegate void PropertyChangedListener(IConfigManager sender, ProfilePropertyChangedCollectionEventArgs evt);

    /// <summary>
    /// Manages the JSON config profiles and the application-global settings.
    /// <para>
    /// CHT-9/ARC-7 — thread-safety contract. The implementation (<c>GobchatConfigManager</c>) serializes
    /// <b>each individual</b> property operation (<see cref="GetProperty{T}(string)"/>,
    /// <see cref="SetProperty"/>, <see cref="DeleteProperty"/>, <see cref="HasProperty"/>, the app-setting
    /// accessors, change-event bookkeeping and listener (un)registration) on one internal lock, so concurrent
    /// callers from different threads — typically the actor-poll thread and the config/UI thread — never read
    /// or mutate the shared config tree at the same time. Raw <c>JToken</c>/<c>JObject</c>/<c>JArray</c> reads
    /// return a detached deep clone (ARC-2), so a returned token can be iterated/mutated freely without racing
    /// a concurrent write.
    /// </para>
    /// <para>
    /// What is <b>not</b> guaranteed: a compound read-modify-write spanning several calls (e.g. read a value,
    /// decide, then write it back) is <b>not</b> atomic — another thread may write the same key between the
    /// read and the write, so such sequences are last-writer-wins. Callers that need an atomic compound update
    /// must either run on a single owning thread or tolerate last-writer-wins. There is no single "config
    /// thread" to marshal onto; change events are dispatched on thread-pool tasks and on the calling thread.
    /// </para>
    /// </summary>
    public interface IConfigManager
    {
        #region event handling

        event EventHandler<ActiveProfileChangedEventArgs>? OnActiveProfileChange;

        event EventHandler<ProfileChangedEventArgs>? OnProfileChange;

        /// <summary>Raised after an application-global setting changes (instant apply, already persisted).</summary>
        event EventHandler? OnAppSettingChange;

        bool AddPropertyChangeListener(string path, PropertyChangedListener listener);

        bool AddPropertyChangeListener(string path, bool onActiveProfile, PropertyChangedListener listener);

        bool AddPropertyChangeListener(string path, bool onActiveProfile, bool initialize, PropertyChangedListener listener);

        void RemovePropertyChangeListener(string path, PropertyChangedListener listener);

        void RemovePropertyChangeListener(PropertyChangedListener listener);

        #endregion event handling

        IGobchatConfigProfile DefaultProfile { get; }

        IGobchatConfigProfile GetProfile(string profileId);

        // IGobchatConfigProfile ActiveProfile { get; }

        string ActiveProfileId { get; set; }

        IList<string> Profiles { get; }

        void DeleteProfile(string profileId);

        string CreateNewProfile();

        void SaveProfiles();

        void CopyProfile(string srcProfileId, string dstProfileId);

        JObject? ParseProfile(string path);

        #region property handling

        T GetProperty<T>(string key);

        T GetProperty<T>(string key, T defaultValue);

        bool HasProperty(string key);

        void SetProperty(string key, object value);

        void DeleteProperty(string key);

        void SetGlobalProperty(string key, object value);

        void DispatchChangeEvents();

        #endregion property handling

        #region app settings

        T GetAppSetting<T>(string key);

        T GetAppSetting<T>(string key, T defaultValue);

        void SetAppSetting(string key, object value);

        JObject AppSettingsAsJson();

        #endregion app settings

        JObject AsJson();

        void Synchronize(JToken json);
    }
}