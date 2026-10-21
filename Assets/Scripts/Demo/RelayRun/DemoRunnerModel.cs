using System;

namespace Baseline.Demo.RelayRun
{
    public enum DemoRunnerOutcome { Running, Crashed, Complete }

    /// <summary>Logical pixels, independent of viewport size and Unity's physics/frame rate.</summary>
    public sealed class DemoRunnerModel
    {
        public const float RunnerX = 300;
        // The rig's hip is ahead of its root; collision follows the body, not the tail-side origin.
        public const float RunnerBodyOffset = 28;
        public const float RunnerBodyX = RunnerX + RunnerBodyOffset;
        public const float GroundY = 230;
        public const float RunnerWidth = 66;
        // The Jump clip folds the feet above the rig origin while rising; they only partly unfold to land.
        public const float RisingFootClearance = 32;
        public const float FallingFootClearance = 26;
        // Visible overlap tolerated before contact counts as a crash.
        public const float HitForgiveness = 6;
        public const int ObstacleCapacity = 5;
        public const float JumpWindupSeconds = .06f;
        const float SpawnX = 1720;
        public const float StepSeconds = 1f / 120;

        /// <summary>Gravity at a vertical velocity: the takeoff scale easing to the apex scale, then to the landing scale.</summary>
        /// <remarks>
        /// A standard platformer "juice" technique, usually called apex hang time plus a fall gravity
        /// multiplier (see Kyle Pittman's GDC talk "Building a Better Jump"). Lighter gravity near the top
        /// stretches the moment of control at the peak; heavier gravity on the way down makes the landing
        /// read as weighty and decisive instead of drifting back. Constant gravity gives a symmetric parabola
        /// that tends to feel floaty on descent. The scale is driven by vertical speed, so it eases in rather
        /// than switching at the apex: a sudden change in pull reads as a hitch in the arc.
        /// </remarks>
        public static float JumpGravity(DemoRunnerTuning tuning, float velocity)
        {
            // Both halves flatten at the apex, so the change in pull is continuous across the arc.
            float t = Math.Clamp(Math.Abs(velocity) / tuning.jumpVelocity, 0, 1);
            t = t * t * (3 - 2 * t);
            float scale = velocity > 0
                ? tuning.apexGravityScale + (tuning.takeoffGravityScale - tuning.apexGravityScale) * t
                : tuning.apexGravityScale + (tuning.landingGravityScale - tuning.apexGravityScale) * t;
            return tuning.gravity * scale;
        }

        public sealed class Obstacle
        {
            public float X { get; internal set; }
            public bool Active { get; internal set; }
            public bool Passed { get; internal set; }
            public int Appearance { get; internal set; }
        }

        /// <summary>One box of an obstacle's opaque outline, as fractions of its rect from the bottom-left.</summary>
        public readonly struct OutlineStep
        {
            public readonly float Left, Right, Top;
            public OutlineStep(float left, float right, float top) { Left = left; Right = right; Top = top; }
        }

        // Traced from DemoRelayObstacles, indexed by Appearance: the crate and its raised lid, then the
        // broken pylon, whose leading rubble sits far below its spire.
        static readonly OutlineStep[][] outlines =
        {
            new[] { new OutlineStep(.08f, .93f, .96f), new OutlineStep(.39f, .6f, 1) },
            new[] { new OutlineStep(.2f, .31f, .38f), new OutlineStep(.32f, .47f, .54f),
                new OutlineStep(.48f, .88f, .73f), new OutlineStep(.48f, .61f, .97f) },
        };
        public static int AppearanceCount => outlines.Length;
        public static System.Collections.Generic.IReadOnlyList<OutlineStep> Outline(int appearance) => outlines[appearance];

        readonly DemoRunnerTuning tuning;
        readonly Random random;
        readonly Obstacle[] obstacles = new Obstacle[ObstacleCapacity];
        float travelled, nextSpawn, bufferedJump, jumpWindup, elapsed;

        public float Height { get; private set; }
        public float Velocity { get; private set; }
        public float Speed { get; private set; }
        public float Distance => Math.Min(tuning.goalDistance, travelled / tuning.pixelsPerMetre);
        public float Travelled => travelled;
        public float Elapsed => elapsed;
        public int Cleared { get; private set; }
        public int Jumps { get; private set; }
        public int JumpPreparations { get; private set; }
        public int Landings { get; private set; }
        public bool Grounded => Height <= 0;
        public bool PreparingJump => jumpWindup > 0;
        /// <summary>Height of the visible feet above the rig origin in the current jump pose.</summary>
        public float FootClearance => Grounded ? 0 : Velocity > 0 ? RisingFootClearance : FallingFootClearance;
        public DemoRunnerOutcome Outcome { get; private set; }
        public System.Collections.Generic.IReadOnlyList<Obstacle> Obstacles => obstacles;

        public DemoRunnerModel(DemoRunnerTuning settings, int seed)
        {
            settings.Validate();
            // Snapshot authored values so editing an asset cannot change a live attempt halfway through.
            tuning = new DemoRunnerTuning
            {
                speed = settings.speed, maxSpeed = settings.maxSpeed, acceleration = settings.acceleration,
                jumpVelocity = settings.jumpVelocity, gravity = settings.gravity,
                takeoffGravityScale = settings.takeoffGravityScale, apexGravityScale = settings.apexGravityScale,
                landingGravityScale = settings.landingGravityScale,
                goalDistance = settings.goalDistance,
                pixelsPerMetre = settings.pixelsPerMetre, minGap = settings.minGap, maxGap = settings.maxGap,
                obstacleWidth = settings.obstacleWidth, obstacleHeight = settings.obstacleHeight
            };
            random = new Random(seed);
            for (int i = 0; i < obstacles.Length; i++) obstacles[i] = new Obstacle();
            Speed = tuning.speed;
            Spawn();
            nextSpawn = NextGap();
        }

        public void Jump()
        {
            if (Outcome != DemoRunnerOutcome.Running || PreparingJump) return;
            bufferedJump = .12f;
            if (Grounded) PrepareJump();
        }

        void PrepareJump()
        {
            jumpWindup = JumpWindupSeconds;
            bufferedJump = 0;
            JumpPreparations++;
        }

        public void Advance(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            while (seconds > 0 && Outcome == DemoRunnerOutcome.Running)
            {
                float dt = Math.Min(StepSeconds, seconds);
                Step(dt);
                seconds -= dt;
            }
        }

        void Step(float dt)
        {
            if (bufferedJump > 0 && Grounded && !PreparingJump) PrepareJump();
            if (PreparingJump)
            {
                jumpWindup = Math.Max(0, jumpWindup - dt);
                if (!PreparingJump)
                {
                    Velocity = tuning.jumpVelocity;
                    Jumps++;
                }
            }
            bufferedJump = Math.Max(0, bufferedJump - dt);
            if (Height > 0 || Velocity > 0)
            {
                float gravity = JumpGravity(tuning, Velocity);
                Height = Math.Max(0, Height + Velocity * dt - gravity * dt * dt * .5f);
                Velocity -= gravity * dt;
                if (Grounded)
                {
                    Velocity = 0;
                    Landings++;
                }
            }

            elapsed += dt;
            Speed = Math.Min(tuning.maxSpeed, tuning.speed + tuning.acceleration * elapsed);
            float travel = Speed * dt;
            travelled += travel;
            foreach (Obstacle obstacle in obstacles)
            {
                if (!obstacle.Active) continue;
                obstacle.X -= travel;
                if (Hits(obstacle))
                {
                    Outcome = DemoRunnerOutcome.Crashed;
                    return;
                }
                if (!obstacle.Passed && obstacle.X + tuning.obstacleWidth * .5f < RunnerBodyX - RunnerWidth * .5f)
                {
                    obstacle.Passed = true;
                    Cleared++;
                }
                if (obstacle.X < -tuning.obstacleWidth) obstacle.Active = false;
            }
            if (Distance >= tuning.goalDistance)
            {
                Outcome = DemoRunnerOutcome.Complete;
                return;
            }
            if (travelled >= nextSpawn)
            {
                Spawn();
                nextSpawn = travelled + NextGap();
            }
        }

        // Visible feet against the traced outline: folded legs clear what they visibly clear.
        bool Hits(Obstacle obstacle)
        {
            float left = obstacle.X - tuning.obstacleWidth * .5f, feet = Height + FootClearance;
            foreach (OutlineStep step in outlines[obstacle.Appearance])
                if (left + step.Right * tuning.obstacleWidth > RunnerBodyX - RunnerWidth * .5f
                    && left + step.Left * tuning.obstacleWidth < RunnerBodyX + RunnerWidth * .5f
                    && feet < step.Top * tuning.obstacleHeight - HitForgiveness)
                    return true;
            return false;
        }

        float NextGap() => tuning.minGap + (float)random.NextDouble() * (tuning.maxGap - tuning.minGap);

        void Spawn()
        {
            foreach (Obstacle obstacle in obstacles)
                if (!obstacle.Active)
                {
                    obstacle.Active = true;
                    obstacle.Passed = false;
                    obstacle.X = SpawnX;
                    obstacle.Appearance = random.Next(outlines.Length);
                    return;
                }
        }
    }
}
