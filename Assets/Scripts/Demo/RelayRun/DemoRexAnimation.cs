using Baseline.Presentation;
using Hypocycloid.Core;
using UnityEngine;

namespace Baseline.Demo.RelayRun
{
    public sealed class DemoRexAnimation : MonoBehaviour
    {
        static readonly int Airborne = Animator.StringToHash("Airborne");
        static readonly int RunSpeed = Animator.StringToHash("RunSpeed");
        static readonly int JumpState = Animator.StringToHash("Base Layer.Jump");
        static readonly int Run = Animator.StringToHash("Base Layer.Run");
        // The impact squash overrides only the root scale and body tilt, so the stride resumes on touchdown.
        static readonly int LandState = Animator.StringToHash("Impact.Land");
        static readonly int ImpactIdle = Animator.StringToHash("Impact.Idle");
        const int ImpactLayer = 1;
        const float ImpactFade = .2f;

        public Animator animator;
        public DemoRexFootFx footFx;
        public SoundCueDef footstepCue;
        public RectTransform nearFoot, farFoot;
        bool groundedRunning;

        void Awake() => animator.speed = 0;

        public void Present(bool running, bool airborne, float speed)
        {
            groundedRunning = running && !airborne;
            animator.SetBool(Airborne, airborne);
            animator.SetFloat(RunSpeed, Mathf.Clamp(speed / 520f, .8f, 1.6f));
            // Only the stride speeds up; takeoff and impact keep their authored timing.
            animator.speed = running ? 1 : 0;
            // An idle override layer holds the last pose it wrote, legs included, so it only counts while
            // the squash plays and fades out over the clip's settled tail.
            AnimatorStateInfo impact = animator.GetCurrentAnimatorStateInfo(ImpactLayer);
            animator.SetLayerWeight(ImpactLayer, impact.fullPathHash == LandState
                ? Mathf.Clamp01((1 - impact.normalizedTime) / ImpactFade) : 0);
        }

        public void Jump()
        {
            groundedRunning = false;
            animator.SetBool(Airborne, true);
            // Apply the squash on input, before Unity's next animation update.
            animator.Play(JumpState, 0, 0);
            StopImpact();
            animator.Update(0);
        }

        public void Land()
        {
            animator.Play(LandState, ImpactLayer, 0);
            animator.SetLayerWeight(ImpactLayer, 1);
            animator.Update(0);
        }

        void StopImpact()
        {
            animator.Play(ImpactIdle, ImpactLayer, 0);
            animator.SetLayerWeight(ImpactLayer, 0);
        }

        public void ResetPose()
        {
            groundedRunning = false;
            animator.SetBool(Airborne, false);
            animator.Play(Run, 0, 0);
            StopImpact();
            animator.Update(0);
            animator.speed = 0;
        }

        // Run clip contacts alternate at zero and half a cycle.
        public void Footstep(int far)
        {
            if (!groundedRunning) return;
            if (footFx != null)
                footFx.Footstep((far == 0 ? nearFoot : farFoot).position);
            SignalBus.Fire(new SoundRequestedSignal { Cue = footstepCue }, this);
        }
    }
}
