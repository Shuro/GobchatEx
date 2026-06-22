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

namespace Gobchat.Module.MemoryReader
{
    /// <summary>
    /// A single placed character in the dry-run roster: a display name and its distance (in yalms)
    /// from the player. Distances feed the unchanged actor pipeline's range-filter fade.
    /// </summary>
    public sealed record DryRunCharacter(string Name, float Distance);

    /// <summary>
    /// The control surface for the <c>--dry-run</c> developer mode, driven by hand from the Debug
    /// settings page. It mutates the synthetic state that the dry-run memory manager exposes through
    /// <see cref="IMemoryReaderManager"/>: a "logged-in" character (Connect/Disconnect) and a roster
    /// of placed characters with distances (AddCharacter/RemoveCharacter). The actor poll loop reads
    /// the current player and the roster on each tick, so these mutations need not raise any events of
    /// their own - the existing current-player/actor diffs drive login/logout and the range-filter fade.
    /// Only registered in DI when <c>--dry-run</c> is active.
    /// </summary>
    public interface IDryRunController
    {
        /// <summary>Marks <paramref name="name"/> as the logged-in character (the "current player").</summary>
        void Connect(string name);

        /// <summary>Clears the current player (back to the "logged out" / title-screen state).</summary>
        void Disconnect();

        /// <summary>Places (or re-places) <paramref name="name"/> at <paramref name="distance"/> yalms from the player.</summary>
        void AddCharacter(string name, float distance);

        /// <summary>Removes the placed character with the given <paramref name="name"/> from the roster.</summary>
        void RemoveCharacter(string name);

        /// <summary>A snapshot of the currently placed characters.</summary>
        System.Collections.Generic.IReadOnlyList<DryRunCharacter> GetRoster();

        /// <summary>The logged-in character's name, or <c>null</c> when "logged out".</summary>
        string? CurrentPlayer { get; }
    }
}
