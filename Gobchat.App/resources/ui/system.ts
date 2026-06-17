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

'use strict'

;(function () {
    // The first event is the backend's initial state sync; don't toast it (e.g. don't announce a
    // "logged in" for a character that was already logged in when GobchatEx started).
    let previousPlayer: string | null = null
    let seenFirstEvent = false

    type ConnectionStateDetail = ConnectionStateEvent["detail"]

    // The greeter text is resolved (and localized) by the backend; null means "hide" (connected).
    function updateGreeter(greeterText: string | null): void {
        const greeter = document.getElementById("gob-greeter")
        const status = document.getElementById("gob-greeter-status")
        if (!greeter || !status)
            return

        if (!greeterText) {
            greeter.classList.add("gob-hidden")
        } else {
            status.textContent = greeterText
            greeter.classList.remove("gob-hidden")
        }
    }

    function notifyPlayerChange(detail: ConnectionStateDetail): void {
        const player = detail.player
        if (!seenFirstEvent) {
            previousPlayer = player
            seenFirstEvent = true
            return
        }
        if (player === previousPlayer)
            return

        let message: string
        if (previousPlayer === null && player !== null)
            message = detail.notifyLogin.replace("{0}", player)
        else if (previousPlayer !== null && player === null)
            message = detail.notifyLogout.replace("{0}", previousPlayer)
        else
            message = detail.notifySwitch.replace("{0}", player ?? "")

        previousPlayer = player
        showToast(message)
    }

    function showToast(message: string): void {
        const container = document.getElementById("gob-notifications")
        if (!container)
            return

        const toast = document.createElement("div")
        toast.className = "gob-toast"
        toast.textContent = message
        container.appendChild(toast)

        // Force the initial (hidden) state to apply before transitioning in.
        requestAnimationFrame(() => toast.classList.add("gob-toast--show"))

        window.setTimeout(() => {
            toast.classList.remove("gob-toast--show")
            window.setTimeout(() => toast.remove(), 400)
        }, 4000)
    }

    document.addEventListener("ConnectionStateEvent", function (event: ConnectionStateEvent) {
        updateGreeter(event.detail.greeterText)
        notifyPlayerChange(event.detail)
    } as EventListener)

    // One-off toast pushed on demand (e.g. the Debug settings page's "Trigger notification" button).
    document.addEventListener("ShowNotificationEvent", function (event: CustomEvent<{ message: string }>) {
        showToast(event.detail.message)
    } as EventListener)

    // Debug-only: flip the greeter splash on/off so it can be previewed after FFXIV is connected. A real
    // ConnectionStateEvent will re-assert the correct visibility afterwards.
    document.addEventListener("ToggleGreeterEvent", function () {
        const greeter = document.getElementById("gob-greeter")
        if (greeter)
            greeter.classList.toggle("gob-hidden")
    } as EventListener)
})()
