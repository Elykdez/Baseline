using UnityEngine;
using UnityEngine.Audio;

namespace Baseline.Presentation
{
    /// <summary>
    /// The authored mix: a base level per channel. The player's Master and category sliders multiply on
    /// top, so these set the balance between music, interface and board sounds.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioSettings", menuName = "Baseline/Presentation/Audio Settings")]
    public sealed class AudioSettingsDef : ScriptableObject
    {
        [SerializeField, Range(0f, 1f)]
        float music = 0.6f;

        [SerializeField, Range(0f, 1f)]
        float ui = 0.8f;

        [SerializeField, Range(0f, 1f)]
        float fx = 1f;

        [SerializeField] AudioMixer mixer;
        [SerializeField] AudioMixerGroup musicOutput;
        [SerializeField] AudioMixerGroup menuMusicOutput;
        [SerializeField] AudioMixerGroup fxOutput;
        [SerializeField] AudioMixerGroup uiOutput;

        public float Music => music;

        public float For(SoundChannel channel) => channel == SoundChannel.Ui ? ui : fx;

        public AudioMixerGroup Output(SoundChannel channel) => channel == SoundChannel.Ui ? uiOutput : fxOutput;

        public AudioMixerGroup MusicOutputFor(MusicContext context) =>
            context == MusicContext.Menu ? menuMusicOutput : musicOutput;

        // Player gains follow the mixer effects, so muting also silences reverb tails.
        public float SourceLevel(AudioBus bus) => mixer != null ? 1f : SoundManager.EffectiveLevel(bus);

        public void ApplyPlayerLevels()
        {
            if (mixer == null)
                return;
            mixer.SetFloat("MasterVolume", Decibels(SoundManager.Level(AudioBus.Master)));
            mixer.SetFloat("MusicVolume", Decibels(SoundManager.Level(AudioBus.Music)));
            mixer.SetFloat("FxVolume", Decibels(SoundManager.Level(AudioBus.Effects)));
            mixer.SetFloat("UiVolume", Decibels(SoundManager.Level(AudioBus.Interface)));
        }

        static float Decibels(float level) => level > 0f ? 20f * Mathf.Log10(level) : -80f;
    }
}
