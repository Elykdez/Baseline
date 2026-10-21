using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Baseline.Dialogue;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class DialoguePortraitTests
    {
        [UnityTest]
        public IEnumerator AlternatingSpeakersKeepTheirSlotsExpressionsAndAuthoredTints()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            fixture.Show(
                fixture.Line(fixture.First, "reaction"), fixture.Line(fixture.Second, "reaction"),
                fixture.Line(fixture.First), fixture.Line(fixture.Third));
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.First.expressions[0].illustration));
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);

            fixture.Advance();
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.First.expressions[0].illustration));
            Assert.That(fixture.Right.sprite, Is.SameAs(fixture.Second.expressions[0].illustration));
            Assert.That(fixture.Left.color, Is.EqualTo(fixture.ListeningTint));
            Assert.That(fixture.Right.color, Is.EqualTo(fixture.SpeakingTint));
            fixture.Refresh();
            Assert.That(fixture.Right.sprite, Is.SameAs(fixture.Second.expressions[0].illustration),
                "Refreshing the current line must not reassign either speaker.");

            fixture.Advance();
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.First.illustration));
            Assert.That(fixture.Right.sprite, Is.SameAs(fixture.Second.expressions[0].illustration),
                "The listener keeps its last expression while the returning speaker uses this line's default.");
            Assert.That(fixture.Left.color, Is.EqualTo(fixture.SpeakingTint));
            Assert.That(fixture.Right.color, Is.EqualTo(fixture.ListeningTint));

            fixture.Advance();
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.First.illustration));
            Assert.That(fixture.Right.sprite, Is.SameAs(fixture.Third.illustration),
                "A third speaker replaces the listener opposite the previous speaker.");
        }

        [UnityTest]
        public IEnumerator CompactAndNarrowViewsShowTheSpeakerThenRestoreThePair()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            fixture.Show(fixture.Line(fixture.First, "reaction"), fixture.Line(fixture.Second, "reaction"));
            fixture.Advance();

            fixture.Resize(720, 1280);
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.Second.expressions[0].portrait));
            Assert.That(fixture.Left.color, Is.EqualTo(fixture.SpeakingTint));
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);

            fixture.Resize(960, 540);
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.Second.expressions[0].illustration));
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);

            fixture.Resize(1920, 1080);
            Assert.That(fixture.Left.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Right.gameObject.activeSelf, Is.True);
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.First.expressions[0].illustration));
            Assert.That(fixture.Right.sprite, Is.SameAs(fixture.Second.expressions[0].illustration));
            Assert.That(fixture.Left.color, Is.EqualTo(fixture.ListeningTint));
            Assert.That(fixture.Right.color, Is.EqualTo(fixture.SpeakingTint));
        }

        [UnityTest]
        public IEnumerator OptingOutKeepsOnlyTheCurrentSpeaker()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            fixture.Dialog.dualCharacters = false;
            fixture.Second.facing = PortraitFacing.Mirrored;
            fixture.Show(fixture.Line(fixture.First), fixture.Line(fixture.Second, "reaction"));
            fixture.Advance();
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.Second.expressions[0].illustration));
            Assert.That(fixture.Left.color, Is.EqualTo(fixture.SpeakingTint));
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);
            Assert.That(fixture.Left.Mirrored, Is.True);
            fixture.Resize(720, 1280);
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.Second.expressions[0].portrait));
            Assert.That(fixture.Left.Mirrored, Is.True);
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator FacingDefaultsAndLineOverridesStayWithEachCharacter()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            fixture.First.facing = PortraitFacing.Mirrored;
            fixture.Show(fixture.Line(fixture.First),
                fixture.Line(fixture.Second, facing: PortraitFacingOverride.Mirrored),
                fixture.Line(fixture.First, facing: PortraitFacingOverride.Original),
                fixture.Line(fixture.Second));
            Assert.That(fixture.Left.Mirrored, Is.True);
            fixture.Advance();
            Assert.That(fixture.Left.Mirrored, Is.True, "The listener retains its facing.");
            Assert.That(fixture.Right.Mirrored, Is.True);
            fixture.Resize(720, 1280);
            Assert.That(fixture.Left.Mirrored, Is.True, "The active speaker keeps its override when moving to the compact view.");
            fixture.Resize(1920, 1080);
            fixture.Refresh();
            Assert.That(fixture.Left.Mirrored, Is.True);
            Assert.That(fixture.Right.Mirrored, Is.True);
            fixture.Advance();
            Assert.That(fixture.Left.Mirrored, Is.False, "An Original line overrides a Mirrored character default.");
            Assert.That(fixture.Right.Mirrored, Is.True);
            fixture.Advance();
            Assert.That(fixture.Right.Mirrored, Is.False, "Character Default restores the second character's original facing.");
            fixture.Show(fixture.Line(fixture.First));
            Assert.That(fixture.Left.Mirrored, Is.True, "Replaying must not retain the previous Original override.");
        }

        [UnityTest]
        public IEnumerator MirroringPreservesMeshBoundsAndAuthoredTransforms()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            fixture.Left.rectTransform.pivot = new Vector2(.15f, .3f);
            fixture.Left.transform.localScale = new Vector3(1.25f, .75f, 1);
            fixture.Right.transform.localScale = new Vector3(.9f, 1.1f, 1);
            fixture.Show(fixture.Line(fixture.First), fixture.Line(fixture.Second));
            fixture.Advance();
            foreach (UIPortrait image in new[] { fixture.Left, fixture.Right })
            {
                var rect = image.rectTransform;
                Vector2 position = rect.anchoredPosition, size = rect.sizeDelta, pivot = rect.pivot;
                Vector3 scale = rect.localScale;
                UIVertex[] original = Vertices(image);
                Assert.That(original, Is.Not.Empty);
                float sum = original.Min(vertex => vertex.position.x) + original.Max(vertex => vertex.position.x);
                image.Mirrored = true;
                UIVertex[] mirrored = Vertices(image);
                Assert.That(mirrored, Has.Length.EqualTo(original.Length));
                for (int i = 0; i < original.Length; i++)
                {
                    Assert.That(mirrored[i].position.x, Is.EqualTo(sum - original[i].position.x).Within(.001f));
                    Assert.That(mirrored[i].position.y, Is.EqualTo(original[i].position.y));
                    Assert.That(mirrored[i].uv0, Is.EqualTo(original[i].uv0));
                }
                Assert.That(rect.anchoredPosition, Is.EqualTo(position));
                Assert.That(rect.sizeDelta, Is.EqualTo(size));
                Assert.That(rect.pivot, Is.EqualTo(pivot));
                Assert.That(rect.localScale, Is.EqualTo(scale));
            }
        }

        [UnityTest]
        public IEnumerator AlertsUseTheCharactersDefaultFacing()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            var view = Object.Instantiate(SharedUIContent.Instance.characterDialogPrefab);
            var style = Object.Instantiate(view.style);
            try
            {
                view.style = style;
                style.info.character = fixture.First;
                var rect = (RectTransform)view.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.sizeDelta = new Vector2(1920, 1080);
                fixture.First.facing = PortraitFacing.Mirrored;
                view.Show("Heading", "Body", "", ("Okay", () => { }));
                Assert.That(view.portrait.Mirrored, Is.True);
                rect.sizeDelta = new Vector2(720, 1280);
                view.Show("Heading", "Body", "", ("Okay", () => { }));
                Assert.That(view.portrait.Mirrored, Is.True);
                fixture.First.facing = PortraitFacing.Original;
                view.Show("Heading", "Body", "", ("Okay", () => { }));
                Assert.That(view.portrait.Mirrored, Is.False);
            }
            finally { Object.DestroyImmediate(view.gameObject); Object.DestroyImmediate(style); }
        }

        static UIVertex[] Vertices(UIPortrait image)
        {
            using var mesh = new VertexHelper();
            typeof(UIPortrait).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Invoke(image, new object[] { mesh });
            var vertices = new UIVertex[mesh.currentVertCount];
            for (int i = 0; i < vertices.Length; i++) mesh.PopulateUIVertex(ref vertices[i], i);
            return vertices;
        }

        [UnityTest]
        public IEnumerator ReplayingCancellingAndDisablingDoNotRetainPreviousOccupants()
        {
            yield return LocalizationSettings.InitializationOperation;
            using var fixture = new Fixture();
            fixture.Show(fixture.Line(fixture.First), fixture.Line(fixture.Second));
            fixture.Advance();
            fixture.Show(fixture.Line(fixture.Second), fixture.Line(fixture.Third));
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.Second.illustration),
                "Replaying the same definition starts the former right speaker on the left.");
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);

            fixture.Advance();
            fixture.Panel.Runner.Cancel();
            fixture.Refresh();
            fixture.Show(fixture.Line(fixture.Third), fixture.Line(fixture.First));
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.Third.illustration));
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);

            fixture.Advance();
            fixture.Invoke("OnDisable");
            Assert.That(fixture.Panel.IsOpen, Is.False);
            fixture.Refresh();
            fixture.Show(fixture.Line(fixture.First), fixture.Line(fixture.Second));
            Assert.That(fixture.Left.sprite, Is.SameAs(fixture.First.illustration));
            Assert.That(fixture.Right.gameObject.activeSelf, Is.False);
        }

        sealed class Fixture : IDisposable
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            readonly List<Object> content = new();
            readonly RectTransform safe;
            readonly Action refresh;
            public readonly DialoguePanel Panel;
            public readonly DialogDef Dialog;
            public readonly CharacterDef First, Second, Third;
            public readonly UIPortrait Left, Right;
            public readonly Color SpeakingTint = new(.82f, .91f, .96f, 1);
            public readonly Color ListeningTint = new(.26f, .38f, .47f, .8f);

            public Fixture()
            {
                First = MakeCharacter("First");
                Second = MakeCharacter("Second");
                Third = MakeCharacter("Third");
                Dialog = ScriptableObject.CreateInstance<DialogDef>();
                Dialog.dualCharacters = true;
                content.Add(Dialog);
                Panel = DialoguePanel.Create();
                var serialized = new SerializedObject(Panel);
                Left = (UIPortrait)serialized.FindProperty("character").objectReferenceValue;
                Right = (UIPortrait)serialized.FindProperty("secondCharacter").objectReferenceValue;
                safe = (RectTransform)serialized.FindProperty("safe").objectReferenceValue;
                Assert.That(Right, Is.Not.Null, "The shared conversation prefab must author both portrait views.");
                serialized.FindProperty("activeCharacterTint").colorValue = SpeakingTint;
                serialized.FindProperty("inactiveCharacterTint").colorValue = ListeningTint;
                serialized.FindProperty("pairMinimumWidth").floatValue = 1100;
                serialized.FindProperty("pairMinimumAspect").floatValue = 1.2f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                // EditMode does not guarantee the view's OnEnable subscription.
                refresh = Refresh;
                Panel.Runner.Changed += refresh;
                Resize(1920, 1080, false);
            }

            public DialogLine Line(CharacterDef character, string expression = "default",
                PortraitFacingOverride facing = PortraitFacingOverride.CharacterDefault)
                => new() { speaker = character, expression = expression, facing = facing, text = character.displayName + " speaks." };

            public void Show(params DialogLine[] lines)
            {
                Dialog.lines = lines;
                Panel.Show(Dialog, playVoice: false);
                Refresh();
            }

            public void Advance()
            {
                Assert.That(Panel.Runner.Advance(Panel.Runner.Revision), Is.True);
                Refresh();
            }

            public void Resize(float width, float height, bool arrange = true)
            {
                safe.anchorMin = safe.anchorMax = Vector2.one * .5f;
                safe.sizeDelta = new Vector2(width, height);
                if (arrange) Invoke("Layout");
            }

            public void Refresh() => Invoke("Refresh");
            public void Invoke(string method) => typeof(DialoguePanel).GetMethod(method, Private).Invoke(Panel, null);

            CharacterDef MakeCharacter(string name)
            {
                var character = ScriptableObject.CreateInstance<CharacterDef>();
                content.Add(character);
                character.displayName = name;
                character.role = name + " role";
                character.illustration = MakeSprite(name + " full");
                character.portrait = MakeSprite(name + " compact");
                character.expressions = new[] { new CharacterExpression
                {
                    id = "reaction", illustration = MakeSprite(name + " reaction full"),
                    portrait = MakeSprite(name + " reaction compact"),
                } };
                return character;
            }

            Sprite MakeSprite(string name)
            {
                Sprite sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
                sprite.name = name;
                content.Add(sprite);
                return sprite;
            }

            public void Dispose()
            {
                Panel.Runner.Changed -= refresh;
                Object.DestroyImmediate(Panel.gameObject);
                foreach (Object item in content) Object.DestroyImmediate(item);
            }
        }
    }
}
