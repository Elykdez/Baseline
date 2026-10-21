# Saves and settings

| Store | Location and behavior |
| --- | --- |
| `ConfigSettingsPersistence` | `RuntimeDataPaths.WritableRoot/config/user_config.json`; settings plus keyed int/float/string data, locale code, atomic replacement and legacy migration. No backup history. |
| `RuntimeDataPaths` | Writable StreamingAssets in the editor and Windows/Linux; persistent-data fallback for read-only installations and other platforms. |
| `QuestLog` | `Application.persistentDataPath/Quests.json`; outcomes, not an in-progress journal. Atomic writes keep a previous valid `.bak`; v1 flags/counts migrate to v2 named values. Invalid/incomplete/unknown schemas try the backup. |

Use the typed settings APIs (`SetLocale`, `SetMasterVolume`, etc.) and `GetInt`/`SetInt`,
`GetFloat`/`SetFloat`, `GetString`/`SetString` for feature data. Do not introduce new PlayerPrefs
storage for normal settings. Preserve old identifiers when renaming labels; save-contract changes
need a migration and old-save fixtures, not deletion of the user's data.

`UserConfigBuildFilter` temporarily excludes the editor's StreamingAssets config from player builds.
That generated directory is ignored by Git. It is not a place for authored defaults or shipping
content. The filter does not mean every arbitrary file under StreamingAssets is excluded.

Tests use temporary directories with `QuestLog.UseDirectory(temp)` and
`ConfigSettingsPersistence.UseDirectory(temp)`, disposing both scopes afterward. Config isolation
does not migrate PlayerPrefs unless explicitly requested. A Play Mode domain reload clears static
overrides; reapply isolation after entering Play Mode before accessing the stores. The retained
Index startup smoke can initialize the project's normal settings; it is not a blanket isolation
fixture for new save-writing tests.

## Audit

Run `ConfigSettingsPersistenceTests` for settings changes and the quest-save cases in `BaselineCoreTests` for log changes. Include old-format fixtures, malformed-primary recovery where supported, and round trips. Never delete the user's data to obtain a pass.

Related: [Quests](Quests.md), [Localization](Localization.md), [Audio](Audio.md), [Builds](Builds.md).
