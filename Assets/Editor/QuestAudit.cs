using System;
using System.Collections.Generic;
using System.Linq;
using Baseline.Defs;
using Baseline.Dialogue;
using Baseline.Quests;
using UnityEditor;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace Baseline.Editor
{
    public enum QuestAuditSeverity { Warning, Error }

    public sealed class QuestAuditIssue
    {
        public QuestAuditSeverity Severity { get; }
        public QuestDef Quest { get; }
        public Object Asset { get; }
        public string Path { get; }
        public string Location { get; }
        public string Message { get; }

        public QuestAuditIssue(QuestAuditSeverity severity, QuestDef quest, Object asset, string location, string message)
        {
            Severity = severity;
            Quest = quest;
            Asset = asset;
            Path = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty;
            Location = location;
            Message = message;
        }
    }

    public sealed class QuestAuditReport
    {
        public IReadOnlyList<QuestDef> Quests { get; }
        public IReadOnlyList<DialogDef> Dialogues { get; }
        public IReadOnlyList<QuestAuditIssue> Issues { get; }
        public int ErrorCount => Issues.Count(issue => issue.Severity == QuestAuditSeverity.Error);
        public int WarningCount => Issues.Count - ErrorCount;
        public bool IsValid => ErrorCount == 0;

        internal QuestAuditReport(QuestDef[] quests, DialogDef[] dialogues, List<QuestAuditIssue> issues)
        {
            Quests = quests;
            Dialogues = dialogues;
            Issues = issues;
        }
    }

    /// <summary>Read-only content checks. A known tag is a declared producer, not proof that gameplay reaches it.</summary>
    public static class QuestAudit
    {
        public static QuestAuditReport Scan() => Inspect(FindAssets<QuestDef>(), FindAssets<DialogDef>());

        public static QuestAuditReport Inspect(IEnumerable<QuestDef> quests, IEnumerable<DialogDef> dialogues = null)
        {
            QuestDef[] definitions = (quests ?? Array.Empty<QuestDef>()).Where(quest => quest != null).Distinct().ToArray();
            DialogDef[] contents = (dialogues ?? Array.Empty<DialogDef>()).Where(content => content != null).Distinct().ToArray();
            var issues = new List<QuestAuditIssue>();
            var known = new HashSet<QuestDef>(definitions);
            var tables = new Dictionary<string, StringTable>();
            foreach (string locale in new[] { "en", "zh" })
                tables[locale] = AssetDatabase.LoadAssetAtPath<StringTable>("Assets/Locales/Tables/StringTable_" + locale + ".asset");

            foreach (IGrouping<string, QuestDef> duplicate in definitions.GroupBy(quest => quest.DefName, StringComparer.Ordinal).Where(group => group.Count() > 1))
                foreach (QuestDef quest in duplicate)
                    Add(QuestAuditSeverity.Error, quest, quest, "DefName", "Duplicate quest identity '" + duplicate.Key + "'. Quest saves use this identity.");

            foreach (QuestDef quest in definitions)
            {
                Validate(quest, quest, "definition", quest.Validate);
                if (string.IsNullOrWhiteSpace(quest.DefName))
                    Add(QuestAuditSeverity.Error, quest, quest, "DefName", "Quest identity must not be blank.");
                CheckText(quest, quest, "labelKey", quest.LabelKey);
                foreach ((string where, string tag) in QuestSignalCatalog.Unraised(quest))
                    Add(QuestAuditSeverity.Error, quest, quest, where, "No declared producer raises '" + tag + "'. Check the spelling and [QuestSignalTag] constants.");
                foreach (QuestData step in quest.Objectives ?? Array.Empty<QuestData>())
                {
                    if (step == null) continue;
                    CheckText(quest, quest, step.id + ".textKey", step.textKey);
                    if (string.IsNullOrWhiteSpace(step.textKey) && string.IsNullOrWhiteSpace(step.text))
                        Add(QuestAuditSeverity.Error, quest, quest, step.id + ".text", "Objective has no localization key or fallback text.");
                    if (string.IsNullOrEmpty(step.showOn))
                        Add(QuestAuditSeverity.Warning, quest, quest, step.id + ".showOn", "No show signal is configured; gameplay must show this objective directly.");
                    if (string.IsNullOrEmpty(step.completeOn))
                        Add(QuestAuditSeverity.Warning, quest, quest, step.id + ".completeOn", "No completion signal is configured; gameplay must complete this objective directly.");
                }
                if (quest.Endings != null && quest.Endings.Count == 0)
                    Add(QuestAuditSeverity.Warning, quest, quest, "endings", "No ending is configured; this quest cannot record an outcome.");
                foreach (QuestDef required in quest.Requires ?? Array.Empty<QuestDef>()) CheckReference(quest, required, "requires");
                var guardSpeakers = new HashSet<CharacterDef>();
                foreach (QuestGuard guard in quest.Guards ?? Array.Empty<QuestGuard>())
                {
                    if (guard == null) continue;
                    string location = "guard (" + guard.action + ", " + guard.condition + ")";
                    CheckText(quest, quest, location + ".hintKey", guard.hintKey);
                    if (string.IsNullOrEmpty(guard.condition) || !QuestSignalCatalog.ConditionTags.ContainsKey(guard.condition))
                        Add(QuestAuditSeverity.Error, quest, quest, location + ".condition", "No declared provider reports '" + guard.condition + "'. Check [QuestConditionTag] constants.");
                    if (guard.dialogue != null) CheckDialog(quest, quest, guard.dialogue, location + ".dialogue", guardSpeakers);
                }
                foreach (QuestEnding ending in quest.Endings ?? Array.Empty<QuestEnding>())
                    if (ending != null)
                        foreach (QuestDef other in ending.AlsoRecord ?? Array.Empty<QuestDef>()) CheckReference(quest, other, "alsoRecord");
            }

            var visited = new HashSet<QuestDef>();
            var stack = new List<QuestDef>();
            foreach (QuestDef quest in definitions) Visit(quest);

            var speakers = new HashSet<CharacterDef>();
            foreach (DialogDef content in contents)
            {
                CheckDialog(null, content, content, "dialogue", speakers);
            }
            return new QuestAuditReport(definitions, contents, issues);

            void Add(QuestAuditSeverity severity, QuestDef quest, Object asset, string location, string message) =>
                issues.Add(new QuestAuditIssue(severity, quest, asset, location, message));

            void Validate(QuestDef quest, Object asset, string location, Action validate)
            {
                try { validate(); }
                catch (Exception error) { Add(QuestAuditSeverity.Error, quest, asset, location, error.Message); }
            }

            void CheckReference(QuestDef owner, QuestDef other, string location)
            {
                if (other != null && !known.Contains(other))
                    Add(QuestAuditSeverity.Error, owner, owner, location, "Referenced quest '" + other.DefName + "' is outside the inspected quest set.");
            }

            void Visit(QuestDef quest)
            {
                int cycleStart = stack.IndexOf(quest);
                if (cycleStart >= 0)
                {
                    string cycle = string.Join(" -> ", stack.Skip(cycleStart).Select(item => item.DefName).Concat(new[] { quest.DefName }));
                    Add(QuestAuditSeverity.Error, quest, quest, "requires", "Prerequisite cycle: " + cycle + ".");
                    return;
                }
                if (!visited.Add(quest)) return;
                stack.Add(quest);
                foreach (QuestDef required in quest.Requires ?? Array.Empty<QuestDef>())
                    if (required != null && known.Contains(required)) Visit(required);
                stack.RemoveAt(stack.Count - 1);
            }

            void CheckText(QuestDef quest, Object asset, string location, string key)
            {
                if (string.IsNullOrEmpty(key)) return;
                foreach (KeyValuePair<string, StringTable> table in tables)
                    if (string.IsNullOrWhiteSpace(table.Value?.GetEntry(key)?.Value))
                        Add(QuestAuditSeverity.Error, quest, asset, location, "Missing " + table.Key + " localization for '" + key + "'.");
            }

            void CheckDialog(QuestDef quest, Object owner, DialogDef dialog, string role, HashSet<CharacterDef> speakers)
            {
                if (dialog == null)
                {
                    Add(QuestAuditSeverity.Error, quest, owner, role, "Missing dialogue.");
                    return;
                }
                Validate(quest, dialog, role, dialog.Validate);
                CheckText(quest, dialog, role + ".titleKey", dialog.titleKey);
                for (int i = 0; i < (dialog.lines?.Length ?? 0); i++)
                {
                    DialogLine line = dialog.lines[i];
                    string location = role + ".lines[" + i + "]";
                    if (line == null) continue;
                    CheckText(quest, dialog, location + ".textKey", line.textKey);
                    if (line.speaker == null) continue;
                    if (speakers.Add(line.speaker))
                    {
                        Validate(quest, line.speaker, "speaker", line.speaker.Validate);
                        CheckText(quest, line.speaker, "nameKey", line.speaker.nameKey);
                        CheckText(quest, line.speaker, "roleKey", line.speaker.roleKey);
                    }
                    if (!string.IsNullOrEmpty(line.expression) && line.expression != "default"
                        && !(line.speaker.expressions?.Any(entry => entry != null && entry.id == line.expression) ?? false))
                        Add(QuestAuditSeverity.Error, quest, dialog, location + ".expression",
                            "Speaker '" + line.speaker.DefName + "' has no '" + line.expression + "' expression; playback would use default art.");
                }
            }
        }

        private static IEnumerable<T> FindAssets<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path, StringComparer.Ordinal)
            .SelectMany(path => AssetDatabase.LoadAllAssetsAtPath(path).OfType<T>());
    }
}
