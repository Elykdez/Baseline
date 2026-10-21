using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hypocycloid.Utils
{
    public class UIButtonThrottler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public float interactiveTime = 1f;
        public Button.ButtonClickedEvent OnTrigger;

        // Button selfBtn;
        InputHelper.Throttle actThrottle;
        public bool IsTriggered { get; private set; }

        void Start()
        {
            actThrottle = new InputHelper.Throttle();
        }

        void Update()
        {
            if (IsTriggered)
            {
                Trigger();
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsTriggered = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            IsTriggered = false;
        }

        public void Trigger()
        {
            actThrottle.Run(() =>
            {
                OnTrigger?.Invoke();
            }, interactiveTime);
        }

        // void SetInteractable(bool isInteractable)
        // {
        //     selfBtn.interactable = isInteractable;
        // }
    }
}