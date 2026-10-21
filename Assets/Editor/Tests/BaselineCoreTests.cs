using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Baseline.Defs;
using Baseline.Quests;
using Hypocycloid.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed partial class BaselineCoreTests
    {
        readonly List<Object> assets = new();
        string directory;
        IDisposable store;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "BaselineTests-" + Guid.NewGuid().ToString("N"));
            store = QuestLog.UseDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            store.Dispose();
            foreach (Object asset in assets) Object.DestroyImmediate(asset);
            assets.Clear();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        T Asset<T>(string name) where T : ScriptableObject
        {
            T asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            assets.Add(asset);
            return asset;
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        QuestDef Quest(string name)
        {
            QuestDef quest = Asset<QuestDef>(name);
            Set(quest, "objectives", new[] { new QuestData { id = "step", showOn = QuestRun.BeginSignal, completeOn = "Item.Collected", count = 2 } });
            var ending = new QuestEnding();
            Set(ending, "signal", QuestRun.StepsDoneSignal);
            Set(ending, "outcome", "done");
            Set(ending, "passes", true);
            Set(quest, "endings", new[] { ending });
            return quest;
        }

        [Test]
        public void Run_IsolatesSignalsCountsProgressAndRecordsEndingOnce()
        {
            QuestDef quest = Quest("Collect");
            int before = SignalBus.GetSubscriberCount<QuestSignal>();
            using (var first = new QuestRun(quest))
            using (var second = new QuestRun(quest))
            {
                first.Begin();
                second.Begin();
                first.Raise("Item.Collected");
                Assert.That(first.Steps["step"].Progress, Is.EqualTo(1));
                Assert.That(second.Steps["step"].Progress, Is.Zero);
                SignalBus.Fire(new QuestSignal("Item.Collected", first, 10));
                Assert.That(first.IsFinished, Is.True);
                first.Raise("Item.Collected");
                Assert.That(QuestLog.Value(quest, "done"), Is.EqualTo(1));
                Assert.That(QuestLog.Passed(quest), Is.True);
            }
            Assert.That(SignalBus.GetSubscriberCount<QuestSignal>(), Is.EqualTo(before));
        }

        [Test]
        public void Journal_HiddenStepsIgnoreSignalsAndTerminalStepsStayFinal()
        {
            var journal = new QuestJournal(new[] { new QuestData { id = "step", completeOn = "Collect", count = 2 } });
            journal.Hear("Collect", 2);
            Assert.That(journal["step"].Progress, Is.Zero);
            journal.Show("step");
            journal.Hear("Collect");
            journal.Hide("step");
            journal.Hear("Collect");
            Assert.That(journal["step"].Progress, Is.EqualTo(1));
            journal.Show("step");
            journal.Hear("Collect");
            Assert.That(journal["step"].Done, Is.True);
            Assert.That(journal.Fail("step") || journal.Hide("step"), Is.False);
        }

        [Test]
        public void Run_FailureAndExplicitEndingsWorkWithoutCompletion()
        {
            QuestDef quest = Quest("Failure");
            var failed = new QuestEnding();
            Set(failed, "signal", QuestRun.StepFailedSignal);
            Set(failed, "outcome", "failed");
            Set(quest, "endings", new[] { failed });
            using var run = new QuestRun(quest);
            run.Begin();
            run.Steps.Fail("step");
            Assert.That(run.IsFinished, Is.True);
            Assert.That(QuestLog.Value(quest, "failed"), Is.EqualTo(1));
            Assert.That(QuestLog.Passed(quest), Is.False);
        }

        [Test]
        public void QuestLog_RecoversBackupAndPreservesItWhenSavingAfterCorruption()
        {
            QuestDef quest = Quest("Saved");
            QuestLog.Record(quest, quest.Endings[0]);
            QuestLog.Record(quest, quest.Endings[0]);
            File.WriteAllText(QuestLog.FilePath, "broken JSON");
            using (QuestLog.UseDirectory(directory))
            {
                Assert.That(QuestLog.Value(quest, "done"), Is.EqualTo(1));
                QuestLog.Record(quest, quest.Endings[0]);
                Assert.That(QuestLog.Value(quest, "done"), Is.EqualTo(2));
            }
            Assert.That(File.ReadAllText(QuestLog.FilePath + ".bak"), Does.Contain("done"));
            using (QuestLog.UseDirectory(directory)) Assert.That(QuestLog.Value(quest, "done"), Is.EqualTo(2));
        }

        [Test]
        public void QuestLog_UpgradesVersionOneFlagsAndCountsAndPersistsVersionTwo()
        {
            QuestDef done = Quest("Done"), skipped = Quest("Skipped"), failed = Quest("Failed");
            Directory.CreateDirectory(directory);
            File.WriteAllText(QuestLog.FilePath, "{\"version\":1,\"quests\":["
                + "{\"id\":\"Done\",\"done\":true,\"completions\":3,\"failures\":2},"
                + "{\"id\":\"Skipped\",\"skipped\":true},"
                + "{\"id\":\"Failed\",\"failures\":4}]}");
            Assert.That(QuestLog.Value(done, "done"), Is.EqualTo(3));
            Assert.That(QuestLog.Value(done, "failed"), Is.EqualTo(2));
            Assert.That(QuestLog.Passed(done), Is.True);
            Assert.That(QuestLog.Value(skipped, "skipped"), Is.EqualTo(1));
            Assert.That(QuestLog.Passed(skipped), Is.True);
            Assert.That(QuestLog.Value(failed, "failed"), Is.EqualTo(4));
            Assert.That(QuestLog.Passed(failed), Is.False);
            QuestLog.Record(done, done.Endings[0]);
            Assert.That(JsonUtility.FromJson<QuestLogData>(File.ReadAllText(QuestLog.FilePath)).version, Is.EqualTo(2));
            using (QuestLog.UseDirectory(directory))
            {
                Assert.That(QuestLog.Value(done, "done"), Is.EqualTo(4));
                Assert.That(QuestLog.Value(skipped, "skipped"), Is.EqualTo(1));
                Assert.That(QuestLog.Value(failed, "failed"), Is.EqualTo(4));
            }
        }

        [TestCase("{}")]
        [TestCase("{\"quests\":[]}")]
        [TestCase("{\"version\":1}")]
        [TestCase("{\"version\":2}")]
        [TestCase("{\"version\":99,\"quests\":[]}")]
        public void QuestLog_UsesBackupWhenPrimarySchemaIsIncompleteOrUnknown(string primary)
        {
            QuestDef quest = Quest("Saved");
            QuestLog.Record(quest, quest.Endings[0]);
            QuestLog.Record(quest, quest.Endings[0]);
            File.WriteAllText(QuestLog.FilePath, primary);
            using (QuestLog.UseDirectory(directory))
                Assert.That(QuestLog.Value(quest, "done"), Is.EqualTo(1));
        }

        [Test]
        public void QuestLog_RecordsRelatedQuestsAndResetsRequirementChain()
        {
            QuestDef first = Quest("First"), second = Quest("Second"), unrelated = Quest("Unrelated");
            Set(second, "requires", new[] { first });
            Set(first.Endings[0], "alsoRecord", new[] { second });
            Assert.That(QuestLog.Locked(second), Is.True);
            QuestLog.Record(first, first.Endings[0]);
            QuestLog.Record(unrelated, unrelated.Endings[0]);
            Assert.That(QuestLog.Passed(second), Is.True);
            QuestLog.Reset(first, new[] { first, second, unrelated });
            Assert.That(QuestLog.Passed(first) || QuestLog.Passed(second), Is.False);
            Assert.That(QuestLog.Locked(second), Is.True);
            Assert.That(QuestLog.Passed(unrelated), Is.True);
        }

        [Test]
        public void Database_ValidatesNamesReferencesAndRequirementCycles()
        {
            QuestDef first = Quest("First"), second = Quest("Second");
            DefDatabase database = Asset<DefDatabase>("Database");
            Set(database, "definitions", new Def[] { first, second });
            database.Validate();
            Assert.That(database.Get<QuestDef>("First"), Is.SameAs(first));
            Assert.Throws<KeyNotFoundException>(() => database.Get<QuestDef>("Missing"));
            second.name = "First";
            Assert.Throws<InvalidOperationException>(database.Validate);
            second.name = "Second";
            Set(first, "requires", new[] { second });
            Set(database, "definitions", new Def[] { first });
            Assert.Throws<InvalidOperationException>(database.Validate);
            Set(database, "definitions", new Def[] { first, second });
            Set(second, "requires", new[] { first });
            Assert.Throws<InvalidOperationException>(database.Validate);
        }

        [Test]
        public void QuestDefinition_RejectsDuplicateStepsAndReservedOutcomes()
        {
            QuestDef quest = Quest("Invalid");
            Set(quest.Endings[0], "outcome", QuestLog.PassedValue);
            Assert.Throws<InvalidOperationException>(quest.Validate);
            Set(quest.Endings[0], "outcome", "done");
            Set(quest, "objectives", new[] { new QuestData { id = "same" }, new QuestData { id = "same" } });
            Assert.Throws<InvalidOperationException>(quest.Validate);
        }

        [Test]
        public void ObjectivePrerequisitesBlockEarlySignalsAndUnlockInOrder()
        {
            QuestDef quest = Quest("Ordered");
            Set(quest, "objectives", new[]
            {
                new QuestData { id = "first", showOn = QuestRun.BeginSignal, completeOn = "First" },
                new QuestData { id = "second", requiresObjectives = new[] { "first" }, showOn = "first.Done", completeOn = "Second" },
            });
            using var run = new QuestRun(quest);
            run.Begin();
            run.Raise("Second");
            Assert.That(run.Steps.Show("second"), Is.False);
            Assert.That(run.Steps["second"].Progress, Is.Zero);
            run.Raise("First");
            Assert.That(run.Steps["second"].Active, Is.True);
            run.Raise("Second");
            Assert.That(run.IsFinished, Is.True);
        }

        [Test]
        public void ObjectivePrerequisitesRejectCyclesAndUnknownIds()
        {
            QuestDef quest = Quest("InvalidOrder");
            var first = new QuestData { id = "first", requiresObjectives = new[] { "second" } };
            var second = new QuestData { id = "second", requiresObjectives = new[] { "first" } };
            Set(quest, "objectives", new[] { first, second });
            Assert.Throws<InvalidOperationException>(quest.Validate);
            second.requiresObjectives = new[] { "missing" };
            Assert.Throws<InvalidOperationException>(quest.Validate);
        }

        [Test]
        public void ActionGuardsReportFirstFailureWithoutAdvancingOrWritingOutcomes()
        {
            QuestDef quest = Quest("Guarded");
            var guard = new QuestGuard { action = "Proceed", condition = "Ready", hint = "Finish preparation first." };
            Set(quest, "guards", new[] { guard });
            using var run = new QuestRun(quest);
            int blocked = 0;
            run.ActionBlocked += reason => { Assert.That(reason, Is.SameAs(guard)); blocked++; };
            run.Begin();
            Assert.That(run.CheckAction("Proceed", null), Is.SameAs(guard));
            Assert.That(run.TryAction("Proceed", _ => false), Is.False);
            Assert.That(run.TryAction("Proceed", condition => condition == "Ready"), Is.True);
            Assert.That(blocked, Is.EqualTo(1));
            Assert.That(run.Steps["step"].Progress, Is.Zero);
            Assert.That(File.Exists(QuestLog.FilePath), Is.False);
            run.Dispose();
            Assert.That(run.TryAction("Proceed", _ => true), Is.False);
        }

        [Test]
        public void PreviewFinishesWithoutSavingOrNotifyingQuestLog()
        {
            QuestDef quest = Quest("Preview");
            using var run = QuestRun.CreatePreview(quest);
            run.Begin();
            SignalBus.Fire(new QuestSignal("Item.Collected", run, 2));
            Assert.That(run.IsFinished, Is.True);
            Assert.That(QuestLog.Passed(quest), Is.False);
            Assert.That(File.Exists(QuestLog.FilePath), Is.False);
        }
    }
}
