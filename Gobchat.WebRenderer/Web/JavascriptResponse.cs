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

namespace Gobchat.UI.Web
{
    internal sealed class JavascriptResponse : IJavascriptResponse
    {
        public JavascriptResponse(bool success, object result, string message)
        {
            Success = success;
            Result = result;
            Message = message;
        }

        public string Message { get; }

        public bool Success { get; }

        public object Result { get; }
    }
}
