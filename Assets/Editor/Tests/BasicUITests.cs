using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedObjects;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Localization.Settings;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseline.Tests.Editor
{
    public sealed class BasicUITests
    {
        GameObject root;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Basic UI tests", typeof(RectTransform), typeof(Canvas));
            ((RectTransform)root.transform).sizeDelta = new Vector2(1280, 720);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        T Make<T>(string name, Transform parent = null) where T : Component
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent != null ? parent : root.transform, false);
            return host.AddComponent<T>();
        }

        static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        [UnityTest]
        public IEnumerator LocalizedTextTracksLocaleChangesAndCanReturnToPlainText()
        {
            yield return LocalizationSettings.InitializationOperation;
            Locale previous = LocalizationSettings.SelectedLocale;
            TMP_Text label = Make<TextMeshProUGUI>("Localized label");
            var caption = new LocalizedString(GameRegister.DEF_STT, "Generics_start");
            try
            {
                label.Localize(caption);
                foreach (string code in new[] { "en", "zh" })
                {
                    Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
                    Assert.That(locale, Is.Not.Null);
                    LocalizationSettings.SelectedLocale = locale;
                    var expected = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                        GameRegister.DEF_STT, "Generics_start", locale);
                    yield return expected;
                    float deadline = Time.realtimeSinceStartup + 5;
                    while (label.text != expected.Result && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(label.text, Is.EqualTo(expected.Result));
                }

                label.ShowPlain("Custom name");
                Assert.That(label.GetComponent<LocalizeStringEvent>().enabled, Is.False);
                LocalizationSettings.SelectedLocale = previous;
                yield return null;
                Assert.That(label.text, Is.EqualTo("Custom name"));
                label.ShowPlain(null);
                Assert.That(label.text, Is.Empty);
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
            }
        }

        [Test]
        public void LocalizingAgainReusesOneLocalizerAndPlainTextPreservesItsReference()
        {
            TMP_Text label = Make<TextMeshProUGUI>("Label");
            label.gameObject.SetActive(false);
            var caption = new LocalizedString(GameRegister.DEF_STT, "Generics_start");
            label.Localize(caption);
            label.Localize(caption);
            LocalizeStringEvent localize = label.GetComponent<LocalizeStringEvent>();
            Assert.That(label.GetComponents<LocalizeStringEvent>(), Has.Length.EqualTo(1));
            localize.OnUpdateString.Invoke("Translated caption");
            Assert.That(label.text, Is.EqualTo("Translated caption"));
            label.ShowPlain("Runtime caption");
            Assert.That(localize.enabled, Is.False);
            Assert.That(localize.StringReference, Is.SameAs(caption));
        }

        [UnityTest]
        public IEnumerator EnglishFontOverrideRetainsTheChineseFontVariant()
        {
            yield return LocalizationSettings.InitializationOperation;
            Locale previous = LocalizationSettings.SelectedLocale;
            TMP_Text label = Make<TextMeshProUGUI>("Font variants");
            label.gameObject.SetActive(false);
            var localizer = label.gameObject.AddComponent<GameObjectLocalizer>();
            var tracked = localizer.GetTrackedObject<TrackedUGuiGraphic>(label);
            var fontProperty = tracked.GetTrackedProperty<UnityObjectProperty>("m_fontAsset");
            TMP_FontAsset english = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Bundles/Core/Font/ClearSans-Regular SDF.asset");
            TMP_FontAsset chinese = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/Bundles/Core/Font/SmileySans-Oblique SDF.asset");
            Assert.That(english, Is.Not.Null);
            Assert.That(chinese, Is.Not.Null);
            fontProperty.SetValue(new LocaleIdentifier("zh"), chinese);
            try
            {
                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("zh");
                label.font = chinese;
                label.PreserveEnglishFont(english);
                Assert.That(label.font, Is.SameAs(chinese));
                Assert.That(fontProperty.GetValue(new LocaleIdentifier("en"), out Object englishVariant), Is.True);
                Assert.That(englishVariant, Is.SameAs(english));
                Assert.That(fontProperty.GetValue(new LocaleIdentifier("zh"), out Object chineseVariant), Is.True);
                Assert.That(chineseVariant, Is.SameAs(chinese));

                LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("en");
                label.PreserveEnglishFont(english);
                Assert.That(label.font, Is.SameAs(english));
                Assert.That(label.fontSharedMaterial, Is.SameAs(english.material));
            }
            finally
            {
                LocalizationSettings.SelectedLocale = previous;
            }
        }

        [Test]
        public void SliderValueOverridesAnyAuthoredCaptionLocalizer()
        {
            Slider slider = Make<Slider>("Slider");
            TMP_Text label = Make<TextMeshProUGUI>("Value", slider.transform);
            label.gameObject.SetActive(false);
            var localizer = label.gameObject.AddComponent<LocalizeStringEvent>();
            slider.SetValueWithoutNotify(.25f);
            var valueLabel = label.gameObject.AddComponent<UISliderValueLabel>();
            label.gameObject.SetActive(true);
            // Runtime-only behaviours do not receive OnEnable in EditMode.
            typeof(UISliderValueLabel).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(valueLabel, null);

            Assert.That(label.text, Is.EqualTo("25%"));
            Assert.That(localizer.enabled, Is.False);
            slider.value = .75f;
            Assert.That(label.text, Is.EqualTo("75%"));
        }

        [Test]
        public void ChoicesReuseRowsAndNotifyOnlyChangedUserSelections()
        {
            UIOptionGroup group = Make<UIOptionGroup>("Options");
            var grid = group.gameObject.AddComponent<GridLayoutGroup>();
            group.gameObject.AddComponent<UIResponsiveGridLayout>();
            UIChoice template = Make<UIChoice>("Choice", group.transform);
            TMP_Text label = Make<TextMeshProUGUI>("Caption", template.transform);
            Set(template, "label", label);
            Set(group, "template", template);
            Set(group, "container", (RectTransform)group.transform);
            int changes = 0;
            group.Changed += _ => changes++;
            group.SetOptions(new[] { "One", "Two", "Three" }, 0);
            UIChoice first = group[0];
            group[1].Button.onClick.Invoke();
            group.Pick(1);
            Assert.That(group.Selected, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(1));
            group.SetOptions(new[] { "New one", "New two" }, 0);
            Assert.That(group[0], Is.SameAs(first));
            Assert.That(group[2].gameObject.activeSelf, Is.False);
            Assert.That(grid.constraintCount, Is.EqualTo(2));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void StepperBoundsAndHeldPressSuppressDuplicateReleaseClick()
        {
            UIStepper stepper = Make<UIStepper>("Stepper");
            Set(stepper, "label", Make<TextMeshProUGUI>("Label", stepper.transform));
            Set(stepper, "value", Make<TextMeshProUGUI>("Value", stepper.transform));
            Button minus = Make<Button>("Minus", stepper.transform);
            Button plus = Make<Button>("Plus", stepper.transform);
            UIRepeatPress repeat = plus.gameObject.AddComponent<UIRepeatPress>();
            Set(stepper, "minus", minus);
            Set(stepper, "plus", plus);
            stepper.SetRange(0, 2);
            stepper.SetValue(0);
            Assert.That(minus.interactable, Is.False);
            repeat.OnPointerDown(new PointerEventData(null));
            Set(repeat, "next", -1f);
            typeof(UIRepeatPress).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(repeat, null);
            repeat.OnPointerUp(new PointerEventData(null));
            plus.onClick.Invoke();
            Assert.That(stepper.Value, Is.EqualTo(1));
            plus.onClick.Invoke();
            stepper.Step(1);
            Assert.That(stepper.Value, Is.EqualTo(2));
            Assert.That(plus.interactable, Is.False);
        }

        [Test]
        public void SheetChangesDockAndReportsItsCoveredExtent()
        {
            UISheet sheet = Make<UISheet>("Sheet");
            sheet.Dock(SheetEdge.Bottom, 72, 88);
            sheet.SetState(SheetState.Full, false);
            Assert.That(sheet.Extent, Is.EqualTo(632).Within(.01f));
            sheet.Dock(SheetEdge.Right, 0, 88);
            sheet.SetState(SheetState.Half, false);
            Assert.That(sheet.State, Is.EqualTo(SheetState.Full));
            Assert.That(sheet.Extent, Is.EqualTo(409.6f).Within(.01f));
            sheet.SetState(SheetState.Closed, false);
            Assert.That(sheet.Extent, Is.Zero);
        }

        [Test]
        public void StackedTextHandlesNullAndLineSeparatesCaption()
        {
            TMP_Text label = Make<TextMeshProUGUI>("Stacked caption");
            UIStackedText stacked = label.gameObject.AddComponent<UIStackedText>();
            stacked.SetText("ABC");
            Assert.That(label.text, Is.EqualTo("A\nB\nC"));
            stacked.SetText(null);
            Assert.That(label.text, Is.Empty);
        }
    }
}
