# Dependencies

Unity resolves dependencies from [manifest.json](../Packages/manifest.json). Commit Unity's
updated [packages-lock.json](../Packages/packages-lock.json) with intentional dependency changes.
Git must be available to the Unity process; Git LFS is also required for this project's assets.

## Git packages

| Package | Version | Upstream | Module instructions |
| --- | --- | --- | --- |
| `com.yasirkula.ingamedebugconsole` | 1.9.0 | [UnityIngameDebugConsole](https://github.com/yasirkula/UnityIngameDebugConsole) | [IngameDebugConsole](IngameDebugConsole.md) |
| `com.bgtools.playerprefseditor` | 1.2.5 | [bgTools-playerPrefsEditor](https://github.com/Dysman/bgTools-playerPrefsEditor) | [PlayerPrefsEditor](PlayerPrefsEditor.md) |
| `com.dbrizov.naughtyattributes` | 2.1.6 | [NaughtyAttributes](https://github.com/dbrizov/NaughtyAttributes) | [NaughtyAttributes](NaughtyAttributes.md) |

These three requirements use GitHub URLs pinned to full commit hashes in the manifest. Unity
downloads them into its package cache. Do not copy their implementation into `Assets`, embed
their source under `Packages`, or commit cache directories. ShaderlabVSCode is not required.
The existing Unity MCP Git dependency remains configured separately in the manifest.

## Change or recover a dependency

1. Verify the upstream revision contains a root `package.json` with the expected package name.
2. Change the Git URL/revision in the manifest and let Package Manager resolve it. For an upgrade,
   review upstream changes and compare any serialized script or assembly GUIDs before replacing a pin.
3. Inspect resolution errors and compilation. Avoid duplicate copies under `Assets`; the old
   `Assets/Editor/PlayerPrefsEditor` and `Assets/Editor/NaughtyAttributes` directories were replaced
   by their package requirements.
4. Check the lockfile records the intended Git source and commit. Review unrelated lock changes.
5. Run affected consumers and the module's audit checks. Package installation alone does not wire
   a console prefab into a scene or add feature-specific Inspector attributes.

For Git failures, check executable availability and GitHub connectivity before changing versions.
Do not work around failed resolution by vendoring source. See [Auditing](Auditing.md) for tests
and [Builds](Builds.md) for target verification.
