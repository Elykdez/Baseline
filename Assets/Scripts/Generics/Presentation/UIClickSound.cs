using Hypocycloid.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Baseline.Presentation
{
    /// <summary>
    /// Click sound for interactive UI that is not a plain <see cref="Button"/> (dropdowns, toggles).
    /// Buttons use <see cref="UIButton"/>. Both raise a <see cref="SoundRequestedSignal"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UIClickSound : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField]
        SoundCueDef sound;

        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerClick(PointerEventData eventData) => Play();

        public void OnSubmit(BaseEventData eventData) => Play();

        void Play()
        {
            if (selectable == null || selectable.IsInteractable())
                SignalBus.Fire(new SoundRequestedSignal { Cue = sound }, this);
        }
    }
}
