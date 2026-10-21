using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.PropertyVariants.TrackedObjects;
using UnityEngine.Localization.PropertyVariants.TrackedProperties;
using UnityEngine.Localization.Settings;

namespace Baseline
{
    [RequireComponent(typeof(TMP_Dropdown))]
    public class UIDropdownLocalizer : MonoBehaviour
    {
        public string keyName;

        TMP_Dropdown dropdown;

        IEnumerator Start()
        {
            dropdown = GetComponent<TMP_Dropdown>();
            if (!gameObject.TryGetComponent(out GameObjectLocalizer localizer))
            {
                localizer = gameObject.AddComponent<GameObjectLocalizer>();
            }

            yield return LocalizationSettings.InitializationOperation;

            // Gets the Tracked text or creates a new tracker
            var trackedDropdown = localizer.GetTrackedObject<TrackedTmpDropdown>(dropdown);
            // Setup each option
            for (int i = 0; i < dropdown.options.Count; ++i)
            {
                var optionText = trackedDropdown.GetTrackedProperty<LocalizedStringProperty>(
                    $"m_Options.m_Options.Array.data[{i}].m_Text"
                );
                optionText.LocalizedString.SetReference(GameRegister.DEF_STT, $"{keyName}_{i}");
            }

            // Force an Update
            localizer.ApplyLocaleVariant(LocalizationSettings.SelectedLocale);
        }
    }
}
