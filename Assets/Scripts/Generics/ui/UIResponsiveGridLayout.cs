using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    /// <summary>Keeps a grid's columns fluid when its panel or window changes size.</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform), typeof(GridLayoutGroup))]
    public sealed class UIResponsiveGridLayout : MonoBehaviour
    {
        [SerializeField, Min(1)] int columns = 2;

        GridLayoutGroup layout;
        RectTransform rectTransform;

        public int Columns
        {
            get => columns;
            set
            {
                columns = Mathf.Max(1, value);
                Resize();
            }
        }

        void OnEnable() => Resize();
        void OnRectTransformDimensionsChange() => Resize();

        public void Resize()
        {
            layout ??= GetComponent<GridLayoutGroup>();
            rectTransform ??= GetComponent<RectTransform>();
            if (layout == null || rectTransform == null)
                return;

            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = columns;
            float gaps = layout.spacing.x * Mathf.Max(0, columns - 1);
            float usableWidth = rectTransform.rect.width
                - layout.padding.left
                - layout.padding.right
                - gaps;
            if (usableWidth > 0)
                layout.cellSize = new Vector2(usableWidth / columns, layout.cellSize.y);
        }
    }
}
