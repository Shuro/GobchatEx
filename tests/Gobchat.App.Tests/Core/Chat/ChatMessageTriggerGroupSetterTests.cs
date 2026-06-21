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

using System.Collections.Generic;
using System.Text;
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// Trigger groups tag a message by its sender name so tabs/styles can filter on it. WHY this matters:
    /// the match is name-equality, so a sender whose name is typed in decorative code points (Mathematical
    /// Sans-Serif Bold) must still fold to plain text to match a plain-text trigger — while the displayed
    /// name is left untouched.
    /// </summary>
    public sealed class ChatMessageTriggerGroupSetterTests
    {
        private static string ToMathBold(string ascii)
        {
            var sb = new StringBuilder();
            foreach (var c in ascii)
            {
                if (c >= 'A' && c <= 'Z') sb.Append(char.ConvertFromUtf32(0x1D5D4 + (c - 'A')));
                else if (c >= 'a' && c <= 'z') sb.Append(char.ConvertFromUtf32(0x1D5EE + (c - 'a')));
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static ChatMessage MessageFrom(string characterName)
        {
            return new ChatMessage
            {
                Channel = ChatChannel.Say,
                Source = new ChatMessageSource("origin") { CharacterName = characterName },
            };
        }

        private static TriggerGroup Group(string id, params string[] triggers)
        {
            return new TriggerGroup { Active = true, Id = id, Trigger = new List<string>(triggers) };
        }

        [Fact]
        public void SetTriggerGroup_MathBoldName_MatchesPlainTextTrigger()
        {
            var message = MessageFrom(ToMathBold("Darya"));
            var setter = new ChatMessageTriggerGroupSetter { Groups = new[] { Group("g-darya", "darya") } };

            setter.SetTriggerGroup(message);

            Assert.Equal("g-darya", message.Source.TriggerGroupId);
        }

        [Fact]
        public void SetTriggerGroup_LeavesDisplayedNameUnchanged()
        {
            // Only a folded copy is matched; the original (decorative) name must survive for display.
            var decorated = ToMathBold("Darya");
            var message = MessageFrom(decorated);
            var setter = new ChatMessageTriggerGroupSetter { Groups = new[] { Group("g-darya", "darya") } };

            setter.SetTriggerGroup(message);

            Assert.Equal(decorated, message.Source.CharacterName);
        }

        [Fact]
        public void SetTriggerGroup_NonMatchingName_LeavesGroupNull()
        {
            var message = MessageFrom(ToMathBold("Someone"));
            var setter = new ChatMessageTriggerGroupSetter { Groups = new[] { Group("g-darya", "darya") } };

            setter.SetTriggerGroup(message);

            Assert.Null(message.Source.TriggerGroupId);
        }

        // A cross-world speaker's name carries a " [Server]" suffix, but the server is irrelevant to grouping:
        // a member added as just the player name must match them on any world. WHY: otherwise you'd have to
        // re-add the same person per server, and the right-click "add to group" (which stores the bare name)
        // would never tag their cross-world lines.
        [Fact]
        public void SetTriggerGroup_CrossWorldName_MatchesBareNameTrigger()
        {
            var message = MessageFrom("Khada Iriq [Balmung]");
            var setter = new ChatMessageTriggerGroupSetter { Groups = new[] { Group("g-khada", "khada iriq") } };

            setter.SetTriggerGroup(message);

            Assert.Equal("g-khada", message.Source.TriggerGroupId);
        }

        // Backward compatibility: a legacy member saved with the old "Name [Server]" suffix still matches the
        // same-server speaker, so upgrading doesn't silently drop existing group members.
        [Fact]
        public void SetTriggerGroup_CrossWorldName_StillMatchesLegacyServerSuffixTrigger()
        {
            var message = MessageFrom("Khada Iriq [Balmung]");
            var setter = new ChatMessageTriggerGroupSetter { Groups = new[] { Group("g-khada", "khada iriq [balmung]") } };

            setter.SetTriggerGroup(message);

            Assert.Equal("g-khada", message.Source.TriggerGroupId);
        }
    }
}
