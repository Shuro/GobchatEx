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

import { describe, expect, it } from 'vitest'
import { KonamiProgressTracker, KonamiSequenceDetector } from '../../src/Gobchat.App/resources/ui/modules/KonamiEasterEgg'

// The detector is the gate for the whole easter egg: it must fire on exactly the Konami code and
// nothing else. WHY each case matters is called out per test — a detector that fired loosely would
// trigger the rainbow/tune during ordinary typing, and one that couldn't recover from a typo would
// make the egg feel broken (a single wrong key would lock you out until reload).

const KONAMI = [
    'ArrowUp', 'ArrowUp', 'ArrowDown', 'ArrowDown',
    'ArrowLeft', 'ArrowRight', 'ArrowLeft', 'ArrowRight',
    'b', 'a',
] as const

// Feeds all but the last key (none of which may complete the sequence) and returns the result of the
// final key, so each test asserts the trigger happens on the 10th press and not before.
function pushAll(detector: KonamiSequenceDetector, keys: readonly string[]): boolean {
    let result = false
    for (const key of keys)
        result = detector.push(key)
    return result
}

describe('KonamiSequenceDetector', () => {
    it('fires only once the full sequence is entered, not on any earlier key', () => {
        const detector = new KonamiSequenceDetector()
        for (let i = 0; i < KONAMI.length - 1; i++)
            expect(detector.push(KONAMI[i])).toBe(false) // no premature trigger
        expect(detector.push(KONAMI[KONAMI.length - 1])).toBe(true)
    })

    it('ignores key casing so Shift/Caps on the B/A keys still completes it', () => {
        const detector = new KonamiSequenceDetector()
        const shouted = KONAMI.slice(0, -2).concat(['B', 'A'])
        expect(pushAll(detector, shouted)).toBe(true)
    })

    it('does not fire when a key in the middle is wrong', () => {
        const detector = new KonamiSequenceDetector()
        const wrong = [...KONAMI]
        wrong[5] = 'ArrowLeft' // should have been ArrowRight
        expect(pushAll(detector, wrong)).toBe(false)
    })

    it('still completes the sequence after an aborted attempt (typo recovery)', () => {
        const detector = new KonamiSequenceDetector()
        pushAll(detector, ['ArrowUp', 'ArrowDown', 'x']) // garbage first
        expect(pushAll(detector, KONAMI)).toBe(true)
    })

    it('matches the trailing sequence even with extra leading presses (overlap restart)', () => {
        const detector = new KonamiSequenceDetector()
        // An extra leading "ArrowUp" must not block the otherwise-valid sequence that follows.
        expect(pushAll(detector, ['ArrowUp', ...KONAMI])).toBe(true)
    })

    it('requires the whole sequence again to re-trigger after a match', () => {
        const detector = new KonamiSequenceDetector()
        expect(pushAll(detector, KONAMI)).toBe(true)
        expect(detector.push('a')).toBe(false) // a lone key right after a match must not re-fire
        expect(pushAll(detector, KONAMI)).toBe(true)
    })
})

// The progress tracker drives the About-page hint that lights each glyph gold as the code is typed.
// WHY each case matters: the returned count is exactly how many glyphs are shown gold, so the lighting
// would feel broken if it advanced wrong, failed to clear on a mistake, or couldn't be re-entered.
describe('KonamiProgressTracker', () => {
    it('advances one glyph at a time so the hint lights up in order', () => {
        // The Nth correct key must report N, so glyph N lights exactly when it's been pressed.
        const tracker = new KonamiProgressTracker()
        KONAMI.forEach((key, i) => expect(tracker.press(key)).toBe(i + 1))
    })

    it('clears progress on a wrong key so the hint resets', () => {
        // Pressing out of order must un-light the glyphs, not leave a misleading partial trail.
        const tracker = new KonamiProgressTracker()
        tracker.press('ArrowUp')
        tracker.press('ArrowUp')
        expect(tracker.press('x')).toBe(0)
    })

    it('restarts at the first glyph when the wrong key is itself the sequence start', () => {
        // An accidental extra "up" should restart the run at 1, not read as a dead reset to 0.
        const tracker = new KonamiProgressTracker()
        tracker.press('ArrowUp') // 1
        tracker.press('ArrowUp') // 2
        expect(tracker.press('ArrowUp')).toBe(1) // 3rd up misses "down" but is a fresh first match
    })

    it('ignores key casing so Shift/Caps on the B/A keys still advances', () => {
        const tracker = new KonamiProgressTracker()
        const shouted = KONAMI.slice(0, -2).concat(['B', 'A'])
        let progress = 0
        for (const key of shouted)
            progress = tracker.press(key)
        expect(progress).toBe(KONAMI.length)
    })

    it('reports full completion, then starts a fresh run on the next press', () => {
        // After the all-gold flash the next key must begin a new run, so the egg can be re-entered.
        const tracker = new KonamiProgressTracker()
        let progress = 0
        for (const key of KONAMI)
            progress = tracker.press(key)
        expect(progress).toBe(KONAMI.length)
        expect(tracker.press('ArrowUp')).toBe(1)
    })
})
