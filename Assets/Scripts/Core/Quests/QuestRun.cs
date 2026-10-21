using System;
using System.Collections.Generic;
using System.Diagnostics;
using Hypocycloid.Core;
using Baseline.Defs;

namespace Baseline.Quests
{
    /// <summary>
    /// One play of a quest: its objective journal, the quest signals it hears on the bus and, once,
    /// the ending its def names for one of them, recorded in the <see cref="QuestLog"/>. Leaving before an ending
    /// records nothing.
    /// </summary>
    public sealed class QuestRun : IDisposable
    {
        /// <summary>Raised on the run by <see cref="Begin"/>.</summary>
        [QuestSignalTag] public const string BeginSignal = "Quest.Begin";
        /// <summary>Raised on the run once every primary step is done.</summary>
        [QuestSignalTag] public const string StepsDoneSignal = "Quest.StepsDone";
        /// <summary>Raised on the run once a primary step fails.</summary>
        [QuestSignalTag] public const string StepFailedSignal = "Quest.StepFailed";
        /// <summary>A caller may raise this signal when the player skips a quest.</summary>
        [QuestSignalTag] public const string SkipSignal = "Quest.Skip";

#if UNITY_EDITOR
        /// <summary>Editor only: every run not yet disposed, oldest first, for the Quest Debugger.</summary>
        public static IReadOnlyList<QuestRun> Running => running;
        static readonly List<QuestRun> running = new();
        /// <summary>Editor only: the Quest Debugger while it is open. Builds have no hooks; unset, each costs a null check.</summary>
        public static IQuestRunObserver Observer;
        public bool IsPreview { get; private set; }

        /// <summary>Exercises the real journal and signals without recording outcomes in the player's quest log.</summary>
        public static QuestRun CreatePreview(QuestDef quest) => new QuestRun(quest) { IsPreview = true };

        // Play Mode without a domain reload clears the bus, so runs from before it hear nothing.
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => running.Clear();
#endif

        bool disposed, stepsDone, stepFailed;

        public QuestDef Quest { get; }
        public QuestJournal Steps { get; }
        /// <summary>How the run ended, or null while it runs.</summary>
        public QuestEnding Ending { get; private set; }
        public bool IsFinished => Ending != null;
        public event Action<QuestEnding> Finished;
        public event Action<QuestGuard> ActionBlocked;

        public QuestRun(QuestDef quest)
        {
            Quest = quest ?? throw new ArgumentNullException(nameof(quest));
            quest.Validate();
            Steps = new QuestJournal(quest.Objectives);
            Steps.Changed += OnStepsChanged;
            Steps.Signalled += Raise;
            SignalBus.Subscribe<QuestSignal>(OnSignal);
            Track(true);
        }

        /// <summary>Shows the steps that wait for the start. Call once the run's owner is ready to be heard.</summary>
        public void Begin() => Raise(BeginSignal);

        /// <summary>Checks live conditions in authored order without changing progress or presenting UI. Unknown conditions must evaluate false.</summary>
        public QuestGuard CheckAction(string action, Func<string, bool> evaluateCondition)
        {
            if (disposed) throw new ObjectDisposedException(nameof(QuestRun));
            if (IsFinished) throw new InvalidOperationException("A finished quest run cannot authorize actions.");
            foreach (QuestGuard guard in Quest.Guards)
                if (guard.action == action && (evaluateCondition == null || !evaluateCondition(guard.condition))) return guard;
            return null;
        }

        /// <summary>Checks an attempted action and reports its first blocking guard. Success does not complete objectives or raise gameplay signals.</summary>
        public bool TryAction(string action, Func<string, bool> evaluateCondition)
        {
            if (disposed || IsFinished) return false;
            QuestGuard blocked = CheckAction(action, evaluateCondition);
            if (blocked == null) return true;
            ActionBlocked?.Invoke(blocked);
            return false;
        }

        /// <summary>Raises <paramref name="tag"/> on the bus for this run, and for anything else listening.</summary>
        public void Raise(string tag) { if (!disposed && !string.IsNullOrEmpty(tag)) SignalBus.Fire(new QuestSignal(tag, this)); }

        // A finished run still raises its steps' signals for other listeners, but hears nothing more itself.
        void OnSignal(QuestSignal signal)
        {
            if (!ReferenceEquals(signal.Run, this) || disposed || string.IsNullOrEmpty(signal.Tag)) return;
            Observe(signal, true);
            try
            {
                if (IsFinished) return;
                Steps.Hear(signal.Tag, signal.Amount);
                if (!IsFinished && Quest.Ending(signal.Tag) is { } ending) Finish(ending);
            }
            finally { Observe(signal, false); }
        }

        void Finish(QuestEnding ending)
        {
            Ending = ending;
            ObserveEnding(ending);
#if UNITY_EDITOR
            if (!IsPreview)
#endif
            QuestLog.Record(Quest, ending);
            Finished?.Invoke(ending);
        }

        // The debugger's hooks: calls vanish from builds.
        [Conditional("UNITY_EDITOR")]
        void Track(bool add)
        {
#if UNITY_EDITOR
            if (add)
            {
                running.Add(this);
                Steps.Moved += OnMoved;
            }
            else
            {
                running.Remove(this);
                Steps.Moved -= OnMoved;
            }
#endif
        }

        [Conditional("UNITY_EDITOR")]
        void Observe(QuestSignal signal, bool arriving)
        {
#if UNITY_EDITOR
            if (Observer == null) return;
            if (arriving) Observer.Heard(this, signal, !IsFinished);
            else Observer.Handled(this, signal);
#endif
        }

        [Conditional("UNITY_EDITOR")]
        void ObserveEnding(QuestEnding ending)
        {
#if UNITY_EDITOR
            Observer?.Ended(this, ending);
#endif
        }

#if UNITY_EDITOR
        void OnMoved(QuestSnapshot step) => Observer?.Moved(this, step);
#endif

        // The steps' verdict is a signal like any other: the quest's endings decide whether it ends the run.
        void OnStepsChanged()
        {
            if (disposed || IsFinished) return;
            if (!stepFailed && Steps.PrimaryFailed)
            {
                stepFailed = true;
                Raise(StepFailedSignal);
            }
            else if (!stepsDone && Steps.PrimariesDone)
            {
                stepsDone = true;
                Raise(StepsDoneSignal);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Track(false);
            SignalBus.Unsubscribe<QuestSignal>(OnSignal);
            Steps.Changed -= OnStepsChanged;
            Steps.Signalled -= Raise;
        }
    }

#if UNITY_EDITOR
    /// <summary>Editor only: what the Quest Debugger hears from every run.</summary>
    public interface IQuestRunObserver
    {
        /// <summary>A signal reached its run, before the run handles it; <paramref name="heard"/> is false once the run has ended.</summary>
        void Heard(QuestRun run, QuestSignal signal, bool heard);
        /// <summary>A step moved: while a signal is being handled, by it; otherwise by a direct journal call.</summary>
        void Moved(QuestRun run, QuestSnapshot step);
        /// <summary>The signal being handled ended the run.</summary>
        void Ended(QuestRun run, QuestEnding ending);
        /// <summary>The run is done with the signal last heard, and any it raised meanwhile.</summary>
        void Handled(QuestRun run, QuestSignal signal);
    }
#endif

}
