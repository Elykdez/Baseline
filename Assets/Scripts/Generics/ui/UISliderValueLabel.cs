using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UISliderValueLabel : MonoBehaviour
    {
        [SerializeField] Slider slider;

        TMP_Text label;
        float displayedValue = float.NaN;

        void Awake()
        {
            label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            slider ??= GetComponentInParent<Slider>();
            slider?.onValueChanged.AddListener(Refresh);
            Refresh(slider != null ? slider.value : 0f);
        }

        void OnDisable()
        {
            slider?.onValueChanged.RemoveListener(Refresh);
        }

        void LateUpdate()
        {
            if (slider != null && !Mathf.Approximately(displayedValue, slider.value))
                Refresh(slider.value);
        }

        void Refresh(float value)
        {
            if (label == null)
                label = GetComponent<TMP_Text>();
            displayedValue = value;
            label.ShowPlain($"{Mathf.RoundToInt(value * 100f)}%");
        }
    }
}
