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

namespace Gobchat.Memory.Actor
{
    /// <summary>
    /// The locally logged-in character, read directly from Sharlayan's "current player" rather than
    /// the actor table (the actor-table <see cref="PlayerCharacter.IsUser"/> flag is unreliable - the
    /// active player is often absent from that table). <c>Name</c> is the only stable identity; the
    /// actor <c>Id</c> changes between sessions.
    /// </summary>
    public sealed class CurrentPlayer
    {
        public string Name { get; internal set; } = string.Empty;

        public uint Id { get; internal set; }
    }
}
