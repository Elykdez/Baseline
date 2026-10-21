# Dialogue and alerts

Source: `Assets/Scripts/Generics/Dialogue`. The module supplies character and conversation definitions,
linear playback, a scene-owned conversation panel and queued application alerts. Gameplay owns
entry conditions, pause policy and the effect of each result.

## Authored content

`Assets/Bundles/Core/Config/Generics/SharedUIContent.asset` references the serialized `ui_text`,
`ui_dialogue`, `ui_character_dialog` and `ui_character_alerts` prefabs in `Assets/Bundles/Core/Prefab/UI`, plus the persistent
context and TMP settings. All runtime content belongs in `Assets/Bundles`; Resources folders and
runtime Resources loads are forbidden. `GameBootstrap` asynchronously loads the catalog through
its AssetReference before creating the menu. It activates TMP settings and retains the catalog
handle until application shutdown, so shared UI remains available across scene changes.
Editor tools and directly opened editor scenes use the same catalog through AssetDatabase.
Player scenes must enter through Index or explicitly load, activate and retain the catalog first.

The shared `Config/Dialogue/GuideCharacter.asset` and demo `DemoExampleDialog.asset` are registered
in `DemoExampleDatabase.asset`. The guide remains with the framework when demo content is removed.
The shared character uses the mono-eye robot in `Assets/Bundles/Core/Texture/Characters/RobotGuide.png`,
with separate illustration and cropped portrait sprites for landscape and compact layouts.
Shared captions and sample text have English and Chinese StringTable entries. The text prefab
retains locale-specific font and material variants.

Create assets with **Create > Baseline > Defs > Character/Dialog**. Keep definition IDs stable.
Supply fallback text and StringTable keys for lines, titles and roles. Expressions pair full
illustrations with compact portraits and optional `SoundCueDef` reaction recordings. Unknown
expressions use default art and voice. Register speakers and guard-linked conversations in the
same database; `DefDatabase.Validate()` checks those references.

**Tools > Baseline > Create Missing Shared UI Content** recreates missing defaults and translations
without replacing customized prefabs. The `ui_text` template must already be present.

## Conversations

```csharp
using Baseline.Dialogue;
using UnityEngine;

// Keep this panel as a field on the owning scene controller.
DialoguePanel panel = DialoguePanel.Create();
panel.Runner.Closed += completed => Debug.Log(completed ? "Completed" : "Cancelled");
panel.Show(dialogDefinition, allowClose: false, offerSkip: true);
```

`offerSkip` permits skipping the initial invitation. Subsequent required lines cannot be closed;
the owner can always call `panel.Runner.Cancel()`. Disabling the panel cancels playback. Destroy
the panel when its owner ends. It does not persist across scenes.

Check `panel.BlocksInput` before gameplay input; it includes the closing frame. Next first reveals
unfinished typewriter text, then advances. Stale runner revisions cannot advance another line.
`Closed(true)` means the final line completed. Results never automatically save quest progress.

### Two-character conversations

Enable **Dual Characters** on a `DialogDef` to retain the two most recent speakers on wide
layouts. It is enabled by default; disable it for a conversation that should show only its
current speaker. Existing lines still use `speaker` and `expression`; no participant list or
slot assignment is required.

The first speaker occupies the left slot and the next new speaker occupies the right. Returning
speakers keep their positions. A third speaker replaces the slot opposite the previous speaker.
The listener keeps its last expression and uses the prefab's inactive tint; the active speaker
uses its active tint and supplies the displayed name, role, accent and voice. Closing, cancelling,
disabling or replacing a conversation clears both slots, including replaying the same definition.

Portrait and narrow views display only the active character and its name. Widening the view
restores both characters and their expressions. Each character resolves its full or compact
art through `ResolvePortrait`; resizing or changing locale does not change its slot or restart
a reaction recording.

Author `secondCharacter`, `pairedLayout`, `pairedRightCharacter`, active/inactive tints and the
pair width/aspect thresholds in `ui_dialogue.prefab`. Its existing landscape/portrait profiles
remain the single-character layouts. Image transforms own the authored placement, pivot and scale.
The installed Relay Run briefing
demonstrates the pair with Relay Control and R-07, then returns to the single-speaker outcome
conversations.

### Portrait facing

Set **Facing** on a `CharacterDef` to **Original** or **Mirrored**, relative to its supplied artwork.
Each `DialogLine` also has **Facing**: **Character Default** inherits that setting, while
**Original** and **Mirrored** override it for the line. Existing assets default to Original and
Character Default, so no artwork is flipped implicitly. The same rule applies with Dual Characters
disabled; queued character alerts use the character default.

Each listener keeps the facing from its last line along with its expression. Compact/landscape
switches and locale refreshes preserve that state; a new conversation clears the line overrides.
The authored portrait Images use `UIPortrait` to mirror the rendered mesh inside its existing
bounds. Facing does not change anchors, pivot, scale, child objects or text. Author the intended
default deliberately for asymmetric artwork.

## Alerts

```csharp
using Baseline;
using UnityEngine.Localization;

UICharacterDialog.Confirm(
    new LocalizedString(GameRegister.DEF_STT, "Your_Heading_Key"),
    new LocalizedString(GameRegister.DEF_STT, "Your_Body_Key"),
    accepted => { if (accepted) PerformAction(); });
```

Author both keys in both locale tables. `Alert(...)` supplies acknowledgement; `Confirm(...)` adds
cancellation and reports the result once. Requests queue in arrival order. Scene changes cancel
the current request and queue with `false`, including reentrant requests from cancellation callbacks.
`UICharacterDialog.BlocksInput` includes pending requests and the closing frame. The alert canvas
persists across scenes; requests belong to the scene that opened them.

`CharacterDialogStyle.asset` assigns characters and colors for information, success, warning and
danger. `UICharacterDialogView` presents up to three authored actions; `UICharacterDialog` owns
the application request queue. The catalog's `characterAlertsPrefab` supplies the persistent
canvas and input shield with a nested `characterDialogPrefab` view. Runtime code instantiates
this authored root instead of constructing UI components.

Edit conversation and alert geometry in their prefab's landscape/portrait settings. These contain
anchors, insets, size limits and compact scaling; illustrated and required conversation states
have authored bounds too. Edit fonts, action sizes/spacing and button colors on the actual
components. The alert view swaps to its separate authored destructive button for danger
confirmations and restores normal actions on the next request. Runtime presentation does not
apply a palette or fixed label layout. See [UI authoring](UI.md#prefab-authoring).

## Verification

Run `DialogueTests`, `CharacterDialogLifecycleTests` and `SharedUIContentTests` for playback,
cancellation, queue ordering, stale clicks, serialized bindings, translations and font variants.
Inspect English and Chinese at landscape and portrait sizes after changing art, fonts or layout.
Reaction audio needs an authored cue and listening acceptance.

Verify the authored conversation and alert prefabs with mouse, keyboard and touch interaction
in Play Mode.

Related: [UI](UI.md), [Quests](Quests.md), [Localization](Localization.md), [Audio](Audio.md).
