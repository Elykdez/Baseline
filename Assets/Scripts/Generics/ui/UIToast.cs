using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>A short message over the board that fades out by itself. Errors stay longer.</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class UIToast : MonoBehaviour
    {
        [SerializeField] TMP_Text label;
        [SerializeField] Image background;
        [SerializeField] Color normalTint, errorTint;
        [SerializeField] float seconds = 2.5f, errorSeconds = 5f;

        float hideAt;

        public string Text => label.text;

        public void Show(string message, bool error = false)
        {
            if (string.IsNullOrEmpty(message)) return;
            label.ShowPlain(message);
            if (background != null) background.color = error ? errorTint : normalTint;
            var group = GetComponent<CanvasGroup>();
            group.alpha = 1; group.blocksRaycasts = false; group.interactable = false;
            hideAt = Time.unscaledTime + (error ? errorSeconds : seconds);
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        void Update()
        {
            if (Time.unscaledTime < hideAt) return;
            var group = GetComponent<CanvasGroup>();
            group.alpha = Mathf.MoveTowards(group.alpha, 0, Time.unscaledDeltaTime * 4);
            if (group.alpha <= 0) gameObject.SetActive(false);
        }
    }
}
