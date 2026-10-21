using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Hypocycloid.Utils;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>
    /// Localized, tabbed manuals: one tab per registered game, then the extra documents such as credits.
    /// Every tab reads into the same scrollable page.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIHelpPanel : MonoBehaviour
    {
        const string DescriptionPath = "Desc/{0}_{1}.md";

        /// <summary>A document under StreamingAssets/Desc, named <c>{document}_{locale}.md</c>.</summary>
        [Serializable]
        public struct Manual
        {
            public string document;
            public LocalizedString title;
        }

        [Header("Panel")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Button closeButton;
        [SerializeField] TMP_Text contentLabel;
        [SerializeField] TMP_Text versionLabel;
        [SerializeField] ScrollRect scrollRect;

        [Header("Tabs")]
        [SerializeField] UITabs tabs;
        [SerializeField, Tooltip("Shown after the registered games' manuals, such as credits.")]
        Manual[] extraManuals = Array.Empty<Manual>();

        [Header("Reference Artwork (optional)")]
        [SerializeField, Tooltip("Shows the sprite at the selected tab's index.")]
        Image referenceArtwork;
        [SerializeField] Sprite[] tabArtwork;
        [SerializeField] TMP_Text tabNumberLabel;
        [SerializeField] TMP_Text sectionNameLabel;

        [Header("Manual Formatting")]
        [SerializeField] int titleSizePercent, sectionSizePercent, titleBlankLines, sectionBlankLines;
        [SerializeField] string bulletSymbol;
        [SerializeField] Color bulletColor;
        [SerializeField] int bulletSpacing, listItemBlankLines;

        readonly List<Manual> manuals = new();
        Coroutine initializeRoutine;
        Coroutine loadRoutine;
        int currentTab;

        public int CurrentTab => currentTab;

        public bool Visible => panelRoot != null && panelRoot.activeInHierarchy;

        void Awake()
        {
            panelRoot ??= gameObject;
            closeButton?.onClick.AddListener(Close);
            tabs.Changed += ShowTab;
            if (versionLabel != null)
                versionLabel.ShowPlain($"v{Application.version}");
        }

        void OnEnable()
        {
            LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
            initializeRoutine = StartCoroutine(Initialize());
        }

        void OnDisable()
        {
            LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
            StopRoutines();
        }

        void OnDestroy()
        {
            closeButton?.onClick.RemoveListener(Close);
            if (tabs != null) tabs.Changed -= ShowTab;
        }

        public void ToggleVisible() => SetVisible(!Visible);

        public void Close() => SetVisible(false);

        public void SetVisible(bool visible)
        {
            panelRoot ??= gameObject;
            if (panelRoot.activeSelf != visible)
                panelRoot.SetActive(visible);
        }

        IEnumerator Initialize()
        {
            yield return LocalizationSettings.InitializationOperation;
            CollectManuals();
            ShowTab(currentTab);
            initializeRoutine = null;
        }

        // Games can be registered after the panel was made, so the tabs are listed on every opening.
        void CollectManuals()
        {
            manuals.Clear();
            var games = ContextManager.Ins?.gameRegister?.games;
            // The first game is the index itself.
            for (int i = 1; games != null && i < games.Count; i++)
                manuals.Add(new Manual
                {
                    document = games[i].sceneName,
                    title = new LocalizedString(GameRegister.DEF_STT, games[i].name),
                });
            manuals.AddRange(extraManuals);

            var captions = new LocalizedString[manuals.Count];
            var pages = new GameObject[manuals.Count];
            for (int i = 0; i < manuals.Count; i++)
            {
                captions[i] = manuals[i].title;
                pages[i] = scrollRect.gameObject;
            }
            currentTab = Mathf.Clamp(currentTab, 0, Mathf.Max(0, manuals.Count - 1));
            tabs.SetTabs(captions, pages, currentTab);
            tabs.gameObject.SetActive(manuals.Count > 1);
        }

        /// <summary>Reads a manual; the panel reopens on the last one read.</summary>
        public void ShowTab(int index)
        {
            if (!isActiveAndEnabled || !LocalizationSettings.InitializationOperation.IsDone) return;
            if (loadRoutine != null)
                StopCoroutine(loadRoutine);
            scrollRect.StopMovement();
            if (index < 0 || index >= manuals.Count)
            {
                contentLabel?.Localize(new LocalizedString(GameRegister.DEF_STT, "Help_Unavailable"));
                return;
            }

            currentTab = index;
            tabs.Select(index);
            if (referenceArtwork != null && index < tabArtwork.Length)
                referenceArtwork.sprite = tabArtwork[index];
            if (tabNumberLabel != null)
                tabNumberLabel.ShowPlain($"{index + 1:00}");
            if (sectionNameLabel != null)
                sectionNameLabel.Localize(manuals[index].title);
            loadRoutine = StartCoroutine(LoadDescription(manuals[index].document));
        }

        IEnumerator LoadDescription(string document)
        {
            if (contentLabel != null)
                contentLabel.Localize(new LocalizedString(GameRegister.DEF_STT, "Help_Loading"));

            string localeCode = LocalizationSettings.SelectedLocale?.Identifier.Code ?? "en";
            string content = null;
            yield return ConfigReader.GetConfigFile(
                string.Format(DescriptionPath, document, localeCode), value => content = value);

            if (string.IsNullOrWhiteSpace(content) && !localeCode.Equals("en", StringComparison.OrdinalIgnoreCase))
                yield return ConfigReader.GetConfigFile(
                    string.Format(DescriptionPath, document, "en"), value => content = value);

            if (contentLabel != null)
            {
                if (string.IsNullOrWhiteSpace(content))
                    contentLabel.Localize(new LocalizedString(GameRegister.DEF_STT, "Help_Unavailable"));
                else
                    contentLabel.ShowPlain(MarkdownToRichText(content));
            }

            Canvas.ForceUpdateCanvases();
            if (scrollRect != null)
                scrollRect.verticalNormalizedPosition = 1f;
            loadRoutine = null;
        }

        void OnLocaleChanged(Locale locale)
        {
            if (!isActiveAndEnabled)
                return;
            ShowTab(currentTab);
        }

        void StopRoutines()
        {
            if (initializeRoutine != null)
                StopCoroutine(initializeRoutine);
            if (loadRoutine != null)
                StopCoroutine(loadRoutine);
            initializeRoutine = null;
            loadRoutine = null;
        }

        string MarkdownToRichText(string markdown) => MarkdownText.Format(markdown,
            titleSizePercent, sectionSizePercent, titleBlankLines, sectionBlankLines,
            bulletSymbol, bulletColor, bulletSpacing, listItemBlankLines);
    }
}
