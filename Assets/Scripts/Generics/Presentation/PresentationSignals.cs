using Hypocycloid.Core;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>A player-facing volume. Master scales every category without changing its saved level.</summary>
    public enum AudioBus
    {
        Music,

        /// <summary>Cues on the <see cref="SoundChannel.Ui"/> channel: buttons, panels, dropdowns.</summary>
        Interface,

        /// <summary>Cues on the <see cref="SoundChannel.Fx"/> channel and background ambience.</summary>
        Effects,

        /// <summary>All music, effects and interface sounds.</summary>
        Master,
    }

    /// <summary>Plays a cue once. A null cue is ignored. <see cref="SoundManager"/> handles it.</summary>
    public struct SoundRequestedSignal : ISignal
    {
        public SoundCueDef Cue;
        /// <summary>Optional event gain; omitted requests retain the authored cue volume.</summary>
        public float? Volume;
    }

    /// <summary>One dialog reaction at a time. A null cue stops only this owner's reaction.</summary>
    public struct DialogVoiceRequestedSignal : ISignal
    {
        public Object Owner;
        public SoundCueDef Cue;
    }

    /// <summary>Switches the music for a screen. <see cref="SoundManager"/> handles it.</summary>
    public struct MusicRequestedSignal : ISignal
    {
        public MusicContext Music;
    }

    /// <summary>Plays a board event (visual, sound, camera kick) at a world position. <see cref="FxManager"/> handles it.</summary>
    public struct FxRequestedSignal : ISignal
    {
        public FxEventDef Fx;
        public Vector3 Position;
    }

    /// <summary>Kicks the view camera; 0 to 1, already scaled by the player's screen shake setting. <see cref="CameraShake"/> handles it.</summary>
    public struct CameraShakeRequestedSignal : ISignal
    {
        public float Strength;
    }

    /// <summary>Asks for a player volume, 0 to 1. <see cref="SoundManager"/> applies, saves and announces it.</summary>
    public struct AudioLevelRequestedSignal : ISignal
    {
        public AudioBus Bus;
        public float Level;
    }

    /// <summary>Announces a player volume after it changed.</summary>
    public struct AudioLevelChangedSignal : ISignal
    {
        public AudioBus Bus;
        public float Level;
    }

    /// <summary>Asks for a screen shake strength, 0 (off) to 1. <see cref="FxManager"/> applies, saves and announces it.</summary>
    public struct ScreenShakeRequestedSignal : ISignal
    {
        public float Strength;
    }

    /// <summary>Asks to show or hide board particles. <see cref="FxManager"/> applies, saves and announces it.</summary>
    public struct BoardEffectsRequestedSignal : ISignal
    {
        public bool Enabled;
    }

    /// <summary>Announces the effect settings after either one changed.</summary>
    public struct FxSettingsChangedSignal : ISignal
    {
        public float ScreenShake;
        public bool BoardEffects;
    }
}
