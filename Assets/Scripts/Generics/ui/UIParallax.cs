using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Baseline
{
    /// <summary>Cover-fitted artwork layers with idle drift and gentle pointer steering.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class UIParallax : MonoBehaviour
    {
        [Serializable]
        public sealed class Layer
        {
            public RectTransform target;
            [Tooltip("Movement as a fraction of the viewport. Negative values reverse direction.")]
            public float depth = .018f;
            [Min(0), Tooltip("Additional scale at maximum steering.")]
            public float zoom;
        }

        [SerializeField] Layer[] layers = Array.Empty<Layer>();
        [Header("Artwork")]
        [SerializeField, Min(.01f)] float aspectRatio = 16f / 9f;
        [SerializeField, Min(1)] float overscan = 1.07f;
        [SerializeField, Tooltip("Point in the artwork to retain when cropped; (0, 0) is bottom-left.")]
        Vector2 focalPoint = new(.5f, .5f);
        [Header("Motion")]
        [SerializeField, Min(.01f)] float driftPeriod = 12f;
        [SerializeField] Vector2 driftAmplitude = new(.72f, .22f);
        [SerializeField, Range(0, 1)] float pointerInfluence = .32f;
        [SerializeField, Min(.01f)] float smoothTime = .45f;

        RectTransform frame;
        Canvas canvas;
        Vector2 observer, velocity;
        float elapsed;

        void OnEnable()
        {
            frame = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            ResetView();
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            float angle = elapsed * (2f * Mathf.PI / Mathf.Max(.01f, driftPeriod));
            Vector2 target = new(Mathf.Sin(angle) * driftAmplitude.x, Mathf.Sin(angle * 2f) * driftAmplitude.y);
            target = Vector2.ClampMagnitude(target + PointerOffset() * pointerInfluence, 1f);
            observer = Vector2.SmoothDamp(observer, target, ref velocity, Mathf.Max(.01f, smoothTime), Mathf.Infinity, dt);
            ApplyView(observer);
        }

        Vector2 PointerOffset()
        {
            if (!Application.isFocused) return Vector2.zero;
            Vector2? position = ReadPointer();
            if (!position.HasValue) return Vector2.zero;
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return NormalizePointer(position.Value, camera);
        }

        static Vector2? ReadPointer()
        {
            if (Pointer.current == null) return null;
            // Touchscreen.position retains the last contact after release.
            if (Pointer.current is Touchscreen touch && !touch.primaryTouch.press.isPressed) return null;
            return Pointer.current.position.ReadValue();
        }

        Vector2 NormalizePointer(Vector2 screenPosition, Camera camera)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, screenPosition, camera, out Vector2 point))
                return Vector2.zero;
            Rect rect = frame.rect;
            if (rect.width <= 0 || rect.height <= 0 || !rect.Contains(point)) return Vector2.zero;
            return new Vector2((point.x - rect.xMin) / rect.width * 2f - 1f,
                (point.y - rect.yMin) / rect.height * 2f - 1f);
        }

        /// <summary>Applies a normalized view immediately, including outside Play Mode for previews.</summary>
        public void ApplyView(Vector2 view)
        {
            if (frame == null) frame = (RectTransform)transform;
            Vector2 viewport = frame.rect.size;
            if (viewport.x <= 0 || viewport.y <= 0 || layers == null) return;
            view = Vector2.ClampMagnitude(view, 1f);
            float depth = 0;
            foreach (Layer layer in layers)
                if (layer?.target != null) depth = Mathf.Max(depth, Mathf.Abs(layer.depth));

            // Reserve enough artwork for the deepest layer, including at extreme focal points.
            float aspect = Mathf.Max(.01f, aspectRatio);
            float height = Mathf.Max(viewport.y, viewport.x / aspect) * Mathf.Max(overscan, 1f + depth * 2f);
            Vector2 size = new(height * aspect, height);
            Vector2 focus = new(Mathf.Clamp01(focalPoint.x), Mathf.Clamp01(focalPoint.y));
            foreach (Layer layer in layers)
            {
                if (layer?.target == null) continue;
                float scale = 1f + Mathf.Max(0, layer.zoom) * Mathf.Max(Mathf.Abs(view.x), Mathf.Abs(view.y));
                Vector2 margin = (size * scale - viewport) * .5f;
                Vector2 travel = viewport * layer.depth;
                Vector2 crop = Vector2.Scale(Vector2.one * .5f - focus, size * scale);
                Vector2 cropLimit = new(Mathf.Max(0, margin.x - Mathf.Abs(travel.x)),
                    Mathf.Max(0, margin.y - Mathf.Abs(travel.y)));
                crop.x = Mathf.Clamp(crop.x, -cropLimit.x, cropLimit.x);
                crop.y = Mathf.Clamp(crop.y, -cropLimit.y, cropLimit.y);
                RectTransform rect = layer.target;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = size;
                rect.localScale = Vector3.one * scale;
                rect.anchoredPosition = crop - Vector2.Scale(view, travel);
            }
        }

        void OnRectTransformDimensionsChange()
        {
            if (Application.isPlaying && isActiveAndEnabled) ApplyView(observer);
        }

        void ResetView()
        {
            observer = velocity = Vector2.zero;
            elapsed = 0;
            ApplyView(Vector2.zero);
        }

        void OnDisable() => ResetView();
    }
}
