<!-- Generated: 2026-06-19 | Files scanned: ~25 | Token estimate: ~860 -->

# Data model (config — no database)

There is no DB. State is JSON config managed by
[GobchatConfigManager.cs](../../Gobchat.App/Core/Config/GobchatConfigManager.cs).
User data lives under `%AppData%\Roaming\GobchatEx` (migrated once from legacy `…\Gobchat`).

## Two stores (current schema v20007)

```
Per-profile   default_profile.json  →  %AppData%\GobchatEx\profiles\<id>.json   (save-on-Save)
App-global    default_appsettings.json → %AppData%\GobchatEx\appsettings.json   (instant, no Save)
Index         gobconfig.json  → { activeProfile }
```

`GobchatConfigManager` routes a dotted property path: keys under any **AppSettingRoots**
prefix go to `_appConfig` (instant store, falls back to bundled `default_appsettings.json`),
everything else to the active profile. `MigrateAppSettings()` lifts these roots out of
profiles once after load (when the active profile is known) — see ConfigUpgrade_2_0_7.

AppSettingRoots (app-global): `behaviour.language`, `behaviour.hideOnMinimize`,
`behaviour.appUpdate`, `behaviour.actor`, `behaviour.hotkeys`,
`behaviour.chat.updateInterval`, `style.theme`.

## default_appsettings.json (top level)

```
behaviour.language            UI language ("en")
behaviour.hideOnMinimize      bool
behaviour.appUpdate           { checkOnline, acceptBeta }
behaviour.actor               { updateInterval, active }
behaviour.chat.updateInterval poll interval ms
behaviour.hotkeys.showhide    global hotkey string
style.theme                   active theme name ("FFXIV Modern")
```

## default_profile.json (top level)

```
version    20007 (schema)
profile    { id, name, index }
behaviour  channel.{roleplay,mention,rangefilter,log} (FFXIV channel lists),
           segment.{order,data} (ooc/emote/say token delimiters),
           mentions.player, range-filter, trigger groups, chatlog options
style      theme, chat-history.*, chat-frame.{tab-style,density}, fonts, channel colours
```

## ConfigUpgrader chain

[ConfigUpgrader.cs](../../Gobchat.App/Core/Config/ConfigUpgrader/ConfigUpgrader.cs) applies
ordered `IConfigUpgrade`s by `version` until none applies, then stamps the new version.
Each is idempotent (re-running the chain is a no-op).

```
1_3_0 → 1_6_0 → 1_7_1 → 1_8_0 → 1_9_0 → 1_12_0
2_0_0  FFXIV Modern theme + surface palette as new defaults (move profiles still on old default)
2_0_1  (schema bump / cleanup)
2_0_2  drop trailing !important on search bg; drop style.config.font-size; default font → IBM Plex Sans
2_0_3  seed behaviour.mentions.player (Player Mentions feature)
2_0_4  (schema bump / cleanup)
2_0_5  add style.chat-frame.tab-style + density; drop chat-history.gap
2_0_6  chat bg from theme + per-profile override + opacity; migrate legacy themes → Modern
2_0_7  app-global prefs split to appsettings.json (bump only; migration in GobchatConfigManager)
LegacyAppConfigTransformer  transforms pre-rebrand app config shape on load
```
Latest target version: **20007** (`ConfigUpgrade_2_0_7`).

## Other data inputs

```
resources/sharlayan/*.json   FFXIV memory signatures/structures (downloaded at runtime; repo dev copies)
resources/lang/autotranslate_*.hjson   FFXIV autotranslate data
WebUIResources.*.resx / Resources.*.resx   C#/web localized strings
```

## Related
[architecture.md](architecture.md) · [backend.md](backend.md) · [dependencies.md](dependencies.md)
