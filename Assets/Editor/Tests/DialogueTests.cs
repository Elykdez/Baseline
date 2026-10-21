using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Baseline.Dialogue;
using Baseline.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class DialogueTests
    {
        CharacterDef character;
        DialogDef dialog;
        Texture2D texture;
        Sprite portrait;

        [SetUp]
        public void SetUp()
        {
            texture = new Texture2D(2, 2);
            portrait = Sprite.Create(texture, new Rect(0, 0, 2, 2), Vector2.one * .5f);
            character = ScriptableObject.CreateInstance<CharacterDef>();
            character.displayName = "Guide";
            character.role = "Assistant";
            character.illustration = character.portrait = portrait;
            dialog = ScriptableObject.CreateInstance<DialogDef>();
            dialog.lines = new[]
            {
                new DialogLine { speaker = character, text = "Welcome." },
                new DialogLine { speaker = character, text = "Choose what you want to explore." },
            };
        }

        [TearDown]
        public void Cleanup()
        {
            Object.DestroyImmediate(dialog);
            Object.DestroyImmediate(character);
            Object.DestroyImmediate(portrait);
            Object.DestroyImmediate(texture);
        }

        [Test]
        public void OldButtonRevisionCannotSkipANewLineOrAReplacementConversation()
        {
            var runner = new DialogRunner();
            runner.Play(dialog);
            long first = runner.Revision;
            Assert.That(runner.Advance(first), Is.True);
            Assert.That(runner.Advance(first), Is.False);
            Assert.That(runner.LineIndex, Is.EqualTo(1));
            runner.Play(dialog);
            Assert.That(runner.Advance(first), Is.False);
            Assert.That(runner.LineIndex, Is.Zero);
        }

        [Test]
        public void InvitationCanBeSkippedButLaterRequiredLinesMustFinish()
        {
            var runner = new DialogRunner();
            var completed = new List<bool>();
            runner.Closed += completed.Add;
            runner.Play(dialog, allowClose: false, offerSkip: true);
            Assert.That(runner.OffersSkip, Is.True);
            runner.Close();
            Assert.That(completed, Is.EqualTo(new[] { false }));
            runner.Play(dialog, allowClose: false, offerSkip: true);
            runner.Advance(runner.Revision);
            Assert.That(runner.CanClose, Is.False);
            runner.Close();
            Assert.That(runner.IsOpen, Is.True);
            runner.Advance(runner.Revision);
            Assert.That(completed, Is.EqualTo(new[] { false, true }));
        }

        [Test]
        public void OwnerCancellationEndsRequiredDialogueOnlyOnce()
        {
            var runner = new DialogRunner();
            var completed = new List<bool>();
            runner.Closed += completed.Add;
            runner.Play(dialog, allowClose: false);
            runner.Close();
            Assert.That(runner.IsOpen, Is.True);
            runner.Cancel();
            runner.Cancel();
            Assert.That(runner.IsOpen, Is.False);
            Assert.That(completed, Is.EqualTo(new[] { false }));
        }

        [Test]
        public void InvalidReplacementDoesNotDismissTheCurrentConversation()
        {
            var runner = new DialogRunner();
            runner.Play(dialog);
            DialogLine[] lines = dialog.lines;
            var invalid = ScriptableObject.CreateInstance<DialogDef>();
            try
            {
                Assert.Throws<InvalidOperationException>(() => runner.Play(invalid));
                Assert.That(runner.Definition, Is.SameAs(dialog));
                Assert.That(runner.Current, Is.SameAs(lines[0]));
            }
            finally { Object.DestroyImmediate(invalid); }
        }

        [Test]
        public void ExpressionsFallBackAndRejectDuplicateOrIncompleteArt()
        {
            var expression = new CharacterExpression { id = "happy", illustration = portrait, portrait = portrait };
            character.expressions = new[] { expression };
            Assert.That(character.ResolvePortrait("unknown", true), Is.SameAs(portrait));
            Assert.That(character.ResolvePortrait("happy", false), Is.SameAs(portrait));
            Assert.DoesNotThrow(character.Validate);
            character.expressions = new[] { expression, expression };
            Assert.Throws<InvalidOperationException>(character.Validate);
            character.expressions = new[] { expression };
            expression.portrait = null;
            Assert.Throws<InvalidOperationException>(character.Validate);
        }

        [UnityTest]
        public IEnumerator DisablingThePanelCancelsAnUnskippableConversation()
        {
            yield return LocalizationSettings.InitializationOperation;
            DialoguePanel panel = DialoguePanel.Create();
            var completed = new List<bool>();
            panel.Runner.Closed += completed.Add;
            try
            {
                panel.Show(dialog, allowClose: false, playVoice: false);
                typeof(DialoguePanel).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(panel, null);
                Assert.That(panel.IsOpen, Is.False);
                Assert.That(completed, Is.EqualTo(new[] { false }));
                Assert.That(panel.BlocksInput, Is.True, "The closing frame must still consume input.");
            }
            finally { Object.DestroyImmediate(panel.gameObject); }
        }

        [UnityTest]
        public IEnumerator ConversationRefreshUsesAuthoredLayoutAndButtonAppearance()
        {
            yield return LocalizationSettings.InitializationOperation;
            DialoguePanel panel = DialoguePanel.Create();
            try
            {
                var serialized = new SerializedObject(panel);
                Button next = (Button)serialized.FindProperty("next").objectReferenceValue;
                TMP_Text label = next.GetComponentInChildren<TMP_Text>(true);
                Color ink = new Color(.3f, .2f, .7f);
                label.color = ink;
                label.rectTransform.offsetMin = new Vector2(37, 9);
                var colors = next.colors;
                colors.normalColor = Color.magenta;
                next.colors = colors;
                foreach (string orientation in new[] { "landscape", "portrait" })
                    serialized.FindProperty(orientation).FindPropertyRelative("nextRequired")
                        .FindPropertyRelative("anchorMin").vector2Value = new Vector2(.23f, .05f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                panel.Show(dialog, allowClose: false, playVoice: false);
                panel.Runner.Advance(panel.Runner.Revision);
                // EditMode does not guarantee the OnEnable subscription used in Play Mode.
                typeof(DialoguePanel).GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(panel, null);
                Assert.That(next.colors.normalColor, Is.EqualTo(Color.magenta));
                Assert.That(label.color, Is.EqualTo(ink));
                Assert.That(label.rectTransform.offsetMin, Is.EqualTo(new Vector2(37, 9)));
                Assert.That(((RectTransform)next.transform).anchorMin, Is.EqualTo(new Vector2(.23f, .05f)));
            }
            finally { Object.DestroyImmediate(panel.gameObject); }
        }
    }

    public sealed class CharacterDialogLifecycleTests
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
        static UICharacterDialog Alerts => Object.FindFirstObjectByType<UICharacterDialog>();
        static LocalizedString Text(string key = "Generics_start") => new(GameRegister.DEF_STT, key);

        [UnitySetUp]
        public IEnumerator Initialize()
        {
            yield return LocalizationSettings.InitializationOperation;
            AgeInput();
        }

        [TearDown]
        public void Cleanup()
        {
            if (Alerts == null) return;
            UICharacterDialog service = Alerts;
            Invoke(service, "OnDisable");
            Object.DestroyImmediate(service.gameObject);
        }

        [UnityTest]
        public IEnumerator ConfirmationIgnoresOpeningClicksAndRespondsOnlyOnce()
        {
            var responses = new List<bool>();
            UICharacterDialog.Confirm(Text(), Text(), responses.Add);
            UICharacterDialog service = Alerts;
            service.View.actions[1].onClick.Invoke();
            Assert.That(responses, Is.Empty);
            yield return null;
            AgeInput();
            service.View.actions[0].onClick.Invoke();
            service.View.actions[1].onClick.Invoke();
            Assert.That(responses, Is.EqualTo(new[] { false }));
            Assert.That(UICharacterDialog.BlocksInput, Is.True);
            AgeInput();
            Assert.That(UICharacterDialog.BlocksInput, Is.False);
        }

        [UnityTest]
        public IEnumerator AlertRefreshPreservesAuthoredAppearanceAndMeasuresTheDestructiveAction()
        {
            var view = Object.Instantiate(SharedUIContent.Instance.characterDialogPrefab);
            try
            {
                var rect = (RectTransform)view.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(1920, 1080);
                var serialized = new SerializedObject(view);
                var destructive = (Button)serialized.FindProperty("destructiveAction").objectReferenceValue;
                var dangerSize = destructive.GetComponent<LayoutElement>();
                dangerSize.minHeight = dangerSize.preferredHeight = 97;
                TMP_Text label = view.actions[0].GetComponentInChildren<TMP_Text>(true);
                label.color = Color.magenta;
                label.fontSize = 31;
                label.rectTransform.offsetMin = new Vector2(37, 9);
                var colors = view.actions[0].colors;
                colors.normalColor = Color.blue;
                view.actions[0].colors = colors;
                int accepted = 0;
                view.ShowCharacter(DialogAlertLevel.Danger, "Heading", "Body", "", null,
                    ("Cancel", () => { }), ("Accept", () => accepted++));
                view.ShowDestructiveAction(1);
                var group = view.actions[0].GetComponentInParent<VerticalLayoutGroup>(true);
                float expected = view.actions[0].GetComponent<LayoutElement>().preferredHeight + group.spacing + dangerSize.preferredHeight;
                Assert.That(((RectTransform)group.transform).rect.height, Is.EqualTo(expected).Within(.01f));
                Assert.That(view.actions[1].gameObject.activeSelf, Is.False);
                Assert.That(destructive.gameObject.activeSelf, Is.True);
                yield return null;
                typeof(UICharacterDialogView).GetField("openedFrame", Private).SetValue(view, Time.frameCount - 1);
                destructive.onClick.Invoke();
                Assert.That(accepted, Is.EqualTo(1));
                view.Show("Again", "Body", "", ("Okay", () => { }));
                Assert.That(destructive.gameObject.activeSelf, Is.False);
                Assert.That(view.actions[0].colors.normalColor, Is.EqualTo(Color.blue));
                Assert.That(label.color, Is.EqualTo(Color.magenta));
                Assert.That(label.fontSize, Is.EqualTo(31));
                Assert.That(label.rectTransform.offsetMin, Is.EqualTo(new Vector2(37, 9)));
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        [UnityTest]
        public IEnumerator ConfirmationStillRespondsAfterTheAudioManagerIsDestroyed()
        {
            AudioManager previous = AudioManager.Ins;
            var host = new GameObject("Expired audio manager");
            host.SetActive(false);
            var manager = host.AddComponent<AudioManager>();
            var clip = AudioClip.Create("Expired click", 128, 1, 44100, false);
            manager.sfxSource = host.AddComponent<AudioSource>();
            manager.sfxs = new[] { new Sound { name = "ui_confirm", clip = clip } };
            var singleton = typeof(AudioManager).GetProperty(nameof(AudioManager.Ins));
            singleton.SetValue(null, manager);
            Object.DestroyImmediate(host);
            try
            {
                var responses = new List<bool>();
                UICharacterDialog.Confirm(Text(), Text(), responses.Add);
                yield return null;
                AgeInput();
                Alerts.View.actions[0].onClick.Invoke();
                Assert.That(responses, Is.EqualTo(new[] { false }));
            }
            finally
            {
                singleton.SetValue(null, previous);
                Object.DestroyImmediate(clip);
            }
        }

        [UnityTest]
        public IEnumerator CompletionCanEnqueueWithoutReplacingTheOlderQueuedRequest()
        {
            var responses = new List<bool>();
            UICharacterDialog.Confirm(Text(), Text(), accepted =>
            {
                responses.Add(accepted);
                UICharacterDialog.Alert(Text(), Text());
            });
            UICharacterDialog.Confirm(Text("Generics_exit"), Text(), responses.Add);
            UICharacterDialog service = Alerts;
            service.Respond(true);
            Assert.That(service.IsOpen, Is.False);
            Assert.That(UICharacterDialog.BlocksInput, Is.True);
            yield return null;
            AgeInput();
            Assert.That(service.View.title.text, Is.EqualTo(Text("Generics_exit").GetLocalizedString()));
            service.Respond(false);
            AgeInput();
            Assert.That(service.View.title.text, Is.EqualTo(Text().GetLocalizedString()));
            service.Respond(true);
            Assert.That(responses, Is.EqualTo(new[] { true, false }));
        }

        [UnityTest]
        public IEnumerator SceneChangeCancelsTheQueueAndRejectsReentrantRequests()
        {
            var responses = new List<bool>();
            UICharacterDialog.Confirm(Text(), Text(), accepted =>
            {
                responses.Add(accepted);
                UICharacterDialog.Confirm(Text(), Text(), responses.Add);
            });
            UICharacterDialog.Confirm(Text(), Text(), responses.Add);
            Invoke(Alerts, "SceneChanged", default(Scene), default(Scene));
            Assert.That(responses, Is.EqualTo(new[] { false, false, false }));
            Assert.That(Alerts.IsOpen, Is.False);
            yield return null;
            AgeInput();
            Assert.That(UICharacterDialog.BlocksInput, Is.False);
        }

        static void Invoke(object target, string name, params object[] args)
            => target.GetType().GetMethod(name, Private).Invoke(target, args);

        static void AgeInput()
        {
            // EditMode yields need not advance frameCount or run MonoBehaviour.Update.
            typeof(UICharacterDialog).GetField("closedFrame", BindingFlags.NonPublic | BindingFlags.Static)
                .SetValue(null, Time.frameCount - 1);
            if (Alerts == null) return;
            Invoke(Alerts, "Update");
            typeof(UICharacterDialogView).GetField("openedFrame", Private)
                .SetValue(Alerts.View, Time.frameCount - 1);
        }
    }
}
