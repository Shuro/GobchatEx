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

using Gobchat.Core.Runtime;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Gobchat.Core.Util
{
    internal static class DownloadHelper
    {
        public enum DownloadResult
        {
            CompletedSuccessfully,
            Canceled
        }

        // A single shared client; the User-Agent is set per request so the instance stays stateless.
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        ///
        /// </summary>
        /// <param name="downloadUrl"></param>
        /// <param name="destinationFile"></param>
        /// <param name="progressMonitor"></param>
        /// <returns></returns>
        /// <exception cref="DownloadFailedException"></exception>
        public static DownloadResult DownloadFileFromGithub(string downloadUrl, string destinationFile, IProgressMonitor progressMonitor)
        {
            var cancellationToken = progressMonitor.GetCancellationToken();
            if (cancellationToken.IsCancellationRequested)
                return DownloadResult.Canceled;

            if (!Directory.Exists(Path.GetDirectoryName(destinationFile)))
                throw new DirectoryNotFoundException(Path.GetDirectoryName(destinationFile));

            progressMonitor.Progress = 0d;
            progressMonitor.StatusText = Resources.Core_Util_DownloadHelper_Waiting;
            progressMonitor.Log(Resources.Core_Util_DownloadHelper_Prepare);

            try
            {
                DownloadFileAsync(downloadUrl, destinationFile, progressMonitor, cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                progressMonitor.Log(Resources.Core_Util_DownloadHelper_Canceled);
                return DownloadResult.Canceled;
            }
            catch (Exception ex)
            {
                throw new DownloadFailedException(ex.Message, ex);
            }

            return cancellationToken.IsCancellationRequested ? DownloadResult.Canceled : DownloadResult.CompletedSuccessfully;
        }

        private static async Task DownloadFileAsync(string downloadUrl, string destinationFile, IProgressMonitor progressMonitor, CancellationToken cancellationToken)
        {
            var currentVersion = GobchatContext.ApplicationVersion;

            using (var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl))
            {
                request.Headers.Add("User-Agent", $"GobchatEx v{currentVersion}");

                progressMonitor.Log(StringFormat.Format(Resources.Core_Util_DownloadHelper_Connecting, downloadUrl));

                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    response.EnsureSuccessStatusCode();

                    var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                    long bytesReceived = 0;

                    using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
                    using (var fileStream = new FileStream(destinationFile, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        var buffer = new byte[81920];
                        int read;
                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                            bytesReceived += read;

                            progressMonitor.Progress = totalBytes > 0 ? (double)bytesReceived / totalBytes : 0d;
                            progressMonitor.StatusText = StringFormat.Format(Resources.Core_Util_DownloadHelper_Download, bytesReceived, totalBytes);
                        }
                    }

                    progressMonitor.Progress = 1d;
                    progressMonitor.StatusText = Resources.Core_Util_DownloadHelper_Complete;
                    progressMonitor.Log(Resources.Core_Util_DownloadHelper_Complete);
                }
            }
        }
    }
}
