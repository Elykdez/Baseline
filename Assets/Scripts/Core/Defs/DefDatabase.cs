using System;
using System.Collections.Generic;
using Baseline.Dialogue;
using UnityEngine;

namespace Baseline.Defs
{
    [CreateAssetMenu(fileName = "DefDatabase", menuName = "Baseline/Defs/Database")]
    public sealed class DefDatabase : ScriptableObject
    {
        [SerializeField] Def[] definitions = Array.Empty<Def>();

        public IReadOnlyList<Def> Definitions => definitions;

        public T Find<T>(string defName) where T : Def
        {
            foreach (Def definition in definitions)
                if (definition is T found && found.DefName == defName) return found;
            return null;
        }

        public T Get<T>(string defName) where T : Def => Find<T>(defName)
            ?? throw new KeyNotFoundException($"No {typeof(T).Name} named '{defName}' in '{name}'.");

        public IEnumerable<T> All<T>() where T : Def
        {
            foreach (Def definition in definitions)
                if (definition is T found) yield return found;
        }

        public void Validate()
        {
            if (definitions == null) throw new InvalidOperationException($"Database '{name}' has no definition collection.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            var registered = new HashSet<Def>(definitions);
            foreach (Def definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.DefName) || !names.Add(definition.DefName))
                    throw new InvalidOperationException($"Database '{name}' has an empty or duplicate definition.");
                definition.Validate();
                if (definition is DialogDef dialog)
                    foreach (DialogLine line in dialog.lines) CheckReference(dialog, line.speaker);
                if (definition is not QuestDef quest) continue;
                foreach (var guard in quest.Guards)
                    if (guard.dialogue != null) CheckReference(quest, guard.dialogue);
                foreach (QuestDef required in quest.Requires) CheckReference(quest, required);
                foreach (QuestEnding ending in quest.Endings)
                    foreach (QuestDef other in ending.AlsoRecord) CheckReference(quest, other);
            }

            var visiting = new HashSet<QuestDef>();
            var visited = new HashSet<QuestDef>();
            foreach (QuestDef quest in All<QuestDef>()) Visit(quest);

            void CheckReference(Def owner, Def referenced)
            {
                if (!registered.Contains(referenced))
                    throw new InvalidOperationException($"Definition '{owner.DefName}' references '{referenced.DefName}', which is not in '{name}'.");
            }

            void Visit(QuestDef quest)
            {
                if (visited.Contains(quest)) return;
                if (!visiting.Add(quest))
                    throw new InvalidOperationException($"Quest requirements contain a cycle at '{quest.DefName}'.");
                foreach (QuestDef required in quest.Requires) Visit(required);
                visiting.Remove(quest);
                visited.Add(quest);
            }
        }
    }
}
