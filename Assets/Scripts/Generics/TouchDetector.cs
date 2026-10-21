using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Baseline
{
    public enum TouchType
    {
        NONE = 0,
        UP = 1,
        RIGHT = 2,
        DOWN = 3,
        LEFT = 4,
        TAP = -1,
        PRESS = -2,
        TAPX2 = -3,
    }

    public class TouchDetector : MonoBehaviour
    {
        [SerializeField, Range(0.01f, 1f)]
        float swipeThreshold = .2f;

        [SerializeField, Range(0.01f, 1f)]
        float doubleTapThreshold = .25f;

        [SerializeField, Range(0.01f, 10f)]
        float pressThreshold = 1.5f;

        [SerializeField]
        RectTransform uiBoundaryElement;

        public TouchType Direction { get; private set; }
        public Vector2 PointerPosition { get; private set; }

        Vector2 firstPosition;
        float touchDownTime;
        int tapCount;
        bool trackingTouch;
        bool pressReported;
        Coroutine tapRoutine;

        void Update()
        {
            if (Touchscreen.current == null)
            {
                return;
            }

            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasPressedThisFrame)
            {
                BeginTouch(touch.position.ReadValue());
            }

            if (!trackingTouch)
            {
                return;
            }

            PointerPosition = touch.position.ReadValue();
            InputTouchPhase phase = touch.phase.ReadValue();
            if (touch.press.isPressed)
            {
                touchDownTime += Time.unscaledDeltaTime;
                if (!pressReported && touchDownTime >= pressThreshold)
                {
                    Direction = TouchType.PRESS;
                    pressReported = true;
                }
            }

            if (touch.press.wasReleasedThisFrame || phase == InputTouchPhase.Canceled)
            {
                EndTouch(phase == InputTouchPhase.Canceled);
            }
        }

        public void ResetState()
        {
            Direction = TouchType.NONE;
        }

        void BeginTouch(Vector2 position)
        {
            if (!IsInsideBoundary(position))
            {
                trackingTouch = false;
                ResetState();
                return;
            }

            trackingTouch = true;
            pressReported = false;
            touchDownTime = 0f;
            firstPosition = position;
            PointerPosition = position;
        }

        void EndTouch(bool canceled)
        {
            trackingTouch = false;
            touchDownTime = 0f;

            if (!canceled && !pressReported && IsInsideBoundary(PointerPosition))
            {
                Vector2 delta = PointerPosition - firstPosition;
                float dragDistance = Screen.width * swipeThreshold;
                if (Mathf.Abs(delta.x) > dragDistance || Mathf.Abs(delta.y) > dragDistance)
                {
                    Direction =
                        Mathf.Abs(delta.x) > Mathf.Abs(delta.y)
                            ? (delta.x > 0f ? TouchType.RIGHT : TouchType.LEFT)
                            : (delta.y > 0f ? TouchType.UP : TouchType.DOWN);
                }
                else
                {
                    QueueTap();
                }
            }

            pressReported = false;
        }

        bool IsInsideBoundary(Vector2 position)
        {
            if (uiBoundaryElement == null)
            {
                return true;
            }

            Canvas canvas = uiBoundaryElement.GetComponentInParent<Canvas>();
            Camera eventCamera =
                canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
            return RectTransformUtility.RectangleContainsScreenPoint(
                uiBoundaryElement,
                position,
                eventCamera
            );
        }

        void QueueTap()
        {
            tapCount++;
            if (tapRoutine == null)
            {
                tapRoutine = StartCoroutine(ResolveTap());
            }
        }

        IEnumerator ResolveTap()
        {
            yield return new WaitForSecondsRealtime(doubleTapThreshold);
            Direction = tapCount >= 2 ? TouchType.TAPX2 : TouchType.TAP;
            tapCount = 0;
            tapRoutine = null;
        }
    }
}
