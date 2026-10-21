using Baseline.Presentation;
using Hypocycloid.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Baseline
{
    [RequireComponent(typeof(Button))]
    public class UIButton : MonoBehaviour
    {
        public SoundCueDef sound;
        public AudioManager audioManager;
        public string soundName;

        void Start()
        {
            GetComponent<Button>()
                .onClick.AddListener(() =>
                {
                    if (sound != null)
                    {
                        SignalBus.Fire(new SoundRequestedSignal { Cue = sound }, this);
                        return;
                    }
                    if (audioManager == null)
                        audioManager = FindFirstObjectByType<AudioManager>();
                    if (audioManager != null) audioManager.PlaySfx(soundName);
                });
        }
    }
}
