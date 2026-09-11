# Session summary — start here in the new session

Paste this project open and tell the new session "check unity-mcp and continue" — it also has a
persistent memory of all this, but this doc is the readable version.

## Where things stand

The project is a 2020-era Arknights fan rebuild opened in **Unity 6000.5.1f1**. No real game assets
ship in this repo (they're in an external, copyrighted `kiraio-moe/Arknights-Base` repo), so the
work has been: (1) get it compiling under Unity 6, (2) make it actually playable without the real
assets, (3) wire up Unity MCP so I can drive the live Editor directly.

**(1) and (2) are done and compile-verified. (3) is set up on both ends but not yet confirmed
working — that's the one open thread.**

## 1. Compile fixes (Unity 6 migration)

Unity 6 promoted several `InstanceID` APIs to hard compiler errors, which broke the vendored
spine-unity 3.8 and put the Editor in Safe Mode.

- **5 files under `Assets/Plugins/Spine/`** — `GetInstanceID()`→`GetEntityId()`,
  `InstanceIDToObject`→`EntityIdToObject`, etc. Non-obvious part: `EntityId`→`int` conversion is
  *also* obsolete-as-error, so affected lookup tables became `Dictionary<EntityId, ...>` rather than
  keeping `int` keys.
- **xLua codegen** hit `CS0306` (Unity 6's new `Span<T>`/`ReadOnlySpan<T>` overloads are ref structs,
  can't be generic type args). Fixed with a `[BlackList]` filter:
  **`Assets/XLua/Src/Editor/Unity6GenConfig.cs`** — it **must** stay in that folder
  (`Xlua.Core.Editor` assembly), not `Assets/XLua/Editor/`. Moving it would put it in
  `Assembly-CSharp-Editor`, which depends on the assembly holding `Assets/XLua/Gen/` — if generation
  ever breaks that assembly, the filter would be missing exactly when it's needed. Learned this the
  hard way on the first attempt.

Full API table is in `SETUP.md` → "Unity 6 compatibility".

## 2. Runtime fixes + placeholder assets

**Pre-existing bugs found while tracing the boot path** (nothing to do with Unity 6):
- `Asset.cs` — Resources fallback used `Path.Combine`, which emits a **backslash** on Windows
  (`Prefab/UI\Camera`). `Resources.Load` never resolves that, so the fallback was silently dead.
- `LuaEnvManager.cs` — disk-based Lua loader resolved against the wrong base folder
  (`Assets/` instead of `Assets/Arknights/`), so it missed every `require`.
- Null-guards added: `SoundManager` (null `AudioClip`), `PlayerManager` (null `PlayerData` entries),
  `CommonDialogUI` (null result from a missing prefab).

**New tool: `Assets/Editor/GeneratePlaceholderAssets.cs`**
Menu: **Arknights → Placeholders → Generate Bootstrap Assets**. Builds placeholder prefabs
programmatically (Unity's built-in UI sprites, no external art) so the game boots and is playable:
Camera/Canvas, a fully-wired LoginUI, CommonDialogUI, a Lua-driven HomeUI, and two test accounts.

**→ Login with `Saukiya` / `123456` after running it.**

**New tool: `Assets/Editor/BuildAssetBundles.cs`** — for when real assets arrive. User's active
build target is Android, so it copies built bundles to `Application.persistentDataPath` (where
`ABManager` actually looks on that platform).

**Docs delivered:** `SETUP.md` (full setup + Unity 6 writeup) and `TOOLS.md` (every custom menu
item, when to use it, example workflows). Both already sent to the user as files.

## 3. Unity MCP — the open thread

User has Unity's own **AI Assistant package** (`com.unity.ai.assistant`) installed, which runs a
relay (`relay_win.exe --mcp`) as an MCP bridge to the Editor. This is the one to use — **not**
anything I set up independently.

**Confirmed:** the relay process is alive and has an *established* TCP connection to the Unity
Editor (checked via `Get-NetTCPConnection` on port 9001). The Unity side of the bridge is healthy.

**Not yet confirmed:** no Claude Code session has actually loaded `unity-mcp` tools — checked via
`ToolSearch`, found nothing. MCP servers are only read at session startup, and every session so far
started before the config was in place.

**Config is now in place on both ends**, so a fresh session should pick it up automatically:
- User's own `~/.claude.json` (global scope) — the original registration, made by the user.
- This project's `.mcp.json` — I added this too, mirroring the exact same server name and command,
  so it doesn't depend solely on the global config and travels with the repo.
- My own earlier, separate attempt (the Unity CLI's `unity mcp` command,
  `com.unity.pipeline` package) is disabled at `.mcp.json.disabled-by-user-mcp` — inert, not deleted.

**Checked and ruled out:** no permission/trust setting in either project or global
`.claude/settings.json` is blocking it.

### First thing to do in the new session
1. Confirm `unity-mcp` tools are now visible (check the tool list / try `ToolSearch`).
2. If yes — make one real call against the Editor (e.g. read the scene hierarchy) to confirm the
   full round-trip actually works, not just that the process is running.
3. If still not visible even after a **full app restart** (not just a new tab) — that's a genuinely
   new problem worth investigating fresh, not a repeat of what's documented here.

### Not urgent, left open
- Whether to revert the unused `com.unity.pipeline` line in `Packages/manifest.json` (harmless
  either way — offered to the user, no decision made).
- xLua code generation currently runs off `Assets/XLua/Examples/ExampleGenConfig.cs`, which is
  xLua's own *sample* config, not a project-specific one. Works fine, but worth narrowing once the
  game's actual Lua usage surface stabilizes.
- Real Arknights-Base assets still need to come from the user — out of scope for me to source.

## Quick file map

| File | What |
|---|---|
| `SETUP.md`, `TOOLS.md` | Full reference docs (already delivered) |
| `Assets/Plugins/Spine/**` | Unity 6 `EntityId` migration (5 files) |
| `Assets/XLua/Src/Editor/Unity6GenConfig.cs` | Ref-struct codegen filter — placement is load-bearing |
| `Assets/Arknights/Utils/Asset.cs`, `Manager/LuaEnvManager.cs`, `Manager/SoundManager.cs`, `Data/Player/PlayerManager.cs`, `UI/Sub/CommonDialogUI.cs` | Runtime bug fixes |
| `Assets/Editor/GeneratePlaceholderAssets.cs` | Placeholder generator (run this first) |
| `Assets/Editor/BuildAssetBundles.cs` | Real-asset bundle pipeline (Android-aware) |
| `.mcp.json` | Active — points at user's `unity-mcp` relay |
| `.mcp.json.disabled-by-user-mcp` | My earlier attempt, inert |
