# Baseline agent documentation

Start with [Agent Guide](AgentGuide.md). Each module owns its usage, authoring and audit instructions.
General guides cover shared workflow, asset import and test execution.

## Shared guides

| Task | Guide |
| --- | --- |
| Plan work and respect project boundaries | [Agent Guide](AgentGuide.md) |
| Resolve Git packages and editor tools | [Dependencies](Dependencies.md) |
| Import, replace and preserve asset references | [Assets](Assets.md) |
| Review changes, select tests and preserve evidence | [Auditing](Auditing.md) |
| Build content and players | [Builds](Builds.md) |
| Play and author the integrated robot-rex example | [Relay Run](DemoRelayRun.md) |
| Initialize a new game by removing the sample safely | [Demo cleanup](DemoCleanup.md) |

## Module guides

| Module | Guide |
| --- | --- |
| Typed events and listener ownership | [Signals](Signals.md) |
| Authored definitions and database registration | [Definitions](Defs.md) |
| Objectives, scoped runs and endings | [Quests](Quests.md) |
| Characters, conversations and queued alerts | [Dialogue](Dialogue.md) |
| Settings and persistent quest outcomes | [Saves](Saves.md) |
| Startup, menu registry and scene transitions | [Scenes](Scenes.md) |
| Singletons and pooling | [Utilities](Utilities.md) |
| Prefabs, controls and reusable UI | [UI](UI.md) |
| String tables, locale changes and text binding | [Localization](Localization.md) |
| Sound cues, music, mixing and voice requests | [Audio](Audio.md) |
| Visual effects, camera shake and effect lifetime | [FX](Fx.md) |
| Asset registration, loading and handle ownership | [Addressables](Addressables.md) |
| In-game log and command console | [IngameDebugConsole](IngameDebugConsole.md) |
| Editor preference inspection | [PlayerPrefsEditor](PlayerPrefsEditor.md) |
| Inspector attributes and drawers | [NaughtyAttributes](NaughtyAttributes.md) |

## Scope

These guides describe Baseline's modules, content contracts and verification workflows.
Baseline's current source and serialized assets are authoritative. Define gameplay rules, art
conventions and feature-specific content when building a game on this foundation.

Paths are relative to the Baseline root. Paths marked **proposed** describe future content, not
installed assets. Keep the guides self-contained.
Keep instructions in their owning module and do not record dates, times or timestamps.

See the [project README](../README.md) for setup and project conventions.
