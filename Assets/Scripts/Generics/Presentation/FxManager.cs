using System.Collections;
using Hypocycloid.Core;
using Hypocycloid.Utils;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>
    /// Plays <see cref="FxEventDef"/>s and lives across scenes, like the <see cref="SoundManager"/>.
    /// An <see cref="FxRequestedSignal"/> spawns the event's visual from the <see cref="ObjectPool"/>
    /// and returns it after its lifetime, asks for its sound and kicks the camera
    /// (<see cref="CameraShakeRequestedSignal"/>, for whichever <see cref="CameraShake"/> the scene has).
    /// <para>
    /// It owns the player's effect settings: screen shake strength scales every kick (0 turns them
    /// off) and board effects can hide the visuals; requests are applied, saved and announced as
    /// <see cref="FxSettingsChangedSignal"/>. Presentation only; it never changes game state.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FxManager : MonoSingleton<FxManager>
    {
        // Discard the whole duplicate host when a later scene supplies another manager.
        protected override bool DestroyDuplicateObject => true;

        protected override void Init()
        {
            base.Init();
            SignalBus.Subscribe<FxRequestedSignal>(OnFxRequested, this);
            SignalBus.Subscribe<ScreenShakeRequestedSignal>(OnScreenShakeRequested, this);
            SignalBus.Subscribe<BoardEffectsRequestedSignal>(OnBoardEffectsRequested, this);
        }

        protected override void OnDestroy()
        {
            SignalBus.Unsubscribe<FxRequestedSignal>(OnFxRequested, this);
            SignalBus.Unsubscribe<ScreenShakeRequestedSignal>(OnScreenShakeRequested, this);
            SignalBus.Unsubscribe<BoardEffectsRequestedSignal>(OnBoardEffectsRequested, this);
            base.OnDestroy();
        }

        void OnFxRequested(FxRequestedSignal signal) => Play(signal.Fx, signal.Position);

        /// <summary>Plays an event at a world position. A null event is ignored.</summary>
        public void Play(FxEventDef def, Vector3 position)
        {
            if (def == null)
                return;
            if (def.Vfx != null && ConfigSettingsPersistence.BoardEffects)
                StartCoroutine(PlayVisual(def.Vfx, position, def.VfxLifetime));
            SignalBus.Fire(new SoundRequestedSignal { Cue = def.Sound }, this);
            float shake = def.CameraShake * ConfigSettingsPersistence.ScreenShake;
            if (shake > 0f)
                SignalBus.Fire(new CameraShakeRequestedSignal { Strength = shake }, this);
        }

        IEnumerator PlayVisual(GameObject prefab, Vector3 position, float lifetime)
        {
            // Parented here, so a visual outlives a scene change and still finds its way back.
            GameObject instance = ObjectPool.Ins.Spawn(prefab, transform);
            instance.transform.position = position;
            foreach (ParticleSystem particles in instance.GetComponentsInChildren<ParticleSystem>(true))
            {
                particles.Clear(true);
                particles.Play(true);
            }

            yield return new WaitForSeconds(lifetime);
            if (instance != null && ObjectPool.HasInstance)
                ObjectPool.Ins.Despawn(instance);
        }

        // -------- player settings --------

        void OnScreenShakeRequested(ScreenShakeRequestedSignal signal)
        {
            ConfigSettingsPersistence.SetScreenShake(signal.Strength);
            Announce();
        }

        void OnBoardEffectsRequested(BoardEffectsRequestedSignal signal)
        {
            ConfigSettingsPersistence.SetBoardEffects(signal.Enabled);
            Announce();
        }

        void Announce() =>
            SignalBus.Fire(
                new FxSettingsChangedSignal
                {
                    ScreenShake = ConfigSettingsPersistence.ScreenShake,
                    BoardEffects = ConfigSettingsPersistence.BoardEffects,
                },
                this
            );
    }
}
