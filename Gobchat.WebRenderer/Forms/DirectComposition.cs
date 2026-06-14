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
using System.Runtime.InteropServices;

namespace Gobchat.UI.Forms
{
    /// <summary>
    /// Minimal DirectComposition interop for WebView2 composition hosting.
    ///
    /// WebView2's composition controller does not own a window; it renders into a visual we
    /// supply via <c>CoreWebView2CompositionController.RootVisualTarget</c>. We build the
    /// smallest possible DComp tree: device -&gt; target (bound to our HWND) -&gt; root visual,
    /// and hand that root visual to WebView2. DComp then composites the page (including its
    /// alpha) straight onto the window, which is what lets a transparent page show the
    /// desktop/game behind it.
    ///
    /// Only the vtable slots up to the methods we call are declared; the rest of each COM
    /// interface is intentionally truncated. The declared method ORDER must match dcomp.h
    /// exactly or the calls land on the wrong slot.
    /// </summary>
    internal static class DComp
    {
        public static readonly Guid IID_IDCompositionDevice =
            new Guid("C37EA93A-E7AA-450D-B16F-9746CB0407F3");

        [DllImport("dcomp.dll")]
        public static extern int DCompositionCreateDevice(
            IntPtr dxgiDevice,
            in Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IDCompositionDevice dcompositionDevice);
    }

    [ComImport]
    [Guid("C37EA93A-E7AA-450D-B16F-9746CB0407F3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDCompositionDevice
    {
        // Slots must stay in dcomp.h order. We only call Commit / CreateTargetForHwnd /
        // CreateVisual; the two methods before CreateTargetForHwnd are declared so the vtable
        // offsets line up.
        void Commit();

        void WaitForCommitCompletion();

        void GetFrameStatistics(IntPtr statistics);

        IDCompositionTarget CreateTargetForHwnd(
            IntPtr hwnd,
            [MarshalAs(UnmanagedType.Bool)] bool topmost);

        IDCompositionVisual CreateVisual();

        // remaining IDCompositionDevice methods intentionally omitted
    }

    [ComImport]
    [Guid("eacdd04c-117e-4e17-88f4-d1b12b0e3d89")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDCompositionTarget
    {
        void SetRoot(IDCompositionVisual visual);
    }

    [ComImport]
    [Guid("4d93059d-097b-4651-9a60-f0f25116e2f3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IDCompositionVisual
    {
        // Opaque: we never call methods on the visual directly, we only pass the pointer to
        // IDCompositionTarget.SetRoot and to RootVisualTarget.
    }
}
