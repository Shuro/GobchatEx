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

namespace Gobchat.Core.Chat
{
    public static class ChatUtil
    {
        public static (string name, string server) SplitCharacterName(string name)
        {
            var sIdx = name.IndexOf("[", StringComparison.InvariantCultureIgnoreCase);
            if (sIdx >= 0)
            {
                var eIdx = name.LastIndexOf("]", StringComparison.InvariantCultureIgnoreCase);
                if (eIdx > sIdx)
                    return (name.Substring(0, sIdx).Trim(), name.Substring(sIdx + 1, eIdx - sIdx - 1).Trim());
            }
            return (name, null);
        }

        public static string StripServerName(string name)
        {
            var sIdx = name.IndexOf("[", StringComparison.InvariantCultureIgnoreCase);
            if (sIdx >= 0)
                return name.Substring(0, sIdx).Trim();
            return name;
        }

        // FFXIV re-encodes the "fancy"/boxed uppercase letters, digits and punctuation a player pastes
        // (e.g. Mathematical Sans-Serif Bold "FLUX") into its Private Use Area, laid out contiguously
        // with ASCII so the boxed 'A' lands at U+E071 (see FFXIVUnicodes.Raid_A). That means ASCII
        // 0x30-0x5A ('0'..'Z') maps to U+E060-U+E08A. No standard font has these glyphs, so they show as
        // tofu in the overlay; map them back to plain ASCII for display - which also lets keyword/mention/
        // trigger-group rules match them. Allocation-free unless a boxed glyph is actually present.
        public static string MapBoxedGlyphsToAscii(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            char[] buffer = null;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c >= '' && c <= '')
                {
                    buffer ??= text.ToCharArray();
                    buffer[i] = (char)(c - 0xE030); // U+E060->0x30 ('0') ... U+E071->0x41 ('A') ... U+E08A->0x5A ('Z')
                }
            }
            return buffer == null ? text : new string(buffer);
        }
    }
}
