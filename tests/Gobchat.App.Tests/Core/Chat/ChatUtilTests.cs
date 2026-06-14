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

using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// FFXIV cross-world names arrive as "Name[Server]". The actor realm and range filter key on the
    /// bare name (via <see cref="ChatUtil.StripServerName"/>), so stripping the server suffix must be exact.
    /// </summary>
    public sealed class ChatUtilTests
    {
        [Theory]
        [InlineData("Vtorak Azora[Gilgamesh]", "Vtorak Azora")]
        [InlineData("Vtorak Azora [Gilgamesh]", "Vtorak Azora")]
        [InlineData("Vtorak Azora", "Vtorak Azora")]
        public void StripServerName_RemovesServerSuffix(string input, string expected)
        {
            Assert.Equal(expected, ChatUtil.StripServerName(input));
        }

        [Theory]
        [InlineData("Vtorak Azora[Gilgamesh]", "Vtorak Azora", "Gilgamesh")]
        [InlineData("Vtorak Azora [Gilgamesh]", "Vtorak Azora", "Gilgamesh")]
        [InlineData("Vtorak Azora[ Gilgamesh ]", "Vtorak Azora", "Gilgamesh")]
        public void SplitCharacterName_SeparatesNameAndServer(string input, string expectedName, string expectedServer)
        {
            var (name, server) = ChatUtil.SplitCharacterName(input);

            Assert.Equal(expectedName, name);
            Assert.Equal(expectedServer, server);
        }

        [Fact]
        public void SplitCharacterName_WithoutServer_ReturnsNullServer()
        {
            var (name, server) = ChatUtil.SplitCharacterName("Vtorak Azora");

            Assert.Equal("Vtorak Azora", name);
            Assert.Null(server);
        }
    }
}
