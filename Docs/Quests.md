# Quests

Source: [QuestDef](../Assets/Scripts/Core/Defs/QuestDef.cs),
[QuestRun](../Assets/Scripts/Core/Quests/QuestRun.cs),
[QuestJournal](../Assets/Scripts/Core/Quests/QuestJournal.cs) and
[QuestLog](../Assets/Scripts/Core/Quests/QuestLog.cs).

Create through **Create > Baseline > Defs > Quest**. Baseline quests own `objectives`, `requires`, `guards`
and `endings` directly; there is no activity/story/campaign adapter. Use
[DemoExampleQuest.asset](../Assets/Bundles/Demo/Examples/DemoExampleQuest.asset) and
[DemoExampleDatabase.asset](../Assets/Bundles/Demo/Examples/DemoExampleDatabase.asset) as the working reference:

| Field | Example |
| --- | --- |
| Stable identity | `example.quest` |
| Objective ID | `action` |
| Show signal | `Quest.Begin` |
| Completion signal and count | `Example.Action`, 1 |
| Ending signal | `Quest.StepsDone` |
| Recorded outcome / passing | `done` / true |

The activity owner validates its database and checks `QuestLog.Locked(quest)` before starting
when prerequisites should apply. The constructor validates the quest but does not enforce that gate.
Create a `QuestRun`, attach `Finished` and `Steps.Changed` listeners, then call `Begin()`. Keep the
run for the activity's lifetime and dispose it on exit, including unfinished exits.

This synchronous example completes the bundled one-action quest. In a real activity, store the
run as a field rather than letting a short method dispose it before the player can act:

```csharp
using Baseline.Defs;
using Baseline.Demo.Examples;
using Baseline.Quests;

// database is the assigned DemoExampleDatabase asset.
database.Validate();
QuestDef quest = database.Get<QuestDef>("example.quest");
if (!QuestLog.Locked(quest))
{
    using var run = new QuestRun(quest);
    run.Begin();
    run.Raise(DemoExampleQuestSignals.Action);
}
```

`run.Raise(tag)` increments once. For counted events use
`SignalBus.Fire(new QuestSignal(tag, run, amount))` with `Hypocycloid.Core` and `Baseline.Quests`
in scope. The run reference is mandatory for progression: a signal for another run, or no run,
does not advance this one. Declare producer constants with `[QuestSignalTag]`, as in
[DemoExampleQuestSignals](../Assets/Scripts/Demo/Examples/DemoExampleQuestSignals.cs), so the editor catalog can inspect them.

Read `run.Steps.Snapshot` for objective UI. Hidden steps ignore completion/failure; hiding preserves
progress. Terminal steps stay terminal. Secondary objectives do not decide primary completion.
Steps emit `<id>.Shown`, `.Hidden`, `.Done` and `.Failed`; primary completion/failure emits
`Quest.StepsDone` / `Quest.StepFailed`. These only end the run if the asset has a matching ending.

An objective's `requiresObjectives` lists IDs in the same quest that must be done before it can
show or advance. Validation rejects unknown IDs, self references and dependency cycles. A show
signal received too early is ignored; use a prerequisite's `<id>.Done` signal to reveal its successor.

`QuestGuard` entries associate an action with a live condition, localized hint, optional UI target
and optional conversation. Call `run.TryAction(action, evaluateCondition)` before performing the
action. Return false for unknown conditions. The first failed condition raises `ActionBlocked`
without advancing objectives or opening UI. The owner presents the hint/dialogue and performs the
action only on success. `CheckAction(...)` inspects the first blocker without notification.
Declare condition constants with `[QuestConditionTag]`. Register guard-linked conversations and
their speakers in the same definition database.

Editor tooling can use `QuestRun.CreatePreview(quest)`: it exercises the real journal, guards and
signal bus, but reaching an ending does not call `QuestLog.Record()`. Dispose preview runs just
like real runs. The debugger also inspects live runs, so its real-run Raise Signal action
can write outcomes; creating a preview does not suppress unrelated subscribers' side effects.

Keep ending signal tags distinct and nonempty. Outcomes must be nonempty and cannot be the reserved
`passed` value. Register every requirement and `alsoRecord` target in the same database. A run records
its ending once; disposal alone records nothing. Calling `QuestLog.Record()` repeatedly directly
does increment repeatedly, so do not both let a run finish and manually record the same ending.

## Audit

Open **Tools > Baseline > Quest Debugger** or **Tools > Hypocycloid > Audit > Quests**.
The window discovers quest and dialogue assets without selecting a database. Its tabs provide:

- **Audit**: definition validation, duplicate save identities, prerequisite cycles, unknown signal
  and condition tags, missing English/Chinese text, and invalid dialogue expressions.
- **Preview**: isolated quest attempts with signal amounts, guard condition toggles, action checks,
  objective transitions and signal traces. Restart disposes the previous run. Outcomes never write
  player quest progress; unrelated subscribers can still observe preview signals.
- **Live runs**: current gameplay runs and signals. Raising signals here can change and save real progress.
- **Quest log**: the save path and recorded outcomes, without editing them.

Preview also shows any chosen `DialogDef`, its English/Chinese text, expression art and compact
portrait, with optional voice audition. A guard-linked dialogue is selected automatically; game-specific
tutorial controllers and conversation roles are not part of Baseline.
Closing the window or switching Play Mode disposes previews, stops audition audio and restores observers.

Run `BaselineCoreTests` and `QuestAuditTests`. `QuestSignalCatalog` checks declared producers;
catalog membership does not prove gameplay executes a producer or an ending is reachable.

Related: [Signals](Signals.md), [Definitions](Defs.md), [Saves](Saves.md).
