# Demo cleanup

Remove the installed examples when initializing a new project from Baseline, before adding
production content. Use **Tools > Baseline > Remove Demo Content** to inspect, back up and remove
them explicitly. Importing or starting the project never triggers removal automatically.
The inspection lists the exact affected assets and blocks removal when retained assets or scripts
depend on demo content. Resolve those dependencies before continuing. Save scene changes and leave
Play Mode before running the operation.

The action removes the Relay Run scene, its runtime code, tests, art, definitions,
audio and prefab content, plus the small example quest/dialogue catalog and its sample tests.
The owned roots are `Assets/Scripts/Demo` for demo runtime scripts, `Assets/Editor/Demo` for demo
tests, and `Assets/Bundles/Demo` for demo assets, including
`Assets/Bundles/Demo/DemoRelayRun.unity`. Removing the demo root includes its bundled scene.
The only raw-file exceptions are
`Assets/StreamingAssets/Desc/DemoRelayRun_en.md` and `DemoRelayRun_zh.md`.
Demo runtime namespaces use `Baseline.Demo`; demo scripts and authored config files use the
`Demo` filename prefix. Scripts compile normally, and the `Assets/Editor/Demo` folder
keeps demo tests editor-only. Scripts are not packed as executable bundle content.

Cleanup restores Index's bootstrap reference to the generic `IndexMenu.prefab`, removes demo game
registry and build scene entries, removes Addressables entries and the empty `Demo` group,
and removes `DemoRelay_` and legacy sample dialogue localization keys. The editor opens Index.
Rebuild Addressables and the player afterward; existing built bundles are not rewritten by cleanup.

The reusable framework remains, including its tests, shared UI prefabs, settings, localization,
dialogue styles, and robot guide. `Assets/Bundles/Core/Config/Dialogue/GuideCharacter.asset` is shared
presentation content, so removing example definitions does not break the generic alert UI.
The `Core` folder and Addressables group remain, including the retained
`Assets/Bundles/Core/Sound/Music/relay_ambient.wav` used by the generic menu. Rendering settings,
localization-managed groups and the cleanup tool also remain. Retained code must not reference
demo types; removal is intended to leave the project compiling and the generic Index usable.
**Create Missing Shared UI Content** only repairs shared assets and does not reinstall demos.

Before mutation, the tool writes the removed files and their `.meta` files, Index, the game registry,
build scene settings, Addressables settings, and localization tables to `DemoBackups/<id>` at the
project root. Backups are ignored by Git. The tool restores this backup if removal throws an error.
Use **Restore a demo cleanup backup** in the same window to restore a selected backup manually.
Restoring replaces the recorded files, including shared registry/localization/Addressables settings;
review intervening edits before restoring. Player settings, saves and quest outcomes are untouched.

Do not place production content anywhere under the reserved demo roots. Customized files in those folders are also
removed and backed up. The dependency scan covers Unity asset references, serialized Addressable
GUIDs, localized keys, and direct uses of demo namespaces/types in project C# sources. Runtime
strings assembled dynamically and external code generators still require review before stripping.

`DemoCleanupTests` checks folder boundaries, backup path validation and dependency blocking. Sample
asset tests are partial fixture methods inside `Assets/Editor/Demo/Examples`; they are removed with
the examples, while generic framework fixtures keep their independent validation coverage.

For destructive acceptance, use a disposable project copy and run
`-executeMethod Baseline.Editor.DemoCleanup.RemoveForBatchTest -batchmode -quit`.
The method writes `Logs/demo-cleanup-backup.txt` and logs `DEMO_CLEANUP_REMOVAL_PASSED` after
removal. Run a fresh Unity process to compile and execute the remaining framework tests; the
removal marker alone does not establish that post-removal compilation and startup passed.
