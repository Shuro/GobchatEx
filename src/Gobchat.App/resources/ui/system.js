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
// (0 init, 1 connected, 2 not found, 3 searching, 4 no access, 5 outdated signatures). All user-facing
// text is localized backend-side and pushed in the event, so no GobchatAPI/config is needed here.
'use strict';
(function () {
    // The first event is the backend's initial state sync; don't toast it (e.g. don't announce a
    // "logged in" for a character that was already logged in when GobchatEx started).
    let previousPlayer = null;
    let seenFirstEvent = false;
    // The greeter text is resolved (and localized) by the backend; null means "hide" (connected).
    function updateGreeter(greeterText) {
        const greeter = document.getElementById("gob-greeter");
        const status = document.getElementById("gob-greeter-status");
        if (!greeter || !status)
            return;
        if (!greeterText) {
            greeter.classList.add("gob-hidden");
        }
        else {
            status.textContent = greeterText;
            greeter.classList.remove("gob-hidden");
        }
    }
    // The greeter's close (X) button quits the whole app. The host binds a single minimal API
    // (GobchatSystemAPI) to this otherwise GobchatAPI-free overlay just for this; see SystemOverlayBrowserAPI.
    function wireCloseButton() {
        const button = document.getElementById("gob-greeter-close");
        if (!button)
            return;
        button.addEventListener("click", function () {
            const api = window.GobchatSystemAPI;
            api?.closeGobchat();
        });
    }
    // The button's accessible label/tooltip is localized backend-side and arrives with the state event.
    function applyCloseLabel(label) {
        if (!label)
            return;
        const button = document.getElementById("gob-greeter-close");
        if (button) {
            button.setAttribute("aria-label", label);
            button.setAttribute("title", label);
        }
    }
    function notifyPlayerChange(detail) {
        const player = detail.player;
        if (!seenFirstEvent) {
            previousPlayer = player;
            seenFirstEvent = true;
            return;
        }
        if (player === previousPlayer)
            return;
        let message;
        if (previousPlayer === null && player !== null)
            message = detail.notifyLogin.replace("{0}", player);
        else if (previousPlayer !== null && player === null)
            message = detail.notifyLogout.replace("{0}", previousPlayer);
        else
            message = detail.notifySwitch.replace("{0}", player ?? "");
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
        updateGreeter(event.detail.greeterText);
        applyCloseLabel(event.detail.closeLabel);
        notifyPlayerChange(event.detail);
    });
    wireCloseButton();
    // One-off toast pushed on demand (e.g. the Debug settings page's "Trigger notification" button).
    document.addEventListener("ShowNotificationEvent", function (event) {
        showToast(event.detail.message);
    });
    // Debug-only: flip the greeter splash on/off so it can be previewed after FFXIV is connected. A real
    // ConnectionStateEvent will re-assert the correct visibility afterwards.
    document.addEventListener("ToggleGreeterEvent", function () {
        const greeter = document.getElementById("gob-greeter");
        if (greeter)
            greeter.classList.toggle("gob-hidden");
    });
})();
