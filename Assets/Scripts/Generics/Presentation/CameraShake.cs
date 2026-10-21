using Hypocycloid.Core;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>
    /// Trauma-style shake: each kick adds trauma, which decays; the offset grows with its square
    /// and follows smooth noise. The offset is applied on top of wherever the camera was put, so
    /// it composes with code that positions the camera (the board view's fitting); a camera that
    /// places itself every frame can drive Advance and ApplyPose explicitly instead. Kicks arrive as
    /// <see cref="CameraShakeRequestedSignal"/>, already scaled by the player's setting.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : SignalBehaviour
    {
        [SerializeField, Min(0f), Tooltip("World offset at full trauma.")]
        float maxOffset = 0.18f;

        [SerializeField, Min(0f), Tooltip("Degrees of roll at full trauma.")]
        float maxRoll = 1.2f;

        [SerializeField, Min(0.01f), Tooltip("Trauma lost per second.")]
        float decay = 2.2f;

        [SerializeField, Min(0f)]
        float frequency = 22f;

        float trauma;
        Vector3 appliedOffset;
        float appliedRoll;
        float seed;
        float age;

        public float Trauma => trauma;
        public bool ExternallyDriven { get; set; }
        public Vector3 Offset { get; private set; }
        public float Roll { get; private set; }

        void Awake() => seed = Random.value * 100f;

        protected override void OnEnable()
        {
            base.OnEnable();
            Subscribe<CameraShakeRequestedSignal>(signal => Shake(signal.Strength));
        }

        public void Shake(float strength) => trauma = Mathf.Clamp01(trauma + strength);

        public void Advance(float deltaSeconds)
        {
            age += Mathf.Max(0, deltaSeconds);
            trauma = Mathf.Max(0f, trauma - decay * Mathf.Max(0, deltaSeconds));
            float t = age * frequency;
            float amount = trauma * trauma;
            Offset = new Vector3(
                maxOffset * amount * (Mathf.PerlinNoise(seed, t) * 2f - 1f),
                maxOffset * amount * (Mathf.PerlinNoise(seed + 1f, t) * 2f - 1f),
                0f
            );
            Roll = maxRoll * amount * (Mathf.PerlinNoise(seed + 2f, t) * 2f - 1f);
        }

        public void ApplyPose(Vector3 position, Quaternion rotation, float offsetScale = 1)
        {
            appliedOffset = rotation * Offset * offsetScale;
            appliedRoll = Roll;
            transform.SetPositionAndRotation(position + appliedOffset, rotation * Quaternion.Euler(0, 0, appliedRoll));
        }

        public void Clear()
        {
            transform.position -= appliedOffset;
            transform.rotation *= Quaternion.Euler(0, 0, -appliedRoll);
            appliedOffset = Offset = Vector3.zero;
            appliedRoll = Roll = trauma = 0;
        }

        void LateUpdate()
        {
            if (ExternallyDriven) return;
            Vector3 position = transform.position - appliedOffset;
            Quaternion rotation = transform.rotation * Quaternion.Euler(0, 0, -appliedRoll);
            Advance(Time.unscaledDeltaTime);
            ApplyPose(position, rotation);
        }

        protected override void OnDisable()
        {
            Clear();
            base.OnDisable();
        }
    }
}
