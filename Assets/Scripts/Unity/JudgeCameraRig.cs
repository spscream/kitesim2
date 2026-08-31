using System.Collections.Generic;
using UnityEngine;
using Kitesim.Core;

namespace Kitesim.Unity
{
    /// <summary>
    /// Moves the judge's camera the way a judge moves: the head follows the kites,
    /// and only when no head angle frames them all does the judge step back.
    ///
    /// The geometry is CameraFraming's; this adds the lag that makes it read as a
    /// person rather than a servo, and decides which kites count.
    ///
    /// See docs/superpowers/specs/2026-08-31-judge-view-design.md.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class JudgeCameraRig : MonoBehaviour
    {
        /// <summary>
        /// Neck limits, from the base design section 7. Shared with the M5
        /// first-person camera on purpose: two cameras that both stand for a human
        /// head should not disagree about what a head can do.
        /// </summary>
        [SerializeField] private float maxYawDegrees = 80f;
        [SerializeField] private float maxPitchDegrees = 70f;

        /// <summary>Time constant of the head, seconds. The base design gives ~0.15.</summary>
        [SerializeField] private float headLag = 0.15f;

        /// <summary>
        /// Time constant of the retreat. Much slower than the head, because walking
        /// backwards is a bigger commitment than glancing, and because a retreat that
        /// snapped would read as the world lurching rather than the judge moving.
        /// </summary>
        [SerializeField] private float stepLag = 0.7f;

        /// <summary>
        /// Fraction of each half-frame kept clear of kites. Stops the camera hunting
        /// while a kite balances on the frame edge. See CameraFraming.Frame.
        /// </summary>
        [SerializeField] private float margin = 0.1f;

        /// <summary>How far the judge will retreat before simply losing a kite.</summary>
        [SerializeField] private float maxBackOff = 60f;

        private readonly List<WindowKiteBehaviour> kites = new List<WindowKiteBehaviour>();
        private Camera cam;
        private Vec3 restPosition;
        private Vec3 restForward;

        public void Configure(Vector3 rest, Vector3 forward)
        {
            cam = GetComponent<Camera>();
            restPosition = rest.ToVec3();
            restForward = forward.ToVec3();

            transform.position = rest;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// Pin this head in place: it may look around, but never walks backwards.
        /// A pilot is holding the kite, so retreating to frame it is not something
        /// they can do -- only a judge, who is just watching, has that option.
        /// </summary>
        public void NeverStepsBack()
        {
            maxBackOff = 0f;
        }

        public void Track(WindowKiteBehaviour kite)
        {
            if (kite != null)
                kites.Add(kite);
        }

        void LateUpdate()
        {
            if (cam == null)
                return;

            CameraStance stance = CameraFraming.Frame(
                restPosition, restForward, FlyingKites(),
                maxYawDegrees * Mathf.Deg2Rad, maxPitchDegrees * Mathf.Deg2Rad,
                cam.fieldOfView * Mathf.Deg2Rad, cam.aspect,
                margin, maxBackOff);

            transform.position = Vector3.Lerp(
                transform.position, stance.Position.ToVector3(), Blend(stepLag));
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(stance.Forward.ToVector3(), Vector3.up),
                Blend(headLag));
        }

        /// <summary>
        /// Exponential approach with a time constant, so the motion is frame-rate
        /// independent -- a plain per-frame Lerp factor would make the judge's head
        /// faster on a faster machine.
        /// </summary>
        private static float Blend(float tau)
        {
            if (tau <= 0f)
                return 1f;
            return 1f - Mathf.Exp(-Time.deltaTime / tau);
        }

        /// <summary>
        /// Only kites in the air are framed. A landed kite lies at its pilot's feet,
        /// and counting it would drag the aim down to the ground whenever one flier
        /// puts down while the others keep flying. With nothing airborne the judge
        /// returns to the resting stance, which is where a launch will happen.
        /// </summary>
        private Vec3[] FlyingKites()
        {
            var flying = new List<Vec3>(kites.Count);
            foreach (WindowKiteBehaviour kite in kites)
            {
                if (kite != null && kite.Sim != null && !kite.Landed)
                    flying.Add(kite.View.WorldPosition);
            }
            return flying.ToArray();
        }
    }
}
