using System;
using Hypocycloid.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Baseline
{
    public class UIJoystick : AbstractInputSystem, IDragHandler, IEndDragHandler
    {
        public float joystickRadius = 100f;
        public RectTransform joystickArea; // The area in which the joystick can move
        public RectTransform joystickHandle; // The movable handle of the joystick

        public bool IsKeyPressed { get; private set; }

        [SerializeField]
        Vector2 inputVector;
        public Vector2 InputVector => inputVector;

        [SerializeField]
        UnityEvent onStart;
        public UnityEvent OnStart
        {
            get
            {
                onStart ??= new UnityEvent();
                return onStart;
            }
        }

        [SerializeField]
        UnityEvent<Vector2> onInput;
        public UnityEvent<Vector2> OnInput
        {
            get
            {
                onInput ??= new UnityEvent<Vector2>();
                return onInput;
            }
        }

        void Update()
        {
            Vector2 upRight = new(GetKey(Key.D) ? 1f : 0, GetKey(Key.W) ? 1f : 0);
            Vector2 downLeft = new(GetKey(Key.A) ? -1f : 0, GetKey(Key.S) ? -1f : 0);
            Vector2 moveDir = upRight + downLeft;
            if (moveDir != Vector2.zero)
            {
                IsKeyPressed = true;
                Vector2 dir = Vector2.ClampMagnitude(moveDir * joystickRadius, joystickRadius);
                SetInputDir(dir);
            }
            else
            {
                if (IsKeyPressed)
                {
                    ResetInput();
                    IsKeyPressed = false;
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            // Convert screen position to local position in joystickArea
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                joystickArea,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            );
            // Get the direction from the center
            Vector2 dir = Vector2.ClampMagnitude(localPoint, joystickRadius);
            SetInputDir(dir);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            ResetInput();
        }

        void SetInputDir(Vector2 operationDir)
        {
            // Correct the handle position considering its pivot (0.5, 0.5)
            joystickHandle.anchoredPosition = operationDir;
            if (inputVector == default)
                OnStart.Invoke();
            inputVector = operationDir / joystickRadius;
            OnInput.Invoke(inputVector);
        }

        void ResetInput()
        {
            joystickHandle.anchoredPosition = Vector2.zero;
            inputVector = Vector2.zero;
        }
    }
}
