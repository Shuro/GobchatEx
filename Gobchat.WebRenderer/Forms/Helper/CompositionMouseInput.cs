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

using Microsoft.Web.WebView2.Core;
using NLog;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gobchat.UI.Forms.Helper
{
    /// <summary>
    /// Translates Win32 mouse messages into <see cref="CoreWebView2CompositionController.SendMouseInput"/>.
    /// Composition hosting delivers no input automatically, so every composition-hosted overlay form
    /// (chat, system, settings) must forward mouse messages to its windowless WebView2 while
    /// interactive. Shared so that subtle bit-twiddling lives in one place.
    /// </summary>
    internal static class CompositionMouseInput
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Returns <c>true</c> when the message was a mouse message and has been consumed.
        /// <paramref name="trackingMouse"/> carries the WM_MOUSELEAVE tracking state between calls.
        /// </summary>
        public static bool ForwardMouseMessage(CoreWebView2CompositionController controller, Control form, ref Message m, ref bool trackingMouse)
        {
            if (controller == null)
                return false;

            CoreWebView2MouseEventKind kind;
            switch (m.Msg)
            {
                case 0x0200: kind = CoreWebView2MouseEventKind.Move; break;
                case 0x0201: kind = CoreWebView2MouseEventKind.LeftButtonDown; break;
                case 0x0202: kind = CoreWebView2MouseEventKind.LeftButtonUp; break;
                case 0x0203: kind = CoreWebView2MouseEventKind.LeftButtonDoubleClick; break;
                case 0x0204: kind = CoreWebView2MouseEventKind.RightButtonDown; break;
                case 0x0205: kind = CoreWebView2MouseEventKind.RightButtonUp; break;
                case 0x0206: kind = CoreWebView2MouseEventKind.RightButtonDoubleClick; break;
                case 0x0207: kind = CoreWebView2MouseEventKind.MiddleButtonDown; break;
                case 0x0208: kind = CoreWebView2MouseEventKind.MiddleButtonUp; break;
                case 0x0209: kind = CoreWebView2MouseEventKind.MiddleButtonDoubleClick; break;
                case 0x020A: kind = CoreWebView2MouseEventKind.Wheel; break;
                case 0x020E: kind = CoreWebView2MouseEventKind.HorizontalWheel; break;
                case 0x02A3: kind = CoreWebView2MouseEventKind.Leave; break;
                default: return false;
            }

            long w = m.WParam.ToInt64();
            long l = m.LParam.ToInt64();
            var keys = (CoreWebView2MouseEventVirtualKeys)((uint)w & 0x7F); // MK_* flags map 1:1
            uint mouseData = 0;
            Point pt;

            // From here on we touch the (cross-process) WebView2 composition controller. Those COM
            // calls can throw when the controller is in a transient bad state - e.g. while the host
            // window is being dragged mostly off-screen or torn down. WndProc is an OS->managed
            // callback, so an exception escaping here fail-fasts the process with
            // STATUS_FATAL_USER_CALLBACK_EXCEPTION (0xC000041D). Swallow + log instead, and still
            // report the message as consumed so it isn't re-dispatched.
            try
            {
                if (kind == CoreWebView2MouseEventKind.Wheel || kind == CoreWebView2MouseEventKind.HorizontalWheel)
                {
                    mouseData = (uint)(short)((w >> 16) & 0xFFFF);                                       // wheel delta
                    pt = form.PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF))); // screen -> client
                }
                else if (kind == CoreWebView2MouseEventKind.Leave)
                {
                    trackingMouse = false;
                    pt = Point.Empty;
                }
                else
                {
                    pt = new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)); // already client
                    if (kind == CoreWebView2MouseEventKind.Move && !trackingMouse)
                    {
                        var tme = new NativeMethods.TRACKMOUSEEVENT
                        {
                            cbSize = Marshal.SizeOf<NativeMethods.TRACKMOUSEEVENT>(),
                            dwFlags = NativeMethods.TME_LEAVE,
                            hwndTrack = form.Handle,
                        };
                        NativeMethods.TrackMouseEvent(ref tme);
                        trackingMouse = true;
                    }
                    if (kind == CoreWebView2MouseEventKind.LeftButtonDown ||
                        kind == CoreWebView2MouseEventKind.RightButtonDown ||
                        kind == CoreWebView2MouseEventKind.MiddleButtonDown)
                    {
                        // Route keyboard to the page when the user clicks into it.
                        controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
                    }
                }

                controller.SendMouseInput(kind, keys, mouseData, pt);
            }
            catch (Exception ex)
            {
                logger.Warn(ex, "Failed to forward a mouse message to the WebView2 composition controller");
            }
            return true;
        }
    }
}
