# Scenes and startup

`Assets/Scenes/Index.unity` is the startup scene. Keep its entry first in
`Assets/Bundles/Core/Settings/GameRegister.asset` and enabled first in Build Settings/Build Profiles.
The scene contains `GameBootstrap`, with AssetReferences to the shared UI catalog and menu.
It loads and activates `Assets/Bundles/Core/Config/Generics/SharedUIContent.asset` first, including
TMP settings and `Assets/Bundles/Core/Prefab/UI/GameContext.prefab`, then loads
`Assets/Bundles/Core/Prefab/UI/IndexMenu.prefab`. The menu owns its camera/listener, EventSystem,
audio, locale bootstrap and UI. It is released when Index unloads; the context and catalog survive
reloads. The catalog handle is released at application shutdown. Index directly references no
bundled content, preventing duplicate player-data dependencies.
Inspect the menu prefab to edit those objects instead of adding duplicate managers to Index.

With the sample installed, Index's menu reference points to the Demo menu instead. The cleanup tool
restores the Core menu when initializing a new project; it does not remove Index or its shared catalog.

The menu provides the starter UI, settings and help. The game grid remains empty until another
scene is registered. Shared text is at `Assets/Bundles/Core/Prefab/UI/ui_text.prefab`, with its
localization components, font/material variants and shared catalog binding intact.

Add a gameplay scene after Index in the registry and keep it with its feature-owned content.
The removable sample scene is `Assets/Bundles/Demo/DemoRelayRun.unity`.
Set `GameMeta.sceneName` to
the Unity scene name. Assign `sceneAsset` to load by an explicit Addressable reference; without
that reference, the Addressable key must match `sceneName` for compatibility with existing entries.
Reload looks up the registered scene's reference, then falls back to its Unity scene name.
Set `name` to its StringTable key in both locales and assign an icon. `IndexManager` generates
a button per later entry.
Use `ContextManager.JumpToScene(game)`, `ReturnToIndex()` and `ReloadCurrentScene()`; the context
waits for localization before transition and handles the built-in Index separately from gameplay,
including when Index is passed directly to `JumpToScene(...)`.

## Audit

Run `SceneSmokeTests` for the startup scene and referenced menu's serialized wiring, then
`BaselineIntegrationTests` for asynchronous startup, shared loads and Index reload.
The integration fixture opens Index and enters Play Mode; preserve user scene changes before
running it. Exercise a gameplay return/reload after changing navigation.

Related: [Addressables](Addressables.md), [Localization](Localization.md), [Utilities](Utilities.md).
