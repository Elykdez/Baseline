using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Shared polling helpers backed exclusively by Unity's Input System.
    /// </summary>
    public class AbstractInputSystem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        static readonly List<RaycastResult> uiHits = new();

        float lastMultiTouchDistance;

        /// <summary>
        /// Whether a screen point is over a raycast-target UI element. Raycasts directly rather than
        /// asking the input module, whose answer for a touch that began this frame depends on
        /// whether the EventSystem has updated yet.
        /// </summary>
        protected static bool IsPointerOverUI(Vector2 screenPosition)
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            eventSystem.RaycastAll(pointer, uiHits);
            bool hit = uiHits.Count > 0;
            uiHits.Clear();
            return hit;
        }

        protected static Vector2 GetMousePosition()
        {
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return Touchscreen.current.primaryTouch.position.ReadValue();
            }

            return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        }

        protected static bool GetMouseButton(int index)
        {
            if (index == 0 && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
            {
                return true;
            }

            ButtonControl button = GetMouseButtonControl(index);
            return button != null && button.isPressed;
        }

        protected static bool GetMouseButtonDown(int index)
        {
            if (index == 0 && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                return true;
            }

            ButtonControl button = GetMouseButtonControl(index);
            return button != null && button.wasPressedThisFrame;
        }

        protected static bool GetMouseButtonUp(int index)
        {
            if (index == 0 && Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasReleasedThisFrame)
            {
                return true;
            }

            ButtonControl button = GetMouseButtonControl(index);
            return button != null && button.wasReleasedThisFrame;
        }

        protected static float GetAxis(string axisName)
        {
            switch (axisName)
            {
                case "Mouse X":
                    return Pointer.current != null ? Pointer.current.delta.x.ReadValue() : 0f;
                case "Mouse Y":
                    return Pointer.current != null ? Pointer.current.delta.y.ReadValue() : 0f;
                case "Horizontal":
                    return GetMoveInput().x;
                case "Vertical":
                    return GetMoveInput().y;
                default:
                    return 0f;
            }
        }

        protected static bool GetKey(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].isPressed;
        }

        protected static bool GetKeyDown(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }

        protected static bool GetKeyUp(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasReleasedThisFrame;
        }

        protected Vector2 GetMouseScrollDelta()
        {
            if (Touchscreen.current != null)
            {
                TouchControl firstTouch = Touchscreen.current.touches[0];
                TouchControl secondTouch = Touchscreen.current.touches[1];
                bool firstPressed = firstTouch.press.isPressed;
                bool secondPressed = secondTouch.press.isPressed;

                if (firstPressed && secondPressed)
                {
                    float distance = Vector2.Distance(firstTouch.position.ReadValue(), secondTouch.position.ReadValue());
                    if (firstTouch.press.wasPressedThisFrame || secondTouch.press.wasPressedThisFrame)
                    {
                        lastMultiTouchDistance = distance;
                        return Vector2.zero;
                    }

                    float delta = distance - lastMultiTouchDistance;
                    lastMultiTouchDistance = distance;
                    return new Vector2(0f, delta * 0.05f);
                }
            }

            return Mouse.current != null ? Mouse.current.scroll.ReadValue() * 0.01f : Vector2.zero;
        }

        static ButtonControl GetMouseButtonControl(int index)
        {
            if (Mouse.current == null)
            {
                return null;
            }

            return index switch
            {
                0 => Mouse.current.leftButton,
                1 => Mouse.current.rightButton,
                2 => Mouse.current.middleButton,
                _ => null,
            };
        }

        static Vector2 GetMoveInput()
        {
            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                input.x = (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed ? 1f : 0f);
                input.y = (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed ? 1f : 0f)
                    - (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed ? 1f : 0f);
            }

            if (Gamepad.current != null)
            {
                Vector2 gamepadInput = Gamepad.current.leftStick.ReadValue() + Gamepad.current.dpad.ReadValue();
                if (gamepadInput.sqrMagnitude > input.sqrMagnitude)
                {
                    input = Vector2.ClampMagnitude(gamepadInput, 1f);
                }
            }

            return input;
        }

        public void OnPointerEnter(PointerEventData eventData) { }

        public void OnPointerExit(PointerEventData eventData) { }
    }
}
