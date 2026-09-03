using System;
using System.Collections.Generic;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Configuration
{
    [CreateAssetMenu(
        fileName = "YC-EightRopeMAX-PhysicsProfile",
        menuName = "PortTraining/Yard Crane/Eight Rope MAX Profile")]
    public sealed class YardCraneEightRopeMaxProfile : ScriptableObject
    {
        public const string SupportedSchemaVersion = "1.0.0";
        public const string DefaultResourcePath = "Configuration/YC-EightRopeMAX-Greybox-v1";

        [Serializable]
        public sealed class IdentitySection
        {
            public string schemaVersion = SupportedSchemaVersion;
            public string profileId = "YC-PHYSICS-QD-EIGHT-ROPE-GREYBOX-1.0.0";
            public string topologyProfileId = "QingdaoPort-NewEightRopeMAX-Greybox-v1";
            public string equipmentModel = "QingdaoPort-NewEightRopeMAX-Greybox";
            public string calibrationStatus = "Greybox";
            [TextArea] public string calibrationSource = "2026-08-31 v12-v14 verified greybox baseline; not real-device acceptance data.";
        }

        [Serializable]
        public sealed class MotionSection
        {
            [Min(0.01f)] public float gantrySpeedMetersPerSecond = 1.5f;
            [Min(0.01f)] public float gantryAccelerationMetersPerSecondSquared = 0.45f;
            [Min(0.01f)] public float gantryBrakingMetersPerSecondSquared = 0.65f;
            [Min(0.1f)] public float gantryTravelFromHomeMeters = 8f;
            [Min(0f)] public float gantryLimitSafetyMarginMeters = 0.10f;
            [Min(0f)] public float wheelClampTransitionSeconds = 0.40f;
            [Min(0f)] public float anchorTransitionSeconds = 0.60f;
            [Min(0f)] public float spreaderSizeTransitionSeconds = 2.5f;
            [Min(0.1f)] public float spreaderRotationMaxAngleDegrees = 3f;
            [Min(0f)] public float spreaderRotationRateDegreesPerSecond = 1.5f;
            [Min(0f)] public float spreaderRotationReturnToZeroRateDegreesPerSecond = 1.0f;
            [Min(0.01f)] public float hoistSpeedMetersPerSecond = 0.65f;
            [Min(0.01f)] public float unloadedHoistAccelerationMetersPerSecondSquared = 0.7f;
            [Min(0.01f)] public float hoistBrakingMetersPerSecondSquared = 0.95f;
            [Min(0.01f)] public float trolleySpeedMetersPerSecond = 1.8f;
            [Min(0.01f)] public float trolleyAccelerationMetersPerSecondSquared = 0.9f;
            [Min(0.01f)] public float trolleyBrakingMetersPerSecondSquared = 1.2f;
            [Range(0.01f, 1f)] public float fullyLoadedTrolleyAccelerationFactor = 0.68f;
            [Range(0.01f, 1f)] public float fullyLoadedTrolleyBrakingFactor = 0.78f;
            [Range(0.01f, 1f)] public float fullyLoadedHoistSpeedFactor = 0.65f;
            [Range(0.01f, 1f)] public float fullyLoadedHoistAccelerationFactor = 0.45f;
            [Min(0.1f)] public float trolleyTravelFromHomeMeters = 4.5f;
            [Min(0f)] public float trolleyLimitSafetyMarginMeters = 0.03f;
            [Min(0f)] public float loadedTrolleyStopStabilizationSeconds = 1.0f;
            [Min(0f)] public float loadedTrolleyStopLinearDampingPerSecond = 0f;
            [Min(0f)] public float loadedTrolleyStopAngularDampingPerSecond = 0.55f;
            [Min(0f)] public float motionObservationSpeedThresholdMetersPerSecond = 0.01f;
            [Min(0f)] public float resetSafeSpeedThresholdMetersPerSecond = 0.05f;
        }

        [Serializable]
        public sealed class LoadSection
        {
            [Min(0.01f)] public float simulationProxyMassKg = 2f;
            [Min(0f)] public float spreaderRealMassKg = 12500f;
            [Min(0f)] public float minimumContainerGrossMassKg = 2200f;
            [Min(0f)] public float maximumContainerGrossMassKg = 40500f;
            public Vector3 containerLockedLocalPosition = new(0f, -1.45f, 0f);
            [Min(0f)] public float loadTransferPreTensionDurationSeconds = 0.25f;
        }

        [Serializable]
        public sealed class GeometryAndSafetySection
        {
            [Min(0.01f)] public float minimumSuspendedDropMeters = 2.8f;
            [Min(0.01f)] public float maximumSuspendedDropMeters = 8.5f;
            [Min(0.01f)] public float containerHeightMeters = 2.4f;
            public Vector3 containerColliderSizeMeters = new(4.8f, 2.4f, 2.4f);
            public Vector3 headblockColliderSizeMeters = new(4f, 0.5f, 2.2f);
            [Min(0.01f)] public float spreaderThicknessMeters = 0.5f;
            [Min(0f)] public float containerHorizontalLockToleranceMeters = 0.20f;
            [Min(0f)] public float containerVerticalLockToleranceMeters = 0.18f;
            [Min(0f)] public float containerYawLockToleranceDegrees = 3f;
            [Min(0f)] public float containerLandingToleranceMeters = 0.15f;
            [Min(0f)] public float maximumLockingSpeedMetersPerSecond = 0.05f;
            [Min(0f)] public float containerLandingStableHoldSeconds = 0.25f;
            [Min(0f)] public float loadedTrolleyMinimumClearanceMeters = 0.35f;
        }

        [Serializable]
        public sealed class RopePhysicsSection
        {
            [Range(1, 12)] public int obiSolverSubsteps = 10;
            [Range(1, 4)] public int obiDistanceIterations = 2;
            [Min(0f)] public float unloadedHeadblockLinearDamping = 0.04f;
            [Min(0f)] public float unloadedHeadblockAngularDamping = 0.8f;
            [Min(0f)] public float loadedHeadblockAngularDamping = 0.55f;
            [Min(1)] public int headblockSolverIterations = 32;
            [Min(1)] public int headblockSolverVelocityIterations = 16;
            [Min(0.01f)] public float loadedSuspensionNaturalFrequencyRadiansPerSecond = 2.35f;
            [Min(0f)] public float loadedSuspensionDampingRatio = 0.38f;
            [Min(0f)] public float maximumLoadedSuspensionAccelerationMetersPerSecondSquared = 1.8f;
            [Min(0.01f)] public float loadedHoistNaturalFrequencyRadiansPerSecond = 9.0f;
            [Min(0f)] public float loadedHoistDampingRatio = 0.55f;
            [Min(0f)] public float lightLoadedStaticRopeDeflectionMeters = 0.006f;
            [Min(0f)] public float fullyLoadedStaticRopeDeflectionMeters = 0.018f;
            [Min(0f)] public float maximumLoadedHoistDeflectionMeters = 0.040f;
            [Min(0f)] public float loopConstraintToleranceMeters = 0.00025f;
            [Min(0f)] public float startupPreTensionDurationSeconds = 0.4f;
            [Min(1)] public int positionConstraintIterations = 32;
            [Min(1)] public int velocityConstraintIterations = 8;
            [Min(1)] public int interlockPositionIterations = 4;
            [Min(1)] public int interlockVelocityIterations = 2;
            [Range(0f, 1f)] public float interlockPositionRelaxation = 0.18f;
            [Range(0f, 1f)] public float interlockVelocityRelaxation = 0.45f;
            [Min(0f)] public float maximumAngularCorrectionDegrees = 1.5f;
        }

        [Serializable]
        public sealed class AcceptanceSection
        {
            [Min(0f)] public float loadedHoistSettleDeflectionToleranceMeters = 0.0015f;
            [Min(0f)] public float loadedHoistSettleVelocityToleranceMetersPerSecond = 0.003f;
            [Min(0f)] public float loadedHoistSettleHoldSeconds = 0.30f;
            [Min(0.01f)] public float loadedHoistSettleTimeoutSeconds = 5.0f;
            [Min(0f)] public float maximumPermittedLoopExtensionMeters = 0.008f;
            [Min(0f)] public float prototypeMaximumSwayMeters = 0.35f;
            [Min(0f)] public float prototypeMaximumResidualTwistDegrees = 1.5f;
        }

        [Serializable]
        public sealed class RegressionSection
        {
            [Min(0.1f)] public float loadedStageTimeoutSeconds = 15f;
            [Min(0.1f)] public float loadedTotalTimeoutSeconds = 75f;
            [Min(0f)] public float loadedStableHoldSeconds = 0.35f;
            [Min(0f)] public float loadedLiftClearanceMeters = 1.20f;
            [Min(0f)] public float loadedOutboundTravelMeters = 1.20f;
        }

        [Serializable]
        public sealed class LoopBinding
        {
            public string id;
            public string drum;
            public string antiSwayAxis;
            public string exitPath;
            public string deadEndPath;
            public string sheavePath;
            public string slotPath;

            public LoopBinding(string id, string drum, string antiSwayAxis, string exitPath,
                string deadEndPath, string sheavePath, string slotPath)
            {
                this.id = id;
                this.drum = drum;
                this.antiSwayAxis = antiSwayAxis;
                this.exitPath = exitPath;
                this.deadEndPath = deadEndPath;
                this.sheavePath = sheavePath;
                this.slotPath = slotPath;
            }
        }

        public IdentitySection identity = new();
        public MotionSection motion = new();
        public LoadSection load = new();
        public GeometryAndSafetySection geometryAndSafety = new();
        public RopePhysicsSection ropePhysics = new();
        public AcceptanceSection acceptance = new();
        public RegressionSection regression = new();
        public LoopBinding[] loopBindings = CreateDefaultLoopBindings();

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (identity == null || motion == null || load == null || geometryAndSafety == null ||
                ropePhysics == null || acceptance == null || regression == null)
            {
                error = "Profile contains a missing configuration section.";
                return false;
            }

            if (!string.Equals(identity.schemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
            {
                error = $"Unsupported physics profile schema '{identity.schemaVersion}'. Expected '{SupportedSchemaVersion}'.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(identity.profileId) || string.IsNullOrWhiteSpace(identity.topologyProfileId) ||
                string.IsNullOrWhiteSpace(identity.equipmentModel) || string.IsNullOrWhiteSpace(identity.calibrationStatus) ||
                string.IsNullOrWhiteSpace(identity.calibrationSource))
            {
                error = "Profile identity, equipment model and calibration provenance are required.";
                return false;
            }

            if (!AllFinite(
                    motion.gantrySpeedMetersPerSecond,
                    motion.gantryAccelerationMetersPerSecondSquared,
                    motion.gantryBrakingMetersPerSecondSquared,
                    motion.gantryTravelFromHomeMeters,
                    motion.gantryLimitSafetyMarginMeters,
                    motion.wheelClampTransitionSeconds,
                    motion.anchorTransitionSeconds,
                    motion.spreaderSizeTransitionSeconds,
                    motion.spreaderRotationMaxAngleDegrees,
                    motion.spreaderRotationRateDegreesPerSecond,
                    motion.spreaderRotationReturnToZeroRateDegreesPerSecond,
                    motion.hoistSpeedMetersPerSecond,
                    motion.unloadedHoistAccelerationMetersPerSecondSquared,
                    motion.hoistBrakingMetersPerSecondSquared,
                    motion.trolleySpeedMetersPerSecond,
                    motion.trolleyAccelerationMetersPerSecondSquared,
                    motion.trolleyBrakingMetersPerSecondSquared,
                    motion.fullyLoadedTrolleyAccelerationFactor,
                    motion.fullyLoadedTrolleyBrakingFactor,
                    motion.fullyLoadedHoistSpeedFactor,
                    motion.fullyLoadedHoistAccelerationFactor,
                    motion.trolleyTravelFromHomeMeters,
                    motion.trolleyLimitSafetyMarginMeters,
                    motion.loadedTrolleyStopStabilizationSeconds,
                    motion.loadedTrolleyStopLinearDampingPerSecond,
                    motion.loadedTrolleyStopAngularDampingPerSecond,
                    motion.motionObservationSpeedThresholdMetersPerSecond,
                    motion.resetSafeSpeedThresholdMetersPerSecond) ||
                motion.gantrySpeedMetersPerSecond <= 0f ||
                motion.gantryAccelerationMetersPerSecondSquared <= 0f ||
                motion.gantryBrakingMetersPerSecondSquared <= 0f ||
                motion.gantryTravelFromHomeMeters <= motion.gantryLimitSafetyMarginMeters ||
                motion.gantryLimitSafetyMarginMeters < 0f ||
                motion.wheelClampTransitionSeconds < 0f ||
                motion.anchorTransitionSeconds < 0f ||
                motion.spreaderSizeTransitionSeconds < 0f ||
                motion.spreaderRotationMaxAngleDegrees <= 0f ||
                motion.spreaderRotationRateDegreesPerSecond < 0f ||
                motion.spreaderRotationReturnToZeroRateDegreesPerSecond < 0f ||
                motion.hoistSpeedMetersPerSecond <= 0f ||
                motion.unloadedHoistAccelerationMetersPerSecondSquared <= 0f ||
                motion.hoistBrakingMetersPerSecondSquared <= 0f ||
                motion.trolleySpeedMetersPerSecond <= 0f ||
                motion.trolleyAccelerationMetersPerSecondSquared <= 0f ||
                motion.trolleyBrakingMetersPerSecondSquared <= 0f ||
                motion.fullyLoadedTrolleyAccelerationFactor <= 0f || motion.fullyLoadedTrolleyAccelerationFactor > 1f ||
                motion.fullyLoadedTrolleyBrakingFactor <= 0f || motion.fullyLoadedTrolleyBrakingFactor > 1f ||
                motion.fullyLoadedHoistSpeedFactor <= 0f || motion.fullyLoadedHoistSpeedFactor > 1f ||
                motion.fullyLoadedHoistAccelerationFactor <= 0f || motion.fullyLoadedHoistAccelerationFactor > 1f ||
                motion.trolleyLimitSafetyMarginMeters < 0f ||
                motion.loadedTrolleyStopStabilizationSeconds < 0f ||
                motion.loadedTrolleyStopLinearDampingPerSecond < 0f ||
                motion.loadedTrolleyStopAngularDampingPerSecond < 0f ||
                motion.motionObservationSpeedThresholdMetersPerSecond < 0f ||
                motion.resetSafeSpeedThresholdMetersPerSecond < 0f)
            {
                error = "Motion settings contain a non-finite, non-positive or out-of-range value.";
                return false;
            }

            if (!AllFinite(
                    load.simulationProxyMassKg,
                    load.spreaderRealMassKg,
                    load.minimumContainerGrossMassKg,
                    load.maximumContainerGrossMassKg,
                    load.loadTransferPreTensionDurationSeconds) ||
                !IsFinite(load.containerLockedLocalPosition) ||
                load.minimumContainerGrossMassKg < 0f ||
                load.maximumContainerGrossMassKg <= load.minimumContainerGrossMassKg ||
                load.simulationProxyMassKg <= 0f ||
                load.spreaderRealMassKg < 0f ||
                load.loadTransferPreTensionDurationSeconds < 0f)
            {
                error = "Container mass range or simulation proxy mass is invalid.";
                return false;
            }

            if (!AllFinite(
                    geometryAndSafety.minimumSuspendedDropMeters,
                    geometryAndSafety.maximumSuspendedDropMeters,
                    geometryAndSafety.containerHeightMeters,
                    geometryAndSafety.spreaderThicknessMeters,
                    geometryAndSafety.containerHorizontalLockToleranceMeters,
                    geometryAndSafety.containerVerticalLockToleranceMeters,
                    geometryAndSafety.containerYawLockToleranceDegrees,
                    geometryAndSafety.containerLandingToleranceMeters,
                    geometryAndSafety.maximumLockingSpeedMetersPerSecond,
                    geometryAndSafety.containerLandingStableHoldSeconds,
                    geometryAndSafety.loadedTrolleyMinimumClearanceMeters) ||
                !IsFinite(geometryAndSafety.containerColliderSizeMeters) ||
                !IsFinite(geometryAndSafety.headblockColliderSizeMeters) ||
                geometryAndSafety.minimumSuspendedDropMeters <= 0f ||
                geometryAndSafety.maximumSuspendedDropMeters <= geometryAndSafety.minimumSuspendedDropMeters ||
                geometryAndSafety.containerHeightMeters <= 0f ||
                geometryAndSafety.spreaderThicknessMeters <= 0f ||
                geometryAndSafety.containerHorizontalLockToleranceMeters < 0f ||
                geometryAndSafety.containerVerticalLockToleranceMeters < 0f ||
                geometryAndSafety.containerYawLockToleranceDegrees < 0f ||
                geometryAndSafety.containerLandingToleranceMeters < 0f ||
                geometryAndSafety.maximumLockingSpeedMetersPerSecond < 0f ||
                geometryAndSafety.containerLandingStableHoldSeconds < 0f ||
                geometryAndSafety.loadedTrolleyMinimumClearanceMeters < 0f ||
                motion.trolleyTravelFromHomeMeters <= motion.trolleyLimitSafetyMarginMeters ||
                geometryAndSafety.containerColliderSizeMeters.x <= 0f ||
                geometryAndSafety.containerColliderSizeMeters.y <= 0f ||
                geometryAndSafety.containerColliderSizeMeters.z <= 0f ||
                geometryAndSafety.headblockColliderSizeMeters.x <= 0f ||
                geometryAndSafety.headblockColliderSizeMeters.y <= 0f ||
                geometryAndSafety.headblockColliderSizeMeters.z <= 0f)
            {
                error = "Hoist or trolley geometry limits are invalid.";
                return false;
            }

            if (!AllFinite(
                    ropePhysics.unloadedHeadblockLinearDamping,
                    ropePhysics.unloadedHeadblockAngularDamping,
                    ropePhysics.loadedHeadblockAngularDamping,
                    ropePhysics.loadedSuspensionNaturalFrequencyRadiansPerSecond,
                    ropePhysics.loadedSuspensionDampingRatio,
                    ropePhysics.maximumLoadedSuspensionAccelerationMetersPerSecondSquared,
                    ropePhysics.loadedHoistNaturalFrequencyRadiansPerSecond,
                    ropePhysics.loadedHoistDampingRatio,
                    ropePhysics.lightLoadedStaticRopeDeflectionMeters,
                    ropePhysics.fullyLoadedStaticRopeDeflectionMeters,
                    ropePhysics.maximumLoadedHoistDeflectionMeters,
                    ropePhysics.loopConstraintToleranceMeters,
                    ropePhysics.startupPreTensionDurationSeconds,
                    ropePhysics.interlockPositionRelaxation,
                    ropePhysics.interlockVelocityRelaxation,
                    ropePhysics.maximumAngularCorrectionDegrees) ||
                ropePhysics.obiSolverSubsteps < 1 || ropePhysics.obiSolverSubsteps > 12 ||
                ropePhysics.obiDistanceIterations < 1 || ropePhysics.obiDistanceIterations > 4 ||
                ropePhysics.unloadedHeadblockLinearDamping < 0f ||
                ropePhysics.unloadedHeadblockAngularDamping < 0f ||
                ropePhysics.loadedHeadblockAngularDamping < 0f ||
                ropePhysics.headblockSolverIterations < 1 ||
                ropePhysics.headblockSolverVelocityIterations < 1 ||
                ropePhysics.loadedSuspensionNaturalFrequencyRadiansPerSecond <= 0f ||
                ropePhysics.loadedSuspensionDampingRatio < 0f ||
                ropePhysics.maximumLoadedSuspensionAccelerationMetersPerSecondSquared < 0f ||
                ropePhysics.loadedHoistNaturalFrequencyRadiansPerSecond <= 0f ||
                ropePhysics.loadedHoistDampingRatio < 0f ||
                ropePhysics.lightLoadedStaticRopeDeflectionMeters < 0f ||
                ropePhysics.fullyLoadedStaticRopeDeflectionMeters < 0f ||
                ropePhysics.maximumLoadedHoistDeflectionMeters < 0f ||
                ropePhysics.loopConstraintToleranceMeters < 0f ||
                ropePhysics.startupPreTensionDurationSeconds < 0f ||
                ropePhysics.positionConstraintIterations < 1 ||
                ropePhysics.velocityConstraintIterations < 1 ||
                ropePhysics.interlockPositionIterations < 1 ||
                ropePhysics.interlockVelocityIterations < 1 ||
                ropePhysics.interlockPositionRelaxation < 0f || ropePhysics.interlockPositionRelaxation > 1f ||
                ropePhysics.interlockVelocityRelaxation < 0f || ropePhysics.interlockVelocityRelaxation > 1f ||
                ropePhysics.maximumAngularCorrectionDegrees < 0f)
            {
                error = "Rope physics settings contain a non-finite or out-of-range value.";
                return false;
            }

            if (!AllFinite(
                    acceptance.loadedHoistSettleDeflectionToleranceMeters,
                    acceptance.loadedHoistSettleVelocityToleranceMetersPerSecond,
                    acceptance.loadedHoistSettleHoldSeconds,
                    acceptance.loadedHoistSettleTimeoutSeconds,
                    acceptance.maximumPermittedLoopExtensionMeters,
                    acceptance.prototypeMaximumSwayMeters,
                    acceptance.prototypeMaximumResidualTwistDegrees) ||
                acceptance.loadedHoistSettleDeflectionToleranceMeters < 0f ||
                acceptance.loadedHoistSettleVelocityToleranceMetersPerSecond < 0f ||
                acceptance.loadedHoistSettleHoldSeconds < 0f ||
                acceptance.loadedHoistSettleTimeoutSeconds <= 0f ||
                acceptance.maximumPermittedLoopExtensionMeters <= 0f ||
                acceptance.prototypeMaximumSwayMeters <= 0f ||
                acceptance.prototypeMaximumResidualTwistDegrees <= 0f)
            {
                error = "Acceptance thresholds must be positive.";
                return false;
            }

            if (!AllFinite(
                    regression.loadedStageTimeoutSeconds,
                    regression.loadedTotalTimeoutSeconds,
                    regression.loadedStableHoldSeconds,
                    regression.loadedLiftClearanceMeters,
                    regression.loadedOutboundTravelMeters) ||
                regression.loadedStageTimeoutSeconds <= 0f ||
                regression.loadedTotalTimeoutSeconds <= 0f ||
                regression.loadedStableHoldSeconds < 0f ||
                regression.loadedLiftClearanceMeters < 0f ||
                regression.loadedOutboundTravelMeters < 0f)
            {
                error = "Regression settings contain a non-finite or out-of-range value.";
                return false;
            }

            if (loopBindings == null || loopBindings.Length != 8)
            {
                error = $"Eight-rope MAX profile requires exactly 8 loop bindings; found {loopBindings?.Length ?? 0}.";
                return false;
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var drums = new Dictionary<string, int>(StringComparer.Ordinal);
            var axes = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var binding in loopBindings)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.id) ||
                    string.IsNullOrWhiteSpace(binding.drum) || string.IsNullOrWhiteSpace(binding.antiSwayAxis) ||
                    !IsSafeRelativePath(binding.exitPath) || !IsSafeRelativePath(binding.deadEndPath) ||
                    !IsSafeRelativePath(binding.sheavePath) || !IsSafeRelativePath(binding.slotPath))
                {
                    error = "A loop binding is incomplete or contains an unsafe path.";
                    return false;
                }

                if (!ids.Add(binding.id))
                {
                    error = $"Duplicate loop binding ID '{binding.id}'.";
                    return false;
                }

                Increment(drums, binding.drum);
                Increment(axes, binding.antiSwayAxis);
            }

            if (!HasCount(drums, "A", 4) || !HasCount(drums, "B", 4) ||
                !HasCount(axes, "X", 4) || !HasCount(axes, "Z", 4))
            {
                error = "Eight-rope MAX bindings must contain four A/four B drum loops and four X/four Z anti-sway loops.";
                return false;
            }

            return true;
        }

        public static YardCraneEightRopeMaxProfile CreateRuntimeDefaults()
        {
            var profile = CreateInstance<YardCraneEightRopeMaxProfile>();
            profile.name = "YC-EightRopeMAX-Greybox-v1-RuntimeFallback";
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }

        private static LoopBinding[] CreateDefaultLoopBindings()
        {
            return new[]
            {
                new LoopBinding("L01_X_FL", "A", "X", "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop01_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop01_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop01_Sheave", "40_RopeRig/EightRopeLoops/Loop01_X_FL"),
                new LoopBinding("L02_X_FR", "A", "X", "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop02_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop02_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop02_Sheave", "40_RopeRig/EightRopeLoops/Loop02_X_FR"),
                new LoopBinding("L03_X_RL", "A", "X", "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop03_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop03_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop03_Sheave", "40_RopeRig/EightRopeLoops/Loop03_X_RL"),
                new LoopBinding("L04_X_RR", "A", "X", "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop04_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop04_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop04_Sheave", "40_RopeRig/EightRopeLoops/Loop04_X_RR"),
                new LoopBinding("L05_Z_FL", "B", "Z", "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop05_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop05_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop05_Sheave", "40_RopeRig/EightRopeLoops/Loop05_Z_FL"),
                new LoopBinding("L06_Z_FR", "B", "Z", "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop06_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop06_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop06_Sheave", "40_RopeRig/EightRopeLoops/Loop06_Z_FR"),
                new LoopBinding("L07_Z_RL", "B", "Z", "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop07_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop07_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop07_Sheave", "40_RopeRig/EightRopeLoops/Loop07_Z_RL"),
                new LoopBinding("L08_Z_RR", "B", "Z", "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop08_Exit", "20_TrolleyMotion/RopeDeadEnds/Loop08_DeadEnd", "30_SpreaderPhysics/RopeSheaves/Loop08_Sheave", "40_RopeRig/EightRopeLoops/Loop08_Z_RR")
            };
        }

        private static bool IsSafeRelativePath(string path)
        {
            return !string.IsNullOrWhiteSpace(path) && !path.StartsWith("/", StringComparison.Ordinal) &&
                   !path.StartsWith("\\", StringComparison.Ordinal) && !path.Contains("..") && !path.Contains(":");
        }

        private static bool IsFinite(Vector3 value)
        {
            return AllFinite(value.x, value.y, value.z);
        }

        private static bool AllFinite(params float[] values)
        {
            foreach (var value in values)
            {
                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    return false;
                }
            }

            return true;
        }

        private static void Increment(IDictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out var value);
            counts[key] = value + 1;
        }

        private static bool HasCount(IReadOnlyDictionary<string, int> counts, string key, int expected)
        {
            return counts.TryGetValue(key, out var actual) && actual == expected;
        }
    }
}
