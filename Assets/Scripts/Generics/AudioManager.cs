using System;
using Baseline.Presentation;
using Hypocycloid.Core;
using Hypocycloid.Utils;
using UnityEngine;

namespace Baseline
{
    [Serializable]
    public struct Sound
    {
        public string name;
        public AudioClip clip;
        public bool isMusic;
    }

    public class AudioManager : MonoBehaviour // : MonoSingleton<AudioManager>
    {
        const string SFX_UI_CLICK = "ui_confirm";
        const string SFX_UI_CLOSE = "ui_close";

        public AudioSource musicSource,
            sfxSource;

        [Header("Audio")]
        public Sound[] musics;
        public Sound[] sfxs;

        public static AudioManager Ins { get; private set; }

        void Awake()
        {
            // local singleton
            if (Ins != null)
            {
                DestroyImmediate(gameObject);
                return;
            }
            Ins = this;

            // SoundManager owns the player volumes; follow them, master included.
            ApplyPlayerVolumes(default);
            SignalBus.Subscribe<AudioLevelChangedSignal>(ApplyPlayerVolumes, this);
            LogHelper.Log("System", "Audio online; music and effects sources initialized.");
        }

        void OnDestroy()
        {
            SignalBus.Unsubscribe<AudioLevelChangedSignal>(ApplyPlayerVolumes, this);
            if (Ins == this) Ins = null;
        }

        public void PlayMusic(string name)
        {
            if (Array.Find(musics, x => x.name == name) is Sound sound)
            {
                musicSource.clip = sound.clip;
                musicSource.Play();
            }
            else
            {
                LogHelper.LogError("Audio", $"Music {name} not found.");
            }
        }

        public void PlaySfx(string name)
        {
            if (Array.Find(sfxs, x => x.name == name) is Sound sound)
            {
                sfxSource.PlayOneShot(sound.clip);
            }
            else
            {
                LogHelper.LogError("Audio", $"Sound Effect {name} not found.");
            }
        }

        public void SetMusicVolume(float volume)
        {
            _ = SoundManager.Ins;
            SignalBus.Fire(new AudioLevelRequestedSignal { Bus = AudioBus.Music, Level = volume }, this);
        }

        public void SetSfxVolume(float volume)
        {
            _ = SoundManager.Ins;
            SignalBus.Fire(new AudioLevelRequestedSignal { Bus = AudioBus.Effects, Level = volume }, this);
        }

        void ApplyPlayerVolumes(AudioLevelChangedSignal signal)
        {
            musicSource.volume = SoundManager.EffectiveLevel(AudioBus.Music);
            sfxSource.volume = SoundManager.EffectiveLevel(AudioBus.Effects);
        }

        public void ToggleMusic()
        {
            musicSource.mute = !musicSource.mute;
        }

        public void ToggleSfx()
        {
            sfxSource.mute = !sfxSource.mute;
        }

        // -------- Quick Access --------

        public void PlayClick() => PlaySfx(SFX_UI_CLICK);

        public void PlayClose() => PlaySfx(SFX_UI_CLOSE);
    }
}
