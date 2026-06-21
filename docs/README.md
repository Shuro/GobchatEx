# GobchatEx (FFXIV chat overlay)
GobchatEx is an overlay with the goal to provide a better chat experience for roleplayers.

> GobchatEx is a fork of [Gobchat](https://github.com/MarbleBag/Gobchat) by MarbleBag, licensed under AGPL-3.0.

This app took a lot of inspiration from [quisquous cactbot](https://github.com/quisquous/cactbot)
and uses the great [sharlayan](https://github.com/FFXIVAPP/sharlayan) module from FFXIVAPP to process FFXIV's memory.

The changelog can be found [here.](CHANGELOG.md)

Die deutsche Version dieser Readme kann man [hier](README_de.md) finden.

1. [Features](#features)
   1. [Smart Autoscroll](#smart-autoscroll)
   1. [Text formatting](#text-formatting)
   1. [Chat tabs](#chat-tabs)
   1. [Text-Highlighting](#text-highlighting-for-key-words---mentions)
   1. [Draggable and Resizeable](#draggable-and-resizeable)
   1. [Chat Log](#chat-Log)
     1. [Customizable chat log format](#customizable-chat-log-format)
   1. [Range Filter](#range-filter)
   1. [Groups](#groups)
   1. [Chat Commands](#chat-commands)
1. [Installation](#installation)
1. [Updating GobchatEx](#updating-gobchatex)
1. [How to use GobchatEx](#how-to-use-gobchatex)
1. [Troubleshooting](#troubleshooting)
1. [License](#license)

## Features

### Smart autoscroll
By moving the scroll bar up, autoscroll will be disabled for new messages and you can (re)read the text, without any disturbance.

![no autoscroll](screen_scroll_noautoscroll.png)

By moving the scroll bar back to the bottom of the chat, autoscroll will be re-enabled!

![autoscroll reenables](screen_scroll_bottom.png)

### Text formatting
Enhance your chat experience with colors! They make it easier to follow other people actions.

![Original chat box](screen_unformated.jpg)

Will be turned into this:

![Enhanced chat overlay](screen_formated.jpg)

### Chat tabs
Create as many tabs as you like and control which channels are visible and what formatting to apply

#### Roleplay specific formatting
GobchatEx applies specific colors to speech, emote and ooc comments

On the *Formatting* page you can also tune the FFXIV Modern overlay's look: a **Tab style** (Underline / Pills / Angled) and a **Chat density** with four steps (Dense+ / Dense / Breathable / Breathable+) that sets how tightly chat lines are spaced. Changes apply to the overlay immediately and are saved per profile.

![Different formats](screen_formats.png)

### Text-Highlighting for key words - mentions
Case-insensitive detection for a customizable list of words, which then will be highlighted. This will help you not missing out on important messages.

![Mentions](screen_mention_highlighting.png)

**Global Mentions** are a list of words that always highlight, edited as removable tags (type a word and press *Enter* or comma).

**Player Mentions** (enable *Player Name Mentions*) remember each character you log in as and highlight that character's name while you play it. Per character you choose whether the full name, first name and/or last name counts, and you can add extra words that only apply while you are logged in as that character. Optionally turn on **Fuzzy mention** (off by default) to also catch typo'd spellings of that character's names — missing, extra or swapped letters and dropped apostrophes (e.g. *Jon* or *Jhon* for *John*, *Gobchatt* for *Gobchat*). A **Conservative / Balanced / Aggressive** strength picker (default *Conservative*) sets how forgiving it is; very short names always match exactly, and Global Mentions are never fuzzy.

This feature can be enhanced further by playing a customizable sound.
Sound files must be placed in `GobchatEx\resources\sounds`.

### Customizable formatting settings

### Draggable and Resizeable
Click the **pin** button in the overlay's toolbar to unlock it for moving and resizing. While unlocked, a gold accent ring and a drag grip appear.
To move the overlay, drag it by its **top toolbar** with the left mouse button (the cog/search/pin icons stay clickable).
To resize, move your mouse to one of the four borders or corners of the overlay. The cursor will change, indicating the type of resizing. Now press and hold the left mouse button and resize.
Click the pin again to lock it back in place. The new position and size are saved automatically.

### Chat Log
GobchatEx can write your chat history to a file, preserving informations you might want to look up later or just to reread fun moments.
They can be found under `AppData\Roaming\GobchatEx`.

Each time GobchatEx is started it will create a new file.
By default this feature is deactivated. If you want GobchatEx to create log files, activate it in the settings under `Config / App`

#### Customizable chat log format
GobchatEx provides a few pre-made formats from which you can choose. You can either modify these or create your own format. GobchatEx uses the entered format string and replaces certain key-words.
* __{channel}__ Channel id
* __{sender}__ Sender name
* __{sender-cha}__ Sender name with a channel specific formatting, which is similar to in game. Not supported by the log converter.
* __{date}__ Date in yyyy-mm-dd (year, month, day)
* __{time}__ Time of message in hh:mm:ss (hour of day, minutes, seconds)
* __{time-short}__ Time of message in hh:mm (hour of day, minutes)
* __{time-full}__ Time in hh:mm:ssK (hour of day, minutes, seconds + local time zone)
* __{message}__ Message
* __{break}__ Line break

### Range filter
Filter messages in various channels by distance to the writer. Remove them completely and/or fade them slowly out the farther away they are. The numbers are given in yalms (in game unit). And the degree of fade out effect is computed by the distance between fade out and cut off.

By default this feature is deactivated. You can turn it on in the settings.

### Groups
The game allows you to sort players from your friend-list into seven predefined groups. Doing so, marks said players with a special icon in your chat, making it easier to keep track of them.

GobchatEx includes these groups into its styling options and allows to create as many additional groups as you want.
Each group can have a name, activated or deactivated, styled and keeps track of the players which belong to it.
It's no longer required to add players to your friend-list, just to make it easier to see what they're writing.

Groups are sorted by importance. While a player can belong to multiple groups, only the style of the first matching group is applied. To change the order, just drag & drop the group to its new position.

### Chat Commands
GobchatEx accepts chat commands. To send a chat command to GobchatEx, use the echo channel `/e` and type `gc` (short for GobchatEx!).
Example:
- `/e gc `

GobchatEx supports the following chat commands:
- [group](#chat-command-group)
- [profile](#chat-command-profile)
- [close](#chat-command-close)
- [player](#chat-command-player)
- [config](#chat-command-config)
- [info/error](#chat-command-info--error)

***

#### Chat command Group
Usage:
- `/e gc group groupnumber add/remove/clear playername`

This chat command can be used to manipulate a player group without using the config menu, for example via macros.
To use the group command, type `/e gc group`.

Groupnumber is a number, starting from 1 and references the group you want to manipulate. The assigned number is identical to the position in the config menu.

Next is the task which should be performed. Possible values are `add`, `remove` and `clear`

##### clear
Doesn't need any additional  arguments. This task will remove all players from a group.
Example:
- `/e gc group 3 clear` - will remove all players from group 3

##### add
Needs the full name of a player, which will be added to the group. Names are case-insensitive!
When a player comes from a different server, it is also necessary to specify the server name in brackets.
Placeholders like <t> are an exception to this rule and will always be accepted.

Examples:
- `/e gc group 1 add M'aka Ghin` 			/ `/e gc group 1 add firstname lastname`
- `/e gc group 1 add M'aka Ghin[ultros]` 	/ `/e gc group 1 add firstname lastname[servername]`
- `/e gc group 1 add M'aka Ghin [ultros]` 	/ `/e gc group 1 add firstname lastname [servername]`
- `/e gc group 1 add <t>`

##### remove
Needs the full name of a player, which will be removed to the group. Names are case-insensitive!
When a player comes from a different server, it is also necessary to specify the server name in brackets.
Placeholders like <t> are an exception to this rule and will always be accepted.

Examples:
- `/e gc group 1 remove M'aka Ghin` 			/ `/e gc group 1 remove firstname lastname`
- `/e gc group 1 remove M'aka Ghin[ultros]` 	/ `/e gc group 1 remove firstname lastname[servername]`
- `/e gc group 1 remove M'aka Ghin [ultros]` 	/ `/e gc group 1 remove firstname lastname [servername]`
- `/e gc group 1 remove <t>`


#### Chat command Profile
Usage: `/e gc profile load profilename`\
This chat command can be used to change the active profile and can be easily embedded in a macro, especially useful if you already use a macro to activate your rp flag!

Examples:
- `/e gc profile load Favorite Profile`\
this will activate the profile with the name `Favorite Profile`

#### Chat command Close
Usage: `/e gc close`\
This chat command will close GobchatEx and provides an alternative way to right-clicking the GobchatEx Icon in the tray-icon bar and clicking close.

#### Chat command Player
##### count
Usage: `/e gc player count`\
Returns the number of characters nearby. The definition of nearby depends on the total amount of characters close to you. The client will only display up to 100 characters.

##### list
Usage: `/e gc player list`\
Returns a list of character names and their current distance to you.

##### distance
Usage: `/e gc player distance <t>`\
Returns the distance in yalms to your current target.

#### Chat command config
##### open
Usage: `/e gc config open`\
This allows to open the config dialog via chat command
Usage:

##### reset frame
Usage: `/e gc config reset frame`\
Resets size and position of the overlay to its default value

#### Chat command info / error
Usage:
- `/e gc info on`
- `/e gc info off`
- `/e gc error on`
- `/e gc error off`

Will temporarily deactivate GobchatEx's info and error messages.

## Installation

### Dependencies

GobchatEx needs two runtimes. Both ship with an up-to-date Windows 10/11 and are usually
already installed:

- The [.NET Desktop Runtime 10](https://dotnet.microsoft.com/download/dotnet/10.0) (x64)
- The [Microsoft Edge WebView2 Runtime](https://developer.microsoft.com/microsoft-edge/webview2/) (Evergreen), which renders GobchatEx's HTML/JavaScript UI

### Installing GobchatEx

1. Go to [latest release](https://github.com/Shuro/GobchatEx/releases/latest)
2. Download the latest version of GobchatEx. The file is named gobchatex-{version}.zip
3. Right click the zip file and go to properties. In the bottom right corner of the properties menu, click `Unblock`, and then "OK" to close the menu
4. Unzip the zip file to your preferred location. All files are already in a GobchatEx folder.
5. Go into your GobchatEx folder
6. Start GobchatEx.exe
7. On start GobchatEx will check for new updates
8. GobchatEx renders its UI (written in HTML and JavaScript) through the Microsoft Edge WebView2 runtime that ships with Windows. There is no longer a one-time browser-engine download on first start.

### First launch

The first time you start GobchatEx it shows a short setup screen before the overlay appears. Pick your **language** and **theme**, choose whether GobchatEx should **automatically check for updates**, and — if it finds an existing `%AppData%\Gobchat` install from the original Gobchat — whether to **import your existing profiles** (the old folder is copied over and left untouched in its original location). Your choices take effect on that first launch and the screen does not appear again; you can change all of them later under *Settings → App*.

### Updating GobchatEx

On startup GobchatEx will check for new updates. The installation can be done either manually or automatically.
To do it manually repeat steps 1 to 4 of [installing GobchatEx](#installing-gobchatex) and replace all files.
To do it automatically hit the automatic install button on the patch-note screen. Done.

You can also check at any time without restarting: open *Settings → About* and click **Check for updates**. On an installed build this shows the same patch-note screen and can apply the update and restart; it works even when *Check for updates on start* (on the *App* page) is turned off. A short status next to the button confirms the result — *You're up to date*, *Couldn't check for updates*, or that the releases page was opened in your browser.

## How to use GobchatEx
### Running
1. GobchatEx's Overlay will not be visible in front of FFXIV, when FFXIV runs in full screen mode.  
2. GobchatEx was written for FFXIV 64bit - DirectX 11 version

1. Go into your GobchatEx folder
2. Start GobchatEx.exe
3. On start GobchatEx checks for new updates

Within your tray a new icon will appear: ![gobchat looks for ffxiv](screen_gobchat_off.png)
This icon means GobchatEx is running and looks for an active instance of FFXIV.

If you are running FFXIV and GobchatEx finds it, the icon will switch to ![gobchat is ready to rumble](screen_gobchat_on.png), indicating that GobchatEx is ready.
This may take a while on your first start of GobchatEx.

GobchatEx does not need administrator rights and starts without a UAC prompt. The only exception is when __FFXIV itself runs as administrator__ - GobchatEx then cannot read its chat and will ask whether to restart itself as administrator. Click *Yes* (and accept the UAC prompt) to reconnect. As a rule of thumb, run both as administrator or neither.


### Tray Icon
- Left click: Will show or hide the overlay
- Right click: Will open a context menu

### Hotkeys
Configure global hotkeys under *Settings → App → Hotkeys* (click the field and press the key combination; the reset button clears it). They are off until you assign a key.
- **Show & Hide** — shows or hides the overlay.
- **Focus search** — brings the overlay to the front and opens its search bar with the cursor already in the search field, so you can type straight away (the overlay's click-through/lock state is left unchanged). It only acts while the overlay is on screen; if the overlay is hidden (logged out and not pinned), use the tray/pin to show it first.

### Closing
1. Right click the tray icon of GobchatEx.
2. Click 'close'!


1. Use a chat command in FFXIV
1. enter `/e gc close` in the in-game chat

## Troubleshooting
### Range filter seems not to work
- Check `Config / App`, it's possible that GobchatEx can't retrieve informations about players from your running FFXIV. A red message will inform you about that. This can have many reasons:
  - Be sure FFXIV is running.
  - Close and reopen config dialog. GobchatEx needs some time until it has loaded everything.
  - Close GobchatEx an delete the `sharlayan` folder under `resources`.  The content will be re downloaded and may contain the missing informations.

### GobchatEx doesn't start
- Check `gobchatex_debug.log`
  - An error mentioning the `WebView2` runtime means the Microsoft Edge WebView2 runtime is missing or out of date. Install it from the link in [Dependencies](#dependencies).

## License
This program is free software: you can redistribute it and/or modify it under the terms of the GNU Affero General Public License (AGPL-3.0-only) as published by the Free Software Foundation, version 3.
You can find the full license [here](LICENSE.md) or at https://www.gnu.org/licenses/
