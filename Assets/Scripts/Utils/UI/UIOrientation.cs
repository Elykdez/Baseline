using UnityEngine;
using UnityEngine.Events;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Detect orientation change, get called before AND after Awake.
    /// </summary>
    public class UIOrientation : MonoBehaviour
    {
        public bool isPassive;
        public UnityEvent onOrientationChange;

        RectTransform rectTransform;

        void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            if (!isPassive) SetOrientation();
        }

        void OnRectTransformDimensionsChange()
        {
            SetOrientation();
        }

        public void SetOrientation()
        {
            onOrientationChange?.Invoke();
        }
    }
}