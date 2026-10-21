using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Simple UI Anim
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFadeAnim : MonoBehaviour
    {
        public float showDelay;
        public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        public bool isRewind;
        public bool isPassive;
        public UnityEvent onPlay;

        float lastKeyTime;
        CanvasGroup uiGroup;

        void Awake()
        {
            uiGroup = GetComponent<CanvasGroup>();
            if (!uiGroup.isActiveAndEnabled) uiGroup.enabled = true;
        }

        // OnEnable
        void OnEnable()
        {
            lastKeyTime = fadeCurve[fadeCurve.length - 1].time;
            if (!isPassive) Play();
        }

        IEnumerator SetVisible(float delay)
        {
            uiGroup.alpha = fadeCurve.Evaluate(0);
            ResetInteraction();

            if (delay > 0) yield return new WaitForSeconds(delay);
            onPlay?.Invoke();

            float elapsed = isRewind ? lastKeyTime : 0;
            while (elapsed < lastKeyTime)
            {
                yield return new WaitForSeconds(Time.deltaTime);
                if (isRewind)
                {
                    elapsed -= Time.deltaTime;
                }
                else
                {
                    elapsed += Time.deltaTime;
                }
                // Debug.Log($"[ui]: Animation time -> {lastKeyTime:0.00}, elapsed -> {elapsed:0.00}, uiGroup.alpha -> {uiGroup.alpha:0.00}");
                uiGroup.alpha = fadeCurve.Evaluate(elapsed);
                ResetInteraction();
            }

        }

        /// <summary>
        /// Reset interaction
        /// </summary>
        void ResetInteraction()
        {
            bool state = uiGroup.alpha <= 0;
            uiGroup.interactable = !state;
            uiGroup.blocksRaycasts = !state;
        }

        /// <summary>
        /// Should set on
        /// </summary>
        /// <param name="isOn"></param>
        public void SetOn(bool isOn)
        {
            StopAllCoroutines();
            uiGroup.alpha = isOn ? 1 : 0;
            ResetInteraction();
        }

        /// <summary>
        /// Play anim
        /// </summary>
        /// <param name="delay">delay(s)</param>
        /// <summary>
        public void Play()
        {
            // if (uiGroup == null) uiGroup = GetComponent<CanvasGroup>();
            StopAllCoroutines();
            StartCoroutine(SetVisible(Mathf.Abs(showDelay)));
        }

        /// <summary>
        /// Play anim
        /// </summary>
        /// <param name="delay">delay(s)</param>
        /// <summary>

        public void Play(bool isRewind)
        {
            this.isRewind = !isRewind;
            Play();
        }

    }

}