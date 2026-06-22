# Deprecated-code audit (temporary working doc)

> **Status: temporary.** This is an analysis/working document, not living documentation.
> It backs the "Deprecated / dead code" cleanup bullets in [TODO.md](TODO.md). Delete this
> file once that cleanup has landed.
>
> Audited 2026-06-23. No code was changed by this audit.

## Why this exists

The `/e gc` chat commands were once marked deprecated yet were still load-bearing — they were
rescued and moved to C# (`Module/Chat/Command/`). That prompted a sweep of **every**
deprecation marker in the codebase to answer two questions:

1. What is marked "deprecated" but **still used** (and therefore must not be deleted)?
2. What is **genuinely dead** (and can be removed)?
3. Does any logic live **only** in deprecated code (i.e. would be lost on deletion)?

## Decisive fact

`src/Gobchat.App/resources/ui/gobchat/deprecated/` (21 `.js` files, ~4,542 lines, ~216 KB) is
**not referenced or loaded by anything live.** The HTML entry points
(`resources/ui/gobchat.html:19`, `resources/ui/config/config.html:33`) load only
`gobchat.js` / `config.js` as ES modules plus vendored libs; no live HTML/TS/JS/C# imports or
requests any file in that folder. It still ships in Release because `Gobchat.csproj:72` copies
`resources\**` wholesale — so it is dead weight shipped to users.

Re-confirm before acting:

```sh
grep -rn "gobchat/deprecated\|/deprecated/" src/ \
  --include=*.ts --include=*.js --include=*.html --include=*.cs \
  | grep -v /bin/ | grep -v /gobchat/deprecated/
# expected: no output
```

Because nothing loads the folder, deletion is safe regardless of per-file detail below.

## Part A — `gobchat/deprecated/` folder: per-file verdict (all superseded)

| Deprecated file | Replaced by |
|---|---|
| `ChatManager.js` | `modules/Chat.ts` (`ChatControl`) + C# chat pipeline |
| `GobchatConfig.js` | `modules/Config.ts` (`GobchatConfig`) |
| `Databinding.js` | `modules/Databinding.ts` (`BindingContext`) |
| `LocaleManager.js` | `modules/Locale.ts` |
| `Constants.js` | `modules/Constants.ts` |
| `FFUnicodes.js` | `modules/Constants.ts` (`FFUnicode`) + C# `ChatUtil` / `FFXIVUnicodes` |
| `CommandManager.js` | **C# `Module/Chat/Command/`** (the `gc` commands that moved to C#) |
| `GobchatSearch.js` | `modules/Chat.ts` (`ChatSearchControl`) |
| `ChatTabHtmlElement.js` | `modules/Chat.ts` (`TabBarControl`) |
| `StyleBuilder.js`, `ChatCssStyleBuilder.js`, `CssFileLoader.js` | `modules/Style.ts` |
| `Message.js`, `MessageBuilder.js`, `MessageHtmlBuilder.js`, `MessageParser.js` | C# `ChatMessageBuilder` / `ChatlogCleaner` + `modules/Chat.ts` rendering |
| `MessageSoundPlayer.js` | `modules/Chat.ts` `AudioPlayer` (`Chat.ts:449-512`) — same debounce / volume / visibility gating, reads the **new** `behaviour.mentions` path, adds data-URL caching |
| `CommonUtilFunctions.js`, `jQuery-extension.js` | superseded by the `modules/` layer + jQuery 4 |
| `web-components.js` (`stacked-panel` custom element) | no live reference; replaced by the new config page mechanism |
| `Datacenters.js` | **removed feature** — stale hardcoded FFXIV world list, no live consumer |

**Verification basis.** Whole-folder non-reference is grep-proven (decisive). The three files
with no obvious keyword match were deep-read and resolved: `MessageSoundPlayer.js` →
reimplemented in `Chat.ts`; `web-components.js` → `stacked-panel` unreferenced;
`Datacenters.js` → see flag below. The remaining files were mapped by responsibility to the
known `modules/` layer; re-confirm each file's replacement at deletion time (cheap — the
folder is unloaded).

### "Useful code that might still be needed" — explicit flag

Only one item holds data not present elsewhere: **`Datacenters.js`** — a hardcoded
NA/EU/JP/OCE world + datacenter list (its own comment: *"Needs to be updated on server
changes"*). It powered a server picker that no longer exists. It is **stale** and trivially
replaceable (FFXIV world lists are public), so it is **not** worth preserving in code — but
this is recorded so the decision is explicit rather than accidental. Nothing else in the
folder is load-bearing.

## Part B — scattered markers outside the folder

### Genuinely dead (zero live callers — safe to remove)

| Item | Location | Note |
|---|---|---|
| `PlayerEventArgs`, `ChatlogEventArgs` (`[Obsolete]` classes) | `Gobchat.Memory/Actor/PlayerEventArgs.cs`, `Gobchat.Memory/Chat/ChatlogEventArgs.cs` | Old push-event model; the memory layer polls now. No consumers. |
| `JsonUtil.SwitchResult` / `SwitchError` / `TypeSwitchError` | `Core/Config/JsonUtil.cs:90-127` | Only used by each other + commented-out lines 142-143. **Keep the `TypeSwitch` _method_** (line 129) — heavily used and returns `bool`, not these types. |
| `JsonUtil.ReplaceArrayIfAvailable` | `Core/Config/JsonUtil.cs:474` | `[Obsolete("Use ModifyIfAvailable<T>")]`; zero callers. |
| `makeDatabinding` | `resources/ui/modules/Databinding.ts:363` | `@deprecated`; only caller was inside the dead folder. Live code uses `new BindingContext(...)`. |
| `PerformApplicationUpdate()` no-op exit hook | `Core/Runtime/AbstractGobchatApplicationContext.cs:51,86-94` | Empty body; comment is a stale GobUpdater TODO. Obsolete since the Velopack migration. Remove the method, `OnApplicationExit`, and the `Application.ApplicationExit +=` subscription — the whole chain is dead. |

### Still used despite the marker (the gc-commands trap — do NOT delete; de-stale the marker)

| Item | Location | Reality |
|---|---|---|
| `Gobchat.MessageSegmentEnum` marked `// deprecated` | `resources/ui/globals.d.ts:182` | Used by `config_formatting.ts:25-28,260-261` and `Chat.ts:346`. Marker is stale. |
| `Config.saveToLocalStore` / `loadFromLocalStore` marked `//TODO remove later` | `resources/ui/modules/Config.ts:550-557` | The **live** localStorage handoff between the overlay and the settings window (`config.ts`, `gobchat.ts`, `ProfileControl.ts`). Load-bearing. |

## Summary answer

Besides the already-rescued `gc` commands, the **still-used** "deprecated" items are
`Gobchat.MessageSegmentEnum` and the `saveToLocalStore` / `loadFromLocalStore` localStorage
handoff — both carry misleading markers but are load-bearing. Everything else (the entire
`gobchat/deprecated/` folder and the five scattered `[Obsolete]`/`@deprecated`/no-op items) is
genuinely orphaned and safe to remove.
