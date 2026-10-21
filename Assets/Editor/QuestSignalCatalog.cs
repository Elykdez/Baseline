using System;
using System.Collections.Generic;
using System.Reflection;
using Baseline.Defs;
using Baseline.Quests;
using UnityEditor;

namespace Baseline.Editor
{
    public static class QuestSignalCatalog
    {
        public static IReadOnlyDictionary<string, string> ConditionTags
        {
            get
            {
                var tags = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<QuestConditionTagAttribute>())
                    if (field.IsLiteral && field.GetRawConstantValue() is string tag && tag.Length > 0)
                        tags.TryAdd(tag, field.DeclaringType?.Name ?? "?");
                return tags;
            }
        }

        public static IReadOnlyDictionary<string, string> GameTags
        {
            get
            {
                var tags = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (FieldInfo field in TypeCache.GetFieldsWithAttribute<QuestSignalTagAttribute>())
                    if (field.IsLiteral && field.GetRawConstantValue() is string tag && tag.Length > 0)
                        tags.TryAdd(tag, field.DeclaringType?.Name ?? "?");
                return tags;
            }
        }

        public static Dictionary<string, string> Raised(QuestDef quest)
        {
            var raised = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> entry in GameTags) raised.Add(entry.Key, entry.Value);
            if (quest != null && quest.Objectives != null)
                foreach (QuestData step in quest.Objectives)
                {
                    if (step == null || string.IsNullOrWhiteSpace(step.id)) continue;
                    foreach (string suffix in new[] { QuestJournal.ShownSuffix, QuestJournal.HiddenSuffix,
                        QuestJournal.DoneSuffix, QuestJournal.FailedSuffix })
                        raised.TryAdd(step.id + suffix, $"step '{step.id}'");
                }
            return raised;
        }

        public static List<(string where, string tag)> Waits(QuestDef quest)
        {
            var waits = new List<(string, string)>();
            if (quest == null) return waits;
            foreach (QuestData step in quest.Objectives ?? Array.Empty<QuestData>())
            {
                if (step == null) continue;
                Check(step.id + ".showOn", step.showOn);
                Check(step.id + ".completeOn", step.completeOn);
                Check(step.id + ".failOn", step.failOn);
                Check(step.id + ".hideOn", step.hideOn);
            }
            foreach (QuestEnding ending in quest.Endings ?? Array.Empty<QuestEnding>())
                if (ending != null) Check("ending (" + ending.Outcome + ")", ending.Signal);
            return waits;

            void Check(string where, string tag)
            {
                if (!string.IsNullOrEmpty(tag)) waits.Add((where, tag));
            }
        }

        public static List<(string where, string tag)> Unraised(QuestDef quest)
        {
            Dictionary<string, string> raised = Raised(quest);
            return Waits(quest).FindAll(wait => !raised.ContainsKey(wait.tag));
        }
    }
}
