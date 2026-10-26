using System;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Scene-authored on the container free-body proxy and reused for every lock/unlock cycle.
    /// It reports the normal closing speed of collisions after being armed and is never
    /// added or removed at runtime. This script has no opinion on
    /// training/scoring policy; it only measures and reports, matching the split this codebase
    /// already uses elsewhere (YardCraneTrainingScoreTracker takes pre-computed booleans, it
    /// does not sense physics itself).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YardCraneContainerImpactSensor : MonoBehaviour
    {
        public event Action<float> ImpactDetected;
        private bool armed;

        public void Arm() => armed = true;
        public void Disarm() => armed = false;

        private void OnCollisionEnter(Collision collision)
        {
            if (!armed) return;

            var normalImpactSpeed = 0f;
            for (var index = 0; index < collision.contactCount; index++)
            {
                normalImpactSpeed = Mathf.Max(
                    normalImpactSpeed,
                    Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(index).normal)));
            }

            // The rig decides whether this is a meaningful hard impact and disarms the sensor.
            // A harmless first touch must not consume the entire unlock cycle before the actual landing.
            ImpactDetected?.Invoke(normalImpactSpeed);
        }
    }
}
