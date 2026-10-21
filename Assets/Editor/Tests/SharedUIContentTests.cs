using System.Collections;
using System.Linq;
using Baseline.Dialogue;
using Baseline.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedObjects;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed partial class SharedUIContentTests
    {
        [Test]
        public void CatalogAndPrefabsHaveCompleteSerializedBindings()
        {
            SharedUIContent content = SharedUIContent.Instance;
            Assert.That(content, Is.Not.Null);
            Assert.That(content.contextPrefab, Is.Not.Null);
            Assert.That(content.textSettings, Is.Not.Null);
            content.Activate();
            Assert.That(TMP_Settings.instance, Is.SameAs(content.textSettings));
            Assert.That(TMP_Settings.defaultFontAsset, Is.Not.Null);
            Assert.That(TMP_Settings.defaultSpriteAsset, Is.Not.Null);
            Assert.That(TMP_Settings.defaultStyleSheet, Is.Not.Null);
            Assert.That(content.textPrefab, Is.Not.Null);
            Assert.That(content.dialoguePrefab, Is.Not.Null);
            Assert.That(content.characterDialogPrefab, Is.Not.Null);
            Assert.That(content.characterAlertsPrefab, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(content.characterAlertsPrefab.View),
                Is.SameAs(content.characterDialogPrefab), "The alert canvas must retain the authored nested view.");
            foreach (GameObject prefab in new[] { content.textPrefab.gameObject,
                content.dialoguePrefab.gameObject, content.characterDialogPrefab.gameObject, content.characterAlertsPrefab.gameObject })
                foreach (Transform node in prefab.GetComponentsInChildren<Transform>(true))
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, node.name);

            var panel = new SerializedObject(content.dialoguePrefab);
            foreach (string field in new[] { "surface", "safe", "card", "character", "secondCharacter", "accent", "illustration",
                "title", "speaker", "role", "body", "progress", "nextLabel", "next", "close" })
                Assert.That(panel.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            UICharacterDialogView view = content.characterDialogPrefab;
            foreach (var field in typeof(UICharacterDialogView).GetFields()
                .Where(field => typeof(Object).IsAssignableFrom(field.FieldType)))
                Assert.That(field.GetValue(view), Is.Not.Null, field.Name);
            Assert.That(view.actions.Length, Is.GreaterThanOrEqualTo(2));
            Assert.That(view.copy.rectTransform.anchorMin, Is.EqualTo(new Vector2(0, 1)));
            Assert.That(view.copy.rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(view.copy.rectTransform.sizeDelta.x, Is.Zero, "The scroll content must fill the viewport width.");
            foreach (DialogAlertLevel level in System.Enum.GetValues(typeof(DialogAlertLevel)))
            {
                Assert.That(view.style.For(level), Is.Not.Null);
                view.style.For(level).character.Validate();
            }
        }

        [Test]
        public void SharedGuideUsesItsRobotIllustrationAndCroppedPortrait()
        {
            var guide = AssetDatabase.LoadAssetAtPath<CharacterDef>("Assets/Bundles/Core/Config/Dialogue/GuideCharacter.asset");
            guide.Validate();
            Assert.That(guide.illustration.name, Is.EqualTo("RobotGuide-illustration"));
            Assert.That(guide.portrait.name, Is.EqualTo("RobotGuide-portrait"));
            Assert.That(guide.portrait.texture, Is.SameAs(guide.illustration.texture));
            Assert.That(guide.portrait.rect.height, Is.LessThan(guide.illustration.rect.height));
        }

        [Test]
        public void SceneReferenceUsesItsGuidWhileLegacyEntriesKeepTheirAddress()
        {
            var game = new GameMeta { sceneName = "LegacyScene" };
            Assert.That(game.RuntimeKey, Is.EqualTo("LegacyScene"));
            game.sceneAsset = new AssetReference(AssetDatabase.AssetPathToGUID("Assets/Scenes/Index.unity"));
            Assert.That(game.RuntimeKey, Is.EqualTo(game.sceneAsset.AssetGUID));
        }

        [UnityTest]
        public IEnumerator AuthoredTextKeepsLocalizedFontsAndLiteralTextAcrossLocaleChanges()
        {
            yield return LocalizationSettings.InitializationOperation;
            var previous = LocalizationSettings.SelectedLocale;
            TextMeshProUGUI text = UILocalizedText.Create(null, "Authored text test");
            try
            {
                text.ShowPlain("Player chosen name");
                var localizer = text.GetComponent<GameObjectLocalizer>();
                var tracked = localizer.GetTrackedObject<TrackedUGuiGraphic>(text);
                foreach (string code in new[] { "en", "zh" })
                {
                    var locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    LocalizationSettings.SelectedLocale = locale;
                    var operation = localizer.ApplyLocaleVariant(locale);
                    yield return operation;
                    var property = tracked.GetTrackedProperty<UnityObjectProperty>("m_fontAsset");
                    Assert.That(property.GetValue(locale.Identifier, out Object font), Is.True);
                    Assert.That(text.font, Is.SameAs(font));
                    Assert.That(text.text, Is.EqualTo("Player chosen name"));
                    Assert.That(text.GetComponent<LocalizeStringEvent>().enabled, Is.False);
                }
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
                Object.DestroyImmediate(text.gameObject);
            }
        }
    }
}
