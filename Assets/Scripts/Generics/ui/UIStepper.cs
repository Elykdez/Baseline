using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>A labelled whole number with minus and plus buttons. Holding a button repeats it.</summary>
    public sealed class UIStepper : MonoBehaviour
    {
        [SerializeField] TMP_Text label, value;
        [SerializeField] Image icon;
        [SerializeField] Button minus, plus;

        int[] steps = { 0 };
        int current;
        Func<int, string> format;
        bool bound;
        bool interactable = true;

        public event Action<int> Changed;
        public int Value => current;
        public TMP_Text Label => label;
        public Image Icon => icon;
        public Button Minus => minus;
        public Button Plus => plus;

        /// <summary>Every whole number from min to max in steps of step.</summary>
        public void SetRange(int min, int max, int step = 1, Func<int, string> formatter = null)
        {
            var values = new List<int>();
            for (int v = min; v <= max; v += Math.Max(1, step)) values.Add(v);
            SetValues(values, formatter);
        }

        /// <summary>Only these values, in any order; a value outside them still shows, and steps to its neighbours.</summary>
        public void SetValues(IEnumerable<int> values, Func<int, string> formatter = null)
        {
            Bind();
            steps = values.Distinct().OrderBy(v => v).ToArray();
            if (steps.Length == 0) steps = new[] { 0 };
            format = formatter;
            Refresh();
        }

        /// <summary>Shows a value without raising Changed.</summary>
        public void SetValue(int number)
        {
            Bind();
            current = number;
            Refresh();
        }

        public void SetInteractable(bool interactable)
        {
            Bind();
            this.interactable = interactable;
            Refresh();
        }

        /// <summary>The same as a press of minus (-1) or plus (+1).</summary>
        public void Step(int direction)
        {
            if (!interactable || direction == 0) return;
            int? next = direction < 0 ? Below() : Above();
            if (next == null) return;
            current = next.Value;
            Refresh();
            Changed?.Invoke(current);
        }

        int? Below() => steps.Where(v => v < current).Select(v => (int?)v).LastOrDefault();
        int? Above() => steps.Where(v => v > current).Select(v => (int?)v).FirstOrDefault();

        void Refresh()
        {
            value.ShowPlain(format != null ? format(current) : current.ToString());
            minus.interactable = interactable && Below() != null;
            plus.interactable = interactable && Above() != null;
        }

        void Awake() => Bind();

        void Bind()
        {
            if (bound) return;
            bound = true;
            Hook(minus, -1);
            Hook(plus, 1);
        }

        void Hook(Button button, int direction)
        {
            UIRepeatPress repeat = button.GetComponent<UIRepeatPress>();
            button.onClick.AddListener(() =>
            {
                if (repeat == null || !repeat.TakeRepeated()) Step(direction);
            });
            if (repeat != null) repeat.Repeated += () => Step(direction);
        }
    }
}
