# Setup

This repository contains **source code only**. The Arknights art, audio, prefabs and data are
not here — they live in [kiraio-moe/Arknights-Base](https://github.com/kiraio-moe/Arknights-Base)
and are copyright Hypergryph / Studio Montagne / Yostar.

Without them the project compiles and enters Play mode, but `UIManager` has no `Camera` prefab to
instantiate and logs a missing-asset error instead of drawing anything.

## 0. Just want to see it run?

Run **Arknights → Placeholders → Generate Bootstrap Assets**
([`Assets/Editor/GeneratePlaceholderAssets.cs`](Assets/Editor/GeneratePlaceholderAssets.cs)), then
press Play on `Assets/Arknights/Scenes/StartMenu.unity` and log in with **`Saukiya` / `123456`**.

That generates the minimum the boot path needs — a Camera/Canvas prefab, a full `LoginUI`, a
`CommonDialogUI`, a Lua-driven `HomeUI`, and two `PlayerData` accounts — using only Unity's built-in
UI sprites. No copyrighted assets involved. It is placeholder scaffolding, not the real game art.

These land under `Assets/Arknights/Resources/`, and because `Asset.Load` checks AssetBundles **first**,
real bundles automatically take precedence once you supply them. Nothing to undo.
**Arknights → Placeholders → Delete Generated Assets** removes them again.

The rest of this document covers wiring up the real assets.

## 1. Requirements

- **Unity 6000.5.1f1** (the version in `ProjectSettings/ProjectVersion.txt`).
- **spine-unity must stay on 3.8.x.** Arknights `.skel` files are exported from Spine Editor 3.8.99;
  a newer spine-unity throws null reference exceptions on those assets. The bundled copy has been
  patched for Unity 6 — see [Unity 6 compatibility](#unity-6-compatibility) below.

## 2. How the game loads assets

`Asset.Load<T>(path, name)` → `ABManager.LoadAsset<T>(name, path)`, falling back to `Resources.Load`.

Note the argument order: **the first argument is the AssetBundle name, the second is the asset name.**

`ABManager` reads bundles from:

| Platform | Path |
|---|---|
| Windows standalone | `Application.streamingAssetsPath` → `Assets/StreamingAssets/` |
| Android / iOS | `Application.persistentDataPath` |

Any other build target leaves the path empty and logs `未指定该平台的名字`.

It first loads a **manifest bundle** named after the platform — `Win`, `Android` or `IOS` — reads
`AssetBundleManifest` out of it to resolve dependencies, then loads each individual bundle by name.

## 3. Bundles the game asks for

Recovered from every `Asset.Load` / `Asset.LoadAll` call site:

| Bundle name | Assets it must contain |
|---|---|
| `Prefab/UI` | `Camera`, plus one prefab per screen: `LoginUI`, `GameUI`, `ShopUI`, `SquadUI`, `HouseUI`, `CharInfoUI`, `CharSelectUI`, `CommonDialogUI`, `SelectDungeonUI`, `SettingUI`, `ItemInfoUI`, `GameWinUI`, `GameLoseUI` |
| `Prefab/Game` | `CharPrefab`, `MonsterPrefab`, `CharPlacePrefab`, `AtkRangeDisplay` |
| `Meta/Char` | one `CharMeta` asset per character id |
| `Meta/Monster` | one `MonsterData` asset per monster id |
| `Meta/Dungeon` | one `DungeonMeta` asset per dungeon id |
| `Meta/Item` | one `ItemMeta` asset per item id |
| `Sprite/Char` | `Camp`, `CardGround`, `Elite`, `Profession`, `ProfessionSmall`, `Star` |
| `Sprite/Item` | `ItemGround` |
| `Audio/Music/Login` | `m_sys_title_intro`, `m_sys_title_loop` |
| `Audio/Music/Game` | `m_bat_indust_intro`, `m_bat_indust_loop` |
| `Audio/Music/Shop` | `m_sys_shop_intro`, `m_sys_shop_loop` |
| `Data/Shop` | `ShopItemDataList` |
| `Data/User` | `Saukiya`, `Test` |

The `Prefab/UI` prefab for each screen must have the matching `UIBase` subclass on its root —
`UIManager.Load` calls `GetComponent<UIBase>()` on the instantiated object.

Lua scripts are loaded from `Assets/Arknights/LuaScripts/` on disk first (`LuaEnvManager.LuaFolderLoader`),
so they do **not** need to be bundled during development.

## 4. Import the assets

1. Clone or download [kiraio-moe/Arknights-Base](https://github.com/kiraio-moe/Arknights-Base).
2. Copy its asset folders into this project under `Assets/`.
3. Let Unity import them (Spine `.skel` files need spine-unity 3.8.x, already bundled).

## 5. Assign AssetBundle names

For each folder, set the **AssetBundle** field at the bottom of the Inspector to the bundle name
from the table above — e.g. every UI prefab gets `Prefab/UI`.

To do it in bulk, use `AssetImporter.assetBundleName`:

```csharp
var importer = AssetImporter.GetAtPath("Assets/Arknights/Prefab/UI/LoginUI.prefab");
importer.assetBundleName = "Prefab/UI";
importer.SaveAndReimport();
```

Check your work with **Arknights → AssetBundles → List Assigned Bundle Names**, which prints what is
assigned and warns about any bundle the game loads but that nothing is assigned to.

## 6. Build the bundles

Run **Arknights → AssetBundles → Build For Active Target**
([`Assets/Editor/BuildAssetBundles.cs`](Assets/Editor/BuildAssetBundles.cs)).

It builds into `Assets/StreamingAssets/` and then renames the manifest bundle to the platform name.
That rename matters: `BuildPipeline.BuildAssetBundles` names the manifest bundle after the **output
folder**, so a plain build produces a bundle called `StreamingAssets`, but `ABManager` looks for the
platform name.

**On Android (the project's current active target)** the build step then *copies* everything to
`Application.persistentDataPath`, because that is where `ABManager.ABPath` points under
`UNITY_ANDROID`. Without that copy nothing you build is ever visible to Play mode. In the Editor on
Windows that folder is:

```
C:/Users/<you>/AppData/LocalLow/<Company Name>/<Product Name>/
├── Android             <- manifest bundle
├── Android.manifest
├── Prefab/UI
├── Prefab/Game
├── Meta/Char
└── ...
```

Company and Product come from Player Settings — here `Rhodes Island` / `Arknights`. Use
**Arknights → AssetBundles → Reveal Bundle Folder** to open the right folder for the active target
instead of hunting through `AppData`.

Switching the active target to Windows standalone instead uses `Assets/StreamingAssets/` directly,
with the manifest bundle named `Win` and no copy step.

## 7. Run

Open `Assets/Arknights/Scenes/StartMenu.unity` (the only scene in Build Settings) and press Play.
`GameStart.Awake` boots `GameManager`, `LuaEnvManager` and then shows `LoginUI`.

If assets are still missing you will get an explicit console error naming the bundle and asset,
rather than a bare `ArgumentNullException`.

## Unity 6 compatibility

This project was written for Unity 2020. Unity 6 replaced integer instance IDs with the `EntityId`
struct and made the old APIs **hard compile errors**, which stopped the bundled spine-unity 3.8 from
building and put the Editor into Safe Mode.

The following were patched in `Assets/Plugins/Spine/`:

| Removed API | Replacement |
|---|---|
| `Object.GetInstanceID()` | `Object.GetEntityId()` |
| `EditorUtility.InstanceIDToObject(int)` | `EditorUtility.EntityIdToObject(EntityId)` |
| `AssetDatabase.GetAssetPath(int)` | `AssetDatabase.GetAssetPath(Object)` |
| `EditorApplication.hierarchyWindowItemOnGUI` | `EditorApplication.hierarchyWindowItemByEntityIdOnGUI` |

`EntityId`'s implicit conversion **to** `int` is itself deprecated-as-error, so the affected lookup
tables are keyed by `EntityId` rather than `int`, and the hierarchy callbacks take an `EntityId`
parameter. Files touched: `SkeletonMecanim.cs`, `SpineEditorUtilities.cs`, `AssetUtility.cs`,
`DataReloadHandler.cs`, `SpineAtlasAssetInspector.cs`.

Keep these edits if you update spine-unity within the 3.8.x line.

### xLua code generation

Unity 6 also added `Span<T>` / `ReadOnlySpan<T>` overloads across core APIs. Those are `ref struct`s,
which cannot be used as generic type arguments, so xLua's generated wrappers failed with `CS0306`.

[`Assets/XLua/Src/Editor/Unity6GenConfig.cs`](Assets/XLua/Src/Editor/Unity6GenConfig.cs) registers a
`[BlackList]` member filter that skips any member with a ref-struct in its signature — the same thing
xLua already does for pointers. It lives in the `Xlua.Core.Editor` assembly deliberately: a config
class under `Assets/XLua/Editor/` would land in `Assembly-CSharp-Editor`, which depends on
`Assembly-CSharp` — the assembly holding `Assets/XLua/Gen/`. Once generation emits broken code that
assembly stops building, and the filter would be missing exactly when the generator needs it.

## Known pre-existing bugs fixed

These were latent in the original source, unrelated to the Unity 6 port:

| Fix | Why it mattered |
|---|---|
| `Asset.cs` — build Resources paths with `/`, not `Path.Combine` | On Windows `Path.Combine` emits a backslash (`Prefab/UI\Camera`), which `Resources.Load` never resolves. The entire Resources fallback was dead. |
| `LuaEnvManager.cs` — resolve scripts under `Assets/Arknights/` | `Application.dataPath` is `Assets/`, so the disk loader looked in `Assets/LuaScripts/` and missed every `require`, always falling through to the AssetBundle loader. `LoadLuaText` gained the same disk fallback so `RuaUI` screens work without bundles. |
| `SoundManager.cs` — skip null clips | `startYingPingJiShi` dereferenced `clip.length` in a coroutine. |
| `PlayerManager.cs` — never store null `PlayerData` | `Login` called `data.GetName()` on the nulls the constructor added. |
| `CommonDialogUI.cs` — handle a missing prefab | `Message()` dereferenced the result of `UIManager.Show`, which is null when the prefab is absent. |
| New: `Manager/LuaReflectionConfig.cs` — register DOTween's extension classes with `[ReflectionUse]` | Lua calls like `self.canvasGroup:DOFade(...)` threw `attempt to call a nil value` — `DOFade`, `DOScaleY`, etc. are C# *extension* methods (declared on `DOTweenModuleUI`/`ShortcutExtensions`, not on `CanvasGroup`/`Transform` themselves), invisible to plain reflection unless the declaring class is registered. Nothing in the original project ever registered DOTween for Lua, so this would have failed on Unity 2020 too — it's independent of the Unity 6 port. See the comment in that file for the extend-it-yourself pattern (any future `DOTweenModule*` class used from Lua needs adding to the same list). No `XLua → Generate Code` needed — this is a runtime reflection lookup. |
