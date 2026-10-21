using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Baseline
{
    /// <summary>A row or grid of choices with one selected. It replaces buttons that cycle through values.</summary>
    public sealed class UIOptionGroup : MonoBehaviour
    {
        [SerializeField] UIChoice template;
        [SerializeField] RectTransform container;
        [SerializeField, Min(1)] int maxColumns = 4;

        [SerializeField, Tooltip("A fixed set of choices, captioned here in order. Left empty when the owner supplies names (sides, armies).")]
        LocalizedString[] labels = Array.Empty<LocalizedString>();

        readonly List<UIChoice> choices = new();
        int selected = -1;

        public event Action<int> Changed;
        public int Selected => selected;
        public int Count { get; private set; }
        public int MaxColumns { get => maxColumns; set { maxColumns = Mathf.Max(1, value); Arrange(); } }
        public UIChoice this[int index] => choices[index];



        /// <summary>The fixed choices captioned in the inspector.</summary>
        public IReadOnlyList<LocalizedString> Labels => labels;

        /// <summary>Shows the choices captioned in the inspector and selects one without raising Changed.</summary>
        public void SetOptions(int selection, IReadOnlyList<Sprite> icons = null) => SetOptions(labels, selection, icons);

        /// <summary>Shows these names, which no string entry drives, and selects one without raising Changed.</summary>
        public void SetOptions(IReadOnlyList<string> names, int selection, IReadOnlyList<Sprite> icons = null)
        {
            Fill(names.Count);
            for (int i = 0; i < Count; i++) choices[i].Show(names[i], Icon(icons, i));
            Arrange();
            Select(selection);
        }

        /// <summary>Shows these authored captions and selects one without raising Changed.</summary>
        public void SetOptions(IReadOnlyList<LocalizedString> captions, int selection, IReadOnlyList<Sprite> icons = null)
        {
            Fill(captions.Count);
            for (int i = 0; i < Count; i++) choices[i].Show(captions[i], Icon(icons, i));
            Arrange();
            Select(selection);
        }

        // Reuses the choices already made and hides the rest.
        void Fill(int count)
        {
            template.gameObject.SetActive(false);
            while (choices.Count < count)
            {
                UIChoice choice = Instantiate(template, container, false);
                choice.name = template.name + " " + choices.Count;
                int index = choices.Count;
                choice.Button.onClick.AddListener(() => Pick(index));
                choices.Add(choice);
            }
            Count = count;
            for (int i = 0; i < choices.Count; i++) choices[i].gameObject.SetActive(i < count);
        }

        static Sprite Icon(IReadOnlyList<Sprite> icons, int index) => icons != null && index < icons.Count ? icons[index] : null;

        public void Select(int index)
        {
            selected = index;
            for (int i = 0; i < choices.Count; i++) choices[i].Selected = i == index;
        }

        public void SetInteractable(bool interactable)
        {
            foreach (UIChoice choice in choices) choice.Button.interactable = interactable;
        }

        /// <summary>The same as a tap on that choice: selects it and raises Changed.</summary>
        public void Pick(int index)
        {
            if (index < 0 || index >= Count || index == selected) return;
            Select(index);
            Changed?.Invoke(index);
        }

        void Arrange()
        {
            var grid = container != null ? container.GetComponent<UIResponsiveGridLayout>() : null;
            if (grid != null) grid.Columns = Mathf.Clamp(Count, 1, maxColumns);
        }
    }
}
