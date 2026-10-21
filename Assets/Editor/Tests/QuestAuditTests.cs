using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Baseline.Defs;
using Baseline.Dialogue;
using Baseline.Editor;
using Baseline.Quests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed partial class QuestAuditTests
    {
        [QuestSignalTag] private const string CompleteSignal = "QuestAuditTest.Action";
        private const string FailSignal = QuestRun.SkipSignal;
        private readonly List<Object> created = new();

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created) Object.DestroyImmediate(item);
            created.Clear();
        }

        [Test]
        public void CatalogListsWaitsAndDistinguishesDeclaredAndUnknownTags()
        {
            QuestDef quest = Quest("catalog");
            Assert.That(QuestSignalCatalog.GameTags.ContainsKey(CompleteSignal), Is.True);
            Assert.That(QuestSignalCatalog.Raised(quest).ContainsKey("target.Done"), Is.True);
            Assert.That(QuestSignalCatalog.Waits(quest), Does.Contain(("target.completeOn", CompleteSignal)));
            Assert.That(QuestSignalCatalog.Unraised(quest), Is.Empty);
            SetStepText(quest, "completeOn", "QuestAuditTest.Misspelled");
            QuestAuditIssue issue = QuestAudit.Inspect(new[] { quest }).Issues.Single(item => item.Location == "target.completeOn");
            Assert.That(issue.Severity, Is.EqualTo(QuestAuditSeverity.Error));
            Assert.That(issue.Quest, Is.SameAs(quest));
            Assert.That(issue.Asset, Is.SameAs(quest));
            Assert.That(issue.Message, Does.Contain("QuestAuditTest.Misspelled"));
        }

        [Test]
        public void GuardAuditFindsUnknownConditionsAndMissingHintTranslations()
        {
            var quest = Quest("guard-audit");
            var serialized = new SerializedObject(quest);
            var guards = serialized.FindProperty("guards");
            guards.arraySize = 1;
            var guard = guards.GetArrayElementAtIndex(0);
            guard.FindPropertyRelative("action").stringValue = "Example.Attempt";
            guard.FindPropertyRelative("condition").stringValue = "Example.Misspelled";
            guard.FindPropertyRelative("hint").stringValue = "Complete preparation";
            guard.FindPropertyRelative("hintKey").stringValue = "Missing.Guard.Hint";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var issues = QuestAudit.Inspect(new[] { quest }).Issues;
            Assert.That(issues.Any(issue => issue.Location.EndsWith(".condition") && issue.Severity == QuestAuditSeverity.Error), Is.True);
            Assert.That(issues.Count(issue => issue.Location.EndsWith(".hintKey")), Is.EqualTo(2));
        }

        [Test]
        public void GuardPreviewUsesIsolatedFactsAndDoesNotAdvanceOrSave()
        {
            var quest = Quest("guard-preview");
            var serialized = new SerializedObject(quest);
            var guards = serialized.FindProperty("guards");
            guards.arraySize = 1;
            var guard = guards.GetArrayElementAtIndex(0);
            guard.FindPropertyRelative("action").stringValue = "Example.Attempt";
            guard.FindPropertyRelative("condition").stringValue = "Example.Ready";
            guard.FindPropertyRelative("hint").stringValue = "Complete preparation";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            string directory = Path.Combine(Path.GetTempPath(), "BaselineGuardPreview-" + Guid.NewGuid().ToString("N"));
            using (QuestLog.UseDirectory(directory))
            using (var preview = new QuestPreviewSession(quest))
            {
                preview.Begin();
                Assert.That(preview.TryAction("Example.Attempt"), Is.False);
                Assert.That(preview.LastBlocker, Is.SameAs(quest.Guards[0]));
                preview.SetCondition("Example.Ready", true);
                Assert.That(preview.TryAction("Example.Attempt"), Is.True);
                Assert.That(preview.Run.Steps["target"].Progress, Is.Zero);
                preview.Raise(CompleteSignal);
                Assert.That(preview.Run.IsFinished, Is.True);
                Assert.That(File.Exists(QuestLog.FilePath), Is.False);
                preview.Reset();
                preview.Begin();
                Assert.That(preview.TryAction("Example.Attempt"), Is.False);
            }
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        [Test]
        public void MissingAutomaticSignalsAreWarningsRatherThanPretendProducers()
        {
            QuestDef quest = Quest("manual");
            SetStepText(quest, "showOn", "");
            SetStepText(quest, "completeOn", "");
            QuestAuditReport report = QuestAudit.Inspect(new[] { quest });
            Assert.That(report.IsValid, Is.True);
            Assert.That(report.WarningCount, Is.EqualTo(2));
            Assert.That(report.Issues.All(issue => issue.Severity == QuestAuditSeverity.Warning), Is.True);
        }

        [Test]
        public void DuplicateIdentityAndPrerequisiteCyclesAreReported()
        {
            QuestDef first = Quest("duplicate");
            QuestDef second = Quest("duplicate");
            Require(first, second);
            Require(second, first);
            QuestAuditReport report = QuestAudit.Inspect(new[] { first, second });
            Assert.That(report.Issues.Count(issue => issue.Location == "DefName"), Is.EqualTo(2));
            Assert.That(report.Issues.Any(issue => issue.Message.Contains("Prerequisite cycle")), Is.True);
            Assert.That(report.IsValid, Is.False);
        }

        [Test]
        public void InvalidDefinitionsAndReferencesOutsideTheAuditSetAreReported()
        {
            QuestDef quest = Quest("invalid");
            QuestDef external = Quest("external");
            Require(quest, external);
            SetStepText(quest, "id", "");
            QuestAuditReport report = QuestAudit.Inspect(new[] { quest });
            Assert.That(report.Issues.Any(issue => issue.Location == "definition" && issue.Severity == QuestAuditSeverity.Error), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Location == "requires" && issue.Message.Contains("external")), Is.True);
        }

        [Test]
        public void DialogueAuditFindsMissingExpressionAndLocalizedText()
        {
            QuestDef quest = Quest("tutorial");
            var speaker = Make<CharacterDef>("Sam");
            speaker.displayName = "Sam";
            speaker.role = "Engineer";
            Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);
            created.Add(sprite);
            speaker.illustration = speaker.portrait = sprite;
            var dialog = Make<DialogDef>("test-dialog");
            dialog.lines = new[] { new DialogLine { speaker = speaker, text = "Test", textKey = "QuestAuditTest.MissingTranslation", expression = "unmapped" } };
            QuestAuditReport report = QuestAudit.Inspect(new[] { quest }, new[] { dialog });
            Assert.That(report.Issues.Any(issue => issue.Location == "dialogue.lines[0].expression" && issue.Message.Contains("unmapped")), Is.True);
            Assert.That(report.Issues.Count(issue => issue.Location == "dialogue.lines[0].textKey"), Is.EqualTo(2));
        }

        [Test]
        public void PreviewCompletesAndFailsWithoutCreatingOrChangingPlayerProgress()
        {
            QuestDef quest = Quest("preview");
            string directory = Path.Combine(Path.GetTempPath(), "IgnitionQuestAudit-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (QuestLog.UseDirectory(directory))
                {
                    using (var preview = new QuestPreviewSession(quest))
                    {
                        int finished = 0;
                        preview.Run.Finished += _ => finished++;
                        preview.Begin();
                        preview.Raise(CompleteSignal);
                        Assert.That(preview.Run.IsPreview, Is.True);
                        Assert.That(preview.Run.Steps["target"].Done, Is.True);
                        Assert.That(preview.Run.Ending.Passes, Is.True);
                        Assert.That(finished, Is.EqualTo(1));
                    }
                    Assert.That(File.Exists(QuestLog.FilePath), Is.False);
                    Assert.That(QuestLog.Values(quest), Is.Empty);

                    using (var real = new QuestRun(quest))
                    {
                        Assert.That(real.IsPreview, Is.False);
                        real.Begin();
                        real.Raise(CompleteSignal);
                    }
                    string saved = File.ReadAllText(QuestLog.FilePath);
                    using (var preview = new QuestPreviewSession(quest))
                    {
                        preview.Begin();
                        preview.Raise(CompleteSignal);
                        preview.Reset();
                        preview.Begin();
                        preview.Raise(FailSignal);
                        Assert.That(preview.Run.Ending.Passes, Is.False);
                    }
                    Assert.That(File.ReadAllText(QuestLog.FilePath), Is.EqualTo(saved));
                    Assert.That(QuestLog.Value(quest, "done"), Is.EqualTo(1));
                    Assert.That(QuestLog.Value(quest, "failed"), Is.Zero);
                }
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public void PreviewResetHonorsAmountsAndDisposesOldRunsWithoutReplacingTheObserver()
        {
            QuestDef quest = Quest("reset", 3);
            IQuestRunObserver previous = QuestRun.Observer;
            var observer = new Observer();
            QuestRun.Observer = observer;
            QuestPreviewSession preview = null;
            try
            {
                preview = new QuestPreviewSession(quest);
                preview.Begin();
                preview.Raise(CompleteSignal, 2);
                Assert.That(preview.Run.Steps["target"].Progress, Is.EqualTo(2));
                Assert.That(preview.Run.IsFinished, Is.False);
                QuestRun old = preview.Run;
                preview.Reset();
                Assert.That(QuestRun.Running, Has.No.Member(old));
                Assert.That(preview.Run.Steps["target"].Shown, Is.False);
                old.Raise(CompleteSignal);
                preview.Begin();
                Assert.That(preview.Run.Steps["target"].Progress, Is.Zero);
                preview.Raise(CompleteSignal, 3);
                Assert.That(preview.Run.IsFinished, Is.True);
                Assert.That(QuestRun.Observer, Is.SameAs(observer));
                Assert.That(observer.HeardCount, Is.GreaterThan(0));
                QuestRun last = preview.Run;
                preview.Dispose();
                Assert.That(QuestRun.Running, Has.No.Member(last));
                Assert.Throws<ObjectDisposedException>(() => preview.Raise(CompleteSignal));
            }
            finally
            {
                preview?.Dispose();
                QuestRun.Observer = previous;
            }
        }

        [Test]
        public void ClosingTheTrackerDisposesItsPreviewAndRestoresTheObserver()
        {
            QuestDef quest = Quest("window-preview");
            IQuestRunObserver previous = QuestRun.Observer;
            var observer = new Observer();
            QuestRun.Observer = observer;
            QuestDebugger window = null;
            QuestRun run = null;
            try
            {
                window = ScriptableObject.CreateInstance<QuestDebugger>();
                Assert.That(QuestRun.Observer, Is.SameAs(window));
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                typeof(QuestDebugger).GetMethod("SelectQuest", flags).Invoke(window, new object[] { quest });
                var preview = (QuestPreviewSession)typeof(QuestDebugger).GetField("preview", flags).GetValue(window);
                run = preview.Run;
                Assert.That(QuestRun.Running, Does.Contain(run));
                Object.DestroyImmediate(window);
                window = null;
                Assert.That(QuestRun.Observer, Is.SameAs(observer));
                Assert.That(QuestRun.Running, Has.No.Member(run));
            }
            finally
            {
                if (window != null) Object.DestroyImmediate(window);
                run?.Dispose();
                QuestRun.Observer = previous;
            }
        }

        private T Make<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            created.Add(asset);
            return asset;
        }

        private QuestDef Quest(string name, int count = 1)
        {
            QuestDef quest = Make<QuestDef>(name);
            var serialized = new SerializedObject(quest);
            SerializedProperty objectives = serialized.FindProperty("objectives");
            objectives.arraySize = 1;
            SerializedProperty step = objectives.GetArrayElementAtIndex(0);
            step.FindPropertyRelative("id").stringValue = "target";
            step.FindPropertyRelative("text").stringValue = "Reach the target";
            step.FindPropertyRelative("showOn").stringValue = QuestRun.BeginSignal;
            step.FindPropertyRelative("completeOn").stringValue = CompleteSignal;
            step.FindPropertyRelative("failOn").stringValue = FailSignal;
            step.FindPropertyRelative("count").intValue = count;
            SerializedProperty endings = serialized.FindProperty("endings");
            endings.arraySize = 2;
            Ending(endings.GetArrayElementAtIndex(0), QuestRun.StepsDoneSignal, "done", true);
            Ending(endings.GetArrayElementAtIndex(1), QuestRun.StepFailedSignal, "failed", false);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return quest;
        }

        private static void Ending(SerializedProperty ending, string signal, string outcome, bool passes)
        {
            ending.FindPropertyRelative("signal").stringValue = signal;
            ending.FindPropertyRelative("outcome").stringValue = outcome;
            ending.FindPropertyRelative("passes").boolValue = passes;
        }

        private static void Require(QuestDef quest, QuestDef required)
        {
            var serialized = new SerializedObject(quest);
            SerializedProperty requirements = serialized.FindProperty("requires");
            requirements.arraySize = 1;
            requirements.GetArrayElementAtIndex(0).objectReferenceValue = required;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStepText(QuestDef quest, string field, string text)
        {
            var serialized = new SerializedObject(quest);
            serialized.FindProperty("objectives").GetArrayElementAtIndex(0).FindPropertyRelative(field).stringValue = text;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Observer : IQuestRunObserver
        {
            public int HeardCount;
            public void Heard(QuestRun run, QuestSignal signal, bool heard) => HeardCount++;
            public void Moved(QuestRun run, QuestSnapshot step) { }
            public void Ended(QuestRun run, QuestEnding ending) { }
            public void Handled(QuestRun run, QuestSignal signal) { }
        }
    }
}
