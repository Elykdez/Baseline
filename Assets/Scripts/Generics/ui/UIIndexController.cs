using System.Collections;
using System.Collections.Generic;
using Hypocycloid.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Baseline
{
    public class UIIndexController : MonoBehaviour
    {
        [Header("Controller - Lang")]
        public LocaleSelector localeSelector; // using context locale if not specifically assigned.

        [SerializeField]
        TMP_Dropdown langSelect;

        [Header("Controller - Modifier")]
        [SerializeField]
        TMP_Dropdown difficultySelect;

        [Header("Controller - Sound")]
        [SerializeField]
        Slider musicSlider;

        [SerializeField]
        Slider sfxSlider;

        IEnumerator Start()
        {
            if (difficultySelect != null)
            {
                ContextManager.Ins.SetDifficulty(ContextManager.Ins.GetDifficulty());
                difficultySelect.SetValueWithoutNotify((int)ContextManager.Ins.GetDifficulty());
                // foreach (int i in Enum.GetValues(typeof(GameDifficulty)))
                difficultySelect.onValueChanged.AddListener(
                    (value) =>
                    {
                        ContextManager.Ins.SetDifficulty((GameDifficulty)value);
                        LogHelper.Log("Index", $"Set difficulty = {value}");
                    }
                );
            }

            if (localeSelector == null)
                localeSelector = FindFirstObjectByType<LocaleSelector>();

            if (langSelect != null)
            {
                int selected = localeSelector.GetLocale();

                // Wait for the localization system to initialize, loading Locales, preloading etc.
                yield return LocalizationSettings.InitializationOperation;
                while (localeSelector.IsChanging)
                    yield return null;

                // Generate list of available Locales
                langSelect.ClearOptions();
                var options = new List<TMP_Dropdown.OptionData>();

                for (int i = 0; i < LocalizationSettings.AvailableLocales.Locales.Count; ++i)
                {
                    Locale locale = LocalizationSettings.AvailableLocales.Locales[i];
                    if (LocalizationSettings.SelectedLocale == locale)
                        selected = i;
                    options.Add(
                        new TMP_Dropdown.OptionData(locale.Identifier.CultureInfo.NativeName)
                    );
                }

                langSelect.AddOptions(options);
                langSelect.onValueChanged.AddListener(
                    (value) =>
                    {
                        localeSelector.ChangeLocale(value);
                        LogHelper.Log(
                            "Index",
                            $"Set lang = {LocalizationSettings.AvailableLocales.Locales[value].name}"
                        );
                    }
                );
                langSelect.SetValueWithoutNotify(selected);
            }

            if (musicSlider != null)
            {
                musicSlider.value = ConfigSettingsPersistence.MusicVolume;
            }

            if (sfxSlider != null)
            {
                sfxSlider.value = ConfigSettingsPersistence.SfxVolume;
            }
        }

        public void MusicVolume()
        {
            AudioManager.Ins.SetMusicVolume(musicSlider.value);
        }

        public void SfxVolume()
        {
            AudioManager.Ins.SetSfxVolume(sfxSlider.value);
        }
    }
}
