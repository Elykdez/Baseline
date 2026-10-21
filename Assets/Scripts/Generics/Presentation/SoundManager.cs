using System.Collections.Generic;
using Hypocycloid.Core;
using Hypocycloid.Utils;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>
    /// Plays every sound in the game and lives across scenes, so the music carries from the menu
    /// into a match. Music crossfades between two sources; cues share a small pool of sources so
    /// overlapping sounds do not cut each other off. Levels are the authored mix
    /// (<see cref="AudioSettingsDef"/>) times the player's category and master volumes.
    /// <para>
    /// Nothing calls it directly. It answers <see cref="SoundRequestedSignal"/> and
    /// <see cref="MusicRequestedSignal"/>, and owns the player's volumes: an
    /// <see cref="AudioLevelRequestedSignal"/> is applied, saved and announced as
    /// <see cref="AudioLevelChangedSignal"/>.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoundManager : MonoSingleton<SoundManager>
    {
        [SerializeField, Tooltip("Base level per channel.")]
        AudioSettingsDef mix;

        [SerializeField, Tooltip("Menu track and battle playlist.")]
        BgmDef bgm;

        [SerializeField, Min(1), Tooltip("Cues that can sound at once; the oldest is cut when all are busy.")]
        int voices = 8;

        [SerializeField, Min(0f)]
        float musicFadeSeconds = 1.2f;

        readonly List<AudioSource> pool = new();
        readonly Dictionary<AudioSource, (AudioBus Bus, float Gain)> voiceGains = new();
        readonly Dictionary<SoundCueDef, float> lastPlayed = new();
        readonly Dictionary<SoundCueDef, int> lastClip = new();
        readonly System.Random random = new();
        AudioSource[] music;
        AudioSource dialogVoice;
        Object dialogVoiceOwner;
        int activeMusic;
        float fade = 1f;
        MusicContext context = MusicContext.Menu;
        BgmPlaylist battlePlaylist;
        bool ready;

        // Discard the whole duplicate host when a later scene supplies another manager.
        protected override bool DestroyDuplicateObject => true;

        protected override void Init()
        {
            base.Init();
            Setup();
            SignalBus.Subscribe<SoundRequestedSignal>(OnSoundRequested, this);
            SignalBus.Subscribe<DialogVoiceRequestedSignal>(OnDialogVoiceRequested, this);
            SignalBus.Subscribe<MusicRequestedSignal>(OnMusicRequested, this);
            SignalBus.Subscribe<AudioLevelRequestedSignal>(OnLevelRequested, this);
        }

        protected override void OnDestroy()
        {
            SignalBus.Unsubscribe<SoundRequestedSignal>(OnSoundRequested, this);
            SignalBus.Unsubscribe<DialogVoiceRequestedSignal>(OnDialogVoiceRequested, this);
            SignalBus.Unsubscribe<MusicRequestedSignal>(OnMusicRequested, this);
            SignalBus.Unsubscribe<AudioLevelRequestedSignal>(OnLevelRequested, this);
            base.OnDestroy();
        }

        // Unity's mixer parameters are ready after Awake/OnEnable.
        void Start() => mix?.ApplyPlayerLevels();

        void Setup()
        {
            if (ready)
                return;
            ready = true;
            music = new[] { NewSource("Music A"), NewSource("Music B") };
            for (int i = 0; i < voices; i++)
                pool.Add(NewSource("Voice " + i));
            if (bgm != null)
                battlePlaylist = new BgmPlaylist(bgm.Battle);
        }

        AudioSource NewSource(string label)
        {
            var source = new GameObject(label).AddComponent<AudioSource>();
            source.transform.SetParent(transform, false);
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        void OnSoundRequested(SoundRequestedSignal signal) => Play(signal.Cue, signal.Volume ?? 1f);

        void OnMusicRequested(MusicRequestedSignal signal) => PlayMusic(signal.Music);

        void OnDialogVoiceRequested(DialogVoiceRequestedSignal signal)
        {
            if (signal.Owner == null) return;
            if (signal.Cue == null && dialogVoiceOwner != signal.Owner) return;
            if (dialogVoice != null)
            {
                dialogVoice.Stop();
                dialogVoice.clip = null;
            }
            dialogVoiceOwner = signal.Cue != null ? signal.Owner : null;
            if (signal.Cue == null) return;
            Setup();
            if (dialogVoice == null) dialogVoice = NewSource("Dialog Voice");
            PlayCue(signal.Cue, 1f, dialogVoice);
        }

        // -------- cues --------

        /// <summary>Plays a cue once. Null and empty cues are ignored so callers need no checks.</summary>
        public void Play(SoundCueDef cue, float volume = 1f) => PlayCue(cue, volume);

        void PlayCue(SoundCueDef cue, float volume, AudioSource source = null)
        {
            if (cue == null || cue.ClipCount == 0)
                return;
            Setup();
            float now = Time.unscaledTime;
            if (lastPlayed.TryGetValue(cue, out float last) && now - last < cue.MinInterval)
                return;
            lastPlayed[cue] = now;

            int index = cue.PickIndex(random, lastClip.TryGetValue(cue, out int previous) ? previous : -1);
            lastClip[cue] = index;
            AudioClip clip = cue.ClipAt(index);
            if (clip == null)
                return;

            if (source == null) source = FreeVoice();
            source.outputAudioMixerGroup = mix != null ? mix.Output(cue.Channel) : null;
            source.clip = clip;
            source.pitch = cue.PickPitch(random);
            AudioBus bus = BusOf(cue.Channel);
            float gain = cue.Volume * Mathf.Clamp01(volume) * (mix != null ? mix.For(cue.Channel) : 1f);
            voiceGains[source] = (bus, gain);
            source.volume = gain * SourceLevel(bus);
            source.Play();
        }

        AudioSource FreeVoice()
        {
            AudioSource oldest = pool[0];
            foreach (AudioSource source in pool)
            {
                if (!source.isPlaying)
                    return source;
                if (source.time > oldest.time)
                    oldest = source;
            }
            return oldest;
        }

        // -------- music --------

        /// <summary>Switches music for a screen. The current track keeps playing when it is already right.</summary>
        public void PlayMusic(MusicContext next)
        {
            Setup();
            context = next;
            foreach (AudioSource source in music)
                source.outputAudioMixerGroup = mix != null ? mix.MusicOutputFor(context) : null;
            if (next == MusicContext.Silence)
            {
                FadeOut();
                return;
            }
            if (bgm == null)
                return;
            if (next == MusicContext.Battle && bgm.Battle.Count > 0)
                CrossfadeTo(battlePlaylist.Next(), loop: false);
            else
                CrossfadeTo(bgm.Menu, loop: true);
        }

        void CrossfadeTo(AudioClip clip, bool loop)
        {
            AudioSource current = music[activeMusic];
            if (clip == null || (current.clip == clip && current.isPlaying))
            {
                current.loop = loop;
                return;
            }
            activeMusic = 1 - activeMusic;
            AudioSource next = music[activeMusic];
            next.clip = clip;
            next.loop = loop;
            next.volume = 0f;
            next.Play();
            fade = musicFadeSeconds > 0f ? 0f : 1f;
            ApplyMusicVolume();
        }

        /// <summary>Fades the playing track out to silence, the same way a crossfade fades it out.</summary>
        void FadeOut()
        {
            AudioSource current = music[activeMusic];
            if (current.clip == null || !current.isPlaying)
                return;
            activeMusic = 1 - activeMusic;
            AudioSource silent = music[activeMusic];
            silent.Stop();
            silent.clip = null;
            fade = musicFadeSeconds > 0f ? 0f : 1f;
            ApplyMusicVolume();
            if (fade >= 1f)
                current.Stop();
        }

        void Update()
        {
            if (!ready)
                return;
            if (fade < 1f)
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / Mathf.Max(0.01f, musicFadeSeconds));
                ApplyMusicVolume();
                if (fade >= 1f)
                    music[1 - activeMusic].Stop();
            }
            // A battle playlist advances when a track ends.
            AudioSource current = music[activeMusic];
            if (context == MusicContext.Battle && current.clip != null && !current.loop && !current.isPlaying && fade >= 1f)
                CrossfadeTo(battlePlaylist.Next(), loop: false);
        }

        void ApplyMusicVolume()
        {
            float level = (mix != null ? mix.Music : 1f) * SourceLevel(AudioBus.Music);
            music[activeMusic].volume = level * fade;
            music[1 - activeMusic].volume = level * (1f - fade);
        }

        // -------- player volumes --------

        float SourceLevel(AudioBus bus) => mix != null ? mix.SourceLevel(bus) : EffectiveLevel(bus);

        /// <summary>The player's saved volume for a bus, 0 to 1.</summary>
        public static float Level(AudioBus bus) =>
            bus switch
            {
                AudioBus.Master => ConfigSettingsPersistence.MasterVolume,
                AudioBus.Music => ConfigSettingsPersistence.MusicVolume,
                AudioBus.Interface => ConfigSettingsPersistence.UiVolume,
                _ => ConfigSettingsPersistence.SfxVolume,
            };

        /// <summary>Playback level after Master; slider values still use the unscaled saved Level.</summary>
        public static float EffectiveLevel(AudioBus bus) =>
            bus == AudioBus.Master ? Level(bus) : Level(AudioBus.Master) * Level(bus);

        public static AudioBus BusOf(SoundChannel channel) =>
            channel == SoundChannel.Ui ? AudioBus.Interface : AudioBus.Effects;

        /// <summary>Applies immediately to music and pooled cues, including sounds already playing.</summary>
        void OnLevelRequested(AudioLevelRequestedSignal signal)
        {
            float level = Mathf.Clamp01(signal.Level);
            switch (signal.Bus)
            {
                case AudioBus.Master:
                    ConfigSettingsPersistence.SetMasterVolume(level);
                    break;
                case AudioBus.Music:
                    ConfigSettingsPersistence.SetMusicVolume(level);
                    break;
                case AudioBus.Interface:
                    ConfigSettingsPersistence.SetUiVolume(level);
                    break;
                default:
                    ConfigSettingsPersistence.SetSfxVolume(level);
                    break;
            }
            mix?.ApplyPlayerLevels();
            if (ready && (signal.Bus == AudioBus.Master || signal.Bus == AudioBus.Music))
                ApplyMusicVolume();
            foreach (var voice in voiceGains)
            {
                if (signal.Bus == AudioBus.Master || voice.Value.Bus == signal.Bus)
                    voice.Key.volume = voice.Value.Gain * SourceLevel(voice.Value.Bus);
            }
            SignalBus.Fire(new AudioLevelChangedSignal { Bus = signal.Bus, Level = Level(signal.Bus) }, this);
        }
    }
}
