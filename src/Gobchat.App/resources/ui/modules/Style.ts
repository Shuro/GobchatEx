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

import * as Utility from './CommonUtility.js'
import * as Constants from './Constants.js'
import * as Chat from './Chat.js'
import * as MathFontFallback from './MathFontFallback.js'
import * as CssSanitize from './CssSanitize.js'

export class StyleLoader {
    #styles: { [key: string]: { label: string, files: string[] } } = {}
    #activeStyles: string[] = []
    #activeStyleSheetIds: string[] = []
    readonly #filePrefix: string | null

    constructor(filePrefix: string) {
        this.#filePrefix = filePrefix ? filePrefix : null
    }

    async initialize(): Promise<void> {
        this.#styles = {}

        const json = await GobchatAPI.readTextFromFile("ui/styles/styles.json")

        // A corrupt or non-array styles.json must not take the whole overlay down (this runs during
        // startup, before setUIReady). On any parse/shape problem, log and continue with no styles —
        // the overlay still renders, just without selectable themes until the file is fixed.
        let styles: any
        try {
            styles = JSON.parse(json)
        } catch (e) {
            console.error("StyleLoader.initialize: styles.json is not valid JSON", (e as any)?.stack ?? e)
            return
        }
        if (!Array.isArray(styles)) {
            console.error("StyleLoader.initialize: styles.json did not contain a style array")
            return
        }

        for (const style of styles) {
            if (!style || typeof style.label !== "string")
                continue

            const styleKey = style.label.toLowerCase().trim()
            if (styleKey.length == 0)
                continue

            const files: string[] = [].concat(style.files || []).filter(file => Utility.isString(file))

            this.#styles[styleKey] = {
                label: style.label,
                files: files.map(file => file.trim()).filter(file => file.length > 0)
            }
        }
    }

    get styles(): { id: string, label: string }[] {
        return Object.keys(this.#styles)
            .map(key => {
                return {
                    id: key,
                    label: this.#styles[key].label
                }
            })
    }

    get activeStyles(): string[] {
        return ([] as string[]).concat(this.#activeStyles || [])
    }

    async activateStyles(): Promise<void>
    async activateStyles(styleIds: string | string[], target: HTMLElement | JQuery): Promise<void>
    async activateStyles(styleIds: string | string[], target: HTMLElement | JQuery, insertMod: "in" | "after" | "before"): Promise<void>
    async activateStyles(styleIds?: string | string[], target?: HTMLElement | JQuery, insertMod: "in"|"after"|"before" = "in"): Promise<void> {
        styleIds = ([] as string[]).concat(styleIds || []).filter(e => Utility.isString(e)).map(e => e.toLowerCase())

        for (const styleId of styleIds)
            if (!this.#styles[styleId])
                throw new Error(`Style with id '${styleId}' not available`)

        // No-op when the requested styles are already active. The overlay re-binds style.theme on every
        // config sync (which includes the frame move/resize position writes), and reloading the same
        // <link> here would flash the overlay (body.hide()/show() + re-fetching the CSS + its fonts).
        const active = this.#activeStyles
        if (styleIds.length === active.length && styleIds.every((id, i) => id === active[i]))
            return

        const _target = !target || $(target).length === 0 ? $("head") : $(target).first()
        const body = $("body")

        // use hide / show to trigger a reflow, so the new loaded style gets applied everywhere.
        // Sometimes, without this, styles aren't applied to scrollbars. Still no idea why.
        body.hide()

        for (const id of this.#activeStyleSheetIds)
            $(`#${id}`).remove()

        this.#activeStyleSheetIds = []
        this.#activeStyles = []

        const awaitPromises: Promise<void>[] = []

        for (const styleId of styleIds) {
            this.#activeStyles.push(styleId)

            const style = this.#styles[styleId]
            const randomIdPrefix = Utility.generateId(8)
            const usedIds: string[] = []

            for (const file of style.files) {
                const randomIdSuffix = Utility.generateId(8, usedIds)
                usedIds.push(randomIdSuffix)

                const id = `gobstyle-${randomIdPrefix}-${randomIdSuffix}`

                const $link = $(`<link rel="stylesheet" href="">`).attr('id', id)
                this.#activeStyleSheetIds.push(id)

                awaitPromises.push(new Promise(function (resolve, reject) {
                    $link.one("load", () => resolve())
                    $link.one("error", () => reject())
                }))

                const path = this.#filePrefix ? `${this.#filePrefix}/${file}` : file
                $link.attr("href", path)

                switch(insertMod){
                    case 'in':
                        $link.appendTo(_target)
                        break
                    case 'after':
                        $link.insertAfter(_target)
                        break
                    case 'before':
                        $link.insertBefore(_target)
                }                
            }
        }

        const results = await Promise.allSettled(awaitPromises)

        await new Promise<void>((resolve, reject) => {
            window.requestAnimationFrame(() => resolve())
        })

        body.show()

        let errorMsg = ""
        for (const result of results) {
            if (result.status === "rejected")
                errorMsg += result.reason + '\n'
        }
        if (errorMsg.length > 0)
            throw new Error(errorMsg)
    }
}

type CssRuleGenerator = () => string

function hasValueNoUnit(value: string) {
    return /\d$/.test(value)
}

function addUnitToValueIfMissing(value: string, fallbackUnit: string) {
    return hasValueNoUnit(value) ? value + fallbackUnit : value
}

export class StyleBuilder {

    private static RuleGenerators: CssRuleGenerator[] = []

    public static generateAndSetCssRules(htmlStyleSheetId: string) {
        const rules = StyleBuilder.generateCssRules()
        StyleBuilder.setStyleOnCurrentDocument(htmlStyleSheetId, rules)
    }

    /*
    static { // template
        StyleBuilder.RuleGenerators.push(() => {
            return ""
        })
    }
    */

    static { // font size
        StyleBuilder.RuleGenerators.push(() => {
            //const baseFontSize = gobConfig.get("style.base-font-size")

            const uiFontSize = gobConfig.get("style.chatui.font-size") as string
            const historyFontSize = gobConfig.get("style.chat-history.font-size") as string

            //uiFontSize = addUnitToValueIfMissing(uiFontSize, "px")

            return StyleBuilder.toCss(":root", {
                "--gob-font-size-chat-ui": `max(8px, ${uiFontSize})`,
                // Mirrors the chat-history font so the right-click context menu can match the chat
                // message size (it sits outside .gob-chat_history, so it can't just inherit it).
                "--gob-font-size-chat-history": `max(8px, ${historyFontSize})`,
            })
        })
    }

    static { // general rules
        StyleBuilder.RuleGenerators.push(() => {
            const configStyle = gobConfig.get("style")

            const results: string[] = []

            // Emit everything on chat-history except the background — the theme owns the surface
            // colour/opacity (see below), so a directly-painted background here would bypass it.
            const chatHistory = StyleBuilder.copy(configStyle["chat-history"])
            const chatBackgroundCustom = chatHistory["background-color"]   // null unless the user picked a colour
            const chatBackgroundOpacity = configStyle["chat-history"]["background-opacity"]
            delete chatHistory["background-color"]
            delete chatHistory["background-opacity"]
            results.push(StyleBuilder.toCss(`.${Chat.CssClass.Chat_History}`, chatHistory))

            // The overlay theme paints the whole frame from --gob-chat_background (its own per-mode
            // colour) at --gob-chat_opacity. A user-picked colour overrides via the separate
            // --gob-chat_background-custom property (kept separate so a theme's html.theme-light
            // override can't out-specify it). A null colour is skipped by objectToCss, so clearing
            // the field cleanly falls back to the theme colour.
            results.push(StyleBuilder.toCss(":root", {
                "--gob-chat_opacity": `${chatBackgroundOpacity ?? 90}%`,
                "--gob-chat_background-custom": chatBackgroundCustom,
            }))

            results.push(StyleBuilder.toCss(`.${Chat.CssClass.ChatEntry}`, StyleBuilder.withMathFallback(configStyle.channel["base"]["general"])))
            results.push(StyleBuilder.toCss(`.${Chat.CssClass.ChatEntry_Sender}`, StyleBuilder.withMathFallback(configStyle.channel["base"]["sender"])))

            for (const channel of Object.values(Gobchat.Channels)) {
                if (channel.internalName in configStyle.channel) {
                    const channelClass = Utility.formatString(Chat.CssClass.ChatEntry_Channel_Partial, channel.internalName)
                    const channelStyles = configStyle.channel[channel.internalName]

                    const textSelector = `.${channelClass} .${Chat.CssClass.ChatEntry_Text}`
                    results.push(StyleBuilder.toCss(textSelector, StyleBuilder.withMathFallback(channelStyles["general"])))

                    const senderSelector = `.${channelClass} .${Chat.CssClass.ChatEntry_Sender}`
                    results.push(StyleBuilder.toCss(senderSelector, StyleBuilder.withMathFallback(channelStyles["sender"])))
                }
            }

            return results.join("")
        })
    }

    static { // chat entry formatting
        StyleBuilder.RuleGenerators.push(() => {
            const configStyle = gobConfig.get("style")
            const tabClassesWithMentions = StyleBuilder.filterTabs(tab => tab.formatting.mentions)
            const tabClassesWithRoleplay = StyleBuilder.filterTabs(tab => tab.formatting.roleplay)

            const results: string[] = []

            results.push(StyleBuilder.toCss(
                tabClassesWithMentions.map(tabClass => `.${tabClass} .${Chat.CssClass.ChatEntry_Segment_Mention}`),
                configStyle.segment.mention
            ))

            results.push(StyleBuilder.toCss(
                tabClassesWithRoleplay.map(tabClass => `.${tabClass} .${Chat.CssClass.ChatEntry_Segment_Say}`),
                configStyle.segment.say, configStyle.channel.say.general
            ))

            results.push(StyleBuilder.toCss(
                tabClassesWithRoleplay.map(tabClass => `.${tabClass} .${Chat.CssClass.ChatEntry_Segment_Emote}`),
                configStyle.segment.emote, configStyle.channel.emote.general
            ))

            results.push(StyleBuilder.toCss(
                tabClassesWithRoleplay.map(tabClass => `.${tabClass} .${Chat.CssClass.ChatEntry_Segment_Ooc}`),
                configStyle.segment.ooc
            ))

            results.push(StyleBuilder.toCss(
                `.${Chat.CssClass.ChatEntry_Segment_Link}`,
                configStyle.segment.link
            ))

            return results.join("")
        })
    }

    static { // time stamp
        StyleBuilder.RuleGenerators.push(() => {
            const tabClasses = StyleBuilder.filterTabs(tab => !tab.formatting.timestamps)
            const selectors = tabClasses.map(tabClass => `.${tabClass} .${Chat.CssClass.ChatEntry_Time}`)
            return StyleBuilder.toCss(selectors, { "display": "none" })
        })
    }

    static { // range filter
        StyleBuilder.RuleGenerators.push(() => {
            const tabClasses = StyleBuilder.filterTabs(tab => tab.formatting.rangefilter)
            if (tabClasses.length === 0)
                return ""

            const configRangeFilter = gobConfig.get("behaviour.rangefilter")
            const startopacity = configRangeFilter.startopacity / (configRangeFilter.maxopacity + 0.0)
            const endopacity = configRangeFilter.endopacity / (configRangeFilter.maxopacity + 0.0)
            const opacityByLevel = (startopacity - endopacity) / (configRangeFilter.opacitysteps - 1)

            const results: string[] = []

            for (let i = 0; i <= configRangeFilter.opacitysteps; ++i) {
                const rangeFilterClass = Utility.formatString(Chat.CssClass.ChatEntry_FadeOut_Partial, i)
                const selectors = tabClasses.map(tabClass => `.${tabClass} .${rangeFilterClass}`)

                if(i === 0){
                    results.push(StyleBuilder.toCss(selectors, { "display": "none" }))
                }else{
                    results.push(StyleBuilder.toCss(selectors, { "opacity": `${(i - 1) * opacityByLevel + endopacity}` }))
                }                
            }

            return results.join("")
        })
    }

    static { // visible chat entries by channel by tab
        StyleBuilder.RuleGenerators.push(() => {
            const configTabs = gobConfig.get("behaviour.chattabs.data")

            const tabs = Object.values(configTabs) as any[]

            const results: string[] = []

            for (const tab of tabs) {
                const tabClass = Utility.formatString(Chat.CssClass.Chat_History_Tab_Partial, tab.id)

                {
                    const invisibleChannels = _.difference(Constants.ChannelEnumValues, tab.channel.visible as number[])
                        .map(id => Constants.ChannelEnumToKey[id])
                        .map(key => Gobchat.Channels[key])
                        .map(channel => channel.internalName)

                    const selectors = invisibleChannels.map(channelName => `.${tabClass} .${Utility.formatString(Chat.CssClass.ChatEntry_Channel_Partial, channelName)}`)
                    results.push(StyleBuilder.toCss(selectors, { "display": "none" }))
                }

                if (tab.groups.type === "only") {
                    const notSelector = tab.groups.filter.map((groupId: string) => `.${Chat.triggerGroupCssClass(groupId)}`).join(",")
                    const selector = `.${tabClass} .${Chat.CssClass.ChatEntry}:not(${notSelector})`
                    results.push(StyleBuilder.toCss(selector, { "display": "None" }))
                } else if (tab.groups.type === "hide") {
                    const selectors = tab.groups.filter.map((groupId: string) => `.${tabClass} .${Chat.triggerGroupCssClass(groupId)}`)
                    results.push(StyleBuilder.toCss(selectors, { "display": "none" }))
                }
            }

            return results.join("")
        })
    }

    static { // trigger groups
        StyleBuilder.RuleGenerators.push(() => {
            const configTriggerGroups = gobConfig.get("behaviour.groups.data")
            const results: string[] = []

            for (const key of Object.keys(configTriggerGroups)) {
                const triggerGroup = configTriggerGroups[key]
                const cssClass = Chat.triggerGroupCssClass(triggerGroup.id)
                results.push(StyleBuilder.toCss(`.${cssClass}`, triggerGroup.style.body))
                results.push(StyleBuilder.toCss(`.${cssClass} .${Chat.CssClass.ChatEntry_Sender}`, triggerGroup.style.header))
            }

            return results.join("")
        })
    }

    static { // search
        StyleBuilder.RuleGenerators.push(() => {
            const configStyle = gobConfig.get("style")
            const results: string[] = []

            // The search highlight must override per-channel/per-group backgrounds, so its
            // background-color carries !important. That suffix is NOT stored in the config (Coloris can't
            // parse it, so the colour field wouldn't open); re-attach it here at generation time only.
            const marked = StyleBuilder.copy(configStyle.chatsearch.marked) as { [property: string]: string }
            const markedBg = marked["background-color"]
            if (typeof markedBg === "string" && markedBg.length > 0 && !/!important\s*$/.test(markedBg))
                marked["background-color"] = `${markedBg} !important`

            results.push(StyleBuilder.toCss(`.${Chat.CssClass.ChatEntry_MarkedbySearch}:not(.${Chat.CssClass.ChatEntry_SelectedBySearch})`, marked))
            results.push(StyleBuilder.toCss(`.${Chat.CssClass.ChatEntry_SelectedBySearch}`, configStyle.chatsearch.selected))

            return results.join("")
        })
    }

    private static copy<T>(object: T): T {
        return JSON.parse(JSON.stringify(object))
    }

    // Insert the bundled Noto Sans Math fallback into a style's font-family (see MathFontFallback) so
    // decorative "math" letters render instead of tofu. Returns a copy so the stored config isn't
    // mutated; returns the original object untouched when there's no font-family or the fallback is
    // already present. Done at render time so it reaches existing profiles too, without rewriting their
    // stored font.
    private static withMathFallback(style: { [property: string]: string }): { [property: string]: string } {
        const fontFamily = style ? style["font-family"] : null
        if (typeof fontFamily !== "string" || fontFamily.trim().length === 0)
            return style

        const updated = MathFontFallback.withMathFallback(fontFamily)
        if (updated === fontFamily)
            return style // no-op (already present)

        const copy = StyleBuilder.copy(style)
        copy["font-family"] = updated
        return copy
    }

    private static toCss(selectors: string | string[], ...properties: { [property: string]: string }[]): string {
        selectors = ([] as string[]).concat(selectors).filter(e => e !== undefined && e !== null)
        properties = ([] as { [property: string]: string }[]).concat(properties).filter(e => e !== undefined && e !== null)

        if (selectors.length === 0 || properties.length === 0)
            return ""

        let baseProperties = properties[0]

        if (properties.length > 1) {
            baseProperties = StyleBuilder.copy(baseProperties)
            for (let i = 1; i < properties.length; ++i) {
                Object.keys(baseProperties).forEach(key => {
                    const currentValue = baseProperties[key]
                    if (currentValue === null || currentValue === undefined) {
                        if (key in properties[i])
                            baseProperties[key] = properties[i][key]
                    }
                })
            }
        }

        const allSelectors = selectors.map(selector => `${selector}`).join(",")
        return `${allSelectors}${StyleBuilder.objectToCss(baseProperties)}\n`
    }

    private static objectToCss(object: { [property: string]: string }): string {
        const content = Object.entries(object)
        .filter(e => e[1] !== null && !e[0].startsWith("$"))
        // Drop any value that could break out of the declaration/rule (config is editable/importable,
        // so these are untrusted at the point they reach the <style> sheet). A dropped property simply
        // isn't emitted — the element keeps whatever the theme/default already gave it.
        .map(e => [e[0], CssSanitize.sanitizeCssValue(e[1])] as [string, string | null])
        .filter(e => e[1] !== null)
        .map(e => `\t${e[0]}: ${e[1]};`)
        .join("\n")
        return `{\n${content}\n}`
    }

    private static filterTabs(filter?: (tabConfig: any) => boolean): string[] {
        const configTabs = gobConfig.get("behaviour.chattabs.data")
        const filteredTabs = filter ? Object.keys(configTabs).filter(key => filter(configTabs[key])) : Object.keys(configTabs)
        const tabClasses = filteredTabs.map(key => configTabs[key].id)
            .map(id => Utility.formatString(Chat.CssClass.Chat_History_Tab_Partial, id))
        return tabClasses
    }

    private static generateCssRules(): string {
        const results: string[] = []
        for (const ruleGenerator of StyleBuilder.RuleGenerators)
            results.push(ruleGenerator())
        return results.join("")
    }

    private static setStyleOnCurrentDocument(htmlStyleSheetId: string, cssRules: string): void {
        let styleElement = document.getElementById(htmlStyleSheetId) as HTMLStyleElement
        if (!styleElement) {
            styleElement = document.createElement("style")
            styleElement.id = htmlStyleSheetId
            document.head.appendChild(styleElement)
        }
        // textContent, not innerHTML: a <style>'s content is raw CSS text, and assigning generated
        // rules as text means nothing in them is ever parsed as HTML (no </style> breakout). Values
        // are additionally guarded by objectToCss/CssSanitize before they reach here.
        styleElement.textContent = cssRules
    }
}
