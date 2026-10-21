using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Baseline.Quests
{
    /// <summary>
    /// Where one step stands, as values: whether it is shown, how far it has got and whether it
    /// failed. It is done once its progress reaches its count. Done and failed are final.
    /// </summary>
    public readonly struct QuestSnapshot
    {
        public QuestData Definition { get; }
        public bool Shown { get; }
        /// <summary>Progress so far, out of <see cref="QuestData.Required"/>.</summary>
        public int Progress { get; }
        public bool Failed { get; }
        public bool Done => !Failed && Progress >= Definition.Required;
        /// <summary>Shown and still open: what the objective arrow points at.</summary>
        public bool Active => Shown && !Done && !Failed;

        public QuestSnapshot(QuestData definition, bool shown, int progress = 0, bool failed = false)
        { Definition = definition; Shown = shown; Progress = progress; Failed = failed; }
    }

    /// <summary>
    /// The steps of one quest run. Hidden steps ignore signals until they are shown; done and failed are final. Each
    /// move raises the step's own signal, so other steps and the quest's endings can follow it.
    /// </summary>
    public sealed class QuestJournal
    {
        public const string ShownSuffix = ".Shown", HiddenSuffix = ".Hidden", DoneSuffix = ".Done", FailedSuffix = ".Failed";

        sealed class Step
        {
            public QuestData definition;
            public bool shown, failed;
            public int progress;
            public bool Final => failed || progress >= definition.Required;
            public QuestSnapshot Capture() => new(definition, shown, progress, failed);
        }

        readonly List<Step> steps = new();
        readonly Dictionary<string, Step> byId = new(StringComparer.Ordinal);
        readonly List<string> raised = new();
        public event Action Changed;
        /// <summary>A step's own quest signal, <c>id.Shown</c>, <c>id.Hidden</c>, <c>id.Done</c> or <c>id.Failed</c>, after the change notice.</summary>
        public event Action<string> Signalled;
#if UNITY_EDITOR
        /// <summary>Editor only, for the Quest Debugger: a step as it stands right after it moved. Builds have no such hook.</summary>
        public event Action<QuestSnapshot> Moved;
#endif

        public QuestJournal(IEnumerable<QuestData> objectives = null)
        {
            if (objectives == null) return;
            foreach (QuestData quest in objectives)
            {
                if (quest == null || string.IsNullOrWhiteSpace(quest.id) || byId.ContainsKey(quest.id))
                    throw new ArgumentException("Objectives need distinct, nonempty ids.", nameof(objectives));
                var step = new Step { definition = quest };
                steps.Add(step);
                byId.Add(quest.id, step);
            }
        }

        public QuestSnapshot[] Snapshot
        {
            get
            {
                var result = new QuestSnapshot[steps.Count];
                for (int i = 0; i < result.Length; i++) result[i] = steps[i].Capture();
                return result;
            }
        }

        public bool HasPrimary => steps.Exists(step => !step.definition.secondary);
        /// <summary>Every primary step is done.</summary>
        public bool PrimariesDone => HasPrimary && steps.TrueForAll(step => step.definition.secondary || step.Capture().Done);
        public bool PrimaryFailed => steps.Exists(step => !step.definition.secondary && step.failed);

        public QuestSnapshot this[string id] => Find(id).Capture();

        public bool Show(string id) => Notify(Show(Find(id)));
        public bool Complete(string id) => Notify(Advance(Find(id), int.MaxValue));
        public bool Fail(string id) => Notify(Fail(Find(id)));

        public bool Hide(string id) => Notify(Hide(Find(id)));

        /// <summary>
        /// Moves every step waiting for <paramref name="tag"/>, in authored order, with one change notice: shows or hides
        /// those it shows or hides, then fails, or advances by <paramref name="amount"/> (at least one), the shown steps it
        /// fails or counts.
        /// </summary>
        public bool Hear(string tag, int amount = 1)
        {
            if (string.IsNullOrEmpty(tag)) return false;
            bool changed = false;
            foreach (Step step in steps)
            {
                if (step.definition.showOn == tag) changed |= Show(step);
                if (step.definition.hideOn == tag) changed |= Hide(step);
                if (step.definition.failOn == tag) changed |= Fail(step);
                else if (step.definition.completeOn == tag) changed |= Advance(step, Math.Max(1, amount));
            }
            return Notify(changed);
        }

        bool Show(Step step)
        {
            if (step.shown || step.Final || !PrerequisitesDone(step)) return false;
            step.shown = true;
            raised.Add(step.definition.id + ShownSuffix);
            ReportMoved(step);
            return true;
        }

        bool Hide(Step step)
        {
            if (!step.shown || step.Final) return false;
            step.shown = false;
            raised.Add(step.definition.id + HiddenSuffix);
            ReportMoved(step);
            return true;
        }

        bool Advance(Step step, int amount)
        {
            if (!step.shown || step.Final || !PrerequisitesDone(step)) return false;
            step.progress = (int)Math.Min(step.definition.Required, (long)step.progress + amount);
            if (step.Final) raised.Add(step.definition.id + DoneSuffix);
            ReportMoved(step);
            return true;
        }

        bool PrerequisitesDone(Step step)
        {
            if (step.definition.requiresObjectives == null) return false;
            foreach (string required in step.definition.requiresObjectives)
                if (required == null || !byId.TryGetValue(required, out Step prerequisite) || !prerequisite.Capture().Done)
                    return false;
            return true;
        }

        bool Fail(Step step)
        {
            if (!step.shown || step.Final) return false;
            step.failed = true;
            raised.Add(step.definition.id + FailedSuffix);
            ReportMoved(step);
            return true;
        }

        // Calls vanish from builds; in the editor, one null check while no debugger listens.
        [Conditional("UNITY_EDITOR")]
        void ReportMoved(Step step)
        {
#if UNITY_EDITOR
            Moved?.Invoke(step.Capture());
#endif
        }

        Step Find(string id) => id != null && byId.TryGetValue(id, out Step step)
            ? step : throw new KeyNotFoundException($"Unknown objective '{id}'.");

        bool Notify(bool changed)
        {
            // Taken before the notice: a listener may move steps again, and those moves raise only their own signals.
            string[] tags = raised.Count > 0 ? raised.ToArray() : null;
            raised.Clear();
            if (changed) Changed?.Invoke();
            if (tags != null)
                foreach (string tag in tags) Signalled?.Invoke(tag);
            return changed;
        }
    }
}
