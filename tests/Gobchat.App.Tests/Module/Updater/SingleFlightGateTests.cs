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

using Gobchat.Module.Updater.Internal;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Gobchat.App.Tests.Module.Updater
{
    // WHY: a manual About-page "Check for updates" click must never overlap the startup check (or a prior
    // manual click) — overlapping runs risk a double download. The gate is the single-flight guard that
    // makes the second concurrent caller a no-op, so these tests pin that contract.
    public sealed class SingleFlightGateTests
    {
        [Fact]
        public void TryEnter_AdmitsTheFirstCaller_AndBlocksAConcurrentSecond()
        {
            var gate = new SingleFlightGate();

            Assert.True(gate.TryEnter());   // first run starts
            Assert.True(gate.IsBusy);
            Assert.False(gate.TryEnter());  // a concurrent caller is turned away (would return "Busy")
        }

        [Fact]
        public void Exit_ReleasesTheGate_SoTheNextCheckCanRun()
        {
            var gate = new SingleFlightGate();

            Assert.True(gate.TryEnter());
            gate.Exit();

            Assert.False(gate.IsBusy);
            Assert.True(gate.TryEnter());   // startup check finished, an on-demand check may now run
        }

        [Fact]
        public async Task TryEnter_UnderContention_AdmitsExactlyOneCaller()
        {
            var gate = new SingleFlightGate();
            using var start = new ManualResetEventSlim(false);

            var tasks = Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
            {
                start.Wait();
                return gate.TryEnter();
            })).ToArray();

            start.Set();
            var results = await Task.WhenAll(tasks);

            Assert.Equal(1, results.Count(entered => entered));
        }
    }
}
