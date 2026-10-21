using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>Repeats a held button: after a short delay it raises Repeated until the press ends.</summary>
    [RequireComponent(typeof(Button))]
    public sealed class UIRepeatPress : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField] float delay = .45f, interval = .09f;

        bool held, repeated;
        float next;

        public event Action Repeated;

        /// <summary>True once per press that repeated, so its closing click can be ignored.</summary>
        public bool TakeRepeated()
        {
            bool value = repeated;
            repeated = false;
            return value;
        }

        public void OnPointerDown(PointerEventData data)
        {
            held = GetComponent<Button>().interactable;
            repeated = false;
            next = Time.unscaledTime + delay;
        }

        public void OnPointerUp(PointerEventData data) => held = false;
        public void OnPointerExit(PointerEventData data) => held = false;
        void OnDisable() => held = false;

        void Update()
        {
            if (!held || Time.unscaledTime < next) return;
            if (!GetComponent<Button>().interactable) { held = false; return; }
            next = Time.unscaledTime + interval;
            repeated = true;
            Repeated?.Invoke();
        }
    }
}
