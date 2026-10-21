using System.Collections;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Baseline
{
    // Main scene manager
    public class IndexManager : MonoBehaviour
    {
        [Header("Index Menu")]
        [SerializeField]
        LayoutGroup indexGrid;

        [SerializeField]
        UIIndexButton gridButtonPrefab;

        [SerializeField, Tooltip("Shown instead of the grid while no game is registered.")]
        GameObject emptyState;

        [Header("Index Menu - Panels")]
        [SerializeField]
        Button settingsBtn;

        [SerializeField]
        Button helpBtn;

        [SerializeField]
        UIConfigPanel configPanel;

        [SerializeField]
        UIHelpPanel helpPanel;

        IEnumerator Start()
        {
            BindPanels();

            yield return LocalizationSettings.InitializationOperation;
            if (indexGrid != null)
                DrawGameGrid();

            yield return new WaitForEndOfFrame();
            UpdateLayoutGroup();
        }

        void DrawGameGrid()
        {
            int i = 0;
            var games = ContextManager.Ins.gameRegister.games;
            // The first game is the index itself.
            if (emptyState != null)
                emptyState.SetActive(games.Count <= 1);
            foreach (GameMeta game in games)
            {
                if (i++ == 0)
                    continue;
                var indexButton = Instantiate(gridButtonPrefab, indexGrid.transform);
                indexButton.gameIcon.sprite = game.icon;
                indexButton.onButtonClick = () => ContextManager.Ins.JumpToScene(game);
                indexButton.locale.StringReference = new LocalizedString(
                    GameRegister.DEF_STT,
                    game.name
                );
                indexButton.locale.RefreshString();
                // indexButton.gameName.text = LocalizationSettings.StringDatabase.GetLocalizedString(game.name);
            }
        }

        void BindPanels()
        {
            configPanel ??= FindFirstObjectByType<UIConfigPanel>(FindObjectsInactive.Include);
            helpPanel ??= FindFirstObjectByType<UIHelpPanel>(FindObjectsInactive.Include);
            settingsBtn?.onClick.AddListener(() => configPanel?.SetVisible(true));
            helpBtn?.onClick.AddListener(() => helpPanel?.SetVisible(true));
        }

        void UpdateLayoutGroup()
        {
            indexGrid.enabled = false;
            indexGrid.CalculateLayoutInputVertical();
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)indexGrid.transform);
            indexGrid.enabled = true;
        }
    }
}
