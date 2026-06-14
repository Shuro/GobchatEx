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

using Gobchat.Core.Util;
using Xunit;

namespace Gobchat.App.Tests.Core.Util
{
    /// <summary>
    /// Used by <see cref="EnumUtil"/> to decide whether a boxed config value is a numeric enum index.
    /// </summary>
    public sealed class MathUtilTests
    {
        public static TheoryData<object> Numbers => new()
        {
            (sbyte)1, (byte)1, (short)1, (ushort)1, 1, 1u, 1L, 1UL, 1.0f, 1.0d, 1.0m,
        };

        public static TheoryData<object> NonNumbers => new()
        {
            "1", true, 'a', new object(),
        };

        [Theory]
        [MemberData(nameof(Numbers))]
        public void IsNumber_TrueForNumericTypes(object value)
        {
            Assert.True(MathUtil.IsNumber(value));
        }

        [Theory]
        [MemberData(nameof(NonNumbers))]
        public void IsNumber_FalseForNonNumericTypes(object value)
        {
            Assert.False(MathUtil.IsNumber(value));
        }

        [Fact]
        public void IsNumber_FalseForNull()
        {
            Assert.False(MathUtil.IsNumber(null!));
        }
    }
}
