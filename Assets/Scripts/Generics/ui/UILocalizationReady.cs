using System.Collections;
using UnityEngine;
using UnityEngine.Localization.PropertyVariants;
using UnityEngine.Localization.Settings;

namespace Baseline
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GameObjectLocalizer))]
    public sealed class UILocalizationReady : MonoBehaviour
    {
        GameObjectLocalizer localizer;
        Coroutine initialization;
        bool waiting;

        void Awake()
        {
            if (!Application.isPlaying) return;
            localizer = GetComponent<GameObjectLocalizer>();
            waiting = localizer.enabled;
            if (waiting) localizer.enabled = false;
        }

        void OnEnable()
        {
            if (waiting) initialization = StartCoroutine(EnableWhenReady());
        }

        void OnDisable()
        {
            if (initialization != null) StopCoroutine(initialization);
            initialization = null;
        }

        IEnumerator EnableWhenReady()
        {
            // Locale changes can replace initialization handles while a hidden panel is waiting.
            while (!LocalizationSettings.InitializationOperation.IsDone) yield return null;
            waiting = false;
            initialization = null;
            localizer.enabled = true;
        }
    }
}
