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

import * as Style from './Style.js'
import * as Constants from './Constants.js'
import * as Databinding from './Databinding.js'
import * as Utility from './CommonUtility.js'
import * as ContextMenu from './ContextMenu.js'
import * as ChatVisibility from './ChatVisibility.js'
import * as CssSanitize from './CssSanitize.js'

//#region backend generated types

// created by backend
export type ChatChannelEnum = number

// created by backend
export interface Channel {
    chatChannel: ChatChannelEnum
    clientChannel: number[]
    internalName: string
    configId: string
    relevant: boolean
    abbreviationId: string
    translationId: string
    tooltipId: string
}

// created by backend
export type MessageSegmentEnum = number

// created by backend
export interface ChatMessage {
    source: ChatMessageSource
    timestamp: Date
    channel: ChatChannelEnum
    content: ChatMessageSegment[]
    containsMentions: boolean
}

// created by backend
export interface ChatMessageSource {
    original: string
    characterName: string
    triggerGroupId: string
    ffGroup: number
    party: number
    alliance: number
    visibility: number
    isAPlayer: boolean
    isUser: boolean
    isApp: boolean
}

// created by backend
export interface ChatMessageSegment {
    type: MessageSegmentEnum
    text: string
}

//#endregion

export module CssClass {
    export const Chat = "gob-chat"
    export const Chat_Toolbar = "gob-chat-toolbar"
    // Reveal-mode toggle (eye button): set on the root .gob-chat to show user-hidden entries (dimmed).
    export const Chat_RevealHidden = "gob-chat--reveal-hidden"

    // Per-entry right-click menu (hide / un-hide, add/remove to a custom group).
    export const Chat_ContextMenu = "gob-chat-context-menu"
    export const Chat_ContextMenu_Item = "gob-chat-context-menu_item"
    // A parent item + its flyout submenu (the "Add/Remove Player to/from Custom Group" entries).
    export const Chat_ContextMenu_Group = "gob-chat-context-menu_group"
    export const Chat_ContextMenu_Submenu = "gob-chat-context-menu_submenu"
    // Generic collapse helper (defined once in base.scss) used for the menu's hidden state.
    export const Hidden = "is-hidden"
    // Grays out and stops the flyout of a parent item (the Remove item when the player is in no group).
    export const Disabled = "is-disabled"

    export const Chat_Tabs = "gob-chat-tabbar"
    export const Chat_Tabs_Content = "gob-chat-tabbar_content"
    export const Chat_Tabs_Content_Tab = "gob-chat-tabbar_content_tab"
    export const Chat_Tabs_Content_Tab_Mention_Partial = "gob-chat-tabbar_content_tab--mention-{0}"
    export const Chat_Tabs_Content_Tab_NewMessage_Partial = "gob-chat-tabbar_content_tab--new-message-{0}"

    export const Chat_Search = "gob-chat-toolbar_search"

    export const Chat_History = "gob-chat_history"
    export const Chat_History_Tab_Partial = "gob-chat_history--tab-{0}"

    export const ChatEntry = "gob-chat-entry"
    // Per-entry user-hide flag, toggled from the right-click menu. base.scss hides it unless reveal mode.
    export const ChatEntry_UserHidden = "gob-chat-entry--user-hidden"
    export const ChatEntry_MarkedbySearch = "gob-chat_entry--marked-by-search"
    export const ChatEntry_SelectedBySearch = "gob-chat_entry--selected-by-search"
    export const ChatEntry_Sender = "gob-chat-entry_sender"
    export const ChatEntry_Time = "gob-chat-entry_time"
    export const ChatEntry_Text = "gob-chat-entry_text"
    export const ChatEntry_FadeOut_Partial = "gob-chat-entry--fadeout-{0}"
    export const ChatEntry_Channel_Partial = "gob-chat-entry--channel-{0}"
    export const ChatEntry_TriggerGroup_Partial = "gob-chat-entry--trigger-group-{0}"
    export const ChatEntry_Segment = "gob-chat_entry_text_segment"
    export const ChatEntry_Segment_Say = "gob-chat-entry_text_segment--say"
    export const ChatEntry_Segment_Emote = "gob-chat-entry_text_segment--emote"
    export const ChatEntry_Segment_Ooc = "gob-chat-entry_text_segment--ooc"
    export const ChatEntry_Segment_Mention = "gob-chat-entry_text_segment--mention"
    export const ChatEntry_Segment_Link = "gob-chat-entry_text_segment--link"
}

// Builds the per-trigger-group CSS class from a config-supplied group id. The id is sanitized to a
// CSS-identifier-safe fragment so a crafted profile cannot inject selector syntax — and because this
// single helper feeds BOTH the class put on the chat entry AND the selector StyleBuilder generates,
// the two always still match. Use it everywhere a trigger-group id becomes a class or selector.
export function triggerGroupCssClass(triggerGroupId: string): string {
    return Utility.formatString(CssClass.ChatEntry_TriggerGroup_Partial, CssSanitize.sanitizeCssIdentifier(triggerGroupId))
}

export module HtmlAttribute {
    export const ChatEntry_Source = "data-source"
    export const ChatEntry_Friendgroup = "data-friendgroup"
    export const ChatEntry_TriggerId = "data-triggerid"
}

export class ChatControl {
    static readonly selector_chat_history = `.${CssClass.Chat_History}`
    static readonly selector_tabbar = `.${CssClass.Chat_Tabs}`
    static readonly selector_search = `.${CssClass.Chat_Search}`

    #tabControl: TabBarControl
    #searchControl: ChatSearchControl
    #groupControl: ChatGroupControl
    #menuControl: ChatEntryMenuControl

    #databinding: Databinding.BindingContext | null = null
    #chatBox: JQuery = $()
    #chatHistory: JQuery = $()

    #hideInfo: boolean = false
    #hideError: boolean = false

    constructor() {
        this.#tabControl = new TabBarControl()
        this.#searchControl = new ChatSearchControl()
        this.#groupControl = new ChatGroupControl()
        this.#menuControl = new ChatEntryMenuControl()
    }

    destructor() {
        this.control(null)
    }

    #onNewMessageEvent = (event: ChatMessagesEvent): void => { // bound to class instance
        if (!!event.detail.messages) {
            for (let message of event.detail.messages) {
                this.#onNewMessage(message)
            }
        }
    }

    // `/e gc` commands are detected and run in C# (AppModuleChatCommandManager). The few whose effect is
    // page-side (info/error on-off, config open) are forwarded here; map the command name to its action.
    #onExecuteUiCommandEvent = (event: ExecuteUiCommandEvent): void => { // bound to class instance
        switch (event.detail.command) {
            case "info on": this.showGobInfo(true); break
            case "info off": this.showGobInfo(false); break
            case "error on": this.showGobError(true); break
            case "error off": this.showGobError(false); break
            case "config open": window.openGobConfig(); break
        }
    }

    #onNewMessage(message: ChatMessage): void {
        if (this.#hideInfo && message.channel === Gobchat.ChannelEnum.GOBCHATINFO)
            return

        if (this.#hideError && message.channel === Gobchat.ChannelEnum.GOBCHATERROR)
            return

        const messageAsHtml = MessageBuilder.build(message)
        this.#chatHistory.append(messageAsHtml)

        this.#tabControl.scrollToBottomIfNeeded()
        this.#tabControl.applyNewMessageAnimationToTabs(message.channel, message.containsMentions)
        AudioPlayer.playMentionSoundIfPossible(message)
    }

    showGobInfo(on: boolean): void {
        this.#hideInfo = !on
    }

    showGobError(on: boolean): void {
        this.#hideError = !on
    }

    // Showing/hiding the search bar grows or shrinks the history pane (they are flex siblings). If the
    // chat was scrolled to the bottom, re-anchor it so the newest lines stay visible above the bar
    // instead of sliding underneath it.
    toggleSearch(): void {
        this.#searchControl.toggle()
        this.#tabControl.scrollToBottomIfNeeded(true)
    }

    hideSearch(): void {
        this.#searchControl.hide()
        this.#tabControl.scrollToBottomIfNeeded(true)
    }

    showSearch(): void {
        this.#searchControl.show()
        this.#tabControl.scrollToBottomIfNeeded(true)
    }

    // Reveal mode (eye button): flip a class on the root .gob-chat so user-hidden entries become
    // visible (dimmed, via base.scss) for un-hiding, then re-hide them on the next toggle. Returns the
    // new state so the caller can update the eye icon. Mirrors the simple flip in toggleSearch().
    toggleRevealHidden(): boolean {
        const revealing = !this.#chatBox.hasClass(CssClass.Chat_RevealHidden)
        this.#chatBox.toggleClass(CssClass.Chat_RevealHidden, revealing)
        return revealing
    }

    control(chatBox: HTMLElement | JQuery | null): void {
        // unbind
        document.removeEventListener("ChatMessagesEvent", this.#onNewMessageEvent as EventListener)
        document.removeEventListener("ExecuteUiCommandEvent", this.#onExecuteUiCommandEvent as EventListener)
        this.#databinding?.clearBindings()

        // rebind
        this.#chatBox = $(chatBox)
        this.#chatHistory = this.#chatBox.find(ChatControl.selector_chat_history)

        if (this.#chatBox.length === 0)
            throw new Error("No chat html element found")

        if (this.#chatHistory.length === 0)
            throw new Error("No chat history html element found")

        this.#tabControl.control(this.#chatBox.find(ChatControl.selector_tabbar), this.#chatHistory)
        this.#searchControl.control(this.#chatBox.find(ChatControl.selector_search), this.#chatHistory)
        this.#groupControl.control(this.#chatHistory)
        this.#menuControl.control(this.#chatBox.find(ChatEntryMenuControl.selector_menu), this.#chatHistory)

        document.addEventListener("ChatMessagesEvent", this.#onNewMessageEvent as EventListener)
        document.addEventListener("ExecuteUiCommandEvent", this.#onExecuteUiCommandEvent as EventListener)

        this.#databinding = new Databinding.BindingContext(gobConfig)
        // The channel-abbreviation cache is locale-dependent; the language is app-global now (not a
        // gobConfig key), so build it at init and rebuild it on a live locale change.
        const updateAbbreviations = async () => {
            const channels = Object.values(Gobchat.Channels)

            const requestTranslation = channels.map(data => data.abbreviationId)

            const translations = await gobLocale.getAll(requestTranslation)
            const channelLookup = MessageBuilder.AbbreviationCache
            channelLookup.length = 0

            for (const data of channels) {
                channelLookup[data.chatChannel] = translations[data.abbreviationId]
            }
        }
        gobLocale.addLocaleChangeListener(() => { void updateAbbreviations() })
        void updateAbbreviations()

        this.#databinding.loadBindings()
    }
}

class MessageBuilder {
    public static AbbreviationCache: string[] = []

    public static build(message: ChatMessage): HTMLElement {
        const $body = $("<div></div>")
            .addClass(CssClass.ChatEntry)
            .addClass(MessageBuilder.getMessageChannelCssClass(message))
            .addClass(MessageBuilder.getMessageTriggerGroupCssClass(message))
            .addClass(MessageBuilder.getMessageVisibilityCssClass(message))
            .attr(HtmlAttribute.ChatEntry_Source, MessageBuilder.getSource(message))
            .attr(HtmlAttribute.ChatEntry_Friendgroup, MessageBuilder.getFriendGroup(message))
            .attr(HtmlAttribute.ChatEntry_TriggerId, MessageBuilder.getTriggerGroup(message))

        $("<span></span>")
            .addClass(CssClass.ChatEntry_Time)
            .text(`[${MessageBuilder.formatTimestamp(message)}] `)
            .appendTo($body)

        const $content = $("<span></span>")
            .addClass(CssClass.ChatEntry_Text)
            .appendTo($body)

        const sender = MessageBuilder.formatSender(message)
        if (sender !== null) {
            $("<span></span>")
                .addClass(CssClass.ChatEntry_Sender)
                .text(`${sender} `)
                .appendTo($content)
        }

        for (const messageSegment of message.content) {
            $("<span></span>")
                .addClass(CssClass.ChatEntry_Segment)
                .addClass(MessageBuilder.getMessageSegmentClass(messageSegment.type))
                .text(messageSegment.text)
                .appendTo($content)
        }

        return $body[0]
    }

    static getMessageChannelCssClass(message: ChatMessage): string | null {
        const channelName = Constants.ChannelEnumToKey[message.channel]
        const data = Gobchat.Channels[channelName]
        return Utility.formatString(CssClass.ChatEntry_Channel_Partial, data.internalName)
        // return Utility.formatString(CssClass.ChatEntry_Channel_Partial, message.channel.toString())
    }

    static getMessageTriggerGroupCssClass(message: ChatMessage): string | null {
        if (message.source.triggerGroupId)
            return triggerGroupCssClass(message.source.triggerGroupId)
        return null
    }

    static getTriggerGroup(message: ChatMessage): string | null {
        return message.source.triggerGroupId
    }

    static getMessageVisibilityCssClass(message: ChatMessage): string | null {
        if (!message.source)
            return null

        const ignoreMention = gobConfig.get("behaviour.rangefilter.ignoreMention", false)
        const opacitySteps = gobConfig.get("behaviour.rangefilter.opacitysteps")

        const level = ChatVisibility.getFadeOutLevel(message.source.visibility, message.containsMentions, ignoreMention, opacitySteps)
        if (level === null)
            return null
        return Utility.formatString(CssClass.ChatEntry_FadeOut_Partial, level)
    }

    static getMessageSegmentClass(segmentType: MessageSegmentEnum): string | null {
        switch (segmentType) {
            case Gobchat.MessageSegmentEnum.SAY: return CssClass.ChatEntry_Segment_Say
            case Gobchat.MessageSegmentEnum.EMOTE: return CssClass.ChatEntry_Segment_Emote
            case Gobchat.MessageSegmentEnum.OOC: return CssClass.ChatEntry_Segment_Ooc
            case Gobchat.MessageSegmentEnum.MENTION: return CssClass.ChatEntry_Segment_Mention
            case Gobchat.MessageSegmentEnum.WEBLINK: return CssClass.ChatEntry_Segment_Link
            default: return null
        }
    }

    static formatTimestamp(message: ChatMessage): string {
        function twoDigits(v: number): string {
            return v < 10 ? '0' + v : v.toString()
        }

        const asDate = new Date(message.timestamp)
        const hours = twoDigits(asDate.getHours())
        const minutes = twoDigits(asDate.getMinutes())
        return `${hours}:${minutes}`
    }

    static formatSender(message: ChatMessage): string | null {
        const formatedSource = MessageBuilder.formatSource(message.source)
        return MessageBuilder.formatSourceAccordingToChannel(formatedSource, message.channel)
    }

    static getFriendGroup(message: ChatMessage): string | null {
        const group = message.source.ffGroup
        return group < 0 ? null : group.toString()
    }

    static getSource(message: ChatMessage): string | null {
        if (message.source === null)
            return null
        return message.source.characterName !== null ? message.source.characterName : message.source.original
    }

    static formatSource(messageSource: ChatMessageSource): string | null {
        if (messageSource === null)
            return null

        if (messageSource.characterName !== null) {
            let prefix = ""
            if (messageSource.party >= 0)
                prefix += `[${messageSource.party + 1}]`

            if (messageSource.alliance >= 0)
                prefix += `[${String.fromCharCode('A'.charCodeAt(0) + messageSource.alliance)}]`

            if (messageSource.ffGroup >= 0)
                prefix += Constants.FFGroupUnicodes[messageSource.ffGroup].char

            return `${prefix}${messageSource.characterName}`
        }

        return messageSource.original
    }

    static formatSourceAccordingToChannel(source: string | null, channel: number): string | null {
        switch (channel) {
            case Gobchat.ChannelEnum.GOBCHATINFO:
            case Gobchat.ChannelEnum.GOBCHATERROR: return `[${source}]`
            case Gobchat.ChannelEnum.ECHO: return "Echo:"
            case Gobchat.ChannelEnum.EMOTE: return source
            case Gobchat.ChannelEnum.TELLSEND: return `>> ${source}:`
            case Gobchat.ChannelEnum.TELLRECIEVE: return `${source} >>`
            case Gobchat.ChannelEnum.ERROR: return null
            case Gobchat.ChannelEnum.ANIMATEDEMOTE: return null //source is set, but the animation message already contains the source name
            case Gobchat.ChannelEnum.PARTY: return `(${source})`
            case Gobchat.ChannelEnum.ALLIANCE: return `<${source}>`
            case Gobchat.ChannelEnum.GUILD:
            case Gobchat.ChannelEnum.LINKSHELL_1:
            case Gobchat.ChannelEnum.LINKSHELL_2:
            case Gobchat.ChannelEnum.LINKSHELL_3:
            case Gobchat.ChannelEnum.LINKSHELL_4:
            case Gobchat.ChannelEnum.LINKSHELL_5:
            case Gobchat.ChannelEnum.LINKSHELL_6:
            case Gobchat.ChannelEnum.LINKSHELL_7:
            case Gobchat.ChannelEnum.LINKSHELL_8:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_1:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_2:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_3:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_4:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_5:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_6:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_7:
            case Gobchat.ChannelEnum.CROSSWORLDLINKSHELL_8:
                return `[${MessageBuilder.getChannelAbbreviation(channel)}]<${source}>`
            default:
                if (source !== null && source !== undefined)
                    return source + ":"
                return null
        }
    }

    static getChannelAbbreviation(channel: number): string {
        if (channel in MessageBuilder.AbbreviationCache)
            return MessageBuilder.AbbreviationCache[channel]
        return channel?.toString() ?? ''
    }
}

module AudioPlayer {
    export interface MentionConfig {
        playSound: boolean,
        volume: number,
        soundPath: string,
        soundInterval: number
        trigger: string[]
        userCanTriggerMention: boolean
    }
}

class AudioPlayer {
    private static lastSoundPlayed: Date = new Date()
    private static soundUrlCache: { path: string, url: string } | null = null

    constructor() {
    }

    private static getMentionConfig(): AudioPlayer.MentionConfig {
        return gobConfig.get("behaviour.mentions")
    }

    // Bundled sounds are stored as a path relative to the page ("../sounds/X.mp3") and play directly.
    // A custom sound picked from an arbitrary location is stored as an absolute path which the virtual
    // host can't serve, so it's read through the bridge as a data: URL (cached, since the same file
    // replays each mention).
    private static async resolveSoundSource(soundPath: string): Promise<string> {
        if (!/^[a-zA-Z]:[\\/]/.test(soundPath) && !soundPath.startsWith("\\\\"))
            return soundPath
        if (AudioPlayer.soundUrlCache?.path === soundPath)
            return AudioPlayer.soundUrlCache.url
        const url = (await GobchatAPI.getSoundDataUrl(soundPath)) ?? ""
        AudioPlayer.soundUrlCache = { path: soundPath, url }
        return url
    }

    private static playSound(soundPath: string, volume: number): void {
        AudioPlayer.resolveSoundSource(soundPath).then(src => {
            if (!src)
                return
            const audio = new Audio(src)
            audio.volume = volume
            audio.play()
        }).catch(e => console.error(e))
    }

    public static playMentionSoundIfPossible(message: ChatMessage): void {
        const config = AudioPlayer.getMentionConfig()
        if (!config.playSound || config.volume <= 0 || !config.soundPath)
            return

        if (!message.containsMentions)
            return

        if (message.source.visibility === 0) {
            const ignoreDistance = gobConfig.get("behaviour.fadeout.mention", false)
            if (!ignoreDistance)
                return
        }

        const time = new Date()
        if (time.valueOf() - AudioPlayer.lastSoundPlayed.valueOf() < config.soundInterval)
            return

        AudioPlayer.lastSoundPlayed = time
        AudioPlayer.playSound(config.soundPath, config.volume)
    }

    public static playMentionSound(): void {
        const config = AudioPlayer.getMentionConfig()
        if (!config.playSound || config.volume <= 0 || !config.soundPath)
            return

        AudioPlayer.lastSoundPlayed = new Date()
        AudioPlayer.playSound(config.soundPath, config.volume)
    }
}

/**
 *  Controls the content of a Tabbar, scrolling and animation.
 *  The controlled tabbar element will fire a 'change' event on any button press
 */
class TabBarControl {
    private static readonly ScrollToleranceZone = 5

    private static readonly AttributeTabId = "data-gob-tab-id"

    private static readonly CssNavBar = CssClass.Chat_Tabs
    private static readonly CssNavPanel = CssClass.Chat_History
    private static readonly CssNavPanelActiveTab = CssClass.Chat_History_Tab_Partial

    private static readonly CssActiveTabButton = "is-active"

    private static readonly CssScrollLeftButton = "gob-chat-tabbar_button--left"
    private static readonly CssScrollRightButton = "gob-chat-tabbar_button--right"
    private static readonly CssTabBarContent = CssClass.Chat_Tabs_Content
    private static readonly CssTabButton = CssClass.Chat_Tabs_Content_Tab
    private static readonly CssTabButtonMentionEffect = "gob-chat-tabbar_content_tab--mention-{0}"
    private static readonly CssTabButtonMessageEffect = "gob-chat-tabbar_content_tab--new-message-{0}"


    private static readonly selector_tabbar = `> .${TabBarControl.CssNavBar}`
    private static readonly selector_scrollLeftBtn = `> .${TabBarControl.CssScrollLeftButton}`
    private static readonly selector_scrollRightBtn = `> .${TabBarControl.CssScrollRightButton}`
    private static readonly selector_content = `> .${TabBarControl.CssTabBarContent}`
    private static readonly selector_activeTab = `> .${TabBarControl.CssTabBarContent} > .${TabBarControl.CssTabButton}.${TabBarControl.CssActiveTabButton}`
    private static readonly selector_tabWithId = `> .${TabBarControl.CssTabBarContent} > .${TabBarControl.CssTabButton}[${TabBarControl.AttributeTabId}={0}]`
    private static readonly selector_allTabs = `> .${TabBarControl.CssTabBarContent} > .${TabBarControl.CssTabButton}`

    #databinding: Databinding.BindingContext
    #resizeObserver: ResizeObserver
    #channelToTab: { [channelId: number]: string[] } = {}
    #navPanelData: {
        [tabId: string]: {
            scrollPosition: number
        }
    } = {}
    #cssClassForMentionTabEffect: string | null = null
    #cssClassForNewMessageTabEffect: string | null = null
    #isPanelScrolledToBottom: boolean = false

    #tabbar: JQuery = $()
    #navPanel: JQuery = $()

    constructor() {
        this.#databinding = new Databinding.BindingContext(gobConfig)
        this.#resizeObserver = new ResizeObserver(this.#onTabbarResize)
    }

    control(navBar: HTMLElement | JQuery, navPanel: HTMLElement | JQuery): void {
        // unbind
        this.#tabbar.off("wheel", this.#onNavBarWheelScroll)
        this.#tabbar.find(TabBarControl.selector_scrollLeftBtn).off("click", this.#onNavBarBtnScroll)
        this.#tabbar.find(TabBarControl.selector_scrollRightBtn).off("click", this.#onNavBarBtnScroll)
        this.#tabbar.find(TabBarControl.selector_allTabs).off("click", this.#onTabClick)
        this.#navPanel.off("scroll", this.#onPanelScroll)
        this.#resizeObserver.disconnect()
        this.#databinding.clearBindings()
        

        // rebind
        const $navBar = $(navBar)
        if (!$navBar.hasClass(TabBarControl.CssNavBar))
            throw new Error("navBar not found")

        const $navPanel = $(navPanel)
        if (!$navPanel.hasClass(TabBarControl.CssNavPanel))
            throw new Error("navPanel not found")

        this.#tabbar = $navBar
        this.#tabbar.on("wheel", this.#onNavBarWheelScroll)
        this.#tabbar.find(TabBarControl.selector_scrollLeftBtn).on("click", this.#onNavBarBtnScroll)
        this.#tabbar.find(TabBarControl.selector_scrollRightBtn).on("click", this.#onNavBarBtnScroll)
        this.#resizeObserver.observe(this.#tabbar[0])

        this.#navPanel = $navPanel
        this.#navPanel.on("scroll", this.#onPanelScroll)

        this.#isPanelScrolledToBottom = true

        Databinding.bindListener(this.#databinding, "behaviour.chattabs", config => this.#updateTabs(config))
        Databinding.bindListener(this.#databinding, "behaviour.chattabs.data", config => this.#buildChannelToTabMapping(config))
        Databinding.bindListener(this.#databinding, "behaviour.chattabs.effect", (effect) => {
            this.#cssClassForMentionTabEffect = effect.mention > 0 ? Utility.formatString(TabBarControl.CssTabButtonMentionEffect, effect.mention) : null
            this.#cssClassForNewMessageTabEffect = effect.message > 0 ? Utility.formatString(TabBarControl.CssTabButtonMessageEffect, effect.message) : null
        })
        this.#databinding.loadBindings()
    }

    applyNewMessageAnimationToTabs(channel: number, hasMention: boolean): void {
        const affectedTabs = this.#channelToTab[channel] || []
        const activeTabId = this.#activeTabId
        if (_.includes(affectedTabs, activeTabId))
            return // done, message was visible on active tab

        const cssClassForMentionEffect = this.#cssClassForMentionTabEffect
        const cssClassForNewMessageEffect = this.#cssClassForNewMessageTabEffect

        for (const tabId of affectedTabs) {
            if (tabId === activeTabId)
                continue // do not apply any effects to the active tab

            const $tab = this.#getTab(tabId)

            if (hasMention && cssClassForMentionEffect) {
                $tab.removeClass(cssClassForNewMessageEffect)
                    .addClass(cssClassForMentionEffect)
                    .on("click.tab.effects.mention", function () {
                        $(this).off("click.tab.effects.mention")
                            .removeClass(cssClassForMentionEffect)
                    })
                continue //apply only one effect
            }

            if (cssClassForNewMessageEffect) {
                $tab.filter(`:not(.${cssClassForMentionEffect})`)
                    .addClass(cssClassForNewMessageEffect)
                    .on("click.tab.effects.message", function () {
                        $(this).off("click.tab.effects.message")
                            .removeClass(cssClassForNewMessageEffect)
                    })
                continue //apply only one effect
            }
        }
    }

    scrollToBottomIfNeeded(scrollFast: boolean = false): void {
        if (this.#isPanelScrolledToBottom)
            this.#scrollPanelToBottom(scrollFast)
    }

    #onTabbarResize = () => {
        this.#scrollTabs(0)
    }

    #onNavBarBtnScroll = (event: any) => {
        const scrollDirection = $(event.currentTarget).hasClass(TabBarControl.CssScrollRightButton) ? 1 : -1
        this.#scrollTabs(scrollDirection)
    }

    #onNavBarWheelScroll = (event: any) => {
        const scrollDirection = (event.originalEvent as WheelEvent).deltaY > 0 ? 1 : -1
        this.#scrollTabs(scrollDirection)
    }

    #onPanelScroll = (event: any) => {
        const $panel = $(event.currentTarget)
        const panelBottom = $panel.scrollTop() + $panel.innerHeight()
        const closeToBottm = panelBottom + TabBarControl.ScrollToleranceZone >= event.currentTarget.scrollHeight
        this.#isPanelScrolledToBottom = closeToBottm
    }

    #onTabClick = (event: any) => {
        const id = $(event.currentTarget).attr(TabBarControl.AttributeTabId) as string
        this.#activateTab(id)
    }

    #buildChannelToTabMapping(config: any) {
        this.#channelToTab = {}
        //this.#groupToTab = {}

        for (const chatTab of Object.values(config) as any[]) {
            if (!chatTab.visible)
                continue

            for (const channel of chatTab.channel.visible) {
                if (channel in this.#channelToTab)
                    this.#channelToTab[channel].push(chatTab.id)
                else
                    this.#channelToTab[channel] = [chatTab.id]
            }
        }
    }

    #getTab(idOrIndex: string | number): JQuery {
        idOrIndex ??= 0

        if (Utility.isNumber(idOrIndex)) {
            const $childs = this.#tabbar.find(TabBarControl.selector_content).children()
            const $nextTab = $childs.eq(Math.max(0, Math.min($childs.length, idOrIndex as number)))
            return $nextTab
        } else {
            const selector = Utility.formatString(TabBarControl.selector_tabWithId, idOrIndex)
            const $nextTab = this.#tabbar.find(selector)
            return $nextTab
        }
    }

    #activateTab(idOrIndex: string | number): boolean {
        const lastActiveTabId = this.#activeTabId

        if (lastActiveTabId in this.#navPanelData) {
            this.#navPanelData[lastActiveTabId].scrollPosition = this.#isPanelScrolledToBottom ? -1 : this.#panelScrollPosition
        }

        // deactiate previous active tab
        this.#tabbar.find(TabBarControl.selector_activeTab).removeClass(TabBarControl.CssActiveTabButton)

        // find new active tab
        const $tab = this.#getTab(idOrIndex)
        $tab.addClass(TabBarControl.CssActiveTabButton)

        if ($tab.length === 0) { //there is no tab with this id
            this.#activateTab(0) //fallback
            return false
        }

        const newActiveTabId = this.#activeTabId

        this.#navPanel // used to filter messages depending on which tab is active
            .removeClass(Utility.formatString(TabBarControl.CssNavPanelActiveTab, lastActiveTabId))
            .addClass(Utility.formatString(TabBarControl.CssNavPanelActiveTab, newActiveTabId))

        // restore scroll position
        if (newActiveTabId in this.#navPanelData) {
            if (this.#navPanelData[newActiveTabId].scrollPosition < 0)
                this.#scrollPanelToBottom(true)
            else
                this.#scrollPanelToPosition(this.#navPanelData[newActiveTabId].scrollPosition, true)
        }

        return true
    }

    get #activeTabId(): string {
        const activeTab = this.#tabbar.find(TabBarControl.selector_activeTab)
        if (activeTab.length === 0)
            return ""
        return activeTab.attr(TabBarControl.AttributeTabId) as string
    }

    #updateTabs(config: any): void {
        const configData = config["data"]
        const configSorting = config["sorting"] as string[]
        const newTabsInOrder = configSorting
            .filter(id => configData[id].visible)
            .map(id => { return { id: id, name: configData[id].name } }) as { id: string, name: string }[]

        // remove old tabs and store them in a lookup table
        const $content = this.#tabbar.find(TabBarControl.selector_content)
        const $oldTabs = $content.children().detach()
        const oldTabsLookup: { [id: string]: HTMLElement } = {}
        for (const tab of $oldTabs) {
            const id = tab.getAttribute(TabBarControl.AttributeTabId)
            if(id)
                oldTabsLookup[id] = tab
        }

        // add new tabs or reattach old tabs in order
        for (const entry of newTabsInOrder) {
            if (entry.id in oldTabsLookup) {
                $(oldTabsLookup[entry.id])
                    .text(entry.name)
                    .appendTo($content)
            } else {
                $("<button></button>")
                    .addClass(TabBarControl.CssTabButton)
                    .attr(TabBarControl.AttributeTabId, entry.id)
                    .on("click", this.#onTabClick)
                    .text(entry.name)
                    .appendTo($content)
            }
        }

        // remove old nav panel data
        for (const tabId of Object.keys(this.#navPanelData)) {
            if (!_.includes(configSorting, tabId))
                delete this.#navPanelData[tabId]
        }

        // add new nav panel data
        for (const tabId of configSorting) {
            if (!(tabId in this.#navPanelData)) {
                this.#navPanelData[tabId] = {
                    scrollPosition: -1
                }
            }
        }

        // const idsToRemove = oldTabIds.filter(id => !_.includes(newTabIds, id))
        // for (let id of idsToRemove)
        //     $oldTabs.filter(`[${TabBarControl.attributeTabId}=${id}]`).remove()

        if (!this.#activeTabId)
            this.#activateTab(0)
        this.#scrollTabs(0) //update the scroll view
    }

    /**
     * 
     * @param direction if positive, scroll right, otherwise left
     */
    #scrollTabs(direction: number): void {
        const $content = this.#tabbar.find(TabBarControl.selector_content)
        const numberOfChilds = $content.children().length

        //not perfect, it would be nice to be able to scroll to the 'next' element or to scroll an element into view
        const scrollDistance = Math.max(10, $content.width() / numberOfChilds)
        const newPosition = $content.scrollLeft() + direction * scrollDistance

        $content.animate({
            scrollLeft: newPosition
        }, 50)

        const isAtLeftBorder = newPosition <= 0
        this.#tabbar.find(TabBarControl.selector_scrollLeftBtn)
            .prop("disabled", isAtLeftBorder)

        const scrollWidth = Utility.toFloat($content.prop("scrollWidth"), 0)
        const clientWidth = Utility.toFloat($content.prop("clientWidth"), 0)
        const isAtRightBorder = (scrollWidth - clientWidth) <= newPosition
        this.#tabbar.find(TabBarControl.selector_scrollRightBtn)
            .prop("disabled", isAtRightBorder)
    }

    #scrollPanelToBottom(scrollFast: boolean): void {
        const navPanel = this.#navPanel[0]
        const position = navPanel.scrollHeight - navPanel.clientHeight
        this.#scrollPanelToPosition(position, scrollFast)
    }

    #scrollPanelToPosition(position: number, scrollFast: boolean): void {
        if (scrollFast) {
            this.#navPanel.scrollTop(position)
        } else {
            this.#navPanel.animate({
                scrollTop: position
            }, 10);
        }
    }

    get #panelScrollPosition(): number {
        return this.#navPanel.scrollTop()
    }
}

class ChatSearchControl {
    private static readonly cssMarkedbySearch = CssClass.ChatEntry_MarkedbySearch
    private static readonly cssActiveSelection = CssClass.ChatEntry_SelectedBySearch
    private static readonly cssChatMessage = CssClass.ChatEntry

    private static readonly selector_input = "> .js-input"
    private static readonly selector_counter = "> .js-counter"
    private static readonly selector_up = "> .js-up"
    private static readonly selector_down = "> .js-down"
    private static readonly selector_reset = "> .js-reset"
    private static readonly selector_markedBySearch = `.${this.cssMarkedbySearch}`
    private static readonly selector_activeSelection = `.${this.cssActiveSelection}`
    private static readonly selector_visible_messages = `.${this.cssChatMessage}:visible`

    #searchElement = $()
    #chatHistory = $()
    // The last query actually searched, so pressing Enter again on the same text steps through the
    // matches instead of re-running the search and snapping back to the first match every time.
    #lastSearchText = ""

    constructor() {
    }

    control(searchElement: HTMLElement | JQuery, chatHistory: HTMLElement | JQuery): void {
        // unbind
        this.#searchElement.find(ChatSearchControl.selector_input).off("keyup", this.#onInputKeyup)
        this.#searchElement.find(ChatSearchControl.selector_counter).off("click", this.scrollToSelection)
        this.#searchElement.find(ChatSearchControl.selector_up).off("click", this.moveSelectionUp)
        this.#searchElement.find(ChatSearchControl.selector_down).off("click", this.moveSelectionDown)
        this.#searchElement.find(ChatSearchControl.selector_reset).off("click", this.clearSearch)
        this.#removeMessageMarkers()

        // rebind
        this.#searchElement = $(searchElement).first()
        this.#chatHistory = $(chatHistory).first()

        this.#searchElement.find(ChatSearchControl.selector_input).on("keyup", this.#onInputKeyup)
        this.#searchElement.find(ChatSearchControl.selector_counter).on("click", this.scrollToSelection)
        this.#searchElement.find(ChatSearchControl.selector_up).on("click", this.moveSelectionUp)
        this.#searchElement.find(ChatSearchControl.selector_down).on("click", this.moveSelectionDown)
        this.#searchElement.find(ChatSearchControl.selector_reset).on("click", this.clearSearch)
    }

    #onInputKeyup = (event) => {
        if (event.key === "Escape") { // clear + close the search bar
            this.hide()
            return
        }

        if (event.key !== "Enter")
            return

        const text = String(this.#searchElement.find(ChatSearchControl.selector_input).val() ?? "").trim().toLowerCase()
        const hasMatches = this.#chatHistory.find(ChatSearchControl.selector_markedBySearch).length > 0

        // First Enter (or a changed query / no current matches) runs a fresh search; pressing Enter
        // again on the same query steps to the next match (Shift+Enter steps back). "Next" advances
        // the counter (newest match is 1/N), matching moveSelectionUp.
        if (text !== this.#lastSearchText || !hasMatches)
            this.startSearch()
        else if (event.shiftKey)
            this.moveSelectionDown()
        else
            this.moveSelectionUp()
    }

    #updateCounterValue = () => {
        const $markedMessages = this.#chatHistory.find(ChatSearchControl.selector_markedBySearch)
        const $activeSelection = this.#chatHistory.find(ChatSearchControl.selector_activeSelection)
        const max = $markedMessages.length
        const current = max > 0 ? $markedMessages.index($activeSelection) : 0
        this.#searchElement.find(ChatSearchControl.selector_counter).text(`${max - current} / ${max}`)
    }

    #removeMessageMarkers = () => {
        this.#chatHistory.find(ChatSearchControl.selector_markedBySearch).removeClass(ChatSearchControl.cssMarkedbySearch)
        this.#chatHistory.find(ChatSearchControl.selector_activeSelection).removeClass(ChatSearchControl.cssActiveSelection)
        this.#updateCounterValue()
    }

    hide = () => {
        this.visible = false
    }

    show = () => {
        this.visible = true
    }

    toggle = () => {
        this.visible = !this.visible
    }

    // Visibility is toggled via .is-hidden on the *outer* toolbar (.gob-chat-toolbar--search), so the
    // whole bar collapses (incl. its padding/border) and the theme's "search on" indicator
    // (:has(.gob-chat-toolbar--search:not(.is-hidden))) reads correctly. The input/buttons stay bound
    // to the inner .gob-chat-toolbar_search.
    get visible() {
        return !this.#searchElement.parent("." + CssClass.Chat_Toolbar).hasClass("is-hidden")
    }

    set visible(value) {
        const toolbar = this.#searchElement.parent("." + CssClass.Chat_Toolbar)
        if (value) {
            toolbar.removeClass("is-hidden")
            this.#searchElement.find(ChatSearchControl.selector_input).focus()
        } else {
            toolbar.addClass("is-hidden")
            this.clearSearch()
        }
    }

    clearSearch = () => {
        this.#lastSearchText = ""
        this.#removeMessageMarkers()
        this.#searchElement.find(ChatSearchControl.selector_input).val("").focus()
    }

    scrollToSelection = () => {
        const $selectedElement = this.#chatHistory.find(ChatSearchControl.selector_activeSelection)
        if ($selectedElement.length === 0)
            return

        const selectedElement = $selectedElement[0]
        const scrollableFrame = this.#chatHistory[0]

        const containerTop = scrollableFrame.scrollTop
        const containerBot = scrollableFrame.clientHeight + containerTop
        const elementTop = selectedElement.offsetTop - scrollableFrame.offsetTop
        const elementBot = selectedElement.clientHeight + elementTop
        const isVisible = containerTop <= elementTop && elementBot <= containerBot

        if (isVisible)
            return

        const position = elementTop;

        $(scrollableFrame).animate({
            scrollTop: position
        }, 100)
    }

    search = (text) => {
        this.#removeMessageMarkers()

        if (text === null || text === undefined) {
            this.#lastSearchText = ""
            return
        }

        text = text.trim().toLowerCase()
        this.#lastSearchText = text
        if (text.length === 0)
            return

        this.#chatHistory.find(ChatSearchControl.selector_visible_messages).each(function () {
            if ($(this).text().toLowerCase().indexOf(text) >= 0)
                $(this).addClass(ChatSearchControl.cssMarkedbySearch)
        })

        const $markedMessages = this.#chatHistory.find(ChatSearchControl.selector_markedBySearch)

        if ($markedMessages.length === 0) {
            this.#updateCounterValue()
        } else {
            $markedMessages.last().addClass(ChatSearchControl.cssActiveSelection)
            this.#updateCounterValue()
            this.scrollToSelection()
        }
    }

    startSearch = () => {
        this.search(this.#searchElement.find(ChatSearchControl.selector_input).val())
    }

    moveSelectionUp = () => {
        this.#moveSelection(
            activeSelection => activeSelection.prevAll(ChatSearchControl.selector_markedBySearch).first(),
            allElements => allElements.last()
        )
    }

    moveSelectionDown = () => {
        this.#moveSelection(
            activeSelection => activeSelection.nextAll(ChatSearchControl.selector_markedBySearch).first(),
            allElements => allElements.first()
        )
    }

    #moveSelection(selectNext: (currentSelection: JQuery) => JQuery, restartSelectionAt: (allElements: JQuery) => JQuery) {
        const activeSelection = this.#chatHistory.find(ChatSearchControl.selector_activeSelection)
        activeSelection.removeClass(ChatSearchControl.cssActiveSelection)
        let nextSelection = selectNext(activeSelection)
        if (nextSelection.length === 0)
            nextSelection = restartSelectionAt(this.#chatHistory.find(ChatSearchControl.selector_markedBySearch))

        nextSelection.addClass(ChatSearchControl.cssActiveSelection)
        this.#updateCounterValue()
        this.scrollToSelection()
    }
}

// Right-click menu for a single chat entry. "Hide Entry" toggles CssClass.ChatEntry_UserHidden on that one
// DOM element (messages live only in the DOM, like search highlighting), so the eye button's reveal mode
// (toggleRevealHidden) can surface them again for un-hiding. The two group items (shown only on player
// lines) add/remove the player to/from a custom highlight group; unlike hiding, that is persistent config
// written through gobConfig + saveConfig(). The menu is a fixed-position element clamped to the viewport,
// with CSS hover flyouts for the group submenus.
class ChatEntryMenuControl {
    static readonly selector_menu = `.${CssClass.Chat_ContextMenu}`
    private static readonly selector_entry = `.${CssClass.ChatEntry}`
    private static readonly selector_hideToggle = ".js-hide-toggle"
    private static readonly selector_addGroup = ".js-add-group"
    private static readonly selector_removeGroup = ".js-remove-group"
    private static readonly selector_removeParent = ".js-remove-parent"
    private static readonly selector_addSubmenu = ".js-add-submenu"
    private static readonly selector_removeSubmenu = ".js-remove-submenu"

    private static readonly ConfigKeyGroupOrder = "behaviour.groups.sorting"
    private static readonly ConfigKeyGroupData = "behaviour.groups.data"
    private static readonly ConfigKeyGroupTemplate = "behaviour.groups.data-template"

    #menuElement: JQuery = $()
    #chatHistory: JQuery = $()
    // The entry the menu was opened on; "Hide Entry" toggles the user-hidden class on this element.
    #targetEntry: HTMLElement | null = null
    // The player on that entry. #targetSource keeps the original casing (used to name a new group); the
    // normalized (lowercased) #targetPlayer is the group trigger value. Both null on a non-player line.
    #targetSource: string | null = null
    #targetPlayer: string | null = null

    control(menuElement: HTMLElement | JQuery, chatHistory: HTMLElement | JQuery): void {
        // unbind
        this.#chatHistory.off("contextmenu", this.#onContextMenu)
        this.#chatHistory.off("scroll", this.#close)
        this.#menuElement.off("click", this.#onMenuClick)
        document.removeEventListener("mousedown", this.#onDocumentMouseDown, true)
        document.removeEventListener("keyup", this.#onDocumentKeyup)
        window.removeEventListener("blur", this.#close)
        this.#close()

        // rebind
        this.#menuElement = $(menuElement).first()
        this.#chatHistory = $(chatHistory).first()

        this.#chatHistory.on("contextmenu", this.#onContextMenu)
        this.#chatHistory.on("scroll", this.#close)
        // Delegated: submenu item buttons are rebuilt on each open, so listen on the menu root and dispatch
        // by the clicked button's data-action.
        this.#menuElement.on("click", this.#onMenuClick)
        // Capture phase so a mousedown anywhere dismisses the menu before other handlers run.
        document.addEventListener("mousedown", this.#onDocumentMouseDown, true)
        document.addEventListener("keyup", this.#onDocumentKeyup)
        window.addEventListener("blur", this.#close)
    }

    #onContextMenu = (event): void => {
        const entry = (event.target as HTMLElement).closest(ChatEntryMenuControl.selector_entry) as HTMLElement | null
        if (entry === null)
            return // right-click on empty history space: no menu
        event.preventDefault()
        this.#open(entry, event.clientX ?? 0, event.clientY ?? 0)
    }

    #open(entry: HTMLElement, clientX: number, clientY: number): void {
        this.#targetEntry = entry

        const isHidden = entry.classList.contains(CssClass.ChatEntry_UserHidden)
        this.#menuElement.find(ChatEntryMenuControl.selector_hideToggle).text(ContextMenu.hideMenuLabel(isHidden))

        const dataSource = entry.getAttribute(HtmlAttribute.ChatEntry_Source)
        this.#targetSource = dataSource !== null && dataSource.trim().length > 0 ? dataSource : null
        this.#targetPlayer = this.#targetSource !== null ? ContextMenu.normalizePlayerName(this.#targetSource) : null
        this.#populateGroupMenus()

        // Show first (so it can be measured), then clamp inside the viewport so it never spills off-screen.
        this.#menuElement.removeClass(CssClass.Hidden)
        const menu = this.#menuElement[0]
        const width = menu?.offsetWidth ?? 0
        const height = menu?.offsetHeight ?? 0
        const x = Math.max(0, Math.min(clientX, window.innerWidth - width))
        const y = Math.max(0, Math.min(clientY, window.innerHeight - height))
        this.#menuElement.css({ left: `${x}px`, top: `${y}px` })

        this.#positionSubmenus()
    }

    // Rebuild the add/remove submenus for the current target. The group items only make sense for a real
    // player line, so they stay hidden otherwise (only "Hide Entry" shows).
    #populateGroupMenus(): void {
        const addGroup = this.#menuElement.find(ChatEntryMenuControl.selector_addGroup)
        const removeGroup = this.#menuElement.find(ChatEntryMenuControl.selector_removeGroup)

        if (this.#targetSource === null || this.#targetPlayer === null) {
            addGroup.addClass(CssClass.Hidden)
            removeGroup.addClass(CssClass.Hidden)
            return
        }
        addGroup.removeClass(CssClass.Hidden)
        removeGroup.removeClass(CssClass.Hidden)

        const groups = this.#readGroups()

        const addSubmenu = addGroup.find(ChatEntryMenuControl.selector_addSubmenu).empty()
        for (const group of ContextMenu.customGroups(groups))
            addSubmenu.append(this.#makeSubmenuButton("add", group.id, group.name))
        addSubmenu.append(this.#makeSubmenuButton("create", null, ContextMenu.Label_CreateNewGroup))

        const memberOf = ContextMenu.groupsContainingPlayer(groups, this.#targetSource)
        const removeSubmenu = removeGroup.find(ChatEntryMenuControl.selector_removeSubmenu).empty()
        for (const group of memberOf)
            removeSubmenu.append(this.#makeSubmenuButton("remove", group.id, group.name))

        // In no group -> grayed out and non-expandable.
        const noGroups = memberOf.length === 0
        removeGroup.toggleClass(CssClass.Disabled, noGroups)
        removeGroup.find(ChatEntryMenuControl.selector_removeParent).prop("disabled", noGroups)
    }

    #readGroups(): ContextMenu.GroupLike[] {
        const order = gobConfig.get(ChatEntryMenuControl.ConfigKeyGroupOrder) as string[]
        return order.map(id => gobConfig.get(`${ChatEntryMenuControl.ConfigKeyGroupData}.${id}`)) as ContextMenu.GroupLike[]
    }

    #makeSubmenuButton(action: string, groupId: string | null, label: string): JQuery {
        const button = $("<button></button>")
            .addClass(CssClass.Chat_ContextMenu_Item)
            .attr("data-action", action)
            .text(label)
        if (groupId !== null)
            button.attr("data-group-id", groupId)
        return button
    }

    // Place each group's flyout submenu with fixed (viewport) coordinates clamped on both axes, so it stays
    // fully on-screen. The chat overlay is a WebView2 composition window sized to the chat frame, so the page
    // can't paint outside it — a flyout that ran off any edge would simply be cut off. The submenu is
    // display:none until hover, so measure it off-screen (visibility:hidden, display:block) first; the
    // computed left/top are written as inline styles and take effect when hover reveals it. The submenu's
    // own max-height/overflow already caps its measured height, so clamping the top keeps it on-screen.
    // Preferred side is the right of the parent item; when that overflows, flip left; when neither side fits
    // (overlay narrower than the menu + flyout), pin to the edge with more room (it may overlap the menu, but
    // stays visible).
    #positionSubmenus(): void {
        const viewportWidth = window.innerWidth
        const viewportHeight = window.innerHeight
        this.#menuElement.find(`.${CssClass.Chat_ContextMenu_Group}`).each((_i, el) => {
            const wrapper = $(el)
            const submenu = wrapper.children(`.${CssClass.Chat_ContextMenu_Submenu}`)[0]
            if (submenu === undefined)
                return

            const prevDisplay = submenu.style.display
            const prevVisibility = submenu.style.visibility
            submenu.style.visibility = "hidden"
            submenu.style.display = "block"
            const submenuWidth = submenu.offsetWidth
            const submenuHeight = submenu.offsetHeight
            submenu.style.display = prevDisplay
            submenu.style.visibility = prevVisibility

            const rect = el.getBoundingClientRect()

            // Horizontal: right of the item if it fits, else left if it fits, else the side with more room.
            let left: number
            if (rect.right + submenuWidth <= viewportWidth)
                left = rect.right
            else if (rect.left - submenuWidth >= 0)
                left = rect.left - submenuWidth
            else
                left = (viewportWidth - rect.right) >= rect.left ? viewportWidth - submenuWidth : 0
            left = Math.max(0, Math.min(left, viewportWidth - submenuWidth))

            // Vertical: align to the item's top, clamped so it never runs off the bottom or top.
            const top = Math.max(0, Math.min(rect.top, viewportHeight - submenuHeight))

            submenu.style.left = `${left}px`
            submenu.style.top = `${top}px`
        })
    }

    #onMenuClick = (event): void => {
        const button = (event.target as HTMLElement).closest("button") as HTMLElement | null
        if (button === null)
            return
        const action = button.getAttribute("data-action")
        if (action === null)
            return // a parent item: its submenu opens on hover, the click itself does nothing
        void this.#runAction(action, button.getAttribute("data-group-id"))
    }

    async #runAction(action: string, groupId: string | null): Promise<void> {
        switch (action) {
            case "hide":
                if (this.#targetEntry !== null)
                    this.#targetEntry.classList.toggle(CssClass.ChatEntry_UserHidden)
                break
            case "add":
                if (groupId !== null)
                    await this.#addPlayerToGroup(groupId)
                break
            case "remove":
                if (groupId !== null)
                    await this.#removePlayerFromGroup(groupId)
                break
            case "create":
                await this.#createGroupForPlayer()
                break
        }
        this.#close()
    }

    async #addPlayerToGroup(groupId: string): Promise<void> {
        if (this.#targetPlayer === null)
            return
        const key = `${ChatEntryMenuControl.ConfigKeyGroupData}.${groupId}.trigger`
        const triggers = gobConfig.get(key, []) as string[]
        if (triggers.includes(this.#targetPlayer))
            return
        gobConfig.set(key, [...triggers, this.#targetPlayer])
        await gobConfig.saveConfig()
    }

    async #removePlayerFromGroup(groupId: string): Promise<void> {
        if (this.#targetPlayer === null)
            return
        const key = `${ChatEntryMenuControl.ConfigKeyGroupData}.${groupId}.trigger`
        const triggers = gobConfig.get(key, []) as string[]
        if (!triggers.includes(this.#targetPlayer))
            return
        gobConfig.set(key, triggers.filter(name => name !== this.#targetPlayer))
        await gobConfig.saveConfig()
    }

    // Mirrors makeNewGroup in config_groups.ts, but seeds the new group with this player: named after them
    // (original casing) with their normalized name as the sole trigger.
    async #createGroupForPlayer(): Promise<void> {
        if (this.#targetPlayer === null || this.#targetSource === null)
            return
        const groups = gobConfig.get(ChatEntryMenuControl.ConfigKeyGroupData)
        const groupId = Utility.generateId(6, Object.keys(groups))

        const newGroup = gobConfig.getDefault(ChatEntryMenuControl.ConfigKeyGroupTemplate)
        newGroup.id = groupId
        newGroup.name = this.#targetSource
        newGroup.trigger = [this.#targetPlayer]
        groups[groupId] = newGroup
        gobConfig.set(ChatEntryMenuControl.ConfigKeyGroupData, groups)

        const sorting = gobConfig.get(ChatEntryMenuControl.ConfigKeyGroupOrder) as string[]
        sorting.push(groupId)
        gobConfig.set(ChatEntryMenuControl.ConfigKeyGroupOrder, sorting)

        await gobConfig.saveConfig()
    }

    #onDocumentMouseDown = (event: MouseEvent): void => {
        // A right-click on an entry re-opens the menu via #onContextMenu; a press inside the menu is the
        // item click. Anything else dismisses it.
        if (this.#menuElement[0]?.contains(event.target as Node))
            return
        this.#close()
    }

    #onDocumentKeyup = (event: KeyboardEvent): void => {
        if (event.key === "Escape")
            this.#close()
    }

    #close = (): void => {
        this.#menuElement.addClass(CssClass.Hidden)
        this.#targetEntry = null
        this.#targetSource = null
        this.#targetPlayer = null
    }
}

class ChatGroupControl {

    private static readonly selector_messages = `.${CssClass.ChatEntry}`

    #databinding = new Databinding.BindingContext(gobConfig)
    #chatHistory: JQuery = $()

    control(chatHistory: HTMLElement | JQuery): void {
        this.#databinding.clearBindings()
        this.#chatHistory = $(chatHistory)

        this.#databinding.bindCallback("behaviour.groups.sorting", () => {
            this.#updateTriggerGroupsForChatEntries()
        }, false)


        this.#databinding.bindCallback("behaviour.groups.data", () => {
            this.#updateTriggerGroupsForChatEntries()
        }, false)

        this.#databinding.loadBindings()
    }

    #updateTriggerGroupsForChatEntries() {
        const doUpdate = gobConfig.get("behaviour.groups.updateChat", false)
        if (!doUpdate)
            return

        // sorting holds custom group ids only (since 2.0.9). Build the full set as custom (in user order)
        // then premade (by ffgroup) so a custom-group highlight wins over a premade one (first match wins).
        const groupOrder = gobConfig.get("behaviour.groups.sorting")
        const customGroups = groupOrder.map(id => gobConfig.get(`behaviour.groups.data.${id}`)) as any[]
        const allGroups = Object.values(gobConfig.get("behaviour.groups.data")) as any[]
        const groups = [...customGroups, ...ContextMenu.premadeGroups(allGroups)]

        for (const message of this.#chatHistory.find(ChatGroupControl.selector_messages)) {
            const triggerId = message.getAttribute(HtmlAttribute.ChatEntry_TriggerId)
            if (triggerId !== null) {
                message.removeAttribute(HtmlAttribute.ChatEntry_TriggerId)
                const cssClass = triggerGroupCssClass(triggerId)
                message.classList.remove(cssClass)
            }

            let matchingGroupId: string | null = null
            for (const group of groups) {
                if (group.ffgroup != null) {
                    if (message.getAttribute(HtmlAttribute.ChatEntry_Friendgroup) == group.ffgroup) {
                        matchingGroupId = group.id as string
                        break
                    }
                } else {
                    const source = message.getAttribute(HtmlAttribute.ChatEntry_Source)?.toLowerCase()
                    if (source !== undefined) {
                        // Match the full source and the server-stripped name, so a member stored as a plain
                        // "Firstname Lastname" matches a cross-world speaker (whose source carries "[Server]")
                        // while a legacy member stored with a "[Server]" suffix keeps matching too.
                        const bareSource = Utility.stripServerName(source)
                        if (_.includes(group.trigger, source) || _.includes(group.trigger, bareSource)) {
                            matchingGroupId = group.id as string
                            break
                        }
                    }
                }
            }

            if (matchingGroupId !== null) {
                const cssClass = triggerGroupCssClass(matchingGroupId)
                message.setAttribute(HtmlAttribute.ChatEntry_TriggerId, matchingGroupId)
                message.classList.add(cssClass)
            }
        }
    }
}