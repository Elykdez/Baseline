using System;
using Baseline.Demo.RelayRun;
using NUnit.Framework;

namespace Baseline.Tests.Editor
{
    public sealed class DemoRunnerModelTests
    {
        [Test]
        public void NoJump_CrashesAtFirstObstacle_AndTerminalStateDoesNotAdvance()
        {
            var model = new DemoRunnerModel(new DemoRunnerTuning(), 17);
            model.Advance(5);
            Assert.That(model.Outcome, Is.EqualTo(DemoRunnerOutcome.Crashed));
            Assert.That(model.Distance, Is.InRange(20, 30));
            float distance = model.Distance;
            model.Jump();
            model.Advance(10);
            Assert.That(model.Distance, Is.EqualTo(distance));
            Assert.That(model.Jumps, Is.Zero);
        }

        [Test]
        public void Jump_HasClearanceAndCannotDoubleJump()
        {
            var model = new DemoRunnerModel(new DemoRunnerTuning(), 17);
            model.Jump();
            model.Advance(DemoRunnerModel.JumpWindupSeconds + .30f);
            Assert.That(model.Height, Is.GreaterThan(140));
            model.Jump();
            model.Advance(.45f);
            Assert.That(model.Grounded, Is.True);
            Assert.That(model.Jumps, Is.EqualTo(1));
        }

        [Test]
        public void JumpPressedJustBeforeLanding_IsBuffered()
        {
            var model = new DemoRunnerModel(new DemoRunnerTuning(), 17);
            model.Jump();
            model.Advance(DemoRunnerModel.JumpWindupSeconds + .65f);
            model.Jump();
            // The buffered takeoff begins its own wind-up on landing.
            model.Advance(.14f + DemoRunnerModel.JumpWindupSeconds);
            Assert.That(model.Jumps, Is.EqualTo(2));
            Assert.That(model.Height, Is.GreaterThan(20));
            Assert.That(model.Landings, Is.EqualTo(1), "The landing must survive the buffered takeoff in the same Advance.");
        }

        [Test]
        public void Landings_CountOnlyAirToGroundContacts()
        {
            var model = new DemoRunnerModel(new DemoRunnerTuning(), 17);
            model.Advance(.2f);
            Assert.That(model.Landings, Is.Zero);
            model.Jump();
            model.Advance(1);
            Assert.That(model.Landings, Is.EqualTo(1));
            model.Advance(.2f);
            Assert.That(model.Landings, Is.EqualTo(1));
            Assert.That(new DemoRunnerModel(new DemoRunnerTuning(), 17).Landings, Is.Zero);
        }

        // Appearance 0 is the crate; 1 is the pylon, whose leading rubble is far below its spire.
        [TestCase(0, 150, DemoRunnerOutcome.Crashed)]
        [TestCase(0, 185, DemoRunnerOutcome.Running)] // Folded feet clear the lid; the rig origin would not.
        [TestCase(0, 210, DemoRunnerOutcome.Running)]
        [TestCase(0, 300, DemoRunnerOutcome.Running)] // Falling feet stay folded over the back edge.
        [TestCase(0, 365, DemoRunnerOutcome.Crashed)]
        [TestCase(1, 115, DemoRunnerOutcome.Crashed)]
        [TestCase(1, 150, DemoRunnerOutcome.Running)]
        [TestCase(1, 385, DemoRunnerOutcome.Crashed)]
        public void JumpTiming_DetectsLateTakeoffAndLanding_ButAllowsClearance(int appearance, int lead, DemoRunnerOutcome expected)
        {
            DemoRunnerModel model = null;
            for (int seed = 0; model == null || model.Obstacles[0].Appearance != appearance; seed++)
                model = new DemoRunnerModel(new DemoRunnerTuning(), seed);
            while (model.Obstacles[0].X - DemoRunnerModel.RunnerX > lead)
                model.Advance(1f / 120);
            model.Jump();
            model.Advance(1);
            Assert.That(model.Outcome, Is.EqualTo(expected));
            if (expected == DemoRunnerOutcome.Running) Assert.That(model.Cleared, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(17)]
        [TestCase(9876)]
        public void AuthoredCourse_CanBeCompletedWithOneJumpPerObstacle(int seed)
        {
            var settings = new DemoRunnerTuning();
            var model = new DemoRunnerModel(settings, seed);
            for (int frame = 0; frame < 9000 && model.Outcome == DemoRunnerOutcome.Running; frame++)
            {
                if (model.Grounded)
                    foreach (DemoRunnerModel.Obstacle obstacle in model.Obstacles)
                        if (obstacle.Active && obstacle.X > DemoRunnerModel.RunnerX
                            && obstacle.X - DemoRunnerModel.RunnerX < 210)
                        {
                            model.Jump();
                            break;
                        }
                model.Advance(1f / 120);
            }
            Assert.That(model.Outcome, Is.EqualTo(DemoRunnerOutcome.Complete));
            Assert.That(model.Distance, Is.EqualTo(settings.goalDistance));
            Assert.That(model.Elapsed, Is.InRange(50, 65));
            Assert.That(model.Cleared, Is.GreaterThan(20));
        }

        [Test]
        public void Restart_ResetsHeightProgressAndObstacles()
        {
            var settings = new DemoRunnerTuning();
            var first = new DemoRunnerModel(settings, 17);
            first.Advance(5);
            var retry = new DemoRunnerModel(settings, 17);
            Assert.That(first.Outcome, Is.EqualTo(DemoRunnerOutcome.Crashed));
            Assert.That(retry.Outcome, Is.EqualTo(DemoRunnerOutcome.Running));
            Assert.That(retry.Distance, Is.Zero);
            Assert.That(retry.Height, Is.Zero);
            Assert.That(retry.Cleared, Is.Zero);
            Assert.That(retry.Obstacles[0].X, Is.EqualTo(1720));
        }

        [Test]
        public void CoarseFrames_CannotTunnelThroughObstacles()
        {
            var coarse = new DemoRunnerModel(new DemoRunnerTuning(), 17);
            var fine = new DemoRunnerModel(new DemoRunnerTuning(), 17);
            coarse.Advance(8);
            for (int i = 0; i < 960; i++) fine.Advance(1f / 120);
            Assert.That(coarse.Outcome, Is.EqualTo(fine.Outcome));
            Assert.That(coarse.Distance, Is.EqualTo(fine.Distance).Within(.02f));
        }

        [Test]
        public void LiveAttempt_CopiesTuningInsteadOfReadingMutableAssetData()
        {
            var settings = new DemoRunnerTuning();
            var model = new DemoRunnerModel(settings, 17);
            settings.speed = 10000;
            settings.goalDistance = 1;
            model.Advance(.5f);
            Assert.That(model.Speed, Is.LessThan(530));
            Assert.That(model.Outcome, Is.EqualTo(DemoRunnerOutcome.Running));
        }

        [Test]
        public void ImpossibleObstacleSpacingOrHeight_IsRejected()
        {
            var settings = new DemoRunnerTuning { minGap = 100 };
            Assert.Throws<InvalidOperationException>(settings.Validate);
            settings = new DemoRunnerTuning { obstacleHeight = 500 };
            Assert.Throws<InvalidOperationException>(settings.Validate);
            settings = new DemoRunnerTuning { obstacleWidth = 500, minGap = 2000, maxGap = 2500 };
            Assert.Throws<InvalidOperationException>(settings.Validate);
        }
    }
}
