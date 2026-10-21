# Baseline

> A personal presets about reusable Unity foundation for assets, scene flow, UI, localization, quests, audio and effects.

## Editing

Open this folder with **Unity 6000.3.14f1**, then open `Assets/Scenes/Index.unity` and press Play.

- The Index provides the generic menu, settings, localization, audio, loading UI and scene navigation.
- The installed **Relay Run** demo is a playable robot-rex runner with a layered parallax menu, a 600-metre quest, conversations, audio and effects. See [Relay Run](Docs/DemoRelayRun.md) for controls, authoring and module integration.
- Index loads the demo menu through Addressables. The generic `Assets/Bundles/Core/Prefab/UI/IndexMenu.prefab` remains available when demo content is removed.
- The localized text template is `Assets/Bundles/Core/Prefab/UI/ui_text.prefab`.
- Reusable runtime content lives in `Assets/Bundles/Core`, owned by the `Core` Addressables group.
  Sample content lives in `Assets/Bundles/Demo`, owned by `Demo`. Resources assets and runtime
  Resources loads are forbidden.
- Index loads the shared UI catalog and TMP settings from Addressables before creating UI.
- Use **Tools > Baseline > Quest Debugger** for audits/previews and **Tools > Hypocycloid > Audio** for audio import optimization and validation.
- Use **Tools > Baseline > Remove Demo Content** to inspect dependencies, back up and remove the demo while retaining the reusable starter. See [Demo cleanup](Docs/DemoCleanup.md).

## Clone

This project uses Git LFS for binary assets and the large font atlas. Install Git LFS before cloning:

```powershell
git lfs install
git clone https://github.com/Elykdez/Baseline.git
cd Baseline
git lfs pull
```

Open the clone with Unity 6000.3 and let Unity resolve packages and import assets. Library caches, local user settings, runtime config, logs and build outputs are not tracked.

## Initialize a new project

Remove the installed sample when initializing a new game from Baseline, before adding production
content. This is a deliberate editor operation, not an automatic action on import or startup.

1. Save scene changes, leave Play Mode and open **Tools > Baseline > Remove Demo Content**.
2. Inspect the removal list and resolve any retained references to demo content. Use **Back up and remove demos** to create a backup and restore the generic Index menu.
3. Let Unity recompile, open Index and verify the retained starter, then rebuild Addressables.

The reserved roots `Assets/Scripts/Demo`, `Assets/Editor/Demo` and `Assets/Bundles/Demo` are removed together,
including the bundled demo scene and the two explicit Relay Run help manuals in StreamingAssets. Keep production code and  content outside those roots. Core assets, framework code, tests, localization and the cleanup tool remain. See [Demo cleanup](Docs/DemoCleanup.md) for dependency checks, restoration and verification.

## Setups

- Project identity and runtime namespace are `Baseline`; shared utilities and signals use `Hypocycloid.Utils` and `Hypocycloid.Core`. Preserve asset GUIDs when moving content.
- Demo gameplay lives in `Baseline.Demo.RelayRun`; demo source and config filenames use the `Demo` prefix. Runtime scripts stay under `Assets/Scripts/Demo`, with demo tests in `Assets/Editor/Demo`.
- Author feature content and scene wiring through the corresponding module guide. Shared conversation, alert and localized text prefabs and registered example definitions are included.
- The package lock resolves Addressables 2.9.1, localization 1.5.12, Input System 1.19.0
  and URP 17.3.0. Newtonsoft JSON 3.2.2 is a direct dependency for quest-save schema checks, because Unity's deserializer fills  in missing collections.
- IngameDebugConsole, PlayerPrefsEditor and NaughtyAttributes are Git package requirements; see [Dependencies](Docs/Dependencies.md). Their implementation is not vendored in the project.

## Documentation

Start with [Docs/AgentGuide.md](Docs/AgentGuide.md). The [documentation index](Docs/README.md) links to separate module guides, asset authoring, Git package requirements and audit workflows. The guides distinguish installed Baseline capabilities from content and tooling a future feature must author.

### Modules

| Module | Instructions |
| --- | --- |
| Startup and scene flow | [Scenes](Docs/Scenes.md) |
| Signals | [Signals](Docs/Signals.md) |
| Definitions | [Definitions](Docs/Defs.md) |
| Persistence | [Saves](Docs/Saves.md) |
| Quests | [Quests](Docs/Quests.md) |
| Conversations and queued alerts | [Dialogue](Docs/Dialogue.md) |
| Presentation | [Audio](Docs/Audio.md), [FX](Docs/Fx.md) |
| Utilities | [Utilities](Docs/Utilities.md) |
| Asset management | [Addressables](Docs/Addressables.md) |
| UI and Localization | [UI](Docs/UI.md), [Localization](Docs/Localization.md) |

Use [Assets](Docs/Assets.md) for import practices, [Dependencies](Docs/Dependencies.md) for Git package requirements, [Builds](Docs/Builds.md) for packaging, and [Auditing](Docs/Auditing.md) for verification.

Keep module-specific instructions in their own guides; do not add dated progress or test histories to the documentation.

## Attribution

- OpenAI Codex for module design and coding.

