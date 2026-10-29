# Agent guide

## Before changing anything

1. Confirm the working project is Baseline. Read [the project README](../README.md) and the relevant
   guide below. Keep changes within the requested project.
2. Inspect the current scripts, serialized asset and its consumers. A class being present does not
   mean a prefab, definition instance or startup registration exists.
3. Inspect `git status` and the relevant diff. Do not assume a clean checkout; preserve unrelated
   user work.
4. State the behavior or content being changed and how it will be verified. For a defect, identify
   a reproducing case. For an asset, identify its runtime consumer, reference chain and acceptance view.
5. If operating Unity, select the exact project instance and inspect its current scene, play state,
   compile state and unsaved changes. Coordinate exclusive scene/test operations with other work.
   Do not close the user's editor or discard an unsaved scene to run a batch job.

## Architectural boundaries

| Area | Contract |
| --- | --- |
| `Assets/Scripts/Core/Defs` | Authored content and validation in `Baseline.Defs`; behavior belongs in consumers. |
| `Assets/Scripts/Core/Quests` | Generic objectives, scoped runs and lasting outcomes in `Baseline.Quests`. |
| `Assets/Scripts/Core/Events/SignalSystem` | Typed communication in `Hypocycloid.Core`. |
| `Assets/Scripts/Utils` | Shared lifecycle, pooling, IO, input and other utilities in `Hypocycloid.Utils`. |
| `Assets/Scripts/Generics` | Starter scene flow, settings and locale behavior in `Baseline`. |
| `Assets/Scripts/Generics/ui` | Reusable UI code; author the prefabs and references the feature needs. |
| `Assets/Scripts/Generics/Presentation` | Signal-driven sound, music and FX in `Baseline.Presentation`; presentation does not change game state. |
| `Assets/Editor` | Editor authoring, audit tools, build helpers and tests. |

Use the existing system before adding another singleton, event bus, save store or pool. Keep
definitions as data; validate them at the loading boundary. If future simulation runs on worker
threads, copy required values into plain data on the main thread rather than reading Unity assets
from a worker. Gameplay-specific simulation and data adapters belong to the consuming feature.

## What is installed, and what is not

The Index starter scene, its menu/settings/help prefabs, English/Chinese localization, shared
Addressables and legacy audio are already wired. The example database and quest live in
`Assets/Bundles/Demo/Examples`. Presentation managers and UI components are available for
integration; author their content and bindings when a feature adopts them.

When initializing a new game from this starter, remove the sample before adding production content
through **Tools > Baseline > Remove Demo Content**. Inspect dependencies, create the tool's backup,
then verify recompilation and the generic Index. Removal is explicit, never an automatic bootstrap
or import action. See [Demo cleanup](DemoCleanup.md). Ordinary edits to Baseline do not imply that
the installed demonstration should be removed.

The dialogue module includes serialized conversation, alert and localized text prefabs
under `Assets/Bundles/Core/Prefab/UI`, a bundled shared catalog and registered example definitions. See
[Dialogue](Dialogue.md) for ownership, loading, localization and integration.

Reusable runtime assets belong under `Assets/Bundles/Core`, categorized by asset type and owned
by the `Core` Addressables group. All removable demo assets belong under `Assets/Bundles/Demo`
and the `Demo` group; demo runtime scripts belong under `Assets/Scripts/Demo`, with demo tests
inside `Assets/Editor/Demo`. Keep new production content outside all three demo roots. The two Relay Run
help manuals in StreamingAssets are explicit cleanup exceptions. Demo scripts compile normally;
retained framework scripts and tests must not depend on their types. The Core robot guide and
ambient music survive cleanup. Use the single Core folder entry and path-based `Core/...` keys; preserve localization-managed groups and
rendering/project settings when editing content ownership.

Do not create Resources folders or use runtime Resources loads. Load the shared catalog asynchronously before creating UI,
and retain Addressables handles until their consumers are finished.

Inspect existing components and assets before adopting a module. Additional gameplay controllers,
UI templates, configured sound/FX managers, cue assets, music definitions, mixers and particle
content must be authored for the feature that needs them. Document only APIs and tools that exist
in this project.

Preserve serialized compatibility when extending the starter. Inspect scene/prefab consumers and
plan migration before removing or renaming existing APIs. Module-specific compatibility details
belong in the affected guide. Package requirements and the distinction between installing a tool
and wiring it into a scene are documented in [Dependencies](Dependencies.md).

## Implementation workflow

1. Select the affected module from the [documentation index](README.md). Read [Assets](Assets.md)
   for general import practices and the module guide for its registration and lifetime contract.
2. Author static UI and defaults directly in prefabs/scenes, including state styles and responsive
   settings. Do not hard-code a visual hierarchy, palette, typography or fixed layout in runtime
   refresh/setup code. Bind runtime data and actions in code. Follow
   [UI](UI.md), [Localization](Localization.md), [Audio](Audio.md) and [FX](Fx.md) as applicable.
3. Make a narrow change. Preserve GUIDs, save identifiers, Addressable keys and serialized references.
   Keep reusable helpers independent of feature-specific controllers and content.
4. Verify compilation before adding a newly created component to an asset. Inspect the saved result
   after using an editor authoring tool; a successful command is not proof of correct serialization.
5. Use the appropriate [audit checks](Auditing.md), fixing focused failures before broader runs.
6. Report what changed, the paths, the exact verification performed, and any remaining manual checks.
   Update these guides if a path, API, content contract or installed capability changed.

## Asset production skills

Read only the matching skill when the task requires it; keep the detailed workflow in its own file.

| Task | Skill |
| --- | --- |
| Render folded paper, bent cards, or curled sheets from flat artwork | [Blender folded 2D assets](../.agents/skills/blender-folded-2d-assets/SKILL.md) |
| Animate a 2D character from separate body parts and capture frames | [Cutout character animation](../.agents/skills/cutout-character-animation/SKILL.md) |

## Documentation maintenance

Describe Baseline as a self-contained foundation. Document current capabilities, contracts and
usage without source-game comparisons or port history. Preserve required third-party attribution.

Keep instructions portable: use project-relative paths and configurable Unity executable paths.
Keep module-specific usage, authoring and audit instructions in that module's own Markdown file;
keep this guide and the project README focused on navigation and shared rules. Link to source
rather than copying large implementations. Do not add dates, times, timestamps or dated progress
and test histories. Identify validation by revision, selection, run ID and result when needed.
New workflows need a real consumer and observable success criteria; do not document proposed
tools as installed. Documentation-only changes normally need link, path and API checks, not another
full Unity suite.
