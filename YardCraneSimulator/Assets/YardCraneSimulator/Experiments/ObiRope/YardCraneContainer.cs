using System;
using PortTraining.YardCraneSimulator.Configuration;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    public enum YardCraneContainerType
    {
        Ft20,
        Ft40
    }

    // ISO 668 nominal exterior dimensions (length, width, height), meters. Lookup rather than
    // free-editable fields on the container so type and geometry cannot be authored out of sync.
    internal static class YardCraneContainerDimensions
    {
        private static readonly Vector3 Ft20Meters = new(6.058f, 2.438f, 2.591f);
        private static readonly Vector3 Ft40Meters = new(12.192f, 2.438f, 2.591f);

        public static Vector3 For(YardCraneContainerType type) => type switch
        {
            YardCraneContainerType.Ft20 => Ft20Meters,
            YardCraneContainerType.Ft40 => Ft40Meters,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    /// <summary>
    /// Per-corner alignment/lock outcome, mirroring how a real spreader reports each twistlock's
    /// own seated state instead of one aggregate "locked" flag. The pass/fail math does not
    /// depend on which physical corner owns which name; front/left vs rear/right is an
    /// internally consistent labeling choice (see YardCraneEightRopeMaxPrototypeRig's
    /// ComputeContainerCornerAlignment), not one verified against real spreader hardware naming.
    /// </summary>
    public readonly struct YardCraneContainerCornerState
    {
        public YardCraneContainerCornerState(bool frontLeft, bool frontRight, bool rearLeft, bool rearRight)
        {
            FrontLeft = frontLeft;
            FrontRight = frontRight;
            RearLeft = rearLeft;
            RearRight = rearRight;
        }

        public bool FrontLeft { get; }
        public bool FrontRight { get; }
        public bool RearLeft { get; }
        public bool RearRight { get; }
        public bool AllAligned => FrontLeft && FrontRight && RearLeft && RearRight;
        public int AlignedCount => (FrontLeft ? 1 : 0) + (FrontRight ? 1 : 0) + (RearLeft ? 1 : 0) + (RearRight ? 1 : 0);
    }

    /// <summary>
    /// Scene-authored data identity for a single container instance (id/type/tare/gross mass).
    /// Optional on ActiveContainer: YardCraneEightRopeMaxPrototypeRig falls back to today's
    /// Rigidbody.mass-is-authoritative behavior when this component is absent, so existing
    /// scenes keep working unchanged until it is added. When present, GrossMassKg here is
    /// authoritative and the rig copies it onto the scene-authored free-body Rigidbody.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YardCraneContainer : MonoBehaviour
    {
        // 2026-09-18: physics layer for the four small, scene-authored lock-hole trigger
        // colliders validated by EnsureLockHoleColliders below. Raycasts from the spreader's
        // twistlock points filter to this layer so they can only ever register a real corner
        // casting, never the container's own body collider or unrelated scene geometry. Must match
        // the layer name added to
        // ProjectSettings/TagManager.asset (slot 7) - if that layer is renamed or removed in the
        // Editor this constant has to move with it.
        public const string LockHoleLayerName = "ContainerLockHole";
        private const float LockHoleColliderRadius = 0.05f;
        private const float EmptyToleranceKg = 50f;

        [SerializeField] private string containerId = string.Empty;
        [SerializeField] private YardCraneContainerType containerType = YardCraneContainerType.Ft40;
        [SerializeField, Min(0f)] private float tareMassKg = 3800f;
        [SerializeField, Min(0f)] private float grossMassKg = 30000f;
        [SerializeField] private Transform lockHolesRoot;

        private bool lockHoleAuthoringErrorReported;

        public string ContainerId => containerId;
        public YardCraneContainerType ContainerType => containerType;
        public float TareMassKg => tareMassKg;
        public float GrossMassKg => grossMassKg;
        public bool IsEmpty => grossMassKg <= tareMassKg + EmptyToleranceKg;
        public Vector3 NominalDimensionsMeters => YardCraneContainerDimensions.For(containerType);

        // Validate on every container, including static yard instances the rig has not bound yet.
        // The nodes are deliberately authored in the prefab/scene so artists can inspect and
        // adjust them. Runtime creation here would hide authoring errors and make the scene differ
        // between Edit and Play modes.
        private void Awake() => EnsureLockHoleColliders();

        // Lets the rig keep this component truthful when it changes the runtime mass through its
        // own debug/test API (TrySetContainerGrossMass). Not for arbitrary external mutation.
        internal void SetGrossMassKg(float value) => grossMassKg = Mathf.Max(0f, value);

        // Container-local axes: X = length, Z = width (matches the existing graybox collider's
        // ContainerColliderSizeMeters X-is-length convention); Y = height, origin at the
        // geometric center (this project's established ActiveContainer convention). Real ISO
        // corner castings sit inset from the absolute edge; this uses the true corner as a
        // deliberate simplification, same one already disclosed for the four-corner alignment
        // math this replaces.
        public Vector3 TopCornerLocalOffset(bool front, bool left)
        {
            var dims = NominalDimensionsMeters;
            var halfLength = dims.x * 0.5f;
            var halfWidth = dims.y * 0.5f;
            var halfHeight = dims.z * 0.5f;
            return new Vector3(front ? halfLength : -halfLength, halfHeight, left ? halfWidth : -halfWidth);
        }

        // Kept under the established method name because the rig already calls it whenever a
        // container becomes active. It now validates only; it never creates or repairs hierarchy
        // at runtime. Trigger-only colliders never participate in physical contact response, only
        // in the twistlock sensor queries on ContainerLockHole.
        public void EnsureLockHoleColliders()
        {
            if (TryValidateLockHoleColliders(out var error))
            {
                lockHoleAuthoringErrorReported = false;
                return;
            }

            if (!lockHoleAuthoringErrorReported)
            {
                Debug.LogError(
                    $"[YC_CONTAINER_LOCK_HOLE_AUTHORING_INVALID] container='{name}', reason={error}. " +
                    "LockHoles and its four trigger colliders must be saved in the prefab/scene; " +
                    "runtime creation is intentionally disabled.",
                    this);
                lockHoleAuthoringErrorReported = true;
            }
        }

        public bool TryValidateLockHoleColliders(out string error)
        {
            var lockHoleLayer = LayerMask.NameToLayer(LockHoleLayerName);
            if (lockHoleLayer < 0)
            {
                error = $"layer '{LockHoleLayerName}' is not defined";
                return false;
            }

            if (lockHolesRoot == null || lockHolesRoot.parent != transform || lockHolesRoot.name != "LockHoles")
            {
                error = "serialized LockHoles root is missing or is not a direct child";
                return false;
            }

            if (lockHolesRoot.localPosition.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(lockHolesRoot.localRotation, Quaternion.identity) > 0.01f ||
                Vector3.Distance(lockHolesRoot.localScale, Vector3.one) > 0.001f)
            {
                error = "LockHoles root local transform must be identity";
                return false;
            }

            if (!TryValidateLockHole("FL", true, true, lockHoleLayer, out error) ||
                !TryValidateLockHole("FR", true, false, lockHoleLayer, out error) ||
                !TryValidateLockHole("RL", false, true, lockHoleLayer, out error) ||
                !TryValidateLockHole("RR", false, false, lockHoleLayer, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        private bool TryValidateLockHole(
            string label,
            bool front,
            bool left,
            int expectedLayer,
            out string error)
        {
            var child = lockHolesRoot.Find($"LockHole_{label}");
            if (child == null)
            {
                error = $"LockHole_{label} is missing";
                return false;
            }

            var collider = child.GetComponent<SphereCollider>();
            var expectedPosition = TopCornerLocalOffset(front, left);
            if (collider == null || !collider.enabled || !collider.isTrigger ||
                child.gameObject.layer != expectedLayer ||
                Vector3.Distance(child.localPosition, expectedPosition) > 0.005f ||
                Mathf.Abs(collider.radius - LockHoleColliderRadius) > 0.001f)
            {
                error = $"LockHole_{label} collider/layer/position does not match {containerType}";
                return false;
            }

            error = null;
            return true;
        }

        public bool TryValidate(YardCraneEightRopeMaxProfile profile, out string error)
        {
            if (profile == null)
            {
                error = "profile is null";
                return false;
            }

            if (tareMassKg > grossMassKg)
            {
                error = $"tareMassKg={tareMassKg:F1}kg exceeds grossMassKg={grossMassKg:F1}kg";
                return false;
            }

            if (grossMassKg < profile.load.minimumContainerGrossMassKg ||
                grossMassKg > profile.load.maximumContainerGrossMassKg)
            {
                error = $"grossMassKg={grossMassKg:F1}kg is outside the supported " +
                    $"[{profile.load.minimumContainerGrossMassKg:F1}, " +
                    $"{profile.load.maximumContainerGrossMassKg:F1}]kg range";
                return false;
            }

            error = null;
            return true;
        }
    }
}
