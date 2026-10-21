using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Baseline
{
    /// <summary>A reusable panel tab bar: selected choices, page visibility and a responsive header row.</summary>
    [DisallowMultipleComponent]
    public sealed class UITabs : MonoBehaviour
    {
        [SerializeField] UIOptionGroup options;

        [SerializeField, Tooltip("One caption per tab, in page order.")]
        LocalizedString[] labels = Array.Empty<LocalizedString>();
        [SerializeField] float minimumInlineWidth, minimumTabWidth, tabSpacing;
        [SerializeField] Vector2 inlineOffsetMin, inlineOffsetMax, stackedOffsetMin, stackedOffsetMax;
        [SerializeField] float inlineBodyInset, stackedBodyInset;

        IReadOnlyList<GameObject> pages = Array.Empty<GameObject>();
        IReadOnlyList<LocalizedString> captions = Array.Empty<LocalizedString>();
        bool bound;

        public event Action<int> Changed;
        public int Count => options.Count;
        public int Selected => options.Selected;
        public UIChoice this[int index] => options[index];
        public LocalizedString Caption(int index) => captions[index];

        /// <summary>Shows one tab per caption, each opening the page at the same index.</summary>
        public void SetTabs(IReadOnlyList<GameObject> tabPages, int selected = 0) => SetTabs(labels, tabPages, selected);

        /// <summary>Shows these captions instead of the inspector's, for tabs the owner lists at runtime.</summary>
        public void SetTabs(IReadOnlyList<LocalizedString> tabCaptions, IReadOnlyList<GameObject> tabPages, int selected = 0)
        {
            if (!bound)
            {
                options.Changed += Pick;
                bound = true;
            }
            pages = tabPages;
            captions = tabCaptions;
            options.MaxColumns = captions.Count;
            options.SetOptions(captions, selected);
            Select(selected);
        }

        /// <summary>Changes pages without raising the user-selection event.</summary>
        public void Select(int index)
        {
            if (index < 0 || index >= Count) return;
            options.Select(index);
            GameObject shown = pages[index];
            // Several tabs can share a page, as the manual's two catalogs do.
            foreach (GameObject page in pages)
                if (page != null) page.SetActive(page == shown);
        }

        void Pick(int index)
        {
            Select(index);
            Changed?.Invoke(index);
        }

        /// <summary>Places tabs beside the title when they fit, otherwise below it; returns the body inset.</summary>
        public float LayoutHeader(RectTransform frame)
        {
            float inlineWidth = Mathf.Max(minimumInlineWidth, inlineOffsetMin.x - inlineOffsetMax.x
                + Count * minimumTabWidth + Mathf.Max(0, Count - 1) * tabSpacing);
            bool inline = frame.rect.width >= inlineWidth;
            var row = (RectTransform)transform;
            row.anchorMin = new Vector2(0, 1); row.anchorMax = Vector2.one; row.pivot = new Vector2(.5f, 1);
            row.offsetMin = inline ? inlineOffsetMin : stackedOffsetMin;
            row.offsetMax = inline ? inlineOffsetMax : stackedOffsetMax;
            return inline ? inlineBodyInset : stackedBodyInset;
        }

        void OnDestroy()
        {
            if (bound && options != null) options.Changed -= Pick;
        }
    }
}
