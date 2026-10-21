using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Localization.PropertyVariants.TrackedObjects;
using UnityEngine.Localization.Settings;

namespace Baseline
{
    /// <summary>
    /// Puts an inspector-assigned <see cref="LocalizedString"/> on a ui_text through its
    /// LocalizeStringEvent, so the text follows locale changes by itself. Components use it for
    /// choices made from a template (option groups, tabs); a label of its own is keyed in its prefab.
    /// </summary>
    public static class UILocalizedText
    {
        public static TextMeshProUGUI Create(Transform parent, string name, TMP_FontAsset englishFont = null)
        {
            var prefab = SharedUIContent.Instance.textPrefab;
#if UNITY_EDITOR
            var text = !Application.isPlaying
                ? ((GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(prefab.gameObject, parent)).GetComponent<TextMeshProUGUI>()
                : Object.Instantiate(prefab, parent, false);
#else
            var text = Object.Instantiate(prefab, parent, false);
#endif
            text.name = name;
            if (englishFont != null) text.PreserveEnglishFont(englishFont);
            return text;
        }

        public static void PreserveEnglishFont(this TMP_Text text, TMP_FontAsset font, Material material = null)
        {
            if (font == null) return;
            material ??= font.material;
            var localizer = text.GetComponent<GameObjectLocalizer>();
            var tracked = localizer.GetTrackedObject<TrackedUGuiGraphic>(text);
            tracked.GetTrackedProperty<UnityObjectProperty>("m_fontAsset").SetValue(new LocaleIdentifier("en"), font);
            tracked.GetTrackedProperty<UnityObjectProperty>("m_sharedMaterial").SetValue(new LocaleIdentifier("en"), material);
            if (LocalizationSettings.SelectedLocale?.Identifier.Code.StartsWith("zh") != true)
            {
                text.font = font;
                text.fontSharedMaterial = material;
            }
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(localizer);
            if (UnityEditor.PrefabUtility.IsPartOfPrefabInstance(localizer)) UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(localizer);
#endif
        }

        public static void Localize(this TMP_Text text, LocalizedString value)
        {
            LocalizeStringEvent localize = text.GetComponent<LocalizeStringEvent>();
            if (localize == null)
            {
                localize = text.gameObject.AddComponent<LocalizeStringEvent>();
                localize.OnUpdateString.AddListener(shown => text.text = shown);
            }
            localize.StringReference = value;
            localize.enabled = true;
        }

        /// <summary>Text that no string entry drives (a name, a number): any localizer on it lets go.</summary>
        public static void ShowPlain(this TMP_Text text, string value)
        {
            LocalizeStringEvent localize = text.GetComponent<LocalizeStringEvent>();
            if (localize != null) localize.enabled = false;
            text.text = value ?? "";
        }
    }
}
