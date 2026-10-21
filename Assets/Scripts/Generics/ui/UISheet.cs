using System;
using UnityEngine;

namespace Baseline
{
    public enum SheetState { Closed, Half, Full }
    public enum SheetEdge { Bottom, Right }

    /// <summary>
    /// A panel docked to the bottom or right edge of its parent that folds away. Dragging its handle
    /// moves it, and it snaps to the nearest stop. Closed leaves only the handle (or nothing) showing.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class UISheet : MonoBehaviour
    {
        [SerializeField] RectTransform handle, content;
        [SerializeField] SheetEdge edge = SheetEdge.Bottom;
        [SerializeField] float closedExtent = 80, halfFraction = .55f, fullFraction = 1, minOpen = 360, maxOpen = 480;
        [SerializeField] float inset;
        [SerializeField] float smoothTime = .12f;

        SheetState state = SheetState.Closed;
        float extent = -1, target, velocity;
        bool dragging;

        /// <summary>Raised whenever the panel's covered area changes, during a move as well as at its end.</summary>
        public event Action Moved;
        /// <summary>Raised when the panel picks a stop, by a drag, a tap or code.</summary>
        public event Action<SheetState> StateChanged;
        public SheetState State => state;
        public SheetEdge Edge => edge;
        public float Extent => Mathf.Max(0, extent);
        public bool IsOpen => state != SheetState.Closed;
        public RectTransform Handle => handle;
        public RectTransform Content => content;

        /// <summary>
        /// Docks the panel. A bottom panel keeps its handle showing when closed and opens to a share of
        /// the height; a right panel closes completely and opens to a clamped share of the width.
        /// <paramref name="inset"/> is kept clear at the far end (the app bar). A right panel given a
        /// <paramref name="header"/> keeps its handle that tall above the content, as a title or tab bar.
        /// </summary>
        public void Dock(SheetEdge side, float closed, float inset, float header = 0)
        {
            edge = side;
            closedExtent = closed;
            this.inset = inset;
            var rect = (RectTransform)transform;
            rect.anchorMin = edge == SheetEdge.Bottom ? Vector2.zero : new Vector2(1, 0);
            rect.anchorMax = edge == SheetEdge.Bottom ? new Vector2(1, 0) : Vector2.one;
            rect.pivot = edge == SheetEdge.Bottom ? new Vector2(.5f, 0) : new Vector2(1, 0);
            rect.anchoredPosition = Vector2.zero;
            float top = edge == SheetEdge.Bottom ? closedExtent : header;
            if (handle != null)
            {
                handle.gameObject.SetActive(top > 0);
                if (top > 0) { handle.offsetMin = new Vector2(handle.offsetMin.x, -top); handle.offsetMax = new Vector2(handle.offsetMax.x, 0); }
            }
            if (content != null)
            {
                content.offsetMin = Vector2.zero;
                content.offsetMax = new Vector2(0, -top);
            }
            extent = -1;
            SetState(edge == SheetEdge.Right && state == SheetState.Half ? SheetState.Full : state, false);
        }

        public float ExtentOf(SheetState stop)
        {
            var parent = transform.parent as RectTransform;
            float size = parent == null ? 0 : edge == SheetEdge.Bottom ? parent.rect.height : parent.rect.width;
            if (stop == SheetState.Closed) return closedExtent;
            if (edge == SheetEdge.Right) return Mathf.Clamp(size * .32f, Mathf.Min(minOpen, size), Mathf.Min(maxOpen, size));
            float full = Mathf.Max(closedExtent, size * fullFraction - inset);
            return stop == SheetState.Full ? full : Mathf.Clamp(size * halfFraction, closedExtent, full);
        }

        public void SetState(SheetState stop, bool animate = true)
        {
            if (edge == SheetEdge.Right && stop == SheetState.Half) stop = SheetState.Full;
            bool changed = stop != state;
            state = stop;
            target = ExtentOf(stop);
            if (!animate || !Application.isPlaying || extent < 0) { velocity = 0; Apply(target); }
            if (changed) StateChanged?.Invoke(state);
        }

        /// <summary>A tap on the handle: open to half, or close an open panel.</summary>
        public void Toggle() => SetState(state == SheetState.Closed ? SheetState.Half : SheetState.Closed);

        internal void BeginDrag() { dragging = true; velocity = 0; }

        internal void Drag(float delta)
        {
            float signed = edge == SheetEdge.Bottom ? delta : -delta;
            Apply(Mathf.Clamp(Extent + signed, ExtentOf(SheetState.Closed), ExtentOf(SheetState.Full)));
        }

        internal void EndDrag(float speed)
        {
            dragging = false;
            if (edge == SheetEdge.Right) speed = -speed;
            float projected = Extent + speed * .15f;
            SheetState best = SheetState.Closed;
            float distance = float.MaxValue;
            foreach (SheetState stop in new[] { SheetState.Closed, SheetState.Half, SheetState.Full })
            {
                if (edge == SheetEdge.Right && stop == SheetState.Half) continue;
                float gap = Mathf.Abs(ExtentOf(stop) - projected);
                if (gap < distance) { distance = gap; best = stop; }
            }
            SetState(best);
        }

        void OnRectTransformDimensionsChange()
        {
            if (!dragging && extent >= 0) { target = ExtentOf(state); velocity = 0; Apply(target); }
        }

        void Update()
        {
            if (dragging || extent < 0 || Mathf.Abs(extent - target) < .5f)
            {
                if (!dragging && extent >= 0 && extent != target) { velocity = 0; Apply(target); }
                return;
            }
            Apply(Mathf.SmoothDamp(extent, target, ref velocity, smoothTime, float.PositiveInfinity, Time.unscaledDeltaTime));
        }

        void Apply(float value)
        {
            if (Mathf.Approximately(value, extent)) return;
            extent = value;
            var rect = (RectTransform)transform;
            if (edge == SheetEdge.Bottom) rect.sizeDelta = new Vector2(0, extent);
            else rect.sizeDelta = new Vector2(extent, -inset);
            if (content != null) content.gameObject.SetActive(extent > closedExtent + 1);
            Moved?.Invoke();
        }
    }
}
