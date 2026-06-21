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

using Gobchat.Module.MemoryReader;
using System.Threading.Tasks;

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Bridge DTO for <see cref="IBrowserMemoryHandler.GetAttachedFFXIVProcess"/>. The bridge response
    /// serializer preserves member names, so the page reads <c>.State</c> (the numeric
    /// <see cref="ConnectionState"/>) and <c>.Id</c> - a typed contract instead of a ValueTuple's
    /// <c>Item1</c>/<c>Item2</c>.
    /// </summary>
    public sealed record AttachedProcessInfo(ConnectionState State, int Id);

    public interface IBrowserMemoryHandler
    {
        Task<int[]> GetAttachableFFXIVProcesses();

        Task<AttachedProcessInfo> GetAttachedFFXIVProcess();

        Task<bool> AttachToFFXIVProcess(int id);
    }
}