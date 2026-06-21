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

'use strict'

// Settings-window easter egg: the Konami code (up up down down left right left right b a) turns the
// whole title bar into a moving rainbow and plays a short chiptune melody. The key-sequence
// detector is kept as a pure, dependency-free export so it stays unit-testable (no DOM / browser
// globals are touched until installKonamiEasterEgg runs).

// Keys are normalized with toLowerCase(): "ArrowUp" -> "arrowup", "B"/"b" -> "b", so Shift/Caps and
// the event's native casing don't matter.
const KONAMI_SEQUENCE: ReadonlyArray<string> = [
    'arrowup', 'arrowup', 'arrowdown', 'arrowdown',
    'arrowleft', 'arrowright', 'arrowleft', 'arrowright',
    'b', 'a',
]

const RAINBOW_CLASS = 'gx-titlebar--rainbow'

// Class toggled on each About-page hint glyph as the code is typed in order (styled gold in config.scss).
const HINT_LIT_CLASS = 'is-lit'

// Tracks the most recent key presses and reports when they end with the Konami sequence. A rolling
// buffer (rather than an index/progress counter) is used so overlapping restarts are handled for free
// — e.g. an extra leading "up" still lets the following full sequence match.
export class KonamiSequenceDetector {
    private readonly buffer: string[] = []

    // Returns true exactly when the last presses complete the sequence. On a match the buffer is
    // cleared, so re-triggering requires entering the whole sequence again.
    push(key: string): boolean {
        this.buffer.push(key.toLowerCase())
        if (this.buffer.length > KONAMI_SEQUENCE.length)
            this.buffer.shift()

        if (this.buffer.length === KONAMI_SEQUENCE.length
            && KONAMI_SEQUENCE.every((k, i) => this.buffer[i] === k)) {
            this.buffer.length = 0
            return true
        }
        return false
    }
}

// Tracks how far the most recent presses have advanced through the sequence, so the About-page hint
// can light each glyph gold as it's pressed in order. Where KonamiSequenceDetector only reports a
// completed match, this reports the matched-prefix length (0..length) for partial progress. Kept pure
// (no DOM) so it stays unit-testable alongside the detector.
export class KonamiProgressTracker {
    private matched = 0

    // Returns how many leading keys of the sequence are now satisfied. A correct next key advances by
    // one; completing the sequence reports the full length, then the next press starts a fresh run. A
    // wrong key resets — but to 1 (not 0) when the key is itself the sequence's first key, so fumbling
    // an extra first press doesn't read as a dead reset.
    press(key: string): number {
        if (this.matched >= KONAMI_SEQUENCE.length)
            this.matched = 0
        const k = key.toLowerCase()
        if (k === KONAMI_SEQUENCE[this.matched])
            this.matched++
        else
            this.matched = k === KONAMI_SEQUENCE[0] ? 1 : 0
        return this.matched
    }
}

// The settings-window tune: a melody transcribed from a Hooktheory/ChordChord clip in B♭ minor over
// the iv9–♭VII–v7–i7 loop (e♭m9 · A♭add9 · fm7 · b♭m7, ×2) — 8 bars of 4/4. Each entry is
// [frequency Hz, start beat (0-based), duration beats]; notes are scheduled at absolute beat times so
// the clip's rests and syncopation survive. Frequencies are equal-temperament (A4 = 440); the source
// gives only scale degree + relative octave, so the absolute register is a choice — scale degree 1
// (B♭) is pinned to B♭4 to match the old riff's range. BEAT sets the tempo.
const BEAT = 0.48 // seconds per quarter-note beat (~125 BPM)
const MELODY: ReadonlyArray<readonly [number, number, number]> = [
    // bar 1 — e♭m9
    [698.46, 0, 0.75], [698.46, 0.75, 0.75], [622.25, 1.5, 1],                                              // F5 F5 Eb5
    [415.30, 3, 0.25], [466.16, 3.25, 0.25], [523.25, 3.5, 0.25], [466.16, 3.75, 0.25],                     // Ab4 Bb4 C5 Bb4
    // bar 2 — A♭add9
    [622.25, 4, 0.75], [622.25, 4.75, 0.75], [554.37, 5.5, 0.5], [523.25, 6, 0.25], [466.16, 6.25, 0.75],   // Eb5 Eb5 Db5 C5 Bb4
    [415.30, 7, 0.25], [466.16, 7.25, 0.25], [554.37, 7.5, 0.25], [466.16, 7.75, 0.25],                     // Ab4 Bb4 Db5 Bb4
    // bar 3 — fm7
    [554.37, 8, 0.75], [622.25, 8.75, 0.75], [523.25, 9.5, 0.25], [466.16, 9.75, 0.75],                     // Db5 Eb5 C5 Bb4
    [415.30, 10.5, 0.5], [415.30, 11.5, 0.5],                                                               // Ab4 Ab4
    // bar 4 — b♭m7
    [622.25, 12, 1], [554.37, 13, 1],                                                                       // Eb5 Db5
    [415.30, 15, 0.25], [466.16, 15.25, 0.25], [554.37, 15.5, 0.25], [466.16, 15.75, 0.25],                 // Ab4 Bb4 Db5 Bb4
    // bar 5 — e♭m9
    [698.46, 16, 0.75], [698.46, 16.75, 0.75], [622.25, 17.5, 1.25],                                        // F5 F5 Eb5
    [415.30, 19, 0.25], [466.16, 19.25, 0.25], [523.25, 19.5, 0.25], [466.16, 19.75, 0.25],                 // Ab4 Bb4 C5 Bb4
    // bar 6 — A♭add9
    [830.61, 20, 0.75], [523.25, 20.75, 0.5], [554.37, 21.25, 0.5], [523.25, 21.75, 0.25], [466.16, 22, 1], // Ab5 C5 Db5 C5 Bb4
    [415.30, 23, 0.25], [466.16, 23.25, 0.25], [523.25, 23.5, 0.25], [466.16, 23.75, 0.25],                 // Ab4 Bb4 C5 Bb4
    // bar 7 — fm7
    [554.37, 24, 0.75], [622.25, 24.75, 0.75], [523.25, 25.5, 0.75], [466.16, 26.25, 0.25],                 // Db5 Eb5 C5 Bb4
    [415.30, 26.5, 0.5], [415.30, 27.5, 0.5],                                                               // Ab4 Ab4
    // bar 8 — b♭m7
    [622.25, 28, 0.5], [554.37, 28.5, 0.5], [554.37, 29, 1],                                                // Eb5 Db5 Db5
]

// True while a tune is playing, so re-triggering the code mid-melody is ignored instead of layering a
// second, overlapping playback. Cleared when the context is closed (or on failure).
let tunePlaying = false

// Synthesizes MELODY once through with the Web Audio API and then releases the audio device. Called
// from a keydown handler, so the AudioContext is user-activated (not autoplay-blocked). A re-trigger
// while it's still playing is a no-op (see tunePlaying). Any failure is swallowed (logged) — the easter
// egg must never disturb the settings window.
function playTune(): void {
    if (tunePlaying)
        return
    try {
        const Ctx = window.AudioContext ?? (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext
        if (!Ctx)
            return

        tunePlaying = true
        const ctx = new Ctx()
        void ctx.resume?.()

        const master = ctx.createGain()
        master.gain.value = 0.04 // keep the surprise gentle
        master.connect(ctx.destination)

        const t0 = ctx.currentTime + 0.02
        let end = t0
        for (const [freq, startBeat, durBeat] of MELODY) {
            const t = t0 + startBeat * BEAT
            const dur = durBeat * BEAT
            const osc = ctx.createOscillator()
            const gain = ctx.createGain()
            osc.type = 'square' // chiptune timbre
            osc.frequency.value = freq

            // Tiny attack/release ramps so each note doesn't click.
            gain.gain.setValueAtTime(0, t)
            gain.gain.linearRampToValueAtTime(1, t + 0.01)
            gain.gain.setValueAtTime(1, Math.max(t + 0.01, t + dur - 0.02))
            gain.gain.linearRampToValueAtTime(0, t + dur)

            osc.connect(gain)
            gain.connect(master)
            osc.start(t)
            osc.stop(t + dur)
            if (t + dur > end)
                end = t + dur
        }

        // Close the context shortly after the last note ends to free the audio device, and clear the
        // guard so the tune can be triggered again.
        window.setTimeout(() => {
            tunePlaying = false
            void ctx.close?.()
        }, (end - ctx.currentTime + 0.2) * 1000)
    } catch (e) {
        tunePlaying = false
        console.error('[konami] failed to play tune', e)
    }
}

// Wires the easter egg onto the given title-bar element. A no-op if the element is missing.
export function installKonamiEasterEgg(barEl: HTMLElement | null | undefined): void {
    if (!barEl)
        return

    const detector = new KonamiSequenceDetector()
    window.addEventListener('keydown', (ev: KeyboardEvent) => {
        if (!detector.push(ev.key))
            return
        barEl.classList.toggle(RAINBOW_CLASS) // re-entering toggles the rainbow back off
        playTune()
    })
}

// Companion to installKonamiEasterEgg for the About page: lights each hint glyph gold one-by-one as the
// Konami code is typed in order (and clears them on a wrong key). The hint element holds one child
// element per glyph, in sequence order. A no-op if the element is missing or has no glyphs. Listens on
// window (like the title-bar egg) so the code registers while the About page is shown regardless of focus.
export function installKonamiHint(hintEl: HTMLElement | null | undefined): void {
    if (!hintEl)
        return

    const glyphs = Array.from(hintEl.children) as HTMLElement[]
    if (glyphs.length === 0)
        return

    const tracker = new KonamiProgressTracker()
    window.addEventListener('keydown', (ev: KeyboardEvent) => {
        const lit = tracker.press(ev.key)
        glyphs.forEach((glyph, i) => glyph.classList.toggle(HINT_LIT_CLASS, i < lit))
    })
}
