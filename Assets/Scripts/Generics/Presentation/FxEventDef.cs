using System;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>One authored moment of feedback: a pooled visual, a sound and an optional camera kick.</summary>
    [CreateAssetMenu(fileName = "FxEvent", menuName = "Baseline/Presentation/Fx Event")]
    public sealed class FxEventDef : ScriptableObject
    {
        [SerializeField, Tooltip("Optional particle prefab spawned at the event position.")]
        GameObject vfx;

        [SerializeField, Min(0.05f), Tooltip("Seconds before the visual returns to its pool.")]
        float vfxLifetime = 1.5f;

        [SerializeField, Tooltip("Optional sound played with the visual.")]
        SoundCueDef sound;

        [SerializeField, Range(0f, 1f), Tooltip("Strength passed to the camera shake. 0 leaves the camera still.")]
        float cameraShake;

        public GameObject Vfx => vfx;
        public float VfxLifetime => vfxLifetime;
        public SoundCueDef Sound => sound;
        public float CameraShake => cameraShake;

        /// <summary>Throws when the event would do nothing or cannot play.</summary>
        public void Validate()
        {
            if (vfx == null && sound == null && cameraShake <= 0f)
                throw new InvalidOperationException($"Fx event '{name}' has no visual, sound or shake.");
            sound?.Validate();
        }
    }
}
