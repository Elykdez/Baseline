using System.Collections;
using System.Collections.Generic;
using Baseline.Presentation;
using Hypocycloid.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>
    /// Application settings overlay used by the index scene, one page per tab. The layout is intentionally
    /// presentation-only; values persist through the versioned JSON configuration file.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIConfigPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button closeButton;

        [Header("Tabs")]
        [SerializeField] UITabs tabs;
        [SerializeField, Tooltip("One page per tab caption, in order.")]
        GameObject[] tabPages;

        [Header("Reference Artwork (optional)")]
        [SerializeField, Tooltip("Shows the sprite at the selected tab's index.")]
        Image referenceArtwork;
        [SerializeField] Sprite[] tabArtwork;
        [SerializeField] TMP_Text tabNumberLabel;
        [SerializeField] TMP_Text sectionNameLabel;

        [Header("Controls")]
        [SerializeField] TMP_Dropdown languageDropdown;
        [SerializeField] TMP_Dropdown windowModeDropdown;
        [SerializeField] TMP_Dropdown difficultyDropdown;
        [SerializeField] Slider masterSlider;
        [SerializeField] Slider musicSlider;
        [SerializeField] Slider sfxSlider;
        [SerializeField] Slider interfaceSlider;
        [SerializeField] Slider screenShakeSlider;
        [SerializeField] Toggle boardEffectsToggle;

        [Header("Labels")]
        [SerializeField] TMP_Text windowModeLabel;

        LocaleSelector localeSelector;
        Coroutine initializeRoutine;
        bool listenersBound;
        int currentTab;

        public int CurrentTab => currentTab;

        public bool Visible => panelRoot != null && panelRoot.activeInHierarchy;

        void Awake()
        {
            panelRoot ??= gameObject;
            closeButton?.onClick.AddListener(Close);
            tabs.SetTabs(tabPages, currentTab);
            tabs.Changed += ShowTab;
#if !UNITY_STANDALONE && !UNITY_EDITOR
            SetWindowModeControlsVisible(false);
#endif
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            ShowTab(currentTab);
            initializeRoutine = StartCoroutine(Initialize());
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            if (initializeRoutine != null)
            {
                StopCoroutine(initializeRoutine);
                initializeRoutine = null;
            }
        }

        void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
            if (tabs != null) tabs.Changed -= ShowTab;
            RemoveControlListeners();
        }

        public void ToggleVisible() => SetVisible(!Visible);

        public void Close() => SetVisible(false);

        /// <summary>Opens a page; the panel reopens on the last page shown.</summary>
        public void ShowTab(int index)
        {
            if (index < 0 || index >= tabPages.Length) return;
            foreach (TMP_Dropdown dropdown in GetComponentsInChildren<TMP_Dropdown>())
                dropdown.Hide();
            currentTab = index;
            tabs.Select(index);
            RefreshSection();
            // A control on the page just hidden would otherwise keep keyboard focus.
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject != null && !events.currentSelectedGameObject.activeInHierarchy)
                events.SetSelectedGameObject(tabs[index].gameObject);
        }

        public void SetVisible(bool visible)
        {
            panelRoot ??= gameObject;
            if (panelRoot.activeSelf != visible)
                panelRoot.SetActive(visible);
        }

        void RefreshSection()
        {
            if (referenceArtwork != null && currentTab < tabArtwork.Length)
                referenceArtwork.sprite = tabArtwork[currentTab];
            if (tabNumberLabel != null)
                tabNumberLabel.ShowPlain($"{currentTab + 1:00}");
            if (sectionNameLabel != null)
                sectionNameLabel.Localize(tabs.Caption(currentTab));
        }

        IEnumerator Initialize()
        {
            yield return LocalizationSettings.InitializationOperation;

            localeSelector = FindFirstObjectByType<LocaleSelector>();
            PopulateLanguageOptions();
            PopulateDifficultyOptions();
#if UNITY_STANDALONE || UNITY_EDITOR
            SetWindowModeControlsVisible(true);
            PopulateWindowModeOptions();
#else
            SetWindowModeControlsVisible(false);
#endif
            RefreshValues();
            BindControlListeners();
            initializeRoutine = null;
        }

        void PopulateLanguageOptions()
        {
            if (languageDropdown == null)
                return;

            var locales = LocalizationSettings.AvailableLocales.Locales;
            var options = new List<TMP_Dropdown.OptionData>(locales.Count);
            int selected = 0;
            for (int i = 0; i < locales.Count; i++)
            {
                Locale locale = locales[i];
                string displayName = locale.Identifier.CultureInfo?.NativeName ?? locale.name;
                options.Add(new TMP_Dropdown.OptionData(displayName));
                if (locale == LocalizationSettings.SelectedLocale)
                    selected = i;
            }

            languageDropdown.ClearOptions();
            languageDropdown.AddOptions(options);
            languageDropdown.SetValueWithoutNotify(selected);
            languageDropdown.RefreshShownValue();
        }

        void PopulateDifficultyOptions()
        {
            if (difficultyDropdown == null)
                return;

            difficultyDropdown.ClearOptions();
            difficultyDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
            {
                new(Localized("Global_difficulty_0", "Easy")),
                new(Localized("Global_difficulty_1", "Normal")),
                new(Localized("Global_difficulty_2", "Hard")),
            });
            difficultyDropdown.RefreshShownValue();
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        void PopulateWindowModeOptions()
        {
            if (windowModeDropdown == null)
                return;

            windowModeDropdown.ClearOptions();
            windowModeDropdown.AddOptions(new List<TMP_Dropdown.OptionData>
            {
                new(Localized("Global_window_0", "Windowed")),
                new(Localized("Global_window_1", "Borderless")),
                new(Localized("Global_window_2", "Fullscreen")),
            });
            windowModeDropdown.RefreshShownValue();
        }
#endif

        void SetWindowModeControlsVisible(bool visible)
        {
            if (windowModeLabel != null)
                windowModeLabel.gameObject.SetActive(visible);
            if (windowModeDropdown != null)
                windowModeDropdown.gameObject.SetActive(visible);
        }

        void RefreshValues()
        {
            masterSlider?.SetValueWithoutNotify(ConfigSettingsPersistence.MasterVolume);
            musicSlider?.SetValueWithoutNotify(ConfigSettingsPersistence.MusicVolume);
            sfxSlider?.SetValueWithoutNotify(ConfigSettingsPersistence.SfxVolume);
            interfaceSlider?.SetValueWithoutNotify(ConfigSettingsPersistence.UiVolume);
            screenShakeSlider?.SetValueWithoutNotify(ConfigSettingsPersistence.ScreenShake);
            boardEffectsToggle?.SetIsOnWithoutNotify(ConfigSettingsPersistence.BoardEffects);
            if (ContextManager.Ins == null)
                return;

            difficultyDropdown?.SetValueWithoutNotify((int)ContextManager.Ins.GetDifficulty());
#if UNITY_STANDALONE || UNITY_EDITOR
            windowModeDropdown?.SetValueWithoutNotify((int)ContextManager.Ins.GetWindowMode());
            windowModeDropdown?.RefreshShownValue();
#endif
        }

        void BindControlListeners()
        {
            if (listenersBound)
                return;

            languageDropdown?.onValueChanged.AddListener(SetLanguage);
            difficultyDropdown?.onValueChanged.AddListener(SetDifficulty);
#if UNITY_STANDALONE || UNITY_EDITOR
            windowModeDropdown?.onValueChanged.AddListener(SetWindowMode);
#endif
            masterSlider?.onValueChanged.AddListener(SetMasterVolume);
            musicSlider?.onValueChanged.AddListener(SetMusicVolume);
            sfxSlider?.onValueChanged.AddListener(SetSfxVolume);
            interfaceSlider?.onValueChanged.AddListener(SetInterfaceVolume);
            screenShakeSlider?.onValueChanged.AddListener(SetScreenShake);
            boardEffectsToggle?.onValueChanged.AddListener(SetBoardEffects);
            listenersBound = true;
        }

        void RemoveControlListeners()
        {
            if (!listenersBound)
                return;

            languageDropdown?.onValueChanged.RemoveListener(SetLanguage);
            difficultyDropdown?.onValueChanged.RemoveListener(SetDifficulty);
#if UNITY_STANDALONE || UNITY_EDITOR
            windowModeDropdown?.onValueChanged.RemoveListener(SetWindowMode);
#endif
            masterSlider?.onValueChanged.RemoveListener(SetMasterVolume);
            musicSlider?.onValueChanged.RemoveListener(SetMusicVolume);
            sfxSlider?.onValueChanged.RemoveListener(SetSfxVolume);
            interfaceSlider?.onValueChanged.RemoveListener(SetInterfaceVolume);
            screenShakeSlider?.onValueChanged.RemoveListener(SetScreenShake);
            boardEffectsToggle?.onValueChanged.RemoveListener(SetBoardEffects);
            listenersBound = false;
        }

        void SetLanguage(int localeIndex)
        {
            localeSelector ??= FindFirstObjectByType<LocaleSelector>();
            localeSelector?.ChangeLocale(localeIndex);
        }

        static void SetDifficulty(int value)
        {
            if (ContextManager.Ins != null)
                ContextManager.Ins.SetDifficulty((GameDifficulty)value);
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        static void SetWindowMode(int value)
        {
            if (ContextManager.Ins != null)
                ContextManager.Ins.SetWindowMode((WindowPresentationMode)value);
        }
#endif

        static void SetMasterVolume(float value) => SetAudioLevel(AudioBus.Master, value);
        static void SetMusicVolume(float value) => SetAudioLevel(AudioBus.Music, value);
        static void SetSfxVolume(float value) => SetAudioLevel(AudioBus.Effects, value);
        static void SetInterfaceVolume(float value) => SetAudioLevel(AudioBus.Interface, value);

        // The managers apply, save and announce the settings; make sure one is listening.
        static void SetAudioLevel(AudioBus bus, float value)
        {
            _ = SoundManager.Ins;
            SignalBus.Fire(new AudioLevelRequestedSignal { Bus = bus, Level = value });
        }

        static void SetScreenShake(float value)
        {
            _ = FxManager.Ins;
            SignalBus.Fire(new ScreenShakeRequestedSignal { Strength = value });
        }

        static void SetBoardEffects(bool value)
        {
            _ = FxManager.Ins;
            SignalBus.Fire(new BoardEffectsRequestedSignal { Enabled = value });
        }

        void OnLocaleChanged(Locale locale)
        {
            RefreshSection();
            PopulateLanguageOptions();
            PopulateDifficultyOptions();
#if UNITY_STANDALONE || UNITY_EDITOR
            PopulateWindowModeOptions();
#endif
            RefreshValues();
        }

        static string Localized(string key, string fallback)
        {
            string value = LocalizationSettings.StringDatabase.GetLocalizedString(
                GameRegister.DEF_STT, key);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
    }
}
