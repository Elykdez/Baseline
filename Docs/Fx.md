# FX and camera shake

Source: [Presentation](../Assets/Scripts/Generics/Presentation).

## Author and wire an effect

1. Create a visual prefab with the required particle systems and materials. Import content through
   [Assets](Assets.md); preserve its intended scale, pivot, transparency and rendering contract.
2. Create **Baseline > Presentation > Fx Event**. Assign a visual prefab/lifetime, sound cue
   and/or camera shake. `Validate()` requires at least one effect and validates the optional cue.
   This ScriptableObject does not derive from `Def` and is not a DefDatabase entry.
3. Provide a dedicated `FxManager` host before requests. It destroys entire duplicate hosts;
   do not attach unrelated scene content to that host.
4. Attach `CameraShake` to the intended scene camera. It shakes on top of wherever other code put
   the camera; a camera that places itself every frame sets `ExternallyDriven` and calls `Advance`
   and `ApplyPose` instead. Provide a configured sound manager when the event has a cue.
5. Assign the event asset to the gameplay presentation adapter that requests it.

```csharp
using Baseline.Presentation;
using Hypocycloid.Core;

// effect is an assigned FxEventDef; worldPosition comes from the presentation event.
SignalBus.Fire(new FxRequestedSignal { Fx = effect, Position = worldPosition });
```

Missing listeners mean no effect; null events are ignored. Presentation must not decide damage,
quest completion or other authoritative behavior. Filter hidden events before requesting effects
if the game has hidden information.

## Settings and lifetime

Use `ScreenShakeRequestedSignal` and `BoardEffectsRequestedSignal` for settings changes;
listen to `FxSettingsChangedSignal` for UI refresh. `BoardEffects` is the retained name for
the visual toggle. `CameraShakeRequestedSignal` carries the camera kick after the manager
scales it by the player's shake setting.

`FxManager` spawns visuals from the pool under its persistent host, clears/restarts child particle
systems, and returns them after `WaitForSeconds(vfxLifetime)`. That is scaled game time: pausing
delays the return. Reset custom per-spawn state through `IPoolable`. Turning `BoardEffects`
off suppresses particles, while sound and shake have their own controls. Keep visual lifetime
and particle tail duration consistent, and verify reuse after the effect returns to the pool.

## Audit

Run the FX cases in `AudioFxManagementTests` and validate the authored event. Inspect gameplay scale, representative backgrounds, pause, scene transitions, sound/shake controls and repeated pool reuse. Record visual inspection separately from automated routing/lifetime checks.

Related: [Audio](Audio.md), [Signals](Signals.md), [Utilities](Utilities.md), [Assets](Assets.md).
