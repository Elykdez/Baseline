using Baseline.Demo.RelayRun;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class DemoRexRigTests
    {
        GameObject rig;
        AnimationClip run;

        [SetUp]
        public void SetUp()
        {
            const string art = "Assets/Bundles/Demo/RelayRun/Art/";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(art + "DemoRexRig.prefab");
            Assert.That(prefab, Is.Not.Null);
            run = AssetDatabase.LoadAssetAtPath<AnimationClip>(art + "DemoRexRun.anim");
            Assert.That(run, Is.Not.Null);
            rig = Object.Instantiate(prefab);
            rig.GetComponent<Animator>().enabled = false;
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(rig);

        Vector3 Ankle(string leg) => rig.transform.InverseTransformPoint(rig.transform.Find(leg + "/Thigh/Shin/Foot").position);

        [Test]
        public void FootstepsOnlyRequestEffectsDuringGroundedRunning()
        {
            var animation = rig.GetComponent<DemoRexAnimation>();
            Assert.That(animation.footstepCue, Is.Not.Null);
            Assert.DoesNotThrow(animation.footstepCue.Validate);
            Assert.That(animation.footstepCue.Channel, Is.EqualTo(Baseline.Presentation.SoundChannel.Fx));
            int contacts = 0;
            System.Action<Baseline.Presentation.SoundRequestedSignal> listener = signal =>
            {
                Assert.That(signal.Cue, Is.SameAs(animation.footstepCue));
                contacts++;
            };
            Hypocycloid.Core.SignalBus.Subscribe(listener);
            try
            {
                animation.Present(false, false, 520);
                animation.Footstep(0);
                animation.Present(true, true, 520);
                animation.Footstep(1);
                Assert.That(contacts, Is.Zero, "Paused and airborne feet must not make running sounds.");
                animation.Present(true, false, 520);
                animation.Footstep(0);
                animation.Footstep(1);
                Assert.That(contacts, Is.EqualTo(2));
            }
            finally { Hypocycloid.Core.SignalBus.Unsubscribe(listener); }
        }

        [Test]
        public void CollisionCenter_MatchesTheAuthoredHipInsteadOfTheRigOrigin()
        {
            run.SampleAnimation(rig, 0);
            Vector3 hip = rig.transform.InverseTransformPoint(rig.transform.Find("Body").position);
            Assert.That(hip.x, Is.EqualTo(DemoRunnerModel.RunnerBodyOffset).Within(.1f));
        }

        // Lowest point of the sprites' tight meshes between two x positions, relative to the rig origin.
        float LowestVisiblePoint(float left, float right)
        {
            float lowest = float.PositiveInfinity;
            foreach (Image image in rig.GetComponentsInChildren<Image>())
            {
                Sprite sprite = image.sprite;
                if (!image.enabled || sprite == null) continue;
                Rect rect = image.rectTransform.rect;
                Vector3[] points = sprite.vertices.Select(vertex => image.rectTransform.TransformPoint(rect.min + Vector2.Scale(
                    (vertex * sprite.pixelsPerUnit + sprite.pivot) / sprite.rect.size, rect.size)) - rig.transform.position).ToArray();
                ushort[] triangles = sprite.triangles;
                for (int i = 0; i < triangles.Length; i++)
                {
                    // Clip each triangle edge to the strip: its lowest point is a vertex or a boundary crossing.
                    Vector3 a = points[triangles[i]], b = points[triangles[i - i % 3 + (i + 1) % 3]];
                    if (a.x >= left && a.x <= right) lowest = Mathf.Min(lowest, a.y);
                    foreach (float edge in new[] { left, right })
                        if ((a.x - edge) * (b.x - edge) < 0)
                            lowest = Mathf.Min(lowest, Mathf.Lerp(a.y, b.y, (edge - a.x) / (b.x - a.x)));
                }
            }
            return lowest;
        }

        [Test]
        public void JumpPose_VisibleFeetMatchTheModelsFootClearance()
        {
            var jump = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Bundles/Demo/RelayRun/Art/DemoRexJump.anim");
            Assert.That(jump, Is.Not.Null);
            var tuning = new DemoRunnerTuning();
            var model = new DemoRunnerModel(tuning, 17);
            float contact = tuning.obstacleHeight - DemoRunnerModel.HitForgiveness;
            float left = DemoRunnerModel.RunnerBodyOffset - DemoRunnerModel.RunnerWidth * .5f;
            int sampled = 0;
            // The animator plays Jump from the press, and the game tilts the rig with vertical velocity.
            model.Jump();
            for (int step = 1; step / 120f < jump.length; step++)
            {
                model.Advance(1f / 120);
                float feet = model.Height + model.FootClearance;
                // Only poses near contact height decide whether a jump clears debris.
                if (model.Grounded || Mathf.Abs(feet - contact) > 15) continue;
                jump.SampleAnimation(rig, step / 120f);
                rig.transform.rotation = Quaternion.Euler(0, 0, Mathf.Clamp(model.Velocity * .012f, -10, 10));
                bool rising = model.Velocity > 0;
                // While falling, the front foot reaches ahead to land; the hit shape forgives it.
                float right = rising ? DemoRunnerModel.RunnerBodyOffset + DemoRunnerModel.RunnerWidth * .5f : 48;
                float lowest = LowestVisiblePoint(left, right);
                Assert.That(lowest, Is.LessThanOrEqualTo(model.FootClearance + DemoRunnerModel.HitForgiveness),
                    $"At {step / 120f:0.000}s the feet are visibly higher than the hit shape, so clear jumps would crash.");
                if (rising)
                    Assert.That(lowest, Is.GreaterThan(model.FootClearance - 12),
                        $"At {step / 120f:0.000}s the folded feet hang well below the hit shape.");
                sampled++;
            }
            Assert.That(sampled, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void Touchdown_ResumesTheStrideUnderTheImpactSquash()
        {
            var animator = rig.GetComponent<Animator>();
            animator.enabled = true;
            animator.Rebind();
            var animation = rig.GetComponent<DemoRexAnimation>();
            animation.Present(true, false, 520);
            animation.Jump();
            for (int i = 0; i < 94; i++)
            {
                animation.Present(true, true, 520);
                animator.Update(1f / 120);
            }
            animation.Land();
            void Step(int frames)
            {
                for (int i = 0; i < frames; i++)
                {
                    animation.Present(true, false, 520);
                    animator.Update(1f / 120);
                }
            }
            Step(1);
            Vector3 contact = Ankle("NearHip");
            Step(4);
            Assert.That(rig.transform.localScale.y, Is.LessThan(.9f), "Touchdown should still squash the body.");
            Step(8);
            Assert.That(contact.x - Ankle("NearHip").x, Is.GreaterThan(25),
                "The planted foot must travel back with the ground instead of sliding.");
            // Long after the squash, the stride must keep cycling rather than hold the impact layer's last pose.
            Step(60);
            float stride = float.PositiveInfinity, reach = float.NegativeInfinity;
            for (int i = 0; i < 60; i++)
            {
                Step(1);
                stride = Mathf.Min(stride, Ankle("NearHip").x);
                reach = Mathf.Max(reach, Ankle("NearHip").x);
            }
            Assert.That(reach - stride, Is.GreaterThan(50), "The run cycle froze after the landing squash.");
            Assert.That(animator.GetLayerWeight(1), Is.Zero, "The idle impact layer must not hold the pose.");
        }

        [Test]
        public void SourceArt_ImportsOneBodyAndThreeSeparateCroppedLegPieces()
        {
            const string art = "Assets/Bundles/Demo/RelayRun/Art/";
            Sprite[] body = AssetDatabase.LoadAllAssetsAtPath(art + "DemoRexBody.png").OfType<Sprite>().ToArray();
            Assert.That(body, Has.Length.EqualTo(1));
            Assert.That(body[0].rect.height, Is.LessThan(body[0].texture.height), "Transparent body padding must be cropped.");
            Sprite[] parts = AssetDatabase.LoadAllAssetsAtPath(art + "DemoRexLegParts.png").OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            Assert.That(parts, Has.Length.EqualTo(3));
            for (int i = 0; i < parts.Length; i++)
            {
                Sprite piece = parts[i];
                Assert.That(piece.name, Is.EqualTo("DemoRexLegParts" + i));
                float cellWidth = piece.texture.width / 3f;
                Assert.That(piece.rect.xMin, Is.GreaterThanOrEqualTo(i * cellWidth));
                Assert.That(piece.rect.xMax, Is.LessThanOrEqualTo((i + 1) * cellWidth));
                Assert.That(piece.rect.width, Is.LessThan(cellWidth));
            }
        }

        [Test]
        public void AuthoredRun_AlternatesWhichLegIsForwardAndPlanted()
        {
            run.SampleAnimation(rig, 0);
            Vector3 nearContact = Ankle("NearHip"), farSwing = Ankle("FarHip");
            Assert.That(nearContact.x - farSwing.x, Is.GreaterThan(40), "Near leg contacts ahead while far leg swings behind.");
            Assert.That(farSwing.y - nearContact.y, Is.GreaterThan(10));
            run.SampleAnimation(rig, .25f);
            Vector3 nearSwing = Ankle("NearHip"), farContact = Ankle("FarHip");
            Assert.That(farContact.x - nearSwing.x, Is.GreaterThan(40), "Half a cycle later the far leg must lead.");
            Assert.That(nearSwing.y - farContact.y, Is.GreaterThan(10));
            Assert.That(farContact.y, Is.EqualTo(nearContact.y).Within(.2f));
            Assert.That(nearSwing.y, Is.EqualTo(farSwing.y).Within(.2f));
        }

        [Test]
        public void AuthoredRun_PlantedFootMovesBackThenFoldsUpForRecovery()
        {
            run.SampleAnimation(rig, 0);
            Vector3 contact = Ankle("NearHip");
            run.SampleAnimation(rig, .1875f);
            Vector3 pushOff = Ankle("NearHip");
            Assert.That(contact.x - pushOff.x, Is.GreaterThan(50));
            Assert.That(pushOff.y, Is.EqualTo(contact.y).Within(.5f));
            run.SampleAnimation(rig, .3125f);
            Assert.That(Ankle("NearHip").y, Is.GreaterThan(contact.y + 25));
            Assert.That(rig.transform.Find("FarHip").GetSiblingIndex(), Is.LessThan(rig.transform.Find("Body").GetSiblingIndex()));
            Assert.That(rig.transform.Find("NearHip").GetSiblingIndex(), Is.GreaterThan(rig.transform.Find("Body").GetSiblingIndex()));
            Assert.That(rig.transform.Find("FarHip/Thigh").GetComponent<Image>().color.r,
                Is.LessThan(rig.transform.Find("NearHip/Thigh").GetComponent<Image>().color.r));
        }
    }
}
