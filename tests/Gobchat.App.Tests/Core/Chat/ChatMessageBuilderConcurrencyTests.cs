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
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Gobchat.Core.Chat;
using Xunit;

namespace Gobchat.App.Tests.Core.Chat
{
    /// <summary>
    /// CHT-3 / CHT-11: the chat worker formats messages while config-change callbacks rewrite the mention
    /// set from another thread. Setting <see cref="ChatMessageBuilder.Mentions"/> runs
    /// <c>ChatMessageMentionFinder.RebuildPatterns</c>, which <c>Clear()</c>s then refills the very
    /// <c>List&lt;Regex&gt;</c> that the formatter enumerates in <c>ReplaceTypeByText.Segment</c>. Without the
    /// builder's single config lock guarding both the whole format pass and every setter, that concurrent
    /// rebuild tears the enumeration — classically an <see cref="InvalidOperationException"/> ("collection
    /// was modified"), or a half-rebuilt pattern list that drops/duplicates highlight spans. This pins that
    /// formatting a message and swapping the mention set never race: no throw, and the message text is
    /// preserved (mention marking only re-types spans, it must never lose or duplicate characters).
    /// </summary>
    public sealed class ChatMessageBuilderConcurrencyTests
    {
        private const string MessageText = "Alice and Bob meet Carol and Dave to talk";

        private static ChatMessage SayMessage()
        {
            var message = new ChatMessage
            {
                Channel = ChatChannel.Say,
                Source = new ChatMessageSource("Tester") { IsUser = false },
            };
            message.Content.Add(new ChatMessageSegment(MessageSegmentType.Undefined, MessageText));
            return message;
        }

        [Fact]
        public void FormattingWhileMentionSetIsRebuilt_NeverRacesOrCorruptsText()
        {
            var builder = new ChatMessageBuilder
            {
                MentionChannels = new[] { ChatChannel.Say },
                Mentions = new[] { "Alice", "Bob" },
            };

            const int readerCount = 3;
            const int formatsPerReader = 4000;
            var failures = new ConcurrentQueue<string>();
            using var stop = new CancellationTokenSource();

            // Writer: churn the mention set so RebuildPatterns runs continuously against the readers.
            var writer = Task.Run(() =>
            {
                var toggle = false;
                while (!stop.IsCancellationRequested)
                {
                    builder.Mentions = toggle ? new[] { "Alice", "Bob" } : new[] { "Carol", "Dave" };
                    toggle = !toggle;
                }
            });

            var readers = Enumerable.Range(0, readerCount).Select(_ => Task.Run(() =>
            {
                for (var i = 0; i < formatsPerReader; ++i)
                {
                    var message = SayMessage();
                    try
                    {
                        builder.FormatChatMessage(message);
                    }
                    catch (Exception ex)
                    {
                        failures.Enqueue($"FormatChatMessage threw under concurrent rebuild: {ex.GetType().Name}: {ex.Message}");
                        return;
                    }

                    var reconstructed = string.Concat(message.Content.Select(s => s.Text));
                    if (reconstructed != MessageText)
                        failures.Enqueue($"Text corrupted under concurrent rebuild: '{reconstructed}'");
                }
            })).ToArray();

            Task.WaitAll(readers);
            stop.Cancel();
            writer.Wait();

            Assert.True(failures.IsEmpty,
                "Concurrent format/rebuild race:" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(5)));
        }
    }
}
