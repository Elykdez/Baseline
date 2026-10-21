using System;
using UnityEngine;

namespace Baseline
{
    /// <summary>Prefab-authored anchors and insets for a responsive layout.</summary>
    [Serializable]
    public struct UIRectLayout
    {
        public Vector2 anchorMin, anchorMax, offsetMin, offsetMax;

        public void Apply(RectTransform target, float scale = 1)
        {
            target.anchorMin = anchorMin;
            target.anchorMax = anchorMax;
            target.offsetMin = offsetMin * scale;
            target.offsetMax = offsetMax * scale;
        }
    }
}
