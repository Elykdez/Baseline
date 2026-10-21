# Relay Run

Relay Run is a short robot-rex runner that connects Baseline's existing systems into a playable
game. Reach the relay station 600 metres away by jumping over debris. Speed increases during the
attempt; the authored course takes roughly one minute. A collision ends the attempt, and retry
starts a fresh scoped quest. Clearing obstacles also advances a secondary objective.

## Play

Open `Assets/Scenes/Index.unity` and press Play, then select Relay Run. The Index loads the shared
UI catalog and demo menu through Addressables. Presentation content stays loaded while the game
and its conversations use it. Opening `Assets/Bundles/Demo/DemoRelayRun.unity` directly in Play Mode
returns to Index so startup follows the same path.

- Space, Up Arrow or W: start an attempt, then jump. A jump pressed just before landing is buffered;
  holding a key does not repeatedly jump.
- Jump button: the same jump action for pointer and touch input.
- P, Escape or Pause: pause or resume an active attempt. Losing application focus also pauses.
- Retry, or Space after the result conversation closes: begin another attempt.
- Menu: return to Index. Leaving an unfinished attempt asks for confirmation; cancelling restores
  the previous pause state. Abandoning an attempt does not record a quest ending.

Starting an attempt begins its quest and shows the required briefing. Completing that conversation
advances the briefing objective; the quest then reveals the delivery objective and starts gameplay.
The briefing alternates Relay Control and R-07. Wide views retain both characters and dim the
listener; portrait/narrow views show the current speaker. Its `DialogDef`'s **Dual Characters**
checkbox can restore the single-character presentation without changing the quest or dialogue flow.
Failure and success each have their own required conversation before the result controls become
available. Every retry starts a new quest and briefing. Gameplay input is blocked while a conversation or alert is
open, including its closing frame. The best distance and completed quest outcomes use Baseline's
existing persistence. Menu settings and help remain accessible through the shared UI.

## Authoring

Edit the existing scene, menu prefabs, definitions and presentation assets through Unity's
Inspector. Static UI is serialized in authored prefabs and scenes; runtime code binds controls
and updates existing views. Update registration and localization when changing their consumers.

| Content | Location |
| --- | --- |
| Gameplay and presentation adapters | `Assets/Scripts/Demo/RelayRun` |
| Scene | `Assets/Bundles/Demo/DemoRelayRun.unity` |
| Art, prefabs, definitions and audio | `Assets/Bundles/Demo/RelayRun` |
| Demo tests | `Assets/Editor/Demo/RelayRun` |
| English and Chinese manuals | `Assets/StreamingAssets/Desc/DemoRelayRun_en.md`, `DemoRelayRun_zh.md` |
| UI strings | `DemoRelay_` entries in the shared StringTable collection |

Demo source types use the `Baseline.Demo.RelayRun` namespace, and demo source/config filenames
begin with `Demo`. Runtime scripts live under `Assets/Scripts/Demo`; demo tests stay editor-only
under `Assets/Editor/Demo`. Scripts compile normally and are not executable bundle content.
The `Demo` Addressables group owns demo assets and the scene, including the separate example
definitions under `Assets/Bundles/Demo/Examples`. Keep production content outside all three reserved
roots: `Assets/Scripts/Demo`, `Assets/Editor/Demo` and `Assets/Bundles/Demo`.

`DemoRunnerDef` supplies speed, acceleration, jump physics, obstacle size/spacing and goal distance.
Jump gravity eases from `takeoffGravityScale` (1) to `apexGravityScale` (0.9) at the peak, then to
`landingGravityScale` (1.1) by touchdown. This apex hang plus fall multiplier makes the arc
less floaty, and the eased scale never jumps between values. Its validator integrates that arc to
check jump clearance, landing space, and agreement with the assigned quest. The
quest begins with a `briefing` objective completed by `Relay.Briefed`. Its `delivery` objective
requires that briefing and appears on `briefing.Done`; its distance must match the configured goal
and its failure signal must be `Relay.Crashed`. Register the runner definition, quest, conversations and speakers in the assigned
`DefDatabase`. `DemoRunnerGame` validates that database before accepting input.

The robot rex uses a native Unity 2D cutout rig: separate body, thigh, shin and foot sprites in an
articulated hierarchy. Animator Run, Jump and Land clips move the two legs independently, with their
run cycles half a cycle apart. Jump compresses briefly, stretches on takeoff, then unfolds toward
landing; Land reverses the stretch into a squash and a small settling rebound. Jump ends on Run's
first frame, so touchdown resumes the stride at once. Land plays on an Impact override layer that
animates only the root scale and body tilt, so the legs never stand still while the ground scrolls.
`DemoRexAnimation` weights that layer only while Land plays: an idle override layer would hold its
last pose, legs included.
These one-shots keep their timing as the RunSpeed parameter increases the stride rate. A grounded press applies
the squash immediately, with a 0.06-second wind-up before lift-off. Repeated presses during
wind-up do not restart it. Buffered jumps start their wind-up on landing, and retry resets the pose.
The far leg is darker and drawn behind the body. Pausing freezes
the animation along with gameplay. Artwork and a forgiving body collision shape are separate, so
the tail does not unfairly collide with debris. The collision center follows the rig's hip offset.
Collision tests the visible feet rather than the rig origin. The Jump clip folds the feet about 32
units above the origin while rising and 26 while falling, and each obstacle sprite has a traced
outline. The pylon's low leading rubble therefore clears earlier than the crate. A crash needs 6
units of overlap. After changing the Jump clip or obstacle art, re-measure
`DemoRunnerModel`'s foot clearances and outlines; the rig and scene tests flag drift.
Run-clip foot contacts emit small canvas dust puffs. Takeoff emits a compact burst;
landing spreads a larger burst along the ground, including buffered land-and-jump frames.
`DemoRexFootFx` reuses 24 authored sprite views in the playfield, scrolls them with
the ground, and freezes with gameplay. Retry/end clears them, and the visual-effects
setting hides them without muting jump audio. `DemoJumpFx` supplies sound and shake;
crash and success retain their pooled particles. Dust art and prefab live inside the
demo's `Art` and `Fx` folders.
The menu poster shows an enlarged, left-facing side-rear lookout rex bust in the lower-right foreground.
Its `UIParallax` layers run front-to-back as rex, middle ground and distant background, with stronger
motion nearer the viewer; the playfield reuses authored scenery pairs that scroll at different speeds. Artwork
uses smooth illustrated shading rather than pixel art.

## Framework integration

| Module | Demo consumer |
| --- | --- |
| [Scenes](Scenes.md), [Addressables](Addressables.md) | Index bootstrap, registered gameplay scene, menu/content references, and persistent presentation asset ownership. |
| [Definitions](Defs.md) | Authored runner tuning, quest and conversation catalog, validated by the gameplay consumer. |
| [Quests](Quests.md) | One `QuestRun` per attempt; briefing completion unlocks gameplay; scoped `Relay.Briefed`, `Relay.Metre`, `Relay.Cleared` and `Relay.Crashed` signals; distinct success/failure endings. |
| [Signals](Signals.md) | Typed quest progression and sound/FX requests. |
| [Dialogue](Dialogue.md) | Shared briefing, failure/success conversations, and queued abandon confirmation. |
| [Saves](Saves.md) | Best distance via context settings; quest endings via `QuestLog`; scene exit disposes an unfinished run. |
| [UI](UI.md), [Localization](Localization.md) | Authored controls, parallax, English/Chinese captions, localized conversations and help manuals. |
| [Audio](Audio.md), [FX](Fx.md) | Authored music/cues, jump/crash/success feedback, pooled particles and shake through the presentation managers. |
| [Utilities](Utilities.md) | Framework presentation pooling and explicit ownership; gameplay reuses five authored obstacle views. |

The sample demonstrates these systems as a coherent game. It does not require every available UI
widget to appear in the game. The deterministic runner model uses logical playfield units, so
collision timing is independent of the rendered viewport scale.

The ambient music is retained shared content at `Assets/Bundles/Core/Sound/Music/relay_ambient.wav`,
owned by the `Core` group under `Core/Sound/Music`. Both the generic Index menu and the demo use this track; demo cleanup
preserves it. Demo effects, cues and presentation definitions remain inside the removable demo folder.
BGM plays only in Index. Relay Run requests `MusicContext.Silence`, fading the menu track out
over the authored transition; returning to Index starts it again. Jump, hit, success and footstep
cues use the effects channel independently. Footstep audio follows the run animation's foot-contact
events and stops while paused or airborne, even when visual dust effects are disabled.

## Verification and cleanup

`DemoRunnerModelTests` covers jump clearance, buffering, per-obstacle hit timing, completion across
several courses, frame-size handling and retry state. `DemoRunnerIntegrationTests` exercises the
authored catalog, conversations, scoped quest outcomes and scene integration, and checks each
obstacle outline against its sprite's alpha. Follow [Auditing](Auditing.md)
for safe Unity test execution and evidence; the presence of tests does not establish a passing run.
`DemoRexRigTests` samples the authored prefab and animation clips to check alternating contact and
swing legs, and that the Jump pose's lowest sprite mesh matches the model's foot clearance.
Inspect the animated gait, transparent sprite edges, menu
parallax, jump timing, sound and UI layout in Play Mode after changing art or tuning.

When initializing a new game from Baseline, use **Tools > Baseline > Remove Demo Content** to
preview, back up and remove the installed sample before adding production content. Removal is
explicit; project startup and asset import do not trigger it. The tool
backs up affected assets, blocks unexpected retained dependencies, restores the generic Index menu,
and removes demo registrations. Rebuild content and players afterward. See [Demo cleanup](DemoCleanup.md)
for backup restoration and limits. Recover the sample's scripts and assets from the cleanup
backup or version control if they are needed again.
