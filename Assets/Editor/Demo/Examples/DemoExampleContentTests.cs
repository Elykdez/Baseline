using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Baseline.Defs;
using Baseline.Dialogue;
using Baseline.Editor;
using Baseline.Quests;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Baseline.Tests.Editor
{
    public sealed partial class BaselineCoreTests
    {
        [Test]
        public void AuthoredExample_LoadsValidatesAndCompletes()
        {
            var database = AssetDatabase.LoadAssetAtPath<DefDatabase>("Assets/Bundles/Demo/Examples/DemoExampleDatabase.asset");
            Assert.That(database, Is.Not.Null);
            database.Validate();
            QuestDef quest = database.Get<QuestDef>("example.quest");
            using var run = new QuestRun(quest);
            run.Begin();
            run.Raise("Example.Action");
            Assert.That(run.IsFinished, Is.True);
            Assert.That(QuestLog.Passed(quest), Is.True);
        }
    }

    public sealed partial class SharedUIContentTests
    {
        [Test]
        public void SampleDatabaseRegistersDialogueSpeakersAndAllTranslations()
        {
            var database = AssetDatabase.LoadAssetAtPath<DefDatabase>("Assets/Bundles/Demo/Examples/DemoExampleDatabase.asset");
            database.Validate();
            var dialog = AssetDatabase.LoadAssetAtPath<DialogDef>("Assets/Bundles/Demo/Examples/DemoExampleDialog.asset");
            Assert.That(database.Definitions, Does.Contain(dialog));
            foreach (DialogLine line in dialog.lines) Assert.That(database.Definitions, Does.Contain(line.speaker));
            foreach (string code in new[] { "en", "zh" })
            {
                var table = AssetDatabase.LoadAssetAtPath<StringTable>($"Assets/Locales/Tables/StringTable_{code}.asset");
                foreach (string key in new[] { dialog.titleKey, dialog.lines[0].textKey, dialog.lines[1].textKey,
                    dialog.lines[0].speaker.roleKey, "Dialog_Next", "Dialog_Close", "Dialog_Learn", "Dialog_Skip",
                    "Dialog_Continue", "Dialog_Alert_Cancel", "Dialog_Alert_Confirm", "Dialog_Alert_Okay" })
                    Assert.That(table.GetEntry(key)?.LocalizedValue, Is.Not.Null.And.Not.Empty, code + "/" + key);
            }
        }
    }

    public sealed partial class QuestAuditTests
    {
        [Test]
        public void ScanDiscoversQuestsAndDialogueWithoutNeedingADatabaseSelection()
        {
            var quest = AssetDatabase.LoadAssetAtPath<QuestDef>("Assets/Bundles/Demo/Examples/DemoExampleQuest.asset");
            var dialog = AssetDatabase.LoadAssetAtPath<DialogDef>("Assets/Bundles/Demo/Examples/DemoExampleDialog.asset");
            QuestAuditReport report = QuestAudit.Scan();
            Assert.That(report.Quests, Does.Contain(quest));
            Assert.That(report.Dialogues, Does.Contain(dialog));
            Assert.That(report.ErrorCount, Is.Zero, string.Join("\n", report.Issues.Select(issue => issue.Path + ": " + issue.Location + ": " + issue.Message)));
        }
    }

    public sealed partial class BaselineIntegrationTests
    {
        [UnityTest]
        public IEnumerator SharedDialogueAndAlertsWorkFromTheirAuthoredPrefabsInPlayMode()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Index.unity");
            yield return new EnterPlayMode();
            yield return WaitForIndex();
            yield return LocalizationSettings.InitializationOperation;
            var previousLocale = LocalizationSettings.SelectedLocale;
            DialoguePanel panel = DialoguePanel.Create();
            var dialogHandle = Addressables.LoadAssetAsync<DialogDef>(AssetDatabase.AssetPathToGUID(
                "Assets/Bundles/Demo/Examples/DemoExampleDialog.asset"));
            yield return dialogHandle;
            var closed = new List<bool>();
            panel.Runner.Closed += closed.Add;
            try
            {
                Assert.That(dialogHandle.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                DialogDef dialog = dialogHandle.Result;
                panel.Show(dialog, allowClose: false, playVoice: false);
                var serialized = new SerializedObject(panel);
                var body = (TextMeshProUGUI)serialized.FindProperty("body").objectReferenceValue;
                var next = (UnityEngine.UI.Button)serialized.FindProperty("next").objectReferenceValue;
                foreach (string code in new[] { "en", "zh" })
                {
                    var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    LocalizationSettings.SelectedLocale = locale;
                    var expected = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                        GameRegister.DEF_STT, dialog.lines[0].textKey, locale);
                    yield return expected;
                    string expectedText = expected.Result;
                    string fontPath = code == "en" ? "ClearSans-Regular SDF.asset" : "SmileySans-Oblique SDF.asset";
                    var font = Addressables.LoadAssetAsync<TMP_FontAsset>(
                        AssetDatabase.AssetPathToGUID("Assets/Bundles/Core/Font/" + fontPath));
                    yield return font;
                    try
                    {
                        Assert.That(font.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                        double deadline = EditorApplication.timeSinceStartup + 5;
                        while ((body.text != expectedText || body.font != font.Result)
                            && EditorApplication.timeSinceStartup < deadline)
                            yield return null;
                        Assert.That(body.text, Is.EqualTo(expectedText));
                        Assert.That(body.font, Is.SameAs(font.Result));
                    }
                    finally { if (font.IsValid()) Addressables.Release(font); }
                }
                panel.Close();
                Assert.That(panel.IsOpen, Is.True);
                yield return null;
                body.maxVisibleCharacters = int.MaxValue;
                next.onClick.Invoke();
                next.onClick.Invoke();
                Assert.That(panel.Runner.LineIndex, Is.EqualTo(1), "A repeated click cannot skip the next line.");
                yield return null;
                body.maxVisibleCharacters = int.MaxValue;
                next.onClick.Invoke();
                Assert.That(closed, Is.EqualTo(new[] { true }));
                Assert.That(panel.BlocksInput, Is.True);

                var responses = new List<bool>();
                var heading = new LocalizedString(GameRegister.DEF_STT, dialog.titleKey);
                var message = new LocalizedString(GameRegister.DEF_STT, dialog.lines[0].textKey);
                UICharacterDialog.Confirm(heading, message, responses.Add);
                var alerts = Object.FindFirstObjectByType<UICharacterDialog>();
                Assert.That(alerts.IsOpen, Is.True);
                yield return null;
                alerts.View.actions[0].onClick.Invoke();
                Assert.That(responses, Is.EqualTo(new[] { false }));
                yield return null;
                UICharacterDialog.Confirm(heading, message, responses.Add);
                UICharacterDialog.Confirm(heading, message, responses.Add);
                Scene original = SceneManager.GetActiveScene();
                Scene temporary = SceneManager.CreateScene("SharedUI cancellation test");
                SceneManager.SetActiveScene(temporary);
                Assert.That(responses, Is.EqualTo(new[] { false, false, false }));
                Assert.That(alerts.IsOpen, Is.False);
                SceneManager.SetActiveScene(original);
                yield return SceneManager.UnloadSceneAsync(temporary);
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previousLocale;
                Object.DestroyImmediate(panel.gameObject);
                var alerts = Object.FindFirstObjectByType<UICharacterDialog>();
                if (alerts != null) Object.DestroyImmediate(alerts.gameObject);
                if (dialogHandle.IsValid()) Addressables.Release(dialogHandle);
            }
            yield return new ExitPlayMode();
        }
    }
}
