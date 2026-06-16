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
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gobchat.Module.UI
{
    /// <summary>
    /// Maps a virtual-host request path (served from <c>https://gobchat.localhost/</c>) to a local
    /// file under the UI resource folder, applying the two layout rules the flat folder mapping
    /// cannot: the <c>module</c>&#8594;<c>modules</c> rename and the <c>.min</c>/extensionless
    /// preference. Returns <c>null</c> to let the request fall through. Shared by every overlay
    /// browser (chat, system, settings).
    /// </summary>
    internal static class UiResourceResolver
    {
        public static string Resolve(string uiRoot, string requestPath)
        {
            if (string.IsNullOrEmpty(uiRoot) || string.IsNullOrEmpty(requestPath))
                return null;

            var rel = requestPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            if (rel.Length == 0)
                return null;

            var modulePrefix = "module" + Path.DirectorySeparatorChar;
            if (rel.StartsWith(modulePrefix, StringComparison.OrdinalIgnoreCase))
                rel = "modules" + Path.DirectorySeparatorChar + rel.Substring(modulePrefix.Length);

            // Sound files live in resources/sounds — a sibling of the UI root (resources/ui), not
            // under it — yet the page requests them as /sounds/<file> (the overlay's mention alert
            // and the Mentions test button both resolve to https://<host>/sounds/<file>). Serve
            // those from the app resources root, scoped so the request can only reach resources/sounds.
            var soundsPrefix = "sounds" + Path.DirectorySeparatorChar;
            if (rel.StartsWith(soundsPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var soundsRoot = Path.GetFullPath(Path.Combine(uiRoot, "..", "sounds")) + Path.DirectorySeparatorChar;
                var soundPath = Path.GetFullPath(Path.Combine(uiRoot, "..", rel));
                if (soundPath.StartsWith(soundsRoot, StringComparison.OrdinalIgnoreCase) && File.Exists(soundPath))
                    return soundPath;
                return null;
            }

            var basePath = Path.GetFullPath(Path.Combine(uiRoot, rel));
            if (!basePath.StartsWith(uiRoot, StringComparison.OrdinalIgnoreCase))
                return null; // outside the UI folder

            var ext = Path.GetExtension(basePath);
            var candidates = new List<string>();
            if (ext.Length == 0)
            {
                candidates.Add(basePath + ".min.js");
                candidates.Add(basePath + ".js");
            }
            else if (ext.Equals(".js", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.ChangeExtension(basePath, ".min.js"));
                candidates.Add(basePath);
            }
            else if (ext.Equals(".css", StringComparison.OrdinalIgnoreCase))
            {
                candidates.Add(Path.ChangeExtension(basePath, ".min.css"));
                candidates.Add(basePath);
            }
            else
            {
                candidates.Add(basePath);
            }

            return candidates.FirstOrDefault(File.Exists);
        }
    }
}
