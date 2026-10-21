using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>One choice in an option group or tool dock: a caption, an optional icon and a selected look.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIChoice : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Image icon;
        [SerializeField] Graphic mark;
        [SerializeField] bool iconAboveLabel;
        [SerializeField] Color normalTint, selectedTint;
        [SerializeField] Color normalInk, selectedInk;
        [SerializeField] Color normalIconTint, selectedIconTint;
        [SerializeField] float labelInset, labelInsetWithIcon;

        Image background;
        bool selected;

        public Button Button => GetComponent<Button>();
        public TMP_Text Label => label;
        public Image Icon => icon;
        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                background ??= GetComponent<Image>();
                if (background != null) background.color = value ? selectedTint : normalTint;
                if (mark != null) mark.enabled = value;
                if (label != null) label.color = value ? selectedInk : normalInk;
                if (icon != null) icon.color = value ? selectedIconTint : normalIconTint;
            }
        }

        /// <summary>A caption no string entry drives, such as a side's or an army's name.</summary>
        public void Show(string text, Sprite sprite)
        {
            if (label != null) label.ShowPlain(text);
            Arrange(!string.IsNullOrEmpty(text), sprite);
        }

        /// <summary>A caption authored in the inspector; it follows the locale by itself.</summary>
        public void Show(LocalizedString text, Sprite sprite)
        {
            bool shown = text != null && !text.IsEmpty;
            if (label != null && shown) label.Localize(text);
            Arrange(shown, sprite);
        }

        void Arrange(bool hasText, Sprite sprite)
        {
            if (label != null)
            {
                label.gameObject.SetActive(hasText);
                if (!iconAboveLabel)
                {
                    Vector2 padding = label.rectTransform.offsetMin;
                    padding.x = sprite != null ? labelInsetWithIcon : labelInset;
                    label.rectTransform.offsetMin = padding;
                }
            }
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.gameObject.SetActive(sprite != null);
            }
            Selected = selected;
        }
    }
}
