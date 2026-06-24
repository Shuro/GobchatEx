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

using Gobchat.UI.Web;
using Gobchat.UI.Web.JavascriptEvents;
using Xunit;

namespace Gobchat.App.Tests.Core.UI
{
    /// <summary>
    /// <see cref="JavascriptBuilder.BuildCustomEventDispatcher"/> concatenates the event name straight
    /// into a snippet that is then run in the page. The name must be emitted as a proper JS string
    /// literal so a name carrying a quote/backslash/control character can neither break the script
    /// (silent failure) nor inject JS (CWE-94). These tests pin that escaping (BRG-4).
    /// </summary>
    public sealed class JavascriptBuilderTests
    {
        // Minimal concrete JSEvent: only the name (and serialized detail) matter here.
        private sealed class FakeEvent : JSEvent
        {
            public FakeEvent(string name) : base(name) { }

            public int Value { get; set; } = 1;
        }

        [Fact]
        public void BuildCustomEventDispatcher_EmitsOrdinaryNameAsQuotedLiteral()
        {
            var builder = new JavascriptBuilder();

            var script = builder.BuildCustomEventDispatcher(new FakeEvent("ChatMessagesEvent"));

            // Double-quoted JSON string literal, no leftover single quotes around the name.
            Assert.Contains("new CustomEvent(\"ChatMessagesEvent\", {", script);
            Assert.DoesNotContain("CustomEvent('", script);
        }

        [Theory]
        [InlineData("evt'); alert(1); ('")] // single-quote breakout attempt
        [InlineData("evt\"); alert(1); (\"")] // double-quote breakout attempt
        [InlineData("evt\\")] // trailing backslash
        [InlineData("evt\n\t")] // control characters
        public void BuildCustomEventDispatcher_EscapesNameSoItCannotBreakTheScript(string name)
        {
            var builder = new JavascriptBuilder();

            var script = builder.BuildCustomEventDispatcher(new FakeEvent(name));

            // The raw, unescaped name must never appear verbatim between the dispatch parens; whatever is
            // emitted is a single escaped JSON literal, so the surrounding script structure stays intact.
            var literal = Newtonsoft.Json.JsonConvert.ToString(name);
            Assert.Contains($"new CustomEvent({literal}, {{", script);
            Assert.StartsWith("document.dispatchEvent(new CustomEvent(", script);
            Assert.EndsWith("}));", script);
        }

        [Fact]
        public void BuildCustomEventDispatcher_IsReusableAcrossCalls()
        {
            // The builder clears its shared StringBuilder after each call; a second build must not carry
            // the first event's bytes.
            var builder = new JavascriptBuilder();

            builder.BuildCustomEventDispatcher(new FakeEvent("First"));
            var second = builder.BuildCustomEventDispatcher(new FakeEvent("Second"));

            Assert.Contains("\"Second\"", second);
            Assert.DoesNotContain("First", second);
        }
    }
}
