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
using Gobchat.Core.Chat;
using Gobchat.Core.Util;
using Xunit;

namespace Gobchat.App.Tests.Core.Util
{
    /// <summary>
    /// Config values arrive untyped (string from JSON, boxed numbers, or already-typed enums) and must
    /// be coerced to enums leniently. WHY this matters: a wrong/throwing coercion here would break
    /// reading channel/segment settings from a user profile.
    /// </summary>
    public sealed class EnumUtilTests
    {
        [Fact]
        public void ObjectToEnum_ParsesStringCaseInsensitively()
        {
            Assert.Equal(ChatChannel.Say, EnumUtil.ObjectToEnum<ChatChannel>("say"));
        }

        [Fact]
        public void ObjectToEnum_PassesThroughEnumValue()
        {
            Assert.Equal(ChatChannel.Emote, EnumUtil.ObjectToEnum<ChatChannel>(ChatChannel.Emote));
        }

        [Fact]
        public void ObjectToEnum_ConvertsUnderlyingNumber()
        {
            // ChatChannel : int; Say == 1
            Assert.Equal(ChatChannel.Say, EnumUtil.ObjectToEnum<ChatChannel>(1));
        }

        [Fact]
        public void ObjectToEnum_NullOrUnrecognized_ReturnsNull()
        {
            Assert.Null(EnumUtil.ObjectToEnum<ChatChannel>(null!));
            Assert.Null(EnumUtil.ObjectToEnum<ChatChannel>("not-a-channel"));
        }

        [Fact]
        public void ObjectToEnum_NonEnumType_Throws()
        {
            // int satisfies the (struct, IConvertible) constraint but is not an enum.
            Assert.Throws<ArgumentException>(() => EnumUtil.ObjectToEnum<int>(5));
        }
    }
}
