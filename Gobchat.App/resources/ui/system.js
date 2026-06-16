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
// The "system" overlay: a fullscreen, transparent, click-through window on the primary monitor that
// hosts the greeter splash and login/logout notifications. It is driven solely by the backend's
// ConnectionStateEvent (AppModuleSystemOverlay) — state ordinals mirror ConnectionState
// (0 init, 1 connected, 2 not found, 3 searching, 4 no access). No GobchatAPI/config is needed here.
'use strict';
(function () {
    // The first event is the backend's initial state sync; don't toast it (e.g. don't announce a
    // "logged in" for a character that was already logged in when GobchatEx started).
    let previousPlayer = null;
    let seenFirstEvent = false;
    function updateGreeter(state, player) {
        const greeter = document.getElementById("gob-greeter");
        const status = document.getElementById("gob-greeter-status");
        if (!greeter || !status)
            return;
        let text;
        switch (state) {
            case 1: // Connected — FFXIV attached, greeter no longer needed
                text = null;
                break;
            case 2: // NotFound
                text = "Waiting for Final Fantasy XIV…";
                break;
            case 3: // Searching
                text = "Searching FFXIV Process…";
                break;
            case 4: // NoAccess (FFXIV more elevated than us)
                text = "Final Fantasy XIV is running with higher privileges — restart GobchatEx as administrator";
                break;
            default: // 0 NotInitialized / unknown
                text = "Starting GobchatEx…";
                break;
        }
        if (text === null) {
            greeter.classList.add("gob-hidden");
        }
        else {
            status.textContent = text;
            greeter.classList.remove("gob-hidden");
        }
    }
    function notifyPlayerChange(player) {
        if (!seenFirstEvent) {
            previousPlayer = player;
            seenFirstEvent = true;
            return;
        }
        if (player === previousPlayer)
            return;
        let message;
        if (previousPlayer === null && player !== null)
            message = `${player} logged in`;
        else if (previousPlayer !== null && player === null)
            message = `${previousPlayer} logged out`;
        else
            message = `Switched to ${player}`;
        previousPlayer = player;
        showToast(message);
    }
    function showToast(message) {
        const container = document.getElementById("gob-notifications");
        if (!container)
            return;
        const toast = document.createElement("div");
        toast.className = "gob-toast";
        toast.textContent = message;
        container.appendChild(toast);
        // Force the initial (hidden) state to apply before transitioning in.
        requestAnimationFrame(() => toast.classList.add("gob-toast--show"));
        window.setTimeout(() => {
            toast.classList.remove("gob-toast--show");
            window.setTimeout(() => toast.remove(), 400);
        }, 4000);
    }
    document.addEventListener("ConnectionStateEvent", function (event) {
        updateGreeter(event.detail.state, event.detail.player);
        notifyPlayerChange(event.detail.player);
    });
    // One-off toast pushed on demand (e.g. the Debug settings page's "Trigger notification" button).
    document.addEventListener("ShowNotificationEvent", function (event) {
        showToast(event.detail.message);
    });
})();
