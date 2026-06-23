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
using System.Text;
using System.Text.RegularExpressions;

namespace Gobchat.Core.UI
{
    /// <summary>
    /// Turns the Keep-a-Changelog markdown carried in a release's notes into readable plain text for the
    /// update dialog's <see cref="System.Windows.Forms.TextBox"/>. That box renders neither markdown nor
    /// lone-LF line breaks, so the raw notes otherwise collapse into one run-on paragraph littered with
    /// literal '##'/'**'/'-' markers. This strips the common section and inline markdown and normalizes
    /// line endings to CRLF, so each heading and bullet lands on its own line. It is intentionally a tiny
    /// best-effort converter (no full markdown parser): patch notes only use a small, known subset.
    /// </summary>
    public static class PatchNotesFormatter
    {
        // [label](url) -> label : drop the URL, which is noise in a small plain-text box.
        private static readonly Regex Link = new Regex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.Compiled);
        // `code` -> code
        private static readonly Regex Code = new Regex(@"`([^`]+)`", RegexOptions.Compiled);
        // **bold** / __bold__ -> bold
        private static readonly Regex Bold = new Regex(@"(\*\*|__)(.+?)\1", RegexOptions.Compiled);
        // *italic* -> italic (after bold is removed; the leading (?!\s) keeps "* " list markers safe).
        private static readonly Regex Italic = new Regex(@"(?<!\*)\*(?!\s)(.+?)(?<!\s)\*(?!\*)", RegexOptions.Compiled);
        // Leading ATX heading markers: "### Added" -> "Added".
        private static readonly Regex Heading = new Regex(@"^\s{0,3}#{1,6}\s+(.*)$", RegexOptions.Compiled);
        // List item: "- foo" / "* foo" / "+ foo" -> bullet.
        private static readonly Regex Bullet = new Regex(@"^\s*[-*+]\s+(.*)$", RegexOptions.Compiled);
        private static readonly Regex CollapseBlankRuns = new Regex(@"(\r\n){3,}", RegexOptions.Compiled);

        /// <summary>
        /// Returns <paramref name="markdown"/> as CRLF-delimited plain text suitable for a WinForms
        /// <c>TextBox</c>. Null/blank input yields an empty string.
        /// </summary>
        public static string FormatForDisplay(string? markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            var lines = markdown!.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var sb = new StringBuilder();

            foreach (var rawLine in lines)
            {
                var line = InlineToPlainText(rawLine.TrimEnd());

                var heading = Heading.Match(line);
                if (heading.Success)
                {
                    // "[2.0.0] - 2026.06.13" -> "2.0.0 - 2026.06.13"; precede a heading with a blank line
                    // for separation (unless it's the very first line).
                    var text = heading.Groups[1].Value.Replace("[", "").Replace("]", "").Trim();
                    if (sb.Length > 0)
                        sb.Append("\r\n");
                    sb.Append(text).Append("\r\n");
                    continue;
                }

                var bullet = Bullet.Match(line);
                if (bullet.Success)
                {
                    sb.Append("  • ").Append(bullet.Groups[1].Value).Append("\r\n");
                    continue;
                }

                sb.Append(line).Append("\r\n");
            }

            // Trim only surrounding blank lines, not leading whitespace - that would eat a first
            // line's bullet indentation.
            return CollapseBlankRuns.Replace(sb.ToString(), "\r\n\r\n").Trim('\r', '\n');
        }

        private static string InlineToPlainText(string line)
        {
            line = Link.Replace(line, "$1");
            line = Code.Replace(line, "$1");
            line = Bold.Replace(line, "$2");
            line = Italic.Replace(line, "$1");
            return line;
        }
    }
}
