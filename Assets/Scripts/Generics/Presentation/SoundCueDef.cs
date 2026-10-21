using System;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>Which category volume a cue follows; both are also scaled by Master.</summary>
    public enum SoundChannel
    {
        Ui,
        Fx,
    }

    /// <summary>One sound request: its variations, level and pitch spread. Played by <see cref="SoundManager"/>.</summary>
    [CreateAssetMenu(fileName = "SoundCue", menuName = "Baseline/Presentation/Sound Cue")]
    public sealed class SoundCueDef : ScriptableObject
    {
        [SerializeField, Tooltip("One clip per play; with several, the same one never plays twice in a row.")]
        AudioClip[] clips = Array.Empty<AudioClip>();

        [SerializeField, Range(0f, 1f)]
        float volume = 1f;

        [SerializeField]
        SoundChannel channel = SoundChannel.Fx;

        [SerializeField, Tooltip("Random pitch range, inclusive: x is the low end, y the high end.")]
        Vector2 pitch = Vector2.one;

        [SerializeField, Min(0f), Tooltip("Seconds; plays closer together than this are dropped so a burst does not stack.")]
        float minInterval = 0.04f;

        public int ClipCount => clips.Length;
        public float Volume => volume;
        public SoundChannel Channel => channel;
        public float MinInterval => minInterval;

        /// <summary>A clip index other than <paramref name="previous"/> when there is a choice.</summary>
        public int PickIndex(System.Random random, int previous)
        {
            if (clips.Length <= 1)
                return 0;
            int index = random.Next(clips.Length - 1);
            return index >= previous && previous >= 0 ? index + 1 : index;
        }

        public AudioClip ClipAt(int index) => clips.Length == 0 ? null : clips[Mathf.Clamp(index, 0, clips.Length - 1)];

        public float PickPitch(System.Random random)
        {
            float low = Mathf.Min(pitch.x, pitch.y);
            float high = Mathf.Max(pitch.x, pitch.y);
            return Mathf.Lerp(low, high, (float)random.NextDouble());
        }

        /// <summary>Throws when the cue cannot play.</summary>
        public void Validate()
        {
            if (clips.Length == 0)
                throw new InvalidOperationException($"Sound cue '{name}' has no clips.");
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null)
                    throw new InvalidOperationException($"Sound cue '{name}' has an empty clip at {i}.");
            }
            if (Mathf.Min(pitch.x, pitch.y) < 0.5f || Mathf.Max(pitch.x, pitch.y) > 2f)
                throw new InvalidOperationException($"Sound cue '{name}' has a pitch outside 0.5-2.");
        }

        void OnValidate()
        {
            volume = Mathf.Clamp01(volume);
            pitch.x = Mathf.Clamp(pitch.x, 0.5f, 2f);
            pitch.y = Mathf.Clamp(pitch.y, 0.5f, 2f);
            minInterval = Mathf.Max(0f, minInterval);
        }
    }
}
