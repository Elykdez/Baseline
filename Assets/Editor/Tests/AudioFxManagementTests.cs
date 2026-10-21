using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Baseline.Presentation;
using Hypocycloid.Core;
using Hypocycloid.Utils;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class AudioFxManagementTests
    {
        const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        readonly List<Object> created = new();
        string directory;
        IDisposable configuration;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "BaselineAudioFx-" + Guid.NewGuid().ToString("N"));
            configuration = ConfigSettingsPersistence.UseDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = created.Count - 1; i >= 0; i--)
                if (created[i] != null) Object.DestroyImmediate(created[i]);
            created.Clear();
            configuration.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        T Track<T>(T value) where T : Object { created.Add(value); return value; }
        T Definition<T>() where T : ScriptableObject => Track(ScriptableObject.CreateInstance<T>());
        AudioClip Clip(string name) => Track(AudioClip.Create(name, 4410, 1, 44100, false));
        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, PrivateInstance).SetValue(target, value);
        static object Call(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, PrivateInstance).Invoke(target, arguments);

        [Test]
        public void AudioAndFxSettings_RoundTripAndClampWithoutChangingCategoryLevels()
        {
            ConfigSettingsPersistence.SetMusicVolume(0.4f);
            ConfigSettingsPersistence.SetSfxVolume(0.8f);
            ConfigSettingsPersistence.SetUiVolume(0.6f);
            ConfigSettingsPersistence.SetMasterVolume(0.5f);
            ConfigSettingsPersistence.SetScreenShake(0.25f);
            ConfigSettingsPersistence.SetBoardEffects(false);
            ConfigSettingsPersistence.Reload();
            Assert.That(SoundManager.EffectiveLevel(AudioBus.Music), Is.EqualTo(0.2f).Within(0.0001f));
            Assert.That(SoundManager.EffectiveLevel(AudioBus.Effects), Is.EqualTo(0.4f).Within(0.0001f));
            Assert.That(SoundManager.EffectiveLevel(AudioBus.Interface), Is.EqualTo(0.3f).Within(0.0001f));
            Assert.That(ConfigSettingsPersistence.ScreenShake, Is.EqualTo(0.25f));
            Assert.That(ConfigSettingsPersistence.BoardEffects, Is.False);
            ConfigSettingsPersistence.SetMasterVolume(-1f);
            Assert.That(SoundManager.EffectiveLevel(AudioBus.Music), Is.Zero);
            Assert.That(SoundManager.Level(AudioBus.Music), Is.EqualTo(0.4f));
            ConfigSettingsPersistence.SetMasterVolume(2f);
            ConfigSettingsPersistence.SetUiVolume(-2f);
            ConfigSettingsPersistence.SetScreenShake(2f);
            Assert.That(ConfigSettingsPersistence.MasterVolume, Is.EqualTo(1f));
            Assert.That(ConfigSettingsPersistence.UiVolume, Is.Zero);
            Assert.That(ConfigSettingsPersistence.ScreenShake, Is.EqualTo(1f));
        }

        [Test]
        public void SoundManager_HandlesVolumeSignalsAndUnsubscribesOnDestruction()
        {
            int before = SignalBus.GetSubscriberCount<AudioLevelRequestedSignal>();
            SoundManager manager = SoundManager.Ins;
            Track(manager.gameObject);
            AudioLevelChangedSignal changed = default;
            int notices = 0;
            Action<AudioLevelChangedSignal> listener = value => { changed = value; notices++; };
            SignalBus.Subscribe(listener);
            try
            {
                Assert.That(SignalBus.GetSubscriberCount<AudioLevelRequestedSignal>(), Is.EqualTo(before + 1));
                SignalBus.Fire(new AudioLevelRequestedSignal { Bus = AudioBus.Interface, Level = 0.3f });
                Assert.That(ConfigSettingsPersistence.UiVolume, Is.EqualTo(0.3f));
                Assert.That(changed.Bus, Is.EqualTo(AudioBus.Interface));
                Assert.That(changed.Level, Is.EqualTo(0.3f));
                Assert.That(notices, Is.EqualTo(1));
                Object.DestroyImmediate(manager.gameObject);
                Assert.That(SignalBus.GetSubscriberCount<AudioLevelRequestedSignal>(), Is.EqualTo(before));
            }
            finally { SignalBus.Unsubscribe(listener); }
        }

        [Test]
        public void DialogVoice_UsesOneSourceAndOnlyItsCurrentOwnerCanStopIt()
        {
            ConfigSettingsPersistence.SetMasterVolume(1f);
            ConfigSettingsPersistence.SetSfxVolume(0.8f);
            SoundManager manager = SoundManager.Ins;
            Track(manager.gameObject);
            var firstOwner = Track(new GameObject("First dialog owner"));
            var secondOwner = Track(new GameObject("Second dialog owner"));
            var first = Definition<SoundCueDef>();
            var second = Definition<SoundCueDef>();
            Set(first, "clips", new[] { Clip("First voice") });
            Set(second, "clips", new[] { Clip("Second voice") });
            SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = firstOwner, Cue = first });
            AudioSource source = Array.Find(manager.GetComponentsInChildren<AudioSource>(), item => item.name == "Dialog Voice");
            Assert.That(source, Is.Not.Null);
            Assert.That(source.clip, Is.SameAs(first.ClipAt(0)));
            SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = secondOwner, Cue = second });
            Assert.That(source.clip, Is.SameAs(second.ClipAt(0)));
            Assert.That(Array.FindAll(manager.GetComponentsInChildren<AudioSource>(), item => item.name == "Dialog Voice"), Has.Length.EqualTo(1));
            SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = firstOwner });
            Assert.That(source.clip, Is.SameAs(second.ClipAt(0)));
            SignalBus.Fire(new AudioLevelRequestedSignal { Bus = AudioBus.Effects, Level = 0.2f });
            Assert.That(source.volume, Is.EqualTo(0.2f).Within(0.0001f));
            SignalBus.Fire(new DialogVoiceRequestedSignal { Owner = secondOwner });
            Assert.That(source.clip, Is.Null);
        }

        [Test]
        public void FxManager_ChangesSettingsAndRoutesSoundAndScaledShake()
        {
            FxManager manager = FxManager.Ins;
            Track(manager.gameObject);
            var cue = Definition<SoundCueDef>();
            var effect = Definition<FxEventDef>();
            Set(effect, "sound", cue);
            Set(effect, "cameraShake", 0.8f);
            SoundCueDef heard = null;
            float shake = -1f;
            int changes = 0;
            Action<SoundRequestedSignal> soundListener = signal => heard = signal.Cue;
            Action<CameraShakeRequestedSignal> shakeListener = signal => shake = signal.Strength;
            Action<FxSettingsChangedSignal> settingsListener = _ => changes++;
            SignalBus.Subscribe(soundListener);
            SignalBus.Subscribe(shakeListener);
            SignalBus.Subscribe(settingsListener);
            try
            {
                SignalBus.Fire(new ScreenShakeRequestedSignal { Strength = 0.25f });
                SignalBus.Fire(new BoardEffectsRequestedSignal { Enabled = false });
                SignalBus.Fire(new FxRequestedSignal { Fx = effect, Position = Vector3.one });
                Assert.That(changes, Is.EqualTo(2));
                Assert.That(ConfigSettingsPersistence.BoardEffects, Is.False);
                Assert.That(heard, Is.SameAs(cue));
                Assert.That(shake, Is.EqualTo(0.2f).Within(0.0001f));
                shake = -1f;
                SignalBus.Fire(new ScreenShakeRequestedSignal { Strength = 0f });
                manager.Play(effect, Vector3.zero);
                Assert.That(shake, Is.EqualTo(-1f), "Disabled camera shake must not send a kick.");
            }
            finally
            {
                SignalBus.Unsubscribe(soundListener);
                SignalBus.Unsubscribe(shakeListener);
                SignalBus.Unsubscribe(settingsListener);
            }
        }

        [Test]
        public void FxVisual_ReturnsToPoolAfterItsLifetimeYieldAndCanBeReused()
        {
            FxManager manager = FxManager.Ins;
            Track(manager.gameObject);
            var prefab = Track(new GameObject("Transient FX template"));
            ObjectPool pool = ObjectPool.Ins;
            Track(pool.gameObject);
            var sequence = (IEnumerator)Call(manager, "PlayVisual", prefab, new Vector3(2, 3, 4), 0.1f);
            Assert.That(sequence.MoveNext(), Is.True);
            Assert.That(sequence.Current, Is.TypeOf<WaitForSeconds>());
            var instance = manager.GetComponentInChildren<PooledObject>();
            Assert.That(instance, Is.Not.Null);
            Assert.That(instance.transform.position, Is.EqualTo(new Vector3(2, 3, 4)));
            Assert.That(sequence.MoveNext(), Is.False);
            Assert.That(instance.gameObject.activeSelf, Is.False);
            GameObject reused = pool.Spawn(prefab, manager.transform);
            Assert.That(reused, Is.SameAs(instance.gameObject));
            pool.Despawn(reused);
        }

        [Test]
        public void Playlist_VisitsEveryTrackAndAvoidsRepeatingAtCycleBoundaries()
        {
            var tracks = new[] { Clip("A"), Clip("B"), Clip("C") };
            var playlist = new BgmPlaylist(tracks, new System.Random(123));
            AudioClip previous = null;
            for (int cycle = 0; cycle < 5; cycle++)
            {
                var heard = new HashSet<AudioClip>();
                for (int i = 0; i < tracks.Length; i++)
                {
                    AudioClip next = playlist.Next();
                    Assert.That(next, Is.Not.SameAs(previous));
                    Assert.That(heard.Add(next), Is.True);
                    previous = next;
                }
            }
            Assert.That(new BgmPlaylist(Array.Empty<AudioClip>()).Next(), Is.Null);
        }

        [Test]
        public void Cue_PicksDistinctVariationsAndPitchWithinAuthoredRange()
        {
            var cue = Definition<SoundCueDef>();
            Set(cue, "clips", new[] { Clip("A"), Clip("B"), Clip("C") });
            Set(cue, "pitch", new Vector2(0.8f, 1.2f));
            cue.Validate();
            var random = new System.Random(123);
            int previous = -1;
            for (int i = 0; i < 30; i++)
            {
                int next = cue.PickIndex(random, previous);
                Assert.That(next, Is.InRange(0, 2));
                Assert.That(next, Is.Not.EqualTo(previous));
                Assert.That(cue.PickPitch(random), Is.InRange(0.8f, 1.2f));
                previous = next;
            }
        }

        [Test]
        public void CameraShake_AccumulatesClampsAndStopsListeningWhenDisabled()
        {
            var host = Track(new GameObject("Transient camera"));
            var camera = host.AddComponent<CameraShake>();
            Call(camera, "OnEnable");
            try
            {
                SignalBus.Fire(new CameraShakeRequestedSignal { Strength = 0.4f });
                Assert.That(camera.Trauma, Is.EqualTo(0.4f));
                SignalBus.Fire(new CameraShakeRequestedSignal { Strength = 0.8f });
                Assert.That(camera.Trauma, Is.EqualTo(1f));
                Call(camera, "OnDisable");
                camera.Shake(-1f);
                SignalBus.Fire(new CameraShakeRequestedSignal { Strength = 0.5f });
                Assert.That(camera.Trauma, Is.Zero);
            }
            finally { Call(camera, "OnDisable"); }
        }

        [Test]
        public void CameraShake_SignalledEffectShakesTheCameraAndSettlesWithoutDrift()
        {
            const float step = 1f / 60;
            var host = Track(new GameObject("Transient camera", typeof(Camera)));
            var position = new Vector3(3, 4, -10);
            Quaternion rotation = Quaternion.Euler(0, 0, 30);
            host.transform.SetPositionAndRotation(position, rotation);
            var camera = host.AddComponent<CameraShake>();
            Call(camera, "OnEnable");
            FxManager manager = FxManager.Ins;
            Track(manager.gameObject);
            var effect = Definition<FxEventDef>();
            Set(effect, "cameraShake", 1f);
            ConfigSettingsPersistence.SetScreenShake(1f);
            try
            {
                SignalBus.Fire(new FxRequestedSignal { Fx = effect, Position = Vector3.zero });
                Assert.That(camera.Trauma, Is.EqualTo(1f));
                float peak = 0f;
                for (int frame = 0; frame < 60; frame++)
                {
                    camera.Advance(step);
                    camera.ApplyPose(position, rotation);
                    Assert.That(Vector3.Distance(host.transform.position, position + rotation * camera.Offset), Is.LessThan(1e-5f));
                    peak = Mathf.Max(peak, Vector3.Distance(host.transform.position, position) + Quaternion.Angle(host.transform.rotation, rotation));
                }
                Assert.That(peak, Is.GreaterThan(1e-3f), "The kick must move the camera.");
                Assert.That(camera.Trauma, Is.Zero, "A full kick settles within a second.");
                Assert.That(Vector3.Distance(host.transform.position, position), Is.LessThan(1e-5f));

                // Left to itself, the shake rides on whatever else moves the camera and leaves no offset behind.
                SignalBus.Fire(new FxRequestedSignal { Fx = effect, Position = Vector3.zero });
                for (int frame = 0; frame < 10; frame++)
                {
                    Call(camera, "LateUpdate");
                    host.transform.position += Vector3.right;
                }
                Call(camera, "OnDisable");
                Assert.That(camera.Trauma, Is.Zero);
                Assert.That(Vector3.Distance(host.transform.position, position + Vector3.right * 10), Is.LessThan(1e-4f));
                Assert.That(Quaternion.Angle(host.transform.rotation, rotation), Is.LessThan(1e-3f));
            }
            finally { Call(camera, "OnDisable"); }
        }
    }
}
