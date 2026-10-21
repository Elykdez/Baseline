using UnityEngine;
using UnityEngine.EventSystems;

namespace Baseline
{
    /// <summary>The grab bar of a <see cref="UISheet"/>: drag to move the panel, tap to open or close it.</summary>
    public sealed class UISheetHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] UISheet sheet;

        float speed, lastTime;

        float Scale
        {
            get
            {
                var canvas = GetComponentInParent<Canvas>();
                return canvas != null && canvas.scaleFactor > 0 ? canvas.scaleFactor : 1;
            }
        }

        public void OnBeginDrag(PointerEventData data)
        {
            speed = 0; lastTime = Time.unscaledTime;
            sheet.BeginDrag();
        }

        public void OnDrag(PointerEventData data)
        {
            float delta = (sheet.Edge == SheetEdge.Bottom ? data.delta.y : data.delta.x) / Scale;
            float time = Time.unscaledTime, elapsed = Mathf.Max(.001f, time - lastTime);
            speed = Mathf.Lerp(speed, delta / elapsed, .5f); lastTime = time;
            sheet.Drag(delta);
        }

        public void OnEndDrag(PointerEventData data) => sheet.EndDrag(speed);

        public void OnPointerClick(PointerEventData data)
        {
            if (!data.dragging) sheet.Toggle();
        }
    }
}
