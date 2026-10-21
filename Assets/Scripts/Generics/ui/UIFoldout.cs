using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    public enum UITone { Neutral, Warning, Danger, Success }

    /// <summary>
    /// A titled section whose body folds away. The header keeps a one-line summary, so a folded
    /// section still shows its state. A viewer's own fold choice can be remembered between sessions.
    /// </summary>
    [ExecuteAlways]
    public sealed class UIFoldout : MonoBehaviour, ILayoutElement
    {
        [SerializeField] Button header;
        [SerializeField] RectTransform chevron, body;
        [SerializeField] TMP_Text title, summary, badge;
        [SerializeField] Image badgeBackground;
        [SerializeField] Color neutralTint, warningTint, dangerTint, successTint;
        [SerializeField] bool expanded = true;

        string memoryKey;
        bool bound;

        /// <summary>Raised when the body folds or unfolds, with the new state.</summary>
        public event Action<bool> Toggled;
        public RectTransform Body => body;
        public Button Header => header;
        public TMP_Text Title => title;
        public TMP_Text Summary => summary;
        // The decorative panel image reports its sprite's native height; the section follows its content instead.
        public int layoutPriority => 1;
        public float minWidth => -1;
        public float preferredWidth => -1;
        public float flexibleWidth => -1;
        public float minHeight => GetComponent<LayoutGroup>().minHeight;
        public float preferredHeight => GetComponent<LayoutGroup>().preferredHeight;
        public float flexibleHeight => 0;
        public void CalculateLayoutInputHorizontal() { }
        public void CalculateLayoutInputVertical() { }
        public bool Expanded
        {
            get => expanded;
            set => SetExpanded(value, false);
        }

        /// <summary>Remembers the viewer's fold choice under this key, and restores it now.</summary>
        public void Remember(string key)
        {
            memoryKey = key;
            if (!string.IsNullOrEmpty(key) && ConfigSettingsPersistence.HasKey(key)) SetExpanded(ConfigSettingsPersistence.GetInt(key) != 0, false);
        }

        public void SetSummary(string text)
        {
            if (summary != null) summary.ShowPlain(text);
        }

        public void SetBadge(string text, UITone tone = UITone.Neutral)
        {
            if (badge == null) return;
            bool shown = !string.IsNullOrEmpty(text);
            badge.ShowPlain(text);
            GameObject pill = badgeBackground != null ? badgeBackground.gameObject : badge.gameObject;
            pill.SetActive(shown);
            if (badgeBackground != null) badgeBackground.color = Tone(tone);
        }

        public Color Tone(UITone tone) => tone switch
        {
            UITone.Warning => warningTint,
            UITone.Danger => dangerTint,
            UITone.Success => successTint,
            _ => neutralTint,
        };

        void Awake() => Bind();
        void OnEnable() { Bind(); Apply(); }

        void Bind()
        {
            if (bound || header == null) return;
            bound = true;
            header.onClick.AddListener(() => SetExpanded(!expanded, true));
        }

        void SetExpanded(bool value, bool byViewer)
        {
            Bind();
            bool changed = value != expanded;
            expanded = value;
            Apply();
            if (byViewer && !string.IsNullOrEmpty(memoryKey)) ConfigSettingsPersistence.SetInt(memoryKey, value ? 1 : 0);
            if (changed) Toggled?.Invoke(value);
        }

        void Apply()
        {
            if (body != null) body.gameObject.SetActive(expanded);
            if (chevron != null) chevron.localEulerAngles = new Vector3(0, 0, expanded ? -90 : 0);
            RefreshLayout((RectTransform)transform);
            var parent = transform.parent as RectTransform;
            if (parent != null)
            {
                RefreshLayout(parent);
                LayoutRebuilder.MarkLayoutForRebuild(parent);
            }
        }

        static void RefreshLayout(RectTransform rect)
        {
            // Refresh cached input before the fitter/parent reads it, including during an existing canvas rebuild.
            LayoutGroup layout = rect.GetComponent<LayoutGroup>();
            if (layout != null)
            {
                layout.CalculateLayoutInputHorizontal();
                layout.CalculateLayoutInputVertical();
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        }
    }
}
