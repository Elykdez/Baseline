# Adding and maintaining assets

Identify the consumer and its data contract, author the content,
register it explicitly, validate references, then inspect it through the real runtime presentation.
A file appearing in the Project window is only the import step.

## Plan the content contract

Before importing or generating an asset, establish:

- The component/definition that consumes it, its serialized field and how the consumer is loaded.
- Whether this is a replacement, a new independent asset or a prefab variant.
- Required dimensions, transparency, pivot, scale, slice borders, orientation and alignment with
  other layers. Inspect the target renderer and a suitable existing asset.
- Applicable platform settings, compression, texture/audio type and memory budget.
- The intended in-game size, background, locale and interaction to use for acceptance.

Define tint, padding, direction, layer and tile contracts where the consumer needs them. Choose
pixel dimensions, PPU, shaders and importer settings for that consumer. Record the game's art
conventions when they are established.

## Locations

| Location | Current meaning |
| --- | --- |
| `Assets/Bundles/Core` | Retained framework content, owned by the Core Addressables group. |
| `Assets/Bundles/Core/Texture` | Existing texture/sprite sources. Put related new runtime images in a named subfolder. |
| `Assets/Bundles/Core/Material` and `Assets/Bundles/Core/Anim` | Existing material and animation content. |
| `Assets/Bundles/Core/Sound` | Existing audio content; use feature/category subfolders for additions. |
| `Assets/Bundles/Core/Font` | Existing fonts and related assets. Preserve locale coverage and fallbacks. |
| `Assets/Bundles/Core/Prefab/UI` | Starter UI prefabs, including `ui_main`, `ui_gridButton` and settings/help panels. |
| `Assets/Bundles/Demo/RelayRun/Fx` | Authored canvas sprites for Rex's run, takeoff and landing dust. |
| `Assets/Bundles/Core/Settings/GameRegister.asset` | The current scene/menu registry. |
| `Assets/Locales/Tables` | StringTable collection, shared key data, English and Chinese tables. |
| `Assets/Settings` | Rendering/project content; not a general definition catalog. |
| `Assets/Bundles/Demo/Examples` | Bundled example quest, database and dialogue definitions. |
| `Assets/Bundles/Demo/RelayRun` | Removable runner assets, owned by the Demo group. |
| `Assets/Bundles/Demo/DemoRelayRun.unity` | Removable demo scene, also owned by the Demo group. |
| `Assets/Scripts/Demo` | Removable demo runtime scripts. |
| `Assets/Editor/Demo` | Removable demo tests, compiled only in the editor. |
| `Assets/AddressableAssetsData` | Addressables settings, groups, schemas and builders. |
| `Assets/StreamingAssets` | Files shipped as raw data unless a build filter excludes them; runtime config is specially excluded. |

Core contains reusable assets, including the robot guide and retained `relay_ambient.wav` music.
The two demo help manuals under `Assets/StreamingAssets/Desc` are explicit raw-file exceptions.
Remove the sample via the [cleanup tool](DemoCleanup.md) when initializing a new game. Do not put
production code or content under `Assets/Scripts/Demo`, `Assets/Editor/Demo` or `Assets/Bundles/Demo`: these roots are reserved
for content the tool may remove. Demo scripts compile normally; keep editor-only code inside
`Assets/Editor/Demo` rather than mixing it into runtime source.

**Proposed when needed:** use a feature-owned folder and Addressables group for new game-specific
content, and `Assets/Bundles/Core/Prefab/UI/Component` for newly authored reusable controls.
These directories are organization suggestions, not automatic discovery mechanisms.
Keep import masters, generation records and preview outputs separate from runtime dependencies.
Do not put shipping content in `Library`, `Temp` or `Logs`.

## Import, replace and move

1. Inspect the existing asset's importer and referencing prefab/definition. Match settings to the
   consumer, not merely to another file with the same extension. For sprites inspect single/multiple
   mode, PPU, pivot, borders, alpha, filtering, wrapping, mipmaps and platform compression. For audio
   inspect load type, streaming/decompression, channels, sample rate and intended looping.
2. For replacement content, preserve the existing asset's `.meta` GUID and path where possible.
   Do not delete/recreate it simply to replace pixels or audio. Sprite-sheet slicing can change
   subasset references even when the texture GUID is preserved; audit those references afterward.
3. Move/rename within Unity or through `AssetDatabase.MoveAsset` so metadata moves with the file.
   For a new independent duplicate, let Unity assign a new GUID. Do not copy a `.meta` unchanged
   to two different assets. When porting a genuinely shared asset from another project, preserve
   its original GUID only after checking for conflicts and bringing the required dependencies.
4. Let Unity import, then inspect errors, subassets and serialized references. A copied `.prefab`
   may still reference absent scripts, fonts, materials, clips or textures from the source project.
5. Author prefab changes with Unity's prefab/serialization APIs or the Inspector. Preserve nested
   prefab links, overrides, UnityEvent bindings and local file IDs. Avoid hand-building YAML or
   replacing whole hierarchies to change one reference. If a targeted YAML edit is necessary,
   resolve identifiers from existing assets and verify the result by loading it in Unity.
6. Record external asset source, license/attribution and edits in a nearby `SOURCES.txt` or equivalent.
   For generated content, record the reference/prompt and
   transformation choices needed to reproduce it. Keep large working files out of runtime dependencies.

A prefab variant is useful when a specialized asset should inherit future changes from a shared
base. A duplicate is independent. Choose deliberately; do not unpack nested prefabs as a shortcut.

## Registration is separate from placement

| Content | Required connection |
| --- | --- |
| A new `Def`/`QuestDef` | Add it to the consumer's `DefDatabase.definitions`; validate and reference/load that database. |
| Sound cue or FX event | Assign it to the button/presentation adapter/event field that requests it; validate the authored asset. |
| Modern audio mix/music | Assign to an authored `SoundManager` before it initializes. |
| UI prefab | Assign it to the owning feature or template field, or register a deliberate Addressable load key. |
| Gameplay scene | Keep Index in `Assets/Scenes`. Place feature scenes with their owned content; the demo scene lives in `Assets/Bundles/Demo/RelayRun/Scenes`. Unity scene name, Addressable key and `GameMeta.sceneName` must agree. Add it after Index in `GameRegister.games`. |
| Player-facing text | Shared StringTable key with both translations, plus an assigned localized reference on its consumer. |

Definitions and presentation assets do not register themselves by folder. A directly referenced
dependency does not necessarily need its own Addressable entry: decide what must be loaded
independently and what should travel with its owning prefab/database.

## Atlases and acceptance

**Tools > Hypocycloid > Assets > Atlas Packer** writes to `Assets/Bundles/Core/Texture/Atlas`.
Inspect [AtlasPacker](../Assets/Editor/Build/AtlasPacker.cs) before using it for a new art style:
its packing settings include padding 4, no rotation/tight packing, alpha dilation, bilinear
filtering and no mipmaps. The packing path does not automatically register generated atlases
as Addressable. Verify references and registration separately.

For a completed asset change, inspect the saved prefab/definition and its reference chain, run
the relevant validation, and view the result in Unity at intended scale. Check transparency,
alignment, tint/material behavior, clipping, locale font coverage and interaction as applicable.
An image viewer proves source pixels, not the imported/runtime result. Save evidence outside
temporary Unity directories and report what was visually inspected. See [Auditing](Auditing.md). Address registration and handle ownership are covered in
[Addressables](Addressables.md); target content/player builds are covered in [Builds](Builds.md).
