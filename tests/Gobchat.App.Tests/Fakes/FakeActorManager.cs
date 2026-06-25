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
using Gobchat.Module.Actor;

namespace Gobchat.App.Tests.Fakes
{
    /// <summary>
    /// Hand-rolled <see cref="IActorManager"/> test double. The interface is tiny, so a fake keeps
    /// the suite dependency-free (no mocking library). Behaviour is driven by the public fields.
    /// </summary>
    internal sealed class FakeActorManager : IActorManager
    {
        public bool IsAvailable { get; set; } = true;
        public string? ActivePlayerName { get; set; }
        public Func<string, float>? DistanceProvider { get; set; }
        public int PlayerCount { get; set; }
        public string[] PlayersInArea { get; set; } = Array.Empty<string>();

        public string GetActivePlayerName() => ActivePlayerName!;

        public int GetPlayerCount() => PlayerCount;

        public float GetDistanceToPlayerWithName(string name) => DistanceProvider?.Invoke(name) ?? 0f;

        public string[] GetPlayersInArea() => PlayersInArea;

        /// <summary>Records the last <see cref="TouchPreviewKeepalive"/> call for assertions.</summary>
        public int PreviewKeepaliveTouchCount { get; private set; }
        public bool PreviewKeepaliveActive { get; set; }

        public void TouchPreviewKeepalive() => PreviewKeepaliveTouchCount++;

        public event EventHandler<CurrentPlayerChangedEventArgs>? OnCurrentPlayerChanged;

        /// <summary>Test helper: raises <see cref="OnCurrentPlayerChanged"/> as if the player changed.</summary>
        public void RaiseCurrentPlayerChanged(string previous, string current)
            => OnCurrentPlayerChanged?.Invoke(this, new CurrentPlayerChangedEventArgs(previous, current));
    }
}
