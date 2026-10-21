using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>
    /// A list of actions that opens from a button. Any choice, or a tap outside, closes it. A menu
    /// whose items never change authors them in its prefab, keyed, and opens with <see cref="Show(RectTransform)"/>;
    /// <see cref="Show(RectTransform, IReadOnlyList{Entry})"/> remains for items made at runtime.
    /// </summary>
    public sealed class UIPopupMenu : MonoBehaviour
    {
        public readonly struct Entry
        {
            public readonly string Label;
            public readonly Action Action;
            public readonly bool Enabled;
            public Entry(string label, Action action, bool enabled = true) { Label = label; Action = action; Enabled = enabled; }
        }

        [SerializeField] Button backdrop, itemTemplate;
        [SerializeField] RectTransform panel;
        [SerializeField] float screenMargin;

        readonly List<Button> items = new();
        Action[] actions = Array.Empty<Action>();
        bool bound;

        public bool IsOpen => gameObject.activeSelf;
        public int Count { get; private set; }
        public Button this[int index] => items[index];

        /// <summary>Opens the menu with its authored items.</summary>
        public void Show(RectTransform anchor)
        {
            Bind();
            itemTemplate.gameObject.SetActive(false);
            foreach (Button made in items) made.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Place(anchor);
        }

        /// <summary>An authored item's action: the menu closes, then it runs.</summary>
        public void Bind(Button item, Action action) => item.onClick.AddListener(() => { Close(); action(); });

        public void Show(RectTransform anchor, IReadOnlyList<Entry> entries)
        {
            Bind();
            itemTemplate.gameObject.SetActive(false);
            actions = new Action[entries.Count];
            while (items.Count < entries.Count)
            {
                Button item = Instantiate(itemTemplate, itemTemplate.transform.parent, false);
                int index = items.Count;
                item.onClick.AddListener(() => Choose(index));
                items.Add(item);
            }
            Count = entries.Count;
            for (int i = 0; i < items.Count; i++)
            {
                bool used = i < entries.Count;
                items[i].gameObject.SetActive(used);
                if (!used) continue;
                actions[i] = entries[i].Action;
                items[i].interactable = entries[i].Enabled;
                var label = items[i].GetComponentInChildren<TMPro.TMP_Text>(true);
                label.ShowPlain(entries[i].Label);
            }
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Place(anchor);
        }

        public void Close() => gameObject.SetActive(false);

        void Choose(int index)
        {
            Close();
            if (index < actions.Length) actions[index]?.Invoke();
        }

        void Bind()
        {
            if (bound) return;
            bound = true;
            backdrop.onClick.AddListener(Close);
        }

        // Opens below the anchor, right edges aligned, and stays inside the screen. An anchor near the
        // bottom (a status bar) opens it above instead, so it never covers its own button.
        void Place(RectTransform anchor)
        {
            var root = (RectTransform)transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(panel);
            Vector2 size = panel.rect.size;
            Vector2 corner = new(root.rect.xMax - screenMargin, root.rect.yMax - screenMargin);
            if (anchor != null)
            {
                var corners = new Vector3[4];
                anchor.GetWorldCorners(corners);
                corner = root.InverseTransformPoint(corners[3]);
                Vector2 top = root.InverseTransformPoint(corners[2]);
                if (corner.y - size.y < root.rect.yMin + screenMargin && top.y + size.y <= root.rect.yMax - screenMargin) corner.y = top.y + size.y;
            }
            panel.anchorMin = panel.anchorMax = new Vector2(.5f, .5f);
            panel.pivot = new Vector2(1, 1);
            float x = Mathf.Clamp(corner.x, root.rect.xMin + size.x + screenMargin, root.rect.xMax - screenMargin);
            float y = Mathf.Clamp(corner.y, root.rect.yMin + size.y + screenMargin, root.rect.yMax - screenMargin);
            panel.anchoredPosition = new Vector2(x, y) - root.rect.center;
        }
    }
}
