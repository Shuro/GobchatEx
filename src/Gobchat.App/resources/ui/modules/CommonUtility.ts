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

export function isString(value: unknown): value is string {
    return typeof value === 'string' || value instanceof String
}

export function isNonEmptyString(value: unknown): value is string {
    return isString(value) && (value as string).trim().length > 0
}

export function isBoolean(value: unknown): value is boolean {
    return typeof value === 'boolean' || value instanceof Boolean
}

export function isNumber(value: unknown): value is number {
    return typeof value === 'number' && isFinite(value)
}

export function isFunction(value: unknown): value is Function {
    return typeof value === 'function';
}

export function isArray(value: unknown): value is Array<unknown> {
    return Array.isArray(value)
    //return value && typeof value === 'object' && value.constructor === Array
}

export function isObject(value: unknown): value is object {
    return Object.prototype.toString.call(value) === "[object Object]"
    //return value && typeof value === 'object' && value.constructor === Object;
}

export function isjQuery(value: unknown): value is JQuery {
    return (value && (value instanceof jQuery || value.constructor.prototype.jquery));
}

interface ExtendOptionsDefault {

}
export type ExtendOptions = Partial<ExtendOptionsDefault>
export function extendObjectX<A extends object, B extends object>(target: A, obj1: B): A & B
export function extendObjectX<A extends object, B extends object, C extends object>(target: A, obj1: B, obj2: C): A & B & C
export function extendObjectX<A extends object, B extends object, C extends object, D extends object>(target: A, obj1: B, obj2: C, obj3: D): A & B & C & D
export function extendObjectX<A, B, C, D>(){
    
}

export function extend<A extends object, B extends object>(dst: A, src1: B): A & B
export function extend<A extends object, B extends object, C extends object>(dst: A, src1: B, src2: C): A & B & C
export function extend<A extends object, B extends object, C extends object, D extends object>(dst: A, src1: B, src2: C, src3: D): A & B & C & D
export function extend<A extends object, B extends object, C extends object, D extends object>(dst: A, src1?: B, src2?: C, src3?: D): A & B & C & D {
    const objects = [src1, src2, src3].filter(o => o !== null && o !== undefined)
    return Object.assign(dst, ...objects)
}

export function extendObject<A extends object, B extends object>(base: A, overwrites: B | B[], deepExtend: boolean = false,
    onlyOverwrite: boolean = false, ignoreOverwriteProperty: "non" | "undefined" | "null" | "both" = "both"): A {

    const objects = ([] as B[]).concat(overwrites || [])
    if (objects.length === 0)
        return base
    

    const assign = (() => {
        switch (ignoreOverwriteProperty) {
            case "non":
                return (a: Record<string, any>, b: Record<string, any>, key: string) => a[key] = b[key]
            case "undefined":
                return (a: Record<string, any>, b: Record<string, any>, key: string) => {
                    if (b[key] !== undefined)
                        a[key] = b[key]
                }
            case "null":
                return (a: Record<string, any>, b: Record<string, any>, key: string) => {
                    if (b[key] !== null)
                        a[key] = b[key]
                }
            default:
                return (a: Record<string, any>, b: Record<string, any>, key: string) => {
                    if (b[key] !== undefined && b[key] !== null)
                        a[key] = b[key]
                }
        }
    })()

    if (onlyOverwrite) {
        const keys = Object.keys(base)
        for (let i = 0; i < objects.length; ++i) {
            if (!objects[i])
                continue

            for (let key of keys) {
                if (key in objects[i]) {
                    assign(base, objects[i], key)
                }
            }
        }
    } else {
        if (ignoreOverwriteProperty === "non") {
            for (let i = 0; i < objects.length; ++i) {
                if (!objects[i])
                    continue

                base = Object.assign(base, objects[i])
            }
        } else {
            const keys = Object.keys(base)
            for (let i = 0; i < objects.length; ++i) {
                if (!objects[i])
                    continue

                for (let key of keys) {
                    assign(base, objects[i], key)
                }
            }
        }
    }

    return base
}

export function toFloat(value: string | number | boolean | undefined | null): number | null
export function toFloat(value: string | number | boolean | undefined | null, fallback: number): number 
export function toFloat(value: string | number | boolean | undefined | null, fallback?: number): number | null {
    if (isNumber(value))
        return value

    if (isString(value))
        return parseFloat(value)

    if (isBoolean(value))
        return value ? 1 : 0

    return fallback !== null && fallback !== undefined ? fallback : null
}

export function toInt(value: string | number | boolean | undefined | null): number | null
export function toInt(value: string | number | boolean | undefined | null, fallback: number): number
export function toInt(value: string | number | boolean | undefined | null, fallback?: number): number | null {
    if (isBoolean(value))
        return value ? 1 : 0

    var number: number | null = null

    if (isNumber(value)) {
        number = Math.round(value)
    }else if (isString(value)) {
        number = parseInt(value)
    }

    if (number !== null) {
        if (number > Number.MAX_SAFE_INTEGER)
            return Number.MAX_SAFE_INTEGER
        if (number < Number.MIN_SAFE_INTEGER)
            return Number.MIN_SAFE_INTEGER
        return number
    }

    return fallback !== null && fallback !== undefined ? fallback : null
}

export function extractNumbers(value: string): number[] {
    const result = value.match(/\d+\.\d+|\d+/g)
    if (result === null)
        return []

    return result.map(element => +element) 
}

export function extractFirstNumber(value: string): number | null {
    const result = value.match(/\d+\.\d+|\d+/)
    if (result === null)
        return null
    return +result[0]
}

export function generateId(length: number, exclude?:string[]): string {
    if(!exclude)
        return Math.random().toString(36).substr(2, Math.max(1, length))
    
    while(true){
        const newKey = Math.random().toString(36).substr(2, Math.max(1, length))
        if(exclude.every(e => e !== newKey))
            return newKey
    }
}

/**
 * Merges comma-separated raw input into an existing tag list. Each piece is run through
 * <paramref name="normalize"/>; blanks and case-insensitive duplicates (against the list and within
 * the input) are dropped. Returns a new array when at least one tag was added, otherwise null.
 */
export function mergeTags(existing: string[], raw: string, normalize: (value: string) => string): string[] | null {
    let words = existing
    let changed = false
    for (const part of raw.split(",")) {
        const word = normalize(part)
        if (word.length === 0)
            continue
        if (words.some(w => w.toLowerCase() === word.toLowerCase()))
            continue
        words = words.concat([word])
        changed = true
    }
    return changed ? words : null
}

/**
 * Strips a trailing " [Server]" world suffix from a character name, returning just the player name. A
 * cross-world speaker's name is built with that suffix appended, so stripping it lets a group member or
 * mention stored as a plain "Firstname Lastname" match the player on any world. Mirrors the C#
 * ChatUtil.StripServerName.
 */
export function stripServerName(name: string): string {
    const idx = name.indexOf("[")
    return idx >= 0 ? name.substring(0, idx).trim() : name
}

// Each part of a FINAL FANTASY XIV character name is 2–15 characters, letters only with the apostrophe
// the single allowed punctuation (e.g. Y'shtola), and must start with a letter.
const FFXIVNamePart = /^[A-Za-z][A-Za-z']*$/
const FFXIVNamePartMin = 2
const FFXIVNamePartMax = 15
const FFXIVNameCombinedMax = 20

/**
 * True when <paramref name="name"/> is a complete FINAL FANTASY XIV character name: a "Firstname Lastname"
 * pair split by a single space, each part {@link FFXIVNamePartMin}–{@link FFXIVNamePartMax} characters,
 * {@link FFXIVNameCombinedMax} combined, made of letters with apostrophe the only allowed special
 * character. Used to gate manual group-member entry to real player names (matching is case-insensitive).
 */
export function isValidFFXIVPlayerName(name: string): boolean {
    const parts = name.trim().split(" ")
    if (parts.length !== 2)
        return false
    const [first, last] = parts
    const partOk = (p: string): boolean =>
        p.length >= FFXIVNamePartMin && p.length <= FFXIVNamePartMax && FFXIVNamePart.test(p)
    if (!partOk(first) || !partOk(last))
        return false
    return first.length + last.length <= FFXIVNameCombinedMax
}

export function formatString(text: string, ...args: (string|number)[]) {
    for (const key in args) {
        text = text.replace(new RegExp("\\{" + key + "\\}", "gi"), args[key].toString())
    }
    return text
}

export function encodeHtmlEntities(str: string): string {
    return str.replace(/[\u00A0-\u9999<>&](?!#)/gim, function (i) {
        return '&#' + i.charCodeAt(0) + ';';
    });
}

export function decodeHtmlEntities(str: string): string {
    return str.replace(/&#([0-9]{1,3});/gi, function (match, num) {
        return String.fromCharCode(parseInt(num));
    });
}

export function decodeUnicode(str: string): string {
    return str.replace(/[uU]\+([\da-fA-F]{4})/g,
        function (match, num) {
            return String.fromCharCode(parseInt(num, 16));
        });
}

export function encodeUnicode(str: string): string {
    return Array.from(str)
        .map(char => char.codePointAt(0))
        .filter(code => typeof code === "number")
        .map(code => code!.toString(16))
        .map(hex => "U+" + "0000".substring(0, 4 - hex.length) + hex)
        .join("")
}

export function decodeKeyEventToText(keyEvent: KeyboardEvent, ignoreEnter: boolean = true): string | null {
    if (ignoreEnter && keyEvent.key === "Enter")
        return null

    if (keyEvent.key === "Shift" || keyEvent.key === "Control" || keyEvent.key === "Alt")
        return ""

    let msg = ""
    if (keyEvent.shiftKey) msg += "Shift + "
    if (keyEvent.altKey) msg += "Alt + "
    if (keyEvent.ctrlKey) msg += "Ctrl + "

    var keyEnum = Gobchat.KeyCodeToKeyEnum(keyEvent.keyCode)
    if (keyEnum === null) {
        msg = ""
    } else {
        msg += keyEnum
    }
    return msg
}