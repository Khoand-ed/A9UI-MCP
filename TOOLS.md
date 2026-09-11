# Editor tools reference

Every menu item this project adds, what it does, and when you'd reach for it.

There are two menus: **`Arknights`** (added for this port) and **`XLua`** (ships with xLua).

---

## Arknights → Placeholders

The game's real assets aren't in this repository. These generate stand-ins so the project actually
runs. Source: [`Assets/Editor/GeneratePlaceholderAssets.cs`](Assets/Editor/GeneratePlaceholderAssets.cs).

### Generate Bootstrap Assets

**Use it when:** you want to press Play and see something. This is the normal starting point.

Creates, using only Unity's built-in UI sprites and the `LiberationSans` font already in the project:

| Asset | Why the code needs it |
|---|---|
| `Prefab/UI/Camera` | `UIManager.Initialization` instantiates this and looks for a **direct child named exactly `Canvas`** with a `CanvasScaler`. Also carries an `EventSystem` so buttons work. |
| `Prefab/UI/LoginUI` | The first screen `GameStart` shows. All ~20 serialised fields wired. |
| `Prefab/UI/CommonDialogUI` | Every validation/error message in `LoginUI` goes through this. |
| `Prefab/UI/HomeUI` | Where a successful login lands. Lua-driven — see below. |
| `Data/User/Saukiya`, `Data/User/Test` | `PlayerData` accounts so `Login` can succeed. Both use password **`123456`**. |

Written to `Assets/Arknights/Resources/`. Safe to re-run — it overwrites.

**After running:** open `Assets/Arknights/Scenes/StartMenu.unity`, press Play, log in as
`Saukiya` / `123456`.

Real AssetBundles take priority over these automatically, because `Asset.Load` checks bundles
*before* falling back to `Resources`. You don't have to delete anything when the real art arrives.

### Delete Generated Assets

**Use it when:** you've got real assets and want the placeholders gone, or generation produced
something you want to redo from scratch.

Deletes `Assets/Arknights/Resources/Prefab/UI` and `Assets/Arknights/Resources/Data`.

> Careful: it removes those **whole folders**. If you've hand-edited a generated prefab or dropped
> your own assets alongside them, move those out first.

---

## Arknights → AssetBundles

For the real assets. Source: [`Assets/Editor/BuildAssetBundles.cs`](Assets/Editor/BuildAssetBundles.cs).

### List Assigned Bundle Names

**Use it when:** before building, to check your bundle naming is right. Start here — it's read-only.

Prints every AssetBundle name currently assigned, then warns about any bundle the game loads at
runtime that nothing is assigned to. The 13 it expects are listed in `SETUP.md` §3.

A fresh clone reports **0 assigned** — that's expected, nothing is tagged yet.

### Build For Active Target

**Use it when:** you've imported real assets and tagged them with bundle names.

1. Refuses to run if no bundle names are assigned (nothing to build).
2. Builds into `Assets/StreamingAssets/`.
3. Renames the manifest bundle to the platform name. This matters — `BuildPipeline` names it after
   the *output folder*, so a plain build produces one called `StreamingAssets`, but `ABManager`
   looks for `Win` / `Android` / `IOS`.
4. **On Android/iOS, copies everything to `Application.persistentDataPath`** — that's where
   `ABManager.ABPath` points on those platforms. Skip this and nothing you build is ever loaded.

Only Windows standalone, Android and iOS are supported; `ABManager` has no path for anything else.

### Reveal Bundle Folder

**Use it when:** you want to see what actually got deployed, or drop bundles in by hand.

Opens the folder the *active build target* reads from. On Android that's buried at
`C:\Users\<you>\AppData\LocalLow\Rhodes Island\Arknights\` — effectively impossible to find
otherwise. On Windows standalone it's `Assets/StreamingAssets/`.

---

## XLua menu

Ships with xLua. Bridges C# and Lua. See `SETUP.md` → *xLua code generation* for the Unity 6 caveat.

### Generate Code

**Use it when:** after changing xLua config, or after a fresh clone if `Assets/XLua/Gen/` is absent.

Writes wrapper classes into `Assets/XLua/Gen/`. Two things it provides:

- **C# → Lua delegates.** `RuaUI` does `scriptEnv.Get("hide", out luaHide)` where `luaHide` is an
  `Action<bool>`. That delegate bridge *must* be generated — this project's API Compatibility Level
  is .NET Standard, which compiles out xLua's runtime `Reflection.Emit` fallback. Without it,
  opening a Lua-driven screen throws `InvalidCastException: This type must add to CSharpCallLua`.
- **Lua → C# wrappers.** Pure speed. Reflection works without them.

Generation is driven by config classes. Right now that's `Assets/XLua/Examples/ExampleGenConfig.cs` —
xLua's *sample* config, not one written for this game. Worth replacing eventually with a list
matching what `Global.lua.txt` actually uses.

### Clear Generated Code

**Use it when:** generation produced code that doesn't compile, or you're changing config and want
a clean slate.

Deletes `Assets/XLua/Gen/`. Always **Clear → let Unity recompile → Generate**; regenerating on top
of broken generated code can leave the Editor unable to rebuild the assembly the config lives in.

### Hotfix Inject In Editor

**Use it when:** basically never, on this project.

Runtime patching of C# via Lua. Requires the `HOTFIX_ENABLE` scripting define, which isn't set — so
`Hotfix.cs` currently compiles to nothing and this menu item does nothing useful.

---

## Typical workflows

**Just want it to run**
```
Arknights → Placeholders → Generate Bootstrap Assets
→ open StartMenu.unity → Play → log in Saukiya / 123456
```

**Lua screens throwing `must add to CSharpCallLua`**
```
XLua → Clear Generated Code
→ wait for recompile
→ XLua → Generate Code
```

**Wiring up real assets**
```
import assets → tag them with bundle names
→ Arknights → AssetBundles → List Assigned Bundle Names   (check for warnings)
→ Arknights → AssetBundles → Build For Active Target
→ Arknights → AssetBundles → Reveal Bundle Folder         (confirm they landed)
```

---

## Not a menu item: `Unity6GenConfig.cs`

[`Assets/XLua/Src/Editor/Unity6GenConfig.cs`](Assets/XLua/Src/Editor/Unity6GenConfig.cs) has no UI —
it registers a filter that xLua's generator consults automatically.

Unity 6 added `Span<T>`/`ReadOnlySpan<T>` overloads across core APIs. Those are `ref struct`s, which
can't be generic type arguments, so xLua's generated wrappers failed to compile (`CS0306`). This
skips any member with a ref-struct in its signature — the same thing xLua already does for pointers.

It lives in `Xlua.Core.Editor` **deliberately**. A config class under `Assets/XLua/Editor/` would land
in `Assembly-CSharp-Editor`, which depends on `Assembly-CSharp` — the assembly holding
`Assets/XLua/Gen/`. Once generation emits broken code that assembly stops building, and the filter
would be missing at exactly the moment the generator needs it. Don't move this file.
