using System;
using System.Collections.Generic;
using Hypocycloid.Core;
using Baseline.Defs;
using Baseline.Quests;

namespace Baseline.Editor
{
    /// <summary>A disposable editor attempt with real quest behavior and no persistent outcomes.</summary>
    public sealed class QuestPreviewSession : IDisposable
    {
        private readonly QuestDef quest;
        private bool disposed;
        private readonly Dictionary<string, bool> conditions = new(StringComparer.Ordinal);
        public QuestRun Run { get; private set; }
        public QuestGuard LastBlocker { get; private set; }

        public QuestPreviewSession(QuestDef quest)
        {
            this.quest = quest ?? throw new ArgumentNullException(nameof(quest));
            Reset();
        }

        public void Begin()
        {
            RequireActive();
            Run.Begin();
        }

        /// <summary>Replaces this attempt with a fresh, unbegun run. Existing observers remain installed.</summary>
        public void Reset()
        {
            RequireActive();
            QuestRun next = QuestRun.CreatePreview(quest);
            Run?.Dispose();
            Run = next;
            conditions.Clear();
            LastBlocker = null;
        }

        public void Raise(string tag, int amount = 1)
        {
            RequireActive();
            if (!string.IsNullOrEmpty(tag)) SignalBus.Fire(new QuestSignal(tag, Run, amount));
        }

        public bool Condition(string key) => conditions.TryGetValue(key, out bool value) && value;

        public void SetCondition(string key, bool value)
        {
            RequireActive();
            conditions[key] = value;
            LastBlocker = null;
        }

        public bool TryAction(string action)
        {
            RequireActive();
            if (Run.IsFinished) return false;
            LastBlocker = Run.CheckAction(action, Condition);
            return Run.TryAction(action, Condition);
        }

        private void RequireActive()
        {
            if (disposed) throw new ObjectDisposedException(nameof(QuestPreviewSession));
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Run.Dispose();
        }
    }
}
