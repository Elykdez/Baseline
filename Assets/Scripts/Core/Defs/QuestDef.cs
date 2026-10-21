using System;
using System.Collections.Generic;
using Baseline.Quests;
using UnityEngine;

namespace Baseline.Defs
{
    [Serializable]
    public sealed class QuestEnding
    {
        [SerializeField] string signal = string.Empty;
        [SerializeField] string outcome = string.Empty;
        [SerializeField] bool passes;
        [SerializeField] QuestDef[] alsoRecord = Array.Empty<QuestDef>();

        public string Signal => signal;
        public string Outcome => outcome;
        public bool Passes => passes;
        public IReadOnlyList<QuestDef> AlsoRecord => alsoRecord;
    }

    [CreateAssetMenu(fileName = "QuestDef", menuName = "Baseline/Defs/Quest")]
    public sealed class QuestDef : Def
    {
        [SerializeField] QuestData[] objectives = Array.Empty<QuestData>();
        [SerializeField] QuestDef[] requires = Array.Empty<QuestDef>();
        [SerializeField] QuestGuard[] guards = Array.Empty<QuestGuard>();
        [SerializeField, Tooltip("The first matching signal ends the run and records its outcome.")]
        QuestEnding[] endings = Array.Empty<QuestEnding>();

        public IReadOnlyList<QuestData> Objectives => objectives;
        public IReadOnlyList<QuestDef> Requires => requires;
        public IReadOnlyList<QuestGuard> Guards => guards;
        public IReadOnlyList<QuestEnding> Endings => endings;

        public QuestEnding Ending(string signal)
        {
            foreach (QuestEnding ending in endings)
                if (ending != null && ending.Signal == signal) return ending;
            return null;
        }

        public override void Validate()
        {
            if (objectives == null || requires == null || guards == null || endings == null)
                throw new InvalidOperationException($"Quest '{DefName}' has a missing collection.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestData step in objectives)
                if (step == null || string.IsNullOrWhiteSpace(step.id) || !ids.Add(step.id))
                    throw new InvalidOperationException($"Quest '{DefName}' has an invalid or duplicate objective.");
            var byId = new Dictionary<string, QuestData>(StringComparer.Ordinal);
            foreach (QuestData step in objectives)
            {
                byId.Add(step.id, step);
                if (step.requiresObjectives == null)
                    throw new InvalidOperationException($"Quest '{DefName}', objective '{step.id}', has a missing prerequisite collection.");
                foreach (string required in step.requiresObjectives)
                    if (required == null || !ids.Contains(required) || required == step.id)
                        throw new InvalidOperationException($"Quest '{DefName}', objective '{step.id}', has an unknown or self-referencing prerequisite '{required}'.");
            }
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var visiting = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestData step in objectives) Visit(step.id);

            var guardKeys = new HashSet<(string, string)>();
            foreach (QuestGuard guard in guards)
            {
                if (guard == null || string.IsNullOrWhiteSpace(guard.action) || string.IsNullOrWhiteSpace(guard.condition)
                    || string.IsNullOrWhiteSpace(guard.hint))
                    throw new InvalidOperationException($"Quest '{DefName}' has a guard without an action, condition or fallback hint.");
                if (!guardKeys.Add((guard.action, guard.condition)))
                    throw new InvalidOperationException($"Quest '{DefName}' repeats condition '{guard.condition}' for action '{guard.action}'.");
            }
            foreach (QuestDef other in requires)
                if (other == null || other == this)
                    throw new InvalidOperationException($"Quest '{DefName}' has an empty or circular requirement.");
            var signals = new HashSet<string>(StringComparer.Ordinal);
            foreach (QuestEnding ending in endings)
            {
                if (ending == null || string.IsNullOrWhiteSpace(ending.Signal) || string.IsNullOrWhiteSpace(ending.Outcome))
                    throw new InvalidOperationException($"Quest '{DefName}' has an ending without a signal or outcome.");
                if (ending.Outcome == QuestLog.PassedValue)
                    throw new InvalidOperationException($"Quest '{DefName}' uses the reserved outcome '{QuestLog.PassedValue}'.");
                if (!signals.Add(ending.Signal))
                    throw new InvalidOperationException($"Quest '{DefName}' ends twice on '{ending.Signal}'.");
                if (ending.AlsoRecord == null)
                    throw new InvalidOperationException($"Quest '{DefName}' has a missing also-record collection.");
                var recorded = new HashSet<QuestDef>();
                foreach (QuestDef other in ending.AlsoRecord)
                    if (other == null || other == this || !recorded.Add(other))
                        throw new InvalidOperationException($"Quest '{DefName}' has an invalid or repeated also-record quest.");
            }

            void Visit(string id)
            {
                if (visited.Contains(id)) return;
                if (!visiting.Add(id))
                    throw new InvalidOperationException($"Quest '{DefName}' has a circular objective prerequisite at '{id}'.");
                foreach (string required in byId[id].requiresObjectives) Visit(required);
                visiting.Remove(id);
                visited.Add(id);
            }
        }
    }
}
