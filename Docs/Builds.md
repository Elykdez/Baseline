# Builds

## Addressables content

Use the Addressables Groups window's **Build > New Build > Default Build Script** command.
Confirm the active target and profile before building. Inspect the build result and output path,
then exercise a real built-content load. A successful content build does not validate a player build.

For editor verification against bundles, select **Use Existing Build** in the Groups window's
Play Mode Script selector after building. Use **Use Asset Database** for ordinary authoring tests.

## Player builds

Inspect `Assets/Editor/BuildConfig.asset`, the active build profile, platform modules, version
configuration and build filters. `BuildPreprocessor` changes the player version.
`UserConfigBuildFilter` temporarily stashes/restores the editor's local config to keep it out
of player builds; verify restoration after a failed build before making unrelated changes.

Set the new product's name, application identifiers, icons and signing configuration in Player
Settings. The starter uses `com.example.baseline` and has no inherited release signing setup.
The Android setup helper reads `BASELINE_KEYSTORE_PATH`, `BASELINE_KEYSTORE_PASS`,
`BASELINE_KEYALIAS_NAME` and `BASELINE_KEYALIAS_PASS` when signing is required. Keep credentials
out of tracked files.

Some starter artwork remains as supporting content; replace it for the new product.
Do not commit Library caches, generated Addressables output, logs or local settings.

## Audit

Check package resolution and compilation first. Build for the required target, verify its
Addressable loads and exercise the player on the intended device. Record the revision, target,
profile, output path and result without dates or timestamps. Editor tests and a Windows content
build do not establish mobile or other player-platform acceptance.

Related: [Dependencies](Dependencies.md), [Addressables](Addressables.md), [Saves](Saves.md),
[Auditing](Auditing.md).
