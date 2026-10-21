# Audio and music

The modern audio layer is in [Presentation](../Assets/Scripts/Generics/Presentation). Its definition
types are plain ScriptableObjects, separate from `Baseline.Defs.DefDatabase`.

| Create menu under `Baseline > Presentation` | Author and validate |
| --- | --- |
| `Sound Cue` | One or more nonnull clips, `Ui` or `Fx` channel, volume, pitch in 0.5–2 and minimum repeat interval. Call `Validate()`. |
| `BGM` | Required menu clip and optional unique, nonnull gameplay playlist. Call `Validate()`. |
| `Audio Settings` | Authored gains and optional mixer/output groups. Verify actual routing and player gains in Play Mode. |

For a new click sound:

1. Import the clip under an appropriate `Assets/Bundles/Core/Sound` subfolder and record its source.
2. Create a Sound Cue asset, choose `SoundChannel.Ui`, fill its clips/settings and validate it.
3. Author a dedicated SoundManager host with its `mix`, optional `bgm`, voice count and fade time.
   Ensure this configured host initializes before a request or lazy `.Ins` access. A lazily created
   manager cannot invent the missing mix/music content.
4. Assign the cue to `UIButton.sound`, or use `UIClickSound` on a supported non-button selectable.
   Inspect the existing click callbacks to avoid requesting the same sound twice.
5. Exercise the click with Master and UI levels changed independently; confirm settings persist
   without changing the music/effects category levels. Test a rapid burst if interval throttling matters.

The starter Index still uses legacy `AudioManager`/`SoundBehaviour` wiring. A `UIButton` with no
modern cue falls back to its legacy fields and requires a usable legacy manager. New buttons
should have a deliberate audio path. Migrating an existing panel requires removing duplicate
playback intentionally; do not simply attach both systems and assume they coordinate.

## Playback and settings

```csharp
using Baseline.Presentation;
using Hypocycloid.Core;

// cue is an assigned SoundCueDef.
SignalBus.Fire(new SoundRequestedSignal { Cue = cue });
SignalBus.Fire(new MusicRequestedSignal { Music = MusicContext.Menu });
```

The configured sound manager must exist before a request. Missing listeners mean no playback;
null cues are ignored. Adapt gameplay events to presentation requests without changing authoritative
game state. Filter requests for hidden events if the game has hidden information.

`SceneMusic` requests the scene's selected context. `MusicContext.Battle` names the gameplay
playlist, and `Silence` fades music out. `SoundManager` destroys entire duplicate hosts;
keep unrelated scene objects off those hosts.

For an assigned mixer, expose exactly `MasterVolume`, `MusicVolume`, `FxVolume` and `UiVolume`,
and assign the music, menu-music, FX and UI output groups in `AudioSettingsDef`. With a mixer,
player gains are applied there; without one, sources use `SoundManager.EffectiveLevel`. Do not
apply Master/category gain twice or assign a mixer without the expected exposed parameters.

Use `AudioLevelRequestedSignal` for player-level changes and `AudioLevelChangedSignal` for UI
refresh. `DialogVoiceRequestedSignal` owns one voice reaction at a time; its `Owner` distinguishes
which caller may stop that reaction. The voice API is present, but a complete story/dialog
presenter is not.

## Audit

**Tools > Hypocycloid > Audio > Optimize Library** applies folder-owned import profiles to
`Assets/Bundles/Core/Sound`; **Validate Library** reports mismatches. The audio postprocessor applies
the same profiles to future imports. Source recordings and serialized clip GUIDs are preserved.
`Sound/Music` uses stereo Vorbis at quality 0.7, streaming without preload and background loading.
Other sound folders, including voice, use mono ADPCM, decompression on load and preload with
optimized sample rate. Standalone, Android, iOS and WebGL overrides are cleared so they cannot
silently shadow the profile. Put long tracks under Music rather than the effect folders.

The Quest Debugger's Preview tab can audition the current dialogue reaction and stop it on line
changes or window closure. This auditions the source clip; gameplay still owns mixer gains and pitch.
Run `AudioImportTests` for profiles, folder boundaries and idempotent imports.

Run affected `AudioFxManagementTests` cases and validate new SoundCue/BGM assets. Check mixer routing, independent Master/category levels, cue throttling and scene transitions. Listen on the intended device; automated routing checks do not prove perceived volume, clipping or recording quality.

Related: [Signals](Signals.md), [FX](Fx.md), [UI](UI.md), [Saves](Saves.md).
