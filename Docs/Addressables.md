# Addressables

Reusable runtime content lives under `Assets/Bundles/Core`; sample assets live under
`Assets/Bundles/Demo`. Resources folders and runtime Resources loads are
forbidden, including TMP defaults. `SharedUIContent` binds TMP settings explicitly; its private
settings field is preserved by `Assets/Scripts/Generics/link.xml` for IL2CPP.

The [Core](../Assets/AddressableAssetsData/AssetGroups/Core.asset) group owns Core content and uses
Pack Together. The [Demo](../Assets/AddressableAssetsData/AssetGroups/Demo.asset) group owns both
Relay Run and the example definitions, including the demo scene. Unity may emit a separate scene
bundle even when the group uses Pack Together. These are Addressables ownership groups, not a
promise of exactly one physical file each. Localization-managed groups keep their existing table,
locale and asset ownership; rendering/project settings also remain outside these content roots.

Demo has one expandable `Demo` folder entry and one explicit `DemoRelayRun` scene entry to
preserve its launch key. `Examples` and `RelayRun` are discovered under the root, with addresses
such as `Demo/RelayRun/Prefabs/DemoRelayMenu.prefab`; do not register those subfolders separately.

Demo runtime scripts live under `Assets/Scripts/Demo`, with demo tests under `Assets/Editor/Demo`.
They compile normally and are not executable assets loaded from the Demo bundle. Use the cleanup
tool described in [Demo cleanup](DemoCleanup.md) to remove sample scripts and assets together.

Core has one expandable folder entry, `Core`, for `Assets/Bundles/Core`. Child addresses follow
their relative asset paths, for example `Core/Prefab/UI/ui_loading.prefab`. Do not add explicit
entries for folders or assets already covered by this root. Localization-owned assets retain
their explicit entries in the localization groups and take precedence over folder discovery.
The old `Shared/...` aliases have been removed; string-based callers must use `Core/...` paths.
Serialized AssetReferences retain their existing GUIDs. Core also owns the robot guide and
retained ambient music, so removing Demo leaves those generic-menu dependencies available.
While the sample is installed, the shared game registry's demo icon references the Demo bundle.
Demo cleanup removes that registry row before rebuilding Core.
Addressables is the authoritative ownership system; migrated content has no legacy
`armychess.*` AssetBundle tags. Build this layout with **Build > New Build > Default Build Script** in the Addressables Groups window.
`GameBootstrap` loads its menu (the generic fallback is `Core/Prefab/UI/IndexMenu.prefab`) through its Inspector-assigned
asset reference, retains the asset handle while the menu lives, and releases it on Index teardown.
It loads the shared catalog first through an AssetReference and retains that handle until shutdown.
The catalog owns the context prefab reference; Index has no direct bundled-content dependencies.

1. Determine whether a parent Addressable folder already includes the asset, and inspect its
   effective child address. Avoid accidental competing explicit entries or duplicate keys.
2. Use an existing compatible local group, or create a feature group with an intentional profile,
   build/load paths and packing policy. Preserve localization-owned groups and their table links.
3. Choose a stable address for independently loaded content. Record the consumer. For gameplay
   scenes, the starter expects the address, Unity scene name and `GameMeta.sceneName` to match.
4. Assign serialized references or the load address, then verify both the entry GUID and its
   dependencies. Existing entry tests prove GUID resolution, not that every future runtime key works.
5. Run **Build > New Build > Default Build Script** in the Addressables Groups window for the intended target. Exercise the actual load path.
   Asset Database play mode can hide a missing or stale built bundle; use built-content testing
   when accepting packaging changes. A content build is not a player build.

Each load needs an owner and release boundary. `Addressables.LoadAssetAsync<T>` gives a handle;
retain it until consumers finish, release it on success or failure cleanup as appropriate, and do
not keep using the asset after release. Loading a prefab asset does not instantiate it. Cloning a
loaded prefab with `Instantiate` does not take a new Addressables reference for each clone; retain
the original handle until all clones, including parked pool objects, are no longer needed.
If using Addressables instantiation, pair that path with its corresponding instance release API.

Existing helpers:

- [AssetLoadBase](../Assets/Scripts/Utils/IO/AssetLoadBase.cs) loads one asset in `Awake`, raises
  `OnAssetLoad` on success and releases its handle on destruction. `AssetLoadGameObject` loads
  a prefab asset; it does not instantiate it.
- [ResourceManager](../Assets/Scripts/Utils/IO/ResourceManager.cs) reads a `resourceName=address`
  map through `resPath`, caches loads and releases through `Release(name)` or destruction. Wait
  for the map to be ready before using it. It is not a project-wide automatic registry.
- [ContextManager](../Assets/Scripts/Generics/ContextManager.cs) owns the starter's scene-loading
  path. Use it for menu/game transitions instead of adding a second scene-loading system.

## Audit

Run the entry/load cases in `BaselineIntegrationTests`, build content for the intended target and exercise the real consumer. Asset Database play mode can conceal packaging errors. Inspect both explicit entries and folder-derived addresses, dependency ownership and releases.

Related: [Assets](Assets.md), [Scenes](Scenes.md), [Builds](Builds.md), [Utilities](Utilities.md).
