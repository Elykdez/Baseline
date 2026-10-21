using System;
using System.IO;
using System.Linq;
using Baseline.Demo.RelayRun;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class DemoRexFootFxTests
    {
        DemoRexFootFx fx;
        IDisposable settings;
        string directory;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "Baseline-RexFx-" + Guid.NewGuid().ToString("N"));
            settings = ConfigSettingsPersistence.UseDirectory(directory);
            ConfigSettingsPersistence.SetBoardEffects(true);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Bundles/Demo/RelayRun/Fx/DemoRexFootFx.prefab");
            Assert.That(prefab, Is.Not.Null);
            fx = Object.Instantiate(prefab).GetComponent<DemoRexFootFx>();
            fx.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            if (fx != null) Object.DestroyImmediate(fx.gameObject);
            settings?.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        int Active => fx.puffs.Count(puff => puff.gameObject.activeSelf);

        [Test]
        public void AuthoredSprites_StayInCanvasAndDoNotInterceptInput()
        {
            Assert.That(fx.puffs, Has.Length.EqualTo(24));
            foreach (var puff in fx.puffs)
            {
                Assert.That(puff.sprite, Is.Not.Null);
                Assert.That(puff.raycastTarget, Is.False);
                Assert.That(puff.transform.parent, Is.SameAs(fx.transform));
                Assert.That(puff.rectTransform.pivot, Is.EqualTo(new Vector2(.5f, 0)));
            }
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Bundles/Demo/RelayRun/Art/DemoRexRun.anim");
            var contacts = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == "Footstep").ToArray();
            Assert.That(contacts.Select(e => e.time), Is.EqualTo(new[] { 0f, .25f }));
            Assert.That(contacts.Select(e => e.intParameter), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void RunDust_TracksGroundScrollAndFadesThenReusesAuthoredViews()
        {
            fx.Footstep(fx.transform.TransformPoint(new Vector3(350, 244)));
            var puff = fx.puffs.Single(p => p.gameObject.activeSelf);
            Assert.That(puff.rectTransform.anchoredPosition.y, Is.EqualTo(DemoRunnerModel.GroundY));
            Vector2 before = puff.rectTransform.anchoredPosition;
            fx.Advance(0, 0);
            Assert.That(puff.rectTransform.anchoredPosition, Is.EqualTo(before), "Pausing must freeze existing dust.");
            fx.Advance(.1f, 52);
            Assert.That(puff.rectTransform.anchoredPosition.x, Is.LessThan(before.x - 51));
            Assert.That(puff.color.a, Is.LessThan(.75f));
            fx.Advance(1, 520);
            Assert.That(Active, Is.Zero);
            for (int i = 0; i < 100; i++) fx.Land();
            Assert.That(Active, Is.EqualTo(24));
            Assert.That(fx.transform.childCount, Is.EqualTo(24), "Repeated impacts cannot create more views.");
            fx.Clear();
            Assert.That(Active, Is.Zero);
            fx.Jump();
            Assert.That(Active, Is.EqualTo(3));
        }

        [Test]
        public void JumpAndLand_HaveDistinctBursts_AndRespectVisualSetting()
        {
            fx.Jump();
            Assert.That(Active, Is.EqualTo(3));
            fx.Land();
            Assert.That(Active, Is.EqualTo(9));
            ConfigSettingsPersistence.SetBoardEffects(false);
            fx.Advance(.01f, 0);
            fx.Land();
            fx.Jump();
            fx.Footstep(Vector3.zero);
            Assert.That(Active, Is.Zero);
            ConfigSettingsPersistence.SetBoardEffects(true);
            fx.Land();
            Assert.That(Active, Is.EqualTo(6));
        }
    }
}
