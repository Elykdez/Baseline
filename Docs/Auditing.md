# Auditing and verification

Start with focused checks using Baseline's actual fixtures and content. Preserve
assertions and evidence; choose a smaller relevant test selection rather than weakening tests.

## Review the change

1. Identify the behavior/content contract and affected consumers. Review the actual changed scripts
   and assets, not just the author's summary. Separate existing defects from regressions introduced
   by this change.
2. Check startup order, scene ownership, enable/disable/destroy cleanup, duplicate managers, signal
   unsubscription, Addressables handles and active/parked pool objects.
3. Check stable identities and serialized links: definition IDs, quest step/outcome tags, save keys,
   GUIDs, sprite subassets, Addressable keys, prefab overrides and UnityEvent targets.
4. Check registration and validation. Does the real consumer reference the asset? Is its database
   loaded and validated? Are cross-asset references and every required translation present?
5. Check visible behavior through the actual scene/prefab. Import success and valid serialization
   do not establish correct layout, art alignment, audible output or interaction.
6. Report actionable findings first, ordered by severity, with file/line or asset/object references,
   the triggering condition, observed risk and a concrete correction. State residual test/manual
   gaps even when no defects are found.

For a new feature, add content checks for its real catalog/reference graph when that graph has
invariants worth protecting. Existing sample tests do not enumerate every asset added in the future.
Document only test fixtures and capture tools that exist in this project.

## Pick the verification boundary

| Change | Checks |
| --- | --- |
| Documentation only | Local links/anchors, path existence, source API/field/menu accuracy, review of executable examples. No full Unity run solely for prose. |
| Focused bug or component change | Reproducing test, affected fixture and directly affected consumers. |
| New/changed authored assets | Import/compile status, missing references/scripts, relevant validation and content checks, actual visual/interaction inspection. |
| Addressable registration/packaging | Entry and load-path checks, target content build and a real built-content load where appropriate. |
| Completed cross-system integration | One unfiltered EditMode suite after focused checks, including its Play Mode transitions; appropriate content/build checks. |
| Player/platform acceptance | Target player build and device checks where required. A Windows content build or editor screenshot does not establish mobile/player acceptance. |

After a pass, repeat only for a relevant change, a failure or unresolved uncertainty. If the full
suite fails, reproduce/fix with focused tests and then repeat integration acceptance. Never describe
a filtered run as the full suite. Do not rerun expensive unrelated simulations.
Use **Use Asset Database** for the full suite: its localization fixtures also run outside Play Mode.
After building content, select **Use Existing Build** and run the two Play Mode transition cases in
`BaselineIntegrationTests` separately. Compare assets loaded through Addressables in these tests;
AssetDatabase copies have different object identities from their bundled counterparts. Restore the
original play-mode builder afterward.

## Installed tests and tools

Tests live under `Assets/Editor/Tests`, in `Assembly-CSharp-Editor`, generally under
`Baseline.Tests.Editor`. Read the fixture before relying on it.

| Fixture | Main coverage |
| --- | --- |
| `BaselineCoreTests` | Definition validation, quest progression, prerequisites, log migration and backup recovery. |
| `SignalBusTests` | Dispatch, subscriptions and lifecycle behavior. |
| `ConfigSettingsPersistenceTests` | Settings round trips, migration, isolated directories and build-config exclusion helpers. |
| `MonoSingletonTests`, `ObjectPoolingTests` | Singleton/pool ownership and reuse. |
| `BasicUITests` | Localized/plain text, actual en/zh changes and selected UI primitive behavior. |
| `DialogueTests`, `CharacterDialogLifecycleTests` | Conversation revisions, required playback, cancellation, queued alerts and repeated input. |
| `SharedUIContentTests` | Serialized shared prefabs, registered sample dialogue, translations, font variants and scene reference keys. |
| `AudioFxManagementTests` | Audio settings, signal routing, voice/FX management and effect lifetime. |
| `AudioImportTests` | Folder-owned music/effect profiles and stable repeated configuration. |
| `QuestAuditTests` | Content discovery, identity/reference checks, isolated previews, guard facts and tracker cleanup. |
| `AssetBundlingTests` | No Resources assets/loads, one bundle owner, and no direct bundled dependencies in Index. |
| `SceneSmokeTests` | Starter scene/prefab serialized wiring checks. |
| `DemoCleanupTests` | Demo ownership boundaries, safe backup paths and retained dependency blocking. |
| `BaselineIntegrationTests` | Addressable entry resolution, Index startup/shared load, and authored dialogue/alert behavior with live locale changes in Play Mode. |

Module-specific audit tools and acceptance checks are described in their own guides:
[Signals](Signals.md), [Quests](Quests.md), [UI](UI.md), [Localization](Localization.md),
[Audio](Audio.md), [FX](Fx.md), [Addressables](Addressables.md) and [Builds](Builds.md).

Authored sample asset cases are partial fixture methods under `Assets/Editor/Demo/Examples`.
[Demo cleanup](DemoCleanup.md) removes those cases with the example content; the generic fixtures
remain and use their own temporary definitions and signals.

## Run safely in the editor

Use **Window > General > Test Runner > EditMode** and select the relevant method/fixture. Some
EditMode integration tests enter Play Mode, reload the domain and open Index. They are not a
read-only inspection of the user's current scene. Preserve user changes before running them.

With Unity MCP, select the exact Baseline instance and inspect editor readiness first. Run one
test job at a time. For example, a focused group selection is:

```json
{"mode":"EditMode","group_names":["^Baseline\\.Tests\\.Editor\\.BasicUITests(?:\\.|$)"],"include_failed_tests":true}
```

For a single regression, get its exact name from test discovery and pass `test_names`; do not
guess parameterized names. For full acceptance omit name, group, assembly and category filters:

```json
{"mode":"EditMode","include_failed_tests":true}
```

Poll the returned job ID with bounded waits. If status stops updating across a domain reload,
inspect the runner and fresh completed XML before starting another job. A stale MCP callback
is neither a failed test result nor authorization to kill/restart the editor. Do not compile,
change scenes or run a second suite concurrently with the active job.

## Batch commands

Use only when this project is closed in the interactive editor. Run from the Baseline root, set
the installed executable path, and keep reports in `Logs` with distinct names. `Start-Process`
waits for Unity's real exit code; an immediately returned PowerShell command is not build evidence.

```powershell
$unityEditor = 'D:/Unity/Editor/6000.3.14f1/Editor/Unity.exe'
$projectRoot = (Get-Location).Path
$runId = [guid]::NewGuid().ToString('N')
$logRoot = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
$resultPath = Join-Path $logRoot "editmode-$runId.xml"
$logPath = Join-Path $logRoot "editmode-$runId.log"
$testArgs = @(
    '-batchmode', '-projectPath', ('"{0}"' -f $projectRoot),
    '-runTests', '-testPlatform', 'EditMode',
    '-testResults', ('"{0}"' -f $resultPath),
    '-logFile', ('"{0}"' -f $logPath)
)
# For a focused run, add its selection before starting the process:
# $testArgs += @('-testFilter', '^Baseline\.Tests\.Editor\.BasicUITests(?:\.|$)')
$testProcess = Start-Process -FilePath $unityEditor -ArgumentList $testArgs `
    -WindowStyle Hidden -Wait -PassThru
if (!(Test-Path -LiteralPath $resultPath)) { throw "No completed test report: $logPath" }
[xml]$report = Get-Content -LiteralPath $resultPath -Raw
$report.'test-run' | Select-Object result, total, passed, failed, skipped
if ($testProcess.ExitCode -ne 0 -or $report.'test-run'.result -ne 'Passed') {
    throw "Tests did not pass; inspect $resultPath and $logPath"
}
```

Also verify the expected fixture names, nonzero case count and unintended skips in the XML.
Do not add `-quit` to the test command. Omit `-nographics` for general use because camera and
render-texture captures require a graphics device.

For content and player build procedures, see [Builds](Builds.md).

## Evidence and cleanup

Record the revision/change, Unity version, target, exact selection, unique run ID, result path,
counts, skips and remaining manual work. Do not record dates, times or timestamps in the guides.
Editor tests may overwrite a shared `TestResults.xml`; associate the completed report with the
actual runner job and selected cases, then preserve it under a unique run ID. `Temp` can be cleared
by Unity. Do not treat a result from another job or revision as current evidence.

For visual inspection, state the scene/prefab, language, viewport size, render mode and behavior
tested. Inspect captures at intended display scale, including clipping, glyphs, contrast, pivots,
layering and input hit areas. Restore scene, locale, view and temporary objects after inspection.
Do not claim Play Mode interaction from an Edit Mode screenshot or sound quality from a test mock.

Tests that write saves must isolate their directories and restore any deliberately changed
PlayerPrefs. Clean up only temporary data and artifacts created by the task. Never delete the
user's saves, loosen assertions, or remove unrelated asset overrides to obtain a pass.
