using System;
using System.Linq;
using Baseline.Defs;
using UnityEngine;

namespace Baseline.Demo.RelayRun
{
    [Serializable]
    public sealed class DemoRunnerTuning
    {
        public float speed = 520;
        public float maxSpeed = 720;
        public float acceleration = 4;
        public float jumpVelocity = 860;
        public float gravity = 2400;
        // Apex hang and fall multiplier: gravity eases from the takeoff scale to a floatier apex, then to a
        // snappier touchdown. See DemoRunnerModel.JumpGravity.
        [Tooltip("Gravity multiplier at takeoff.")]
        public float takeoffGravityScale = 1;
        [Tooltip("Gravity multiplier at the top of the jump; below takeoff gives apex hang time.")]
        public float apexGravityScale = .9f;
        [Tooltip("Gravity multiplier reached by touchdown; above apex makes the fall snappier.")]
        public float landingGravityScale = 1.1f;
        public int goalDistance = 600;
        public float pixelsPerMetre = 55;
        public float minGap = 1050;
        public float maxGap = 1450;
        public float obstacleWidth = 88;
        public float obstacleHeight = 100;

        public void Validate()
        {
            float[] values = { speed, maxSpeed, acceleration, jumpVelocity, gravity, takeoffGravityScale, apexGravityScale,
                landingGravityScale,
                pixelsPerMetre, minGap, maxGap, obstacleWidth, obstacleHeight };
            foreach (float value in values)
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0)
                    throw new InvalidOperationException("Runner tuning must contain finite nonnegative values.");
            if (speed <= 0 || maxSpeed < speed || jumpVelocity <= 0 || gravity <= 0 || takeoffGravityScale <= 0
                || apexGravityScale <= 0 || landingGravityScale <= 0 || pixelsPerMetre <= 0 || goalDistance < 1 || minGap <= 0 || maxGap < minGap
                || obstacleWidth <= 0 || obstacleHeight <= 0)
                throw new InvalidOperationException("Runner tuning has an invalid speed, jump, distance or obstacle range.");
            // Integrate the jump as the model plays it, since gravity changes across the arc.
            float height = 0, velocity = jumpVelocity, apex = 0, airtime = 0, clearanceTime = 0;
            const float step = DemoRunnerModel.StepSeconds;
            do
            {
                float g = DemoRunnerModel.JumpGravity(this, velocity);
                height += velocity * step - g * step * step * .5f;
                velocity -= g * step;
                airtime += step;
                apex = Math.Max(apex, height);
                if (height > obstacleHeight + 12) clearanceTime += step;
            } while (height > 0 && airtime < 10);
            if (apex < obstacleHeight + 20 || minGap < maxSpeed * (airtime + .45f) + obstacleWidth)
                throw new InvalidOperationException("Runner obstacles need a clear jump arc and enough landing/reaction space.");
            if (speed * clearanceTime < obstacleWidth + DemoRunnerModel.RunnerWidth + 16)
                throw new InvalidOperationException("Runner obstacles are too wide to clear at the starting speed.");
        }
    }

    [CreateAssetMenu(fileName = "DemoRelayRun", menuName = "Baseline/Relay Run/Runner Definition")]
    public sealed class DemoRunnerDef : Def
    {
        public DemoRunnerTuning tuning = new();
        public QuestDef quest;

        public override void Validate()
        {
            if (tuning == null || quest == null)
                throw new InvalidOperationException("Relay Run requires tuning and a quest definition.");
            tuning.Validate();
            quest.Validate();
            var briefing = quest.Objectives.SingleOrDefault(step => step.id == "briefing");
            var delivery = quest.Objectives.SingleOrDefault(step => step.id == "delivery");
            if (briefing == null || briefing.secondary || briefing.showOn != Baseline.Quests.QuestRun.BeginSignal
                || briefing.completeOn != DemoRunnerSignals.Briefed || briefing.count != 1)
                throw new InvalidOperationException("Relay Run must begin with its briefing objective.");
            if (delivery == null || delivery.secondary || delivery.completeOn != DemoRunnerSignals.Metre
                || delivery.showOn != "briefing.Done" || !delivery.requiresObjectives.Contains("briefing")
                || delivery.count != tuning.goalDistance || delivery.failOn != DemoRunnerSignals.Crashed
                || quest.Objectives.Count(step => !step.secondary) != 2)
                throw new InvalidOperationException("Relay Run's delivery must wait for the briefing, count distance and fail on a crash.");
            if (quest.Ending(Baseline.Quests.QuestRun.StepsDoneSignal)?.Passes != true
                || quest.Ending(Baseline.Quests.QuestRun.StepFailedSignal)?.Passes != false)
                throw new InvalidOperationException("Relay Run quest needs success and failure endings.");
        }
    }
}
