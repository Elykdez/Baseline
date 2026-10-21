using System;
using UnityEngine;

namespace Baseline
{
    public class SoundBehaviour : MonoBehaviour
    {
        public AudioManager audioManager;
        public string soundName;
        public bool isMusic;

        void Start()
        {
            if (audioManager == null)
                audioManager = FindFirstObjectByType<AudioManager>();
            if (isMusic)
                audioManager.PlayMusic(soundName);
            else
                audioManager.PlaySfx(soundName);
        }
    }
}
