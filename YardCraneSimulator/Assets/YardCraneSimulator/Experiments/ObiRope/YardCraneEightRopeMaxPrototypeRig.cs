using System;
using System.Collections.Generic;
using Obi;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Configuration;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Qingdao-port yard-crane greybox based on the new eight-rope MAX reeving.
    /// Each physical rope is one triangular loop:
    /// drum exit -> headblock sheave -> trolley dead end.
    ///
    /// The loop constraint carries the headblock. Obi renders the two visible legs
    /// of every loop and never acts as a second suspension model.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed class YardCraneEightRopeMaxPrototypeRig : MonoBehaviour, IYardCraneOperationActuator
    {
        private const string ImplementationVersion = "2026-09-03-eight-rope-max-v23-spreader-physical-rotation";
        private const float ContainerContactEnvelopeMarginMeters = 0.03f;

        private float GantrySpeedMetersPerSecond => activeProfile.motion.gantrySpeedMetersPerSecond;
        private float GantryAccelerationMetersPerSecondSquared => activeProfile.motion.gantryAccelerationMetersPerSecondSquared;
        private float GantryBrakingMetersPerSecondSquared => activeProfile.motion.gantryBrakingMetersPerSecondSquared;
        private float GantryTravelFromHomeMeters => activeProfile.motion.gantryTravelFromHomeMeters;
        private float GantryLimitSafetyMarginMeters => activeProfile.motion.gantryLimitSafetyMarginMeters;
        private float WheelClampTransitionSeconds => activeProfile.motion.wheelClampTransitionSeconds;
        private float AnchorTransitionSeconds => activeProfile.motion.anchorTransitionSeconds;
        private float SpreaderSizeTransitionSeconds => activeProfile.motion.spreaderSizeTransitionSeconds;
        private float SpreaderRotationMaxAngleDegrees => activeProfile.motion.spreaderRotationMaxAngleDegrees;
        private float SpreaderRotationRateDegreesPerSecond => activeProfile.motion.spreaderRotationRateDegreesPerSecond;
        private float SpreaderRotationReturnToZeroRateDegreesPerSecond => activeProfile.motion.spreaderRotationReturnToZeroRateDegreesPerSecond;
        private float HoistSpeedMetersPerSecond => activeProfile.motion.hoistSpeedMetersPerSecond;
        private float UnloadedHoistAccelerationMetersPerSecondSquared => activeProfile.motion.unloadedHoistAccelerationMetersPerSecondSquared;
        private float HoistBrakingMetersPerSecondSquared => activeProfile.motion.hoistBrakingMetersPerSecondSquared;
        private float TrolleySpeedMetersPerSecond => activeProfile.motion.trolleySpeedMetersPerSecond;
        private float TrolleyAccelerationMetersPerSecondSquared => activeProfile.motion.trolleyAccelerationMetersPerSecondSquared;
        private float TrolleyBrakingMetersPerSecondSquared => activeProfile.motion.trolleyBrakingMetersPerSecondSquared;
        private float FullyLoadedTrolleyAccelerationFactor => activeProfile.motion.fullyLoadedTrolleyAccelerationFactor;
        private float FullyLoadedTrolleyBrakingFactor => activeProfile.motion.fullyLoadedTrolleyBrakingFactor;
        private float FullyLoadedHoistSpeedFactor => activeProfile.motion.fullyLoadedHoistSpeedFactor;
        private float FullyLoadedHoistAccelerationFactor => activeProfile.motion.fullyLoadedHoistAccelerationFactor;
        private float TrolleyTravelFromHomeMeters => activeProfile.motion.trolleyTravelFromHomeMeters;
        private float TrolleyLimitSafetyMarginMeters => activeProfile.motion.trolleyLimitSafetyMarginMeters;
        private float LoadedTrolleyStopStabilizationSeconds => activeProfile.motion.loadedTrolleyStopStabilizationSeconds;
        private float LoadedTrolleyStopLinearDampingPerSecond => activeProfile.motion.loadedTrolleyStopLinearDampingPerSecond;
        private float LoadedTrolleyStopAngularDampingPerSecond => activeProfile.motion.loadedTrolleyStopAngularDampingPerSecond;
        private float LoadedSuspensionNaturalFrequencyRadiansPerSecond => activeProfile.ropePhysics.loadedSuspensionNaturalFrequencyRadiansPerSecond;
        private float LoadedSuspensionDampingRatio => activeProfile.ropePhysics.loadedSuspensionDampingRatio;
        private float MaximumLoadedSuspensionAccelerationMetersPerSecondSquared => activeProfile.ropePhysics.maximumLoadedSuspensionAccelerationMetersPerSecondSquared;
        private float LoadedHoistNaturalFrequencyRadiansPerSecond => activeProfile.ropePhysics.loadedHoistNaturalFrequencyRadiansPerSecond;
        private float LoadedHoistDampingRatio => activeProfile.ropePhysics.loadedHoistDampingRatio;
        private float LightLoadedStaticRopeDeflectionMeters => activeProfile.ropePhysics.lightLoadedStaticRopeDeflectionMeters;
        private float FullyLoadedStaticRopeDeflectionMeters => activeProfile.ropePhysics.fullyLoadedStaticRopeDeflectionMeters;
        private float MaximumLoadedHoistDeflectionMeters => activeProfile.ropePhysics.maximumLoadedHoistDeflectionMeters;
        private float LoadedHoistSettleDeflectionToleranceMeters => activeProfile.acceptance.loadedHoistSettleDeflectionToleranceMeters;
        private float LoadedHoistSettleVelocityToleranceMetersPerSecond => activeProfile.acceptance.loadedHoistSettleVelocityToleranceMetersPerSecond;
        private float LoadedHoistSettleHoldSeconds => activeProfile.acceptance.loadedHoistSettleHoldSeconds;
        private float LoadedHoistSettleTimeoutSeconds => activeProfile.acceptance.loadedHoistSettleTimeoutSeconds;
        private float MinimumSuspendedDropMeters => activeProfile.geometryAndSafety.minimumSuspendedDropMeters;
        private float MaximumSuspendedDropMeters => activeProfile.geometryAndSafety.maximumSuspendedDropMeters;
        private float SimulationProxyMassKg => activeProfile.load.simulationProxyMassKg;
        private float SpreaderRealMassKg => activeProfile.load.spreaderRealMassKg;
        private float MinimumContainerGrossMassKg => activeProfile.load.minimumContainerGrossMassKg;
        private float MaximumContainerGrossMassKg => activeProfile.load.maximumContainerGrossMassKg;
        private float ContainerHeightMeters => activeProfile.geometryAndSafety.containerHeightMeters;
        private float SpreaderThicknessMeters => activeProfile.geometryAndSafety.spreaderThicknessMeters;
        private Vector3 ContainerColliderSizeMeters => activeProfile.geometryAndSafety.containerColliderSizeMeters;
        private Vector3 HeadblockColliderSizeMeters => activeProfile.geometryAndSafety.headblockColliderSizeMeters;
        private float ContainerHorizontalLockToleranceMeters => activeProfile.geometryAndSafety.containerHorizontalLockToleranceMeters;
        private float ContainerVerticalLockToleranceMeters => activeProfile.geometryAndSafety.containerVerticalLockToleranceMeters;
        private float ContainerYawLockToleranceDegrees => activeProfile.geometryAndSafety.containerYawLockToleranceDegrees;
        private float ContainerLandingToleranceMeters => activeProfile.geometryAndSafety.containerLandingToleranceMeters;
        private float MaximumLockingSpeedMetersPerSecond => activeProfile.geometryAndSafety.maximumLockingSpeedMetersPerSecond;
        private float ContainerLandingStableHoldSeconds => activeProfile.geometryAndSafety.containerLandingStableHoldSeconds;
        private float MotionObservationSpeedThresholdMetersPerSecond => activeProfile.motion.motionObservationSpeedThresholdMetersPerSecond;
        private float ResetSafeSpeedThresholdMetersPerSecond => activeProfile.motion.resetSafeSpeedThresholdMetersPerSecond;
        private float LoadedRegressionStageTimeoutSeconds => activeProfile.regression.loadedStageTimeoutSeconds;
        private float LoadedRegressionTotalTimeoutSeconds => activeProfile.regression.loadedTotalTimeoutSeconds;
        private float LoadedRegressionStableHoldSeconds => activeProfile.regression.loadedStableHoldSeconds;
        private float LoadedRegressionLiftClearanceMeters => activeProfile.regression.loadedLiftClearanceMeters;
        private float LoadedRegressionOutboundTravelMeters => activeProfile.regression.loadedOutboundTravelMeters;
        private float LoadTransferPreTensionDurationSeconds => activeProfile.load.loadTransferPreTensionDurationSeconds;
        private float LoadedTrolleyMinimumClearanceMeters => activeProfile.geometryAndSafety.loadedTrolleyMinimumClearanceMeters;

        private float LoopConstraintToleranceMeters => activeProfile.ropePhysics.loopConstraintToleranceMeters;
        private float MaximumPermittedLoopExtensionMeters => activeProfile.acceptance.maximumPermittedLoopExtensionMeters;
        private float StartupPreTensionDurationSeconds => activeProfile.ropePhysics.startupPreTensionDurationSeconds;
        private int PositionConstraintIterations => activeProfile.ropePhysics.positionConstraintIterations;
        private int VelocityConstraintIterations => activeProfile.ropePhysics.velocityConstraintIterations;
        private int InterlockPositionIterations => activeProfile.ropePhysics.interlockPositionIterations;
        private int InterlockVelocityIterations => activeProfile.ropePhysics.interlockVelocityIterations;
        private float InterlockPositionRelaxation => activeProfile.ropePhysics.interlockPositionRelaxation;
        private float InterlockVelocityRelaxation => activeProfile.ropePhysics.interlockVelocityRelaxation;
        private float MaximumAngularCorrectionDegrees => activeProfile.ropePhysics.maximumAngularCorrectionDegrees;
        private float UnloadedHeadblockLinearDamping => activeProfile.ropePhysics.unloadedHeadblockLinearDamping;
        private float UnloadedHeadblockAngularDamping => activeProfile.ropePhysics.unloadedHeadblockAngularDamping;
        private float LoadedHeadblockAngularDamping => activeProfile.ropePhysics.loadedHeadblockAngularDamping;
        private int HeadblockSolverIterations => activeProfile.ropePhysics.headblockSolverIterations;
        private int HeadblockSolverVelocityIterations => activeProfile.ropePhysics.headblockSolverVelocityIterations;
        private float PrototypeMaximumSwayMeters => activeProfile.acceptance.prototypeMaximumSwayMeters;
        private float PrototypeMaximumResidualTwistDegrees => activeProfile.acceptance.prototypeMaximumResidualTwistDegrees;
        private string TopologyProfile => activeProfile.identity.topologyProfileId;
        private int solverSubsteps => activeProfile.ropePhysics.obiSolverSubsteps;
        private int distanceIterations => activeProfile.ropePhysics.obiDistanceIterations;
        private Vector3 containerLockedLocalPosition => activeProfile.load.containerLockedLocalPosition;
        private LoopDefinition[] LoopDefinitions => loopDefinitions;

        private enum LoadedRegressionStage
        {
            Idle,
            AlignForLock,
            AwaitLock,
            AwaitLoadedReady,
            VerifyLowClearanceInterlock,
            HoistToSafeClearance,
            RejectSuspendedUnlock,
            TravelOutbound,
            ReturnHome,
            LowerToLanding,
            AwaitLanding,
            AwaitUnlock,
            EmptyHoist,
            Complete,
            Failed
        }

        [Header("Versioned Configuration")]
        [SerializeField] private YardCraneEightRopeMaxProfile profileAsset;

        [Header("MAX Prototype")]
        [SerializeField] private bool showGraybox = true;
        [SerializeField] private bool runAutomatedDemo = true;
        [SerializeField] private bool showRuntimePanel = true;
        [SerializeField] private YardCraneInputProfile standaloneInputProfile;

        [Header("Container Load")]
        [SerializeField, Min(0f)] private float containerGrossMassKg = 30000f;

        private readonly List<LoopRuntime> loops = new();
        private readonly List<Material> runtimeMaterials = new();

        private Transform gantryMotion;
        private Transform trolleyMotion;
        private Transform headblockPhysics;
        private Transform spreaderRotationPivot;
        private Transform spreaderGrayboxVisualRoot;
        private Transform spreaderColliderRoot;
        private Transform spreaderSensorRoot;
        private Transform containerAttachmentRoot;
        private Transform activeContainer;
        private Transform containerOriginalParent;
        private Rigidbody headblockBody;
        private BoxCollider headblockCollider;
        private BoxCollider containerCollider;
        private ObiSolver solver;
        private Material ropeMaterial;
        private YardCraneEightRopeMaxProfile activeProfile;
        private YardCraneGantryDriveModel gantryDrive;
        private YardCraneAnchorInterlock trolleyAnchor;
        private YardCraneAnchorInterlock gantryAnchor;
        private YardCraneSpreaderSizeInterlock spreaderSize;
        private bool spreaderPumpOn;
        private YardCraneSpreaderRotationModel spreaderRotation;
        private float spreaderRotationInputAxis;
        private float spreaderPhysicalRotationAngleDegrees;
        private float spreaderRotationAngularVelocityRadiansPerSecond;
        private bool spreaderRotationInhibitReported;
        private bool spreaderRotationMotionReported;
        private Quaternion spreaderRotationPivotHomeLocalRotation;
        private Quaternion spreaderGrayboxVisualHomeLocalRotation;
        private Quaternion spreaderColliderHomeLocalRotation;
        private Quaternion spreaderSensorHomeLocalRotation;
        private Quaternion containerAttachmentHomeLocalRotation;
        private LoopDefinition[] loopDefinitions;
        private string activeProfileSource;
        private Vector3 gantryHome;
        private Vector3 trolleyHome;
        private Vector3 headblockHome;
        private Quaternion headblockHomeRotation;
        private float initialSuspendedDrop;
        private float commandedSuspendedDrop;
        private float currentTrolleyVelocity;
        private float currentHoistVelocity;
        private float gantryInput;
        private float trolleyInput;
        private float hoistInput;
        private YardCraneOperationController operationController;
        private YardCraneStandaloneOperationHost standaloneOperationHost;
        private float demoStartedAt;
        private float startupReleaseAt;
        private float loadTransferReleaseAt;
        private float trolleyStopStabilizationReleaseAt;
        private float maximumObservedSway;
        private float currentHorizontalSway;
        private float currentRelativeHorizontalSpeed;
        private float loadedHoistDeflection;
        private float loadedHoistDeflectionVelocity;
        private float previousHoistSupportVelocity;
        private float previousHoistInput;
        private float loadedHoistSettleReleaseAt;
        private float loadedHoistSettleDrumStopAt;
        private float loadedHoistSettleStableSince = -1f;
        private float loadedHoistSettlePeakDeflection;
        private float loadedHoistSettlePeakError;
        private float loadedHoistSettlePeakVelocity;
        private float maximumObservedLoopExtension;
        private float maximumRawLoopDrift;
        private float maximumObservedTwistDegrees;
        private float residualTwistDegrees;
        private float averageRopeTensionKn;
        private float totalSuspendedMassKg;
        private float containerHorizontalErrorMeters;
        private float containerVerticalErrorMeters;
        private float containerYawErrorDegrees;
        private float containerLandingHorizontalErrorMeters;
        private float containerLandingVerticalErrorMeters;
        private float containerLandingYawErrorDegrees;
        private float containerLandingSpeedMetersPerSecond;
        private float containerLandingStableSince = -1f;
        private float loadedRegressionStartedAt;
        private float loadedRegressionStageStartedAt;
        private float loadedRegressionStableSince = -1f;
        private float loadedRegressionReferenceX;
        private float loadedRegressionTargetDrop;
        private Vector3 loadedRegressionReleasedContainerPosition;
        private Vector3 containerHomePosition;
        private Quaternion containerHomeRotation;
        private Vector3 loadedSuspensionEquilibriumOffset;
        private Vector3 unloadedInertiaTensor;
        private Quaternion unloadedInertiaTensorRotation;
        private float containerRestingCenterY;
        private string peakLoopId = "-";
        private string twistlockStatus = "Unlocked";
        private bool twistDisturbanceApplied;
        private bool suspensionGravityActive;
        private bool containerLocked;
        private bool containerLockRequested;
        private bool containerUnlockRequested;
        private bool loadTransferPreTensioning;
        private bool containerColliderSuppressedForPickup;
        private bool loadedHoistStopPending;
        private bool loadedHoistSettleObservationActive;
        private bool loadedHoistSettleDrumStopped;
        private bool containerLandingConfirmed;
        private bool containerLandingReported;
        private bool trolleyMotionReported;
        private bool trolleyLimitReported;
        private bool gantryMotionReported;
        private bool gantryLimitReported;
        private bool gantryClampInhibitReported;
        private bool gantryClearanceInhibitReported;
        private bool trolleyClearanceInhibitReported;
        private bool gantryAnchorInhibitReported;
        private bool trolleyAnchorInhibitReported;
        private bool gantryProximityInhibited;
        private bool trolleyProximityInhibited;
        private bool hoistMotionReported;
        private bool hoistLimitReported;
        private bool loadedOperationRegressionActive;
        private bool loadedRegressionCommandIssued;
        private LoadedRegressionStage loadedRegressionStage;
        private bool loopsReady;
        private bool startupPreTensioning;
        private bool demoCompleted;
        private bool demoClampApplyRequested;
        private bool demoClampReleaseRequested;
        private bool demoClampInhibitObserved;
        private float demoGantryStartPosition;
        private float demoGantryMaximumTravel;
        private bool setupFailed;
        private bool hornActive;
        private AudioSource hornSource;
        private AudioClip runtimeHornClip;
        private string runtimeStatus = "Initializing eight-rope MAX prototype";

        public bool ShowGraybox => showGraybox;
        public bool LoopsReady => loopsReady;
        public string RuntimeStatus => runtimeStatus;
        public bool ContainerLocked => containerLocked;
        public float ContainerGrossMassKg => containerGrossMassKg;
        public float AverageRopeTensionKn => averageRopeTensionKn;
        public bool ContainerLandingConfirmed => containerLandingConfirmed;
        public bool LoadedOperationRegressionActive => loadedOperationRegressionActive;
        public YardCraneOperationController OperationController => operationController;
        public YardCraneOperationPhase OperationPhase => operationController?.Phase ?? YardCraneOperationPhase.WaitingForRun;
        public float GantryPositionMeters => gantryDrive?.PositionMeters ?? 0f;
        public float GantrySpeedMetersPerSecondActual => gantryDrive?.VelocityMetersPerSecond ?? 0f;
        public WheelClampState CurrentWheelClampState => gantryDrive?.WheelClampState ?? WheelClampState.Unknown;
        public bool HornActive => hornActive;
        public YardCraneAnchorState CurrentTrolleyAnchorState => trolleyAnchor?.State ?? YardCraneAnchorState.Unknown;
        public YardCraneAnchorState CurrentGantryAnchorState => gantryAnchor?.State ?? YardCraneAnchorState.Unknown;
        public bool SpreaderPumpOn => spreaderPumpOn;
        public YardCraneSpreaderSize CurrentSpreaderSize => spreaderSize?.CurrentSize ?? YardCraneSpreaderSize.Unknown;
        public YardCraneSpreaderSizeMechanismState CurrentSpreaderSizeState =>
            spreaderSize?.State ?? YardCraneSpreaderSizeMechanismState.Idle;
        public float SpreaderRotationAngleDegrees => spreaderRotation?.CurrentAngleDegrees ?? 0f;
        public float SpreaderPhysicalRotationAngleDegrees => spreaderPhysicalRotationAngleDegrees;
        public bool SpreaderRotationReturningToZero => spreaderRotation?.IsReturningToZero ?? false;
        public bool SpreaderPhysicalRotationBound =>
            spreaderRotationPivot != null && spreaderGrayboxVisualRoot != null &&
            spreaderColliderRoot != null && spreaderSensorRoot != null && containerAttachmentRoot != null;

        bool IYardCraneOperationActuator.IsInitialized => loopsReady && !setupFailed;
        bool IYardCraneOperationActuator.IsPreTensioning => startupPreTensioning;
        bool IYardCraneOperationActuator.IsLoadTransferActive => loadTransferPreTensioning;
        bool IYardCraneOperationActuator.IsMechanismBusy =>
            startupPreTensioning || loadTransferPreTensioning || containerLockRequested ||
            containerUnlockRequested || loadedOperationRegressionActive ||
            (gantryDrive != null && gantryDrive.WheelClampState == WheelClampState.Transitioning) ||
            (trolleyAnchor != null && trolleyAnchor.State == YardCraneAnchorState.Transitioning) ||
            (gantryAnchor != null && gantryAnchor.State == YardCraneAnchorState.Transitioning) ||
            (spreaderSize != null && spreaderSize.State == YardCraneSpreaderSizeMechanismState.Transitioning) ||
            (runAutomatedDemo && !demoCompleted);
        bool IYardCraneOperationActuator.IsFaulted => setupFailed;
        bool IYardCraneOperationActuator.IsStationary => IsMechanismStationary();
        bool IYardCraneOperationActuator.IsContainerLocked => containerLocked;
        bool IYardCraneOperationActuator.IsContainerLandingConfirmed => containerLandingConfirmed;
        bool IYardCraneOperationActuator.IsAlignedForContainerLock => IsContainerAlignedForLock();
        bool IYardCraneOperationActuator.IsWheelClampClamped =>
            gantryDrive != null && gantryDrive.WheelClampState == WheelClampState.Clamped;
        bool IYardCraneOperationActuator.IsWheelClampTransitioning =>
            gantryDrive != null && gantryDrive.WheelClampState == WheelClampState.Transitioning;
        bool IYardCraneOperationActuator.IsTrolleyAnchorApplied =>
            trolleyAnchor != null && trolleyAnchor.State == YardCraneAnchorState.Anchored;
        bool IYardCraneOperationActuator.IsTrolleyAnchorTransitioning =>
            trolleyAnchor != null && trolleyAnchor.State == YardCraneAnchorState.Transitioning;
        bool IYardCraneOperationActuator.IsGantryAnchorApplied =>
            gantryAnchor != null && gantryAnchor.State == YardCraneAnchorState.Anchored;
        bool IYardCraneOperationActuator.IsGantryAnchorTransitioning =>
            gantryAnchor != null && gantryAnchor.State == YardCraneAnchorState.Transitioning;
        bool IYardCraneOperationActuator.IsSpreaderPumpOn => spreaderPumpOn;
        bool IYardCraneOperationActuator.IsSpreaderSizeTransitioning =>
            spreaderSize != null && spreaderSize.State == YardCraneSpreaderSizeMechanismState.Transitioning;

        public bool TrySetContainerGrossMass(float grossMassKg)
        {
            if (activeProfile == null || containerLocked || grossMassKg < MinimumContainerGrossMassKg ||
                grossMassKg > MaximumContainerGrossMassKg)
            {
                return false;
            }

            containerGrossMassKg = grossMassKg;
            UpdateContainerLoadFeedback();
            return true;
        }

        public bool RequestContainerLock()
        {
            if (!loopsReady || containerLocked || containerLockRequested)
            {
                return false;
            }

            containerLockRequested = true;
            return true;
        }

        public bool RequestContainerUnlock()
        {
            if (!loopsReady || !containerLocked || containerUnlockRequested)
            {
                return false;
            }

            containerUnlockRequested = true;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueContainerLock(out string rejectionCode)
        {
            if (!loopsReady || setupFailed)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (startupPreTensioning || loadTransferPreTensioning || containerLockRequested ||
                containerUnlockRequested || loadedOperationRegressionActive || (runAutomatedDemo && !demoCompleted))
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismBusy;
                return false;
            }

            UpdateContainerAlignmentErrors();
            if (containerLocked || !IsContainerAlignedForLock() || !IsMechanismStationary())
            {
                rejectionCode = YardCraneOperationErrorCodes.LockInterlockRejected;
                return false;
            }

            if (!RequestContainerLock())
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismBusy;
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueContainerUnlock(out string rejectionCode)
        {
            if (!loopsReady || setupFailed)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (startupPreTensioning || loadTransferPreTensioning || containerLockRequested ||
                containerUnlockRequested || loadedOperationRegressionActive || (runAutomatedDemo && !demoCompleted))
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismBusy;
                return false;
            }

            if (!containerLocked || !containerLandingConfirmed || !IsMechanismStationary())
            {
                rejectionCode = YardCraneOperationErrorCodes.UnlockInterlockRejected;
                return false;
            }

            if (!RequestContainerUnlock())
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismBusy;
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryReset(out string rejectionCode)
        {
            return TryResetPrototype(out rejectionCode);
        }

        bool IYardCraneOperationActuator.TryQueueWheelClamp(bool clampApplied, out string rejectionCode)
        {
            if (gantryDrive == null || setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!gantryDrive.TryRequestWheelClamp(clampApplied, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.WheelClampInterlockRejected;
                Debug.LogWarning(
                    $"[YC_WHEEL_CLAMP_REJECTED] target={(clampApplied ? "Clamped" : "Released")}, " +
                    $"state={gantryDrive.WheelClampState}, speed={gantryDrive.VelocityMetersPerSecond:F3}m/s, " +
                    $"reason={driveError}",
                    this);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_WHEEL_CLAMP_REQUESTED] target={(clampApplied ? "Clamped" : "Released")}, " +
                $"state={gantryDrive.WheelClampState}",
                this);
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueTrolleyAnchor(bool anchorApplied, out string rejectionCode)
        {
            if (trolleyAnchor == null || setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            var stationary = Mathf.Abs(currentTrolleyVelocity) <= ResetSafeSpeedThresholdMetersPerSecond;
            if (!trolleyAnchor.TryRequestAnchor(anchorApplied, stationary, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                Debug.LogWarning(
                    $"[YC_TROLLEY_ANCHOR_REJECTED] target={(anchorApplied ? "Anchored" : "Released")}, " +
                    $"state={trolleyAnchor.State}, speed={currentTrolleyVelocity:F3}m/s, reason={driveError}",
                    this);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_TROLLEY_ANCHOR_REQUESTED] target={(anchorApplied ? "Anchored" : "Released")}, " +
                $"state={trolleyAnchor.State}",
                this);
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueGantryAnchor(bool anchorApplied, out string rejectionCode)
        {
            if (gantryAnchor == null || gantryDrive == null || setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!gantryAnchor.TryRequestAnchor(anchorApplied, gantryDrive.IsStationary, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                Debug.LogWarning(
                    $"[YC_GANTRY_ANCHOR_REJECTED] target={(anchorApplied ? "Anchored" : "Released")}, " +
                    $"state={gantryAnchor.State}, speed={gantryDrive.VelocityMetersPerSecond:F3}m/s, " +
                    $"reason={driveError}",
                    this);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_GANTRY_ANCHOR_REQUESTED] target={(anchorApplied ? "Anchored" : "Released")}, " +
                $"state={gantryAnchor.State}",
                this);
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderPump(bool pumpOn, out string rejectionCode)
        {
            if (setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            if (spreaderPumpOn == pumpOn)
            {
                return true;
            }

            spreaderPumpOn = pumpOn;
            Debug.Log($"[YC_SPREADER_PUMP_REQUESTED] target={(pumpOn ? "On" : "Off")}", this);
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderSize(string targetSize, out string rejectionCode)
        {
            if (spreaderSize == null || setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!TryParseSpreaderSize(targetSize, out var parsedSize))
            {
                rejectionCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                return false;
            }

            if (!spreaderSize.TryRequestSize(
                    parsedSize, spreaderPumpOn, IsMechanismStationary(), containerLocked, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderSizeInterlockRejected;
                Debug.LogWarning(
                    $"[YC_SPREADER_SIZE_REJECTED] target={targetSize}, state={spreaderSize.State}, " +
                    $"currentSize={spreaderSize.CurrentSize}, pumpOn={spreaderPumpOn}, " +
                    $"containerLocked={containerLocked}, reason={driveError}",
                    this);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_SPREADER_SIZE_REQUESTED] target={targetSize}, state={spreaderSize.State}",
                this);
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderRotationReturnToZero(out string rejectionCode)
        {
            if (spreaderRotation == null || setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!spreaderRotation.TryRequestReturnToZero(spreaderPumpOn, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderRotationInterlockRejected;
                Debug.LogWarning(
                    $"[YC_SPREADER_ROTATION_RETURN_TO_ZERO_REJECTED] angle={spreaderRotation.CurrentAngleDegrees:F2}deg, " +
                    $"pumpOn={spreaderPumpOn}, reason={driveError}",
                    this);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_SPREADER_ROTATION_RETURN_TO_ZERO_REQUESTED] angle={spreaderRotation.CurrentAngleDegrees:F2}deg",
                this);
            return true;
        }

        private static bool TryParseSpreaderSize(string targetSize, out YardCraneSpreaderSize parsedSize)
        {
            switch (targetSize)
            {
                case "FT20":
                    parsedSize = YardCraneSpreaderSize.Ft20;
                    return true;
                case "FT40":
                    parsedSize = YardCraneSpreaderSize.Ft40;
                    return true;
                case "Twin20":
                    parsedSize = YardCraneSpreaderSize.Twin20;
                    return true;
                default:
                    parsedSize = YardCraneSpreaderSize.Unknown;
                    return false;
            }
        }

        void IYardCraneOperationActuator.SetMotionInputs(float gantry, float trolley, float hoist)
        {
            gantryInput = gantry;
            trolleyInput = trolley;
            hoistInput = hoist;
        }

        void IYardCraneOperationActuator.SetHornActive(bool active)
        {
            SetHornActive(active);
        }

        void IYardCraneOperationActuator.SetSpreaderRotationInput(float rotation)
        {
            spreaderRotationInputAxis = rotation;
        }

        void IYardCraneOperationActuator.ClearMotionInputs()
        {
            gantryInput = 0f;
            trolleyInput = 0f;
            hoistInput = 0f;
            spreaderRotationInputAxis = 0f;
            SetHornActive(false);
        }

        private bool IsContainerAlignedForLock()
        {
            return containerHorizontalErrorMeters <= ContainerHorizontalLockToleranceMeters &&
                   containerVerticalErrorMeters <= ContainerVerticalLockToleranceMeters &&
                   containerYawErrorDegrees <= ContainerYawLockToleranceDegrees;
        }

        private Vector3 TrolleyAxisWorldDirection =>
            trolleyMotion.parent != null
                ? trolleyMotion.parent.TransformDirection(Vector3.right).normalized
                : Vector3.right;

        private Vector3 GantryAxisWorldDirection =>
            gantryMotion.parent != null
                ? gantryMotion.parent.TransformDirection(Vector3.forward).normalized
                : Vector3.forward;

        // Trolley and gantry translate the upper rope boundary kinematically. The interlock
        // therefore predicts contact from the actual collider surface gaps and the current
        // axis stopping distance. Center-error tolerances are intentionally not used here:
        // they describe lock alignment, not collision clearance, and previously stopped
        // fine alignment while the spreader was still about 0.30m above the box.
        private bool IsHorizontalTravelInhibitedByContainerProximity(
            float requestedAxisInput,
            Vector3 axisWorldDirection,
            float currentAxisVelocityMetersPerSecond,
            float brakingMetersPerSecondSquared,
            out float clearanceMeters,
            out float requiredStoppingMarginMeters)
        {
            if (Mathf.Approximately(requestedAxisInput, 0f))
            {
                clearanceMeters = float.PositiveInfinity;
                requiredStoppingMarginMeters = 0f;
                return false;
            }

            if (containerLocked)
            {
                clearanceMeters = activeContainer.position.y - containerRestingCenterY;
                requiredStoppingMarginMeters = LoadedTrolleyMinimumClearanceMeters;
                return clearanceMeters < LoadedTrolleyMinimumClearanceMeters;
            }

            if (headblockCollider == null || containerCollider == null ||
                !headblockCollider.enabled || !containerCollider.enabled)
            {
                clearanceMeters = float.PositiveInfinity;
                requiredStoppingMarginMeters = 0f;
                return false;
            }

            var axis = axisWorldDirection.normalized;
            var orthogonalAxis = Vector3.Cross(Vector3.up, axis).normalized;
            if (!IsFinite(axis) || !IsFinite(orthogonalAxis) ||
                axis.sqrMagnitude <= 0.5f || orthogonalAxis.sqrMagnitude <= 0.5f)
            {
                clearanceMeters = float.NaN;
                requiredStoppingMarginMeters = float.NaN;
                return true;
            }

            var headblockBounds = headblockCollider.bounds;
            var containerBounds = containerCollider.bounds;
            var centerDelta = containerBounds.center - headblockBounds.center;
            var axisCenterOffset = Vector3.Dot(centerDelta, axis);
            var axisSurfaceGap = Mathf.Abs(axisCenterOffset) -
                                 ProjectedBoundsRadius(headblockBounds, axis) -
                                 ProjectedBoundsRadius(containerBounds, axis);
            var orthogonalSurfaceGap = Mathf.Abs(Vector3.Dot(centerDelta, orthogonalAxis)) -
                                       ProjectedBoundsRadius(headblockBounds, orthogonalAxis) -
                                       ProjectedBoundsRadius(containerBounds, orthogonalAxis);
            var verticalSurfaceGap = Mathf.Abs(centerDelta.y) -
                                     headblockBounds.extents.y - containerBounds.extents.y;

            var currentVelocityClosesGap = currentAxisVelocityMetersPerSecond * axisCenterOffset > 0f;
            var closingSpeed = currentVelocityClosesGap
                ? Mathf.Abs(currentAxisVelocityMetersPerSecond)
                : 0f;
            var braking = Mathf.Max(brakingMetersPerSecondSquared, 0.0001f);
            requiredStoppingMarginMeters = ContainerContactEnvelopeMarginMeters +
                                           closingSpeed * Time.fixedDeltaTime +
                                           closingSpeed * closingSpeed / (2f * braking);
            clearanceMeters = Mathf.Max(axisSurfaceGap,
                Mathf.Max(orthogonalSurfaceGap, verticalSurfaceGap));

            return YardCraneContainerApproachInterlock.ShouldInhibit(
                requestedAxisInput,
                axisCenterOffset,
                axisSurfaceGap,
                orthogonalSurfaceGap,
                verticalSurfaceGap,
                requiredStoppingMarginMeters,
                ContainerContactEnvelopeMarginMeters);
        }

        private static float ProjectedBoundsRadius(Bounds bounds, Vector3 axis)
        {
            var extents = bounds.extents;
            return Mathf.Abs(axis.x) * extents.x +
                   Mathf.Abs(axis.y) * extents.y +
                   Mathf.Abs(axis.z) * extents.z;
        }

        private bool IsMechanismStationary()
        {
            if (headblockBody == null)
            {
                return false;
            }

            return Mathf.Abs(currentTrolleyVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                   (gantryDrive == null || gantryDrive.IsStationary) &&
                   (gantryDrive == null || gantryDrive.WheelClampState != WheelClampState.Transitioning) &&
                   Mathf.Abs(currentHoistVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                   headblockBody.linearVelocity.magnitude <= ResetSafeSpeedThresholdMetersPerSecond &&
                   headblockBody.angularVelocity.magnitude <= ResetSafeSpeedThresholdMetersPerSecond;
        }

        private void InitializeOperationController()
        {
            operationController = new YardCraneOperationController(this);
            operationController.PhaseChanged += OnOperationPhaseChanged;
            operationController.InputBecameStale += OnOperationInputBecameStale;

            var bootstrap = FindFirstObjectByType<YardCraneBootstrap>();
            var launchOptions = bootstrap != null ? bootstrap.LaunchOptions : null;
            if (bootstrap != null &&
                (launchOptions == null || launchOptions.LaunchMode == YardCraneLaunchMode.Managed))
            {
                Debug.Log("[YC_MANAGED_INPUT_WAITING] standaloneInput=false", this);
                return;
            }

            standaloneOperationHost = GetComponent<YardCraneStandaloneOperationHost>();
            if (standaloneOperationHost == null)
            {
                standaloneOperationHost = gameObject.AddComponent<YardCraneStandaloneOperationHost>();
            }

            var stationId = launchOptions?.StationId ?? "YC-EDITOR-01";
            standaloneOperationHost.Configure(operationController, stationId, standaloneInputProfile);
        }

        private void OnOperationPhaseChanged(
            YardCraneOperationPhase previous,
            YardCraneOperationPhase next,
            string reason)
        {
            Debug.Log(
                $"[YC_OPERATION_PHASE] from={previous}, to={next}, reason={reason}, " +
                $"run={operationController.Context.OperationRunId:D}",
                this);
        }

        private void OnOperationInputBecameStale(int dataAgeMs, YardCraneInputQuality quality)
        {
            Debug.LogWarning(
                $"[YC_OPERATION_INPUT_STALE] dataAgeMs={dataAgeMs}, quality={quality}, " +
                $"threshold={YardCraneOperationController.StaleWarningThresholdMs}ms",
                this);
        }

        private static long GetMonotonicMilliseconds()
        {
            return (long)(Time.realtimeSinceStartupAsDouble * 1000d);
        }

        private void Awake()
        {
            try
            {
                ResolvePhysicsProfile();
                ResolveHierarchy();
                ApplyGrayboxVisibility();
                CreateRuntimeMaterials();
                ConfigureGantryDrive();
                ConfigureHeadblockPhysics();
                InitializePhysicalSpreaderRotation();
                ConfigureContainerLoad();
                ConfigureHorn();
                CreateSolver();
                CreateEightRopeLoops();
                EnsurePrototypeView();

                gantryHome = gantryMotion.localPosition;
                trolleyHome = trolleyMotion.localPosition;
                headblockHome = headblockPhysics.position;
                headblockHomeRotation = headblockPhysics.rotation;
                initialSuspendedDrop = CalculateSuspendedDrop();
                commandedSuspendedDrop = initialSuspendedDrop;
                InitializeOperationController();
                runtimeStatus = "Waiting for 16 Obi visual rope legs";
            }
            catch (Exception exception)
            {
                setupFailed = true;
                runtimeStatus = exception.Message;
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void ResolvePhysicsProfile()
        {
            activeProfile = profileAsset;
            activeProfileSource = "Inspector";
            if (activeProfile == null)
            {
                activeProfile = Resources.Load<YardCraneEightRopeMaxProfile>(
                    YardCraneEightRopeMaxProfile.DefaultResourcePath);
                activeProfileSource = "Resources";
            }

            if (activeProfile == null)
            {
#if UNITY_EDITOR
                activeProfile = YardCraneEightRopeMaxProfile.CreateRuntimeDefaults();
                activeProfileSource = "RuntimeFallback";
                Debug.LogWarning(
                    $"[YC_PHYSICS_PROFILE_FALLBACK] resource={YardCraneEightRopeMaxProfile.DefaultResourcePath}",
                    this);
#else
                throw new InvalidOperationException(
                    $"Required yard-crane physics profile is missing from Resources/{YardCraneEightRopeMaxProfile.DefaultResourcePath}.asset");
#endif
            }

            if (!activeProfile.TryValidate(out var error))
            {
                throw new InvalidOperationException(
                    $"Yard-crane physics profile '{activeProfile.name}' is invalid: {error}");
            }

            containerGrossMassKg = Mathf.Clamp(
                containerGrossMassKg,
                MinimumContainerGrossMassKg,
                MaximumContainerGrossMassKg);
            loopDefinitions = new LoopDefinition[activeProfile.loopBindings.Length];
            for (var index = 0; index < activeProfile.loopBindings.Length; index++)
            {
                var binding = activeProfile.loopBindings[index];
                loopDefinitions[index] = new LoopDefinition(
                    binding.id,
                    binding.drum,
                    binding.antiSwayAxis,
                    binding.exitPath,
                    binding.deadEndPath,
                    binding.sheavePath,
                    binding.slotPath);
            }

            Debug.Log(
                $"[YC_PHYSICS_PROFILE_LOADED] schema={activeProfile.identity.schemaVersion}, " +
                $"profile={activeProfile.identity.profileId}, topology={TopologyProfile}, " +
                $"source={activeProfileSource}, loops={loopDefinitions.Length}, " +
                $"calibration={activeProfile.identity.calibrationStatus}",
                this);
        }

        private void Update()
        {
            if (setupFailed)
            {
                return;
            }

            if (!loopsReady)
            {
                TryFinishVisualInitialization();
                operationController?.Tick();
                return;
            }

            HandleDeveloperShortcuts();
            operationController?.Tick();
        }

        private void FixedUpdate()
        {
            if (!loopsReady || setupFailed)
            {
                return;
            }

            // The driven child rotation is advanced later in this fixed step. Clear
            // the derived angular velocity first so command-processing and any early
            // return cannot observe the previous step's velocity as current motion.
            spreaderRotationAngularVelocityRadiansPerSecond = 0f;
            ProcessPendingContainerCommands();
            operationController?.Tick();

            if (loadTransferPreTensioning)
            {
                RunLoadTransferPreTension();
                operationController?.Tick();
                return;
            }

            if (startupPreTensioning)
            {
                RunStartupPreTension();
                operationController?.Tick();
                return;
            }

            // Sample the pose left by the previous PhysX step before advancing
            // trolley boundaries or commanded rope lengths for this step. Sampling
            // after those commands but before PhysX integration compares two
            // different time instants and reports a false cable extension.
            ObserveRuntimeState();

            // Apply gravity before the cable velocity solve. Leaving Rigidbody
            // gravity enabled would apply it later inside PhysX, after our loop
            // projection, producing a visible 50 Hz fall/correct sawtooth.
            if (suspensionGravityActive)
            {
                headblockBody.linearVelocity += Physics.gravity * Time.fixedDeltaTime;
            }

            if (runAutomatedDemo && !demoCompleted)
            {
                RunAutomatedDemo(Time.fixedTime - demoStartedAt);
            }
            else
            {
                if (loadedOperationRegressionActive)
                {
                    UpdateLoadedOperationRegression();
                }
                UpdateManualGantryMotion();
                UpdateManualTrolleyMotion();
                UpdateManualHoistMotion();
                spreaderSize?.Advance(Time.fixedDeltaTime);
                AdvanceSpreaderRotation();
            }

            ApplyLoadedHoistSuspensionDynamics();
            ApplyLoadedSuspensionDynamics();
            ApplyLoadedTrolleyStopStabilization();
            CaptureBoundaryMotion();
            SolveEightLoopConstraints();
            SynchronizeVisualLegLengths();
            ProjectVisualRopesToEngineeringGeometry();
            UpdateContainerLoadFeedback();
            UpdateContainerLandingConfirmation();
            UpdateContainerContactStatus();
            RestoreContainerColliderWhenClear();
            operationController?.Tick();
        }

        private void LateUpdate()
        {
            if (!loopsReady || setupFailed)
            {
                return;
            }

            // Obi updates static attachments at the start of its simulation step,
            // while the trolley and headblock move later in FixedUpdate. Projecting
            // after all motion removes the one-step visual lag and makes both ends of
            // every taut MAX leg follow the current engineering anchors exactly.
            ProjectVisualRopesToEngineeringGeometry();
        }

        private void ResolveHierarchy()
        {
            gantryMotion = RequireTransform("10_GantryMotion");
            trolleyMotion = RequireTransform("20_TrolleyMotion");
            headblockPhysics = RequireTransform("30_SpreaderPhysics");
            spreaderRotationPivot = RequireTransform("30_SpreaderPhysics/35_Spreader");
            spreaderGrayboxVisualRoot = RequireTransform("30_SpreaderPhysics/Visual/Graybox_Placeholder");
            spreaderColliderRoot = RequireTransform("30_SpreaderPhysics/Physics/Colliders");
            spreaderSensorRoot = RequireTransform("30_SpreaderPhysics/Sensors");
            containerAttachmentRoot = RequireTransform("30_SpreaderPhysics/ContainerAttachment");
            activeContainer = RequireTransform("50_ContainerWorkspace/ActiveContainer");

            // These nodes are direct children of the rope-owned headblock in the
            // v1 hierarchy. Preserve their authored rest rotations so imported
            // production models do not have to use identity rotations.
            spreaderRotationPivotHomeLocalRotation = spreaderRotationPivot.localRotation;
            spreaderGrayboxVisualHomeLocalRotation = spreaderGrayboxVisualRoot.localRotation;
            spreaderColliderHomeLocalRotation = spreaderColliderRoot.localRotation;
            spreaderSensorHomeLocalRotation = spreaderSensorRoot.localRotation;
            containerAttachmentHomeLocalRotation = containerAttachmentRoot.localRotation;

            foreach (var definition in LoopDefinitions)
            {
                RequireTransform(definition.ExitPath);
                RequireTransform(definition.DeadEndPath);
                RequireTransform(definition.SheavePath);
                RequireTransform(definition.SlotPath);
            }
        }

        private Transform RequireTransform(string path)
        {
            var target = transform.Find(path);
            if (target == null)
            {
                throw new InvalidOperationException($"YC_RuntimeRoot is missing MAX topology node: {path}");
            }

            return target;
        }

        private void InitializePhysicalSpreaderRotation()
        {
            ApplySpreaderRotationPose(0f, true);
            Debug.Log(
                $"[YC_SPREADER_ROTATION_PHYSICAL_BOUND] model={ImplementationVersion}, " +
                "pivot=30_SpreaderPhysics/35_Spreader, visual=legacy-graybox, " +
                "collider=Physics/Colliders, attachment=ContainerAttachment, sensors=Sensors",
                this);
        }

        private void ConfigureGantryDrive()
        {
            var configuration = new YardCraneGantryDriveConfiguration(
                GantrySpeedMetersPerSecond,
                GantryAccelerationMetersPerSecondSquared,
                GantryBrakingMetersPerSecondSquared,
                GantryTravelFromHomeMeters,
                GantryLimitSafetyMarginMeters,
                WheelClampTransitionSeconds,
                ResetSafeSpeedThresholdMetersPerSecond);
            gantryDrive = new YardCraneGantryDriveModel(configuration);
            trolleyAnchor = new YardCraneAnchorInterlock(AnchorTransitionSeconds);
            gantryAnchor = new YardCraneAnchorInterlock(AnchorTransitionSeconds);
            spreaderSize = new YardCraneSpreaderSizeInterlock(SpreaderSizeTransitionSeconds, YardCraneSpreaderSize.Ft40);
            spreaderRotation = new YardCraneSpreaderRotationModel(
                SpreaderRotationMaxAngleDegrees,
                SpreaderRotationRateDegreesPerSecond,
                SpreaderRotationReturnToZeroRateDegreesPerSecond);
        }

        private void ConfigureHorn()
        {
            var alarms = RequireTransform("80_Audio/Alarms");
            hornSource = alarms.GetComponent<AudioSource>();
            if (hornSource == null)
            {
                hornSource = alarms.gameObject.AddComponent<AudioSource>();
            }

            runtimeHornClip = CreateRuntimeHornClip();
            hornSource.clip = runtimeHornClip;
            hornSource.playOnAwake = false;
            hornSource.loop = true;
            hornSource.spatialBlend = 0.65f;
            hornSource.volume = 0.6f;
            hornSource.dopplerLevel = 0f;
        }

        private static AudioClip CreateRuntimeHornClip()
        {
            const int sampleRate = 22050;
            const int sampleCount = sampleRate;
            var samples = new float[sampleCount];
            for (var index = 0; index < sampleCount; index++)
            {
                var time = index / (float)sampleRate;
                var attack = Mathf.Clamp01(time / 0.035f);
                var release = Mathf.Clamp01((1f - time) / 0.035f);
                var envelope = Mathf.Min(attack, release);
                samples[index] =
                    (Mathf.Sin(2f * Mathf.PI * 220f * time) * 0.62f +
                     Mathf.Sin(2f * Mathf.PI * 277.18f * time) * 0.38f) *
                    envelope * 0.22f;
            }

            var clip = AudioClip.Create("YC_Runtime_DualTone_Horn", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private void SetHornActive(bool active)
        {
            if (hornActive == active)
            {
                return;
            }

            hornActive = active;
            if (hornSource != null)
            {
                if (active)
                {
                    hornSource.Play();
                }
                else
                {
                    hornSource.Stop();
                }
            }

            Debug.Log($"[YC_HORN_STATE] active={active}", this);
        }

        private void ConfigureHeadblockPhysics()
        {
            headblockBody = headblockPhysics.GetComponent<Rigidbody>();
            if (headblockBody == null)
            {
                headblockBody = headblockPhysics.gameObject.AddComponent<Rigidbody>();
            }

            headblockBody.mass = SimulationProxyMassKg;
            headblockBody.linearDamping = UnloadedHeadblockLinearDamping;
            headblockBody.angularDamping = UnloadedHeadblockAngularDamping;
            headblockBody.interpolation = RigidbodyInterpolation.Interpolate;
            headblockBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            headblockBody.solverIterations = HeadblockSolverIterations;
            headblockBody.solverVelocityIterations = HeadblockSolverVelocityIterations;
            headblockBody.useGravity = false;

            if (headblockPhysics.GetComponent<ObiRigidbody>() == null)
            {
                headblockPhysics.gameObject.AddComponent<ObiRigidbody>();
            }

            var colliderRoot = RequireTransform("30_SpreaderPhysics/Physics/Colliders");
            var colliderTransform = colliderRoot.Find("Graybox_HeadblockSpreaderCollider");
            if (colliderTransform == null)
            {
                var colliderObject = new GameObject("Graybox_HeadblockSpreaderCollider");
                colliderObject.transform.SetParent(colliderRoot, false);
                colliderTransform = colliderObject.transform;
            }

            colliderTransform.localPosition = Vector3.zero;
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;
            headblockCollider = colliderTransform.GetComponent<BoxCollider>();
            if (headblockCollider == null)
            {
                headblockCollider = colliderTransform.gameObject.AddComponent<BoxCollider>();
            }
            headblockCollider.size = HeadblockColliderSizeMeters;
            headblockBody.ResetCenterOfMass();
            headblockBody.ResetInertiaTensor();
            unloadedInertiaTensor = headblockBody.inertiaTensor;
            unloadedInertiaTensorRotation = headblockBody.inertiaTensorRotation;
        }

        private void ConfigureContainerLoad()
        {
            containerOriginalParent = activeContainer.parent;
            containerHomePosition = activeContainer.position;
            containerHomeRotation = activeContainer.rotation;
            containerRestingCenterY = activeContainer.position.y;

            // The container is a static yard object before locking. Once reparented
            // below the dynamic headblock it becomes part of the compound collider;
            // its real mass is represented by the headblock's equivalent mass,
            // center of mass and inertia instead of a second fighting Rigidbody.
            var containerBody = activeContainer.GetComponent<Rigidbody>();
            if (containerBody != null)
            {
                Destroy(containerBody);
            }

            containerCollider = activeContainer.GetComponent<BoxCollider>();
            if (containerCollider == null)
            {
                containerCollider = activeContainer.gameObject.AddComponent<BoxCollider>();
            }
            containerCollider.center = Vector3.zero;
            containerCollider.size = ContainerColliderSizeMeters;
            containerCollider.enabled = true;

            containerLocked = false;
            twistlockStatus = "Unlocked";
            ApplySuspendedLoadProperties();
            UpdateContainerLoadFeedback();
        }

        private void CreateSolver()
        {
            var ropeRig = RequireTransform("40_RopeRig");
            var runtimeRoot = ropeRig.Find("_ObiMAXRuntime");
            if (runtimeRoot == null)
            {
                var runtimeObject = new GameObject("_ObiMAXRuntime");
                runtimeObject.transform.SetParent(ropeRig, false);
                runtimeRoot = runtimeObject.transform;
            }

            var collisionWorldObject = new GameObject("ObiBurstCollisionWorld");
            collisionWorldObject.transform.SetParent(runtimeRoot, false);
            collisionWorldObject.AddComponent<BurstColliderWorld>();

            solver = ropeRig.GetComponent<ObiSolver>();
            if (solver == null)
            {
                solver = ropeRig.gameObject.AddComponent<ObiSolver>();
            }
            solver.backendType = ObiSolver.BackendType.Burst;
            solver.synchronization = ObiSolver.Synchronization.SynchronousFixed;
            solver.substeps = Mathf.Max(8, solverSubsteps);
            solver.maxStepsPerFrame = 1;
            solver.gravity = Vector3.zero;
            solver.gravitySpace = Space.World;
            solver.distanceConstraintParameters.iterations = Mathf.Clamp(distanceIterations, 1, 2);
            solver.collisionConstraintParameters.enabled = false;
            solver.particleCollisionConstraintParameters.enabled = false;
            var parameters = solver.parameters;
            parameters.damping = 0.3f;
            solver.parameters = parameters;
        }

        private void CreateEightRopeLoops()
        {
            foreach (var definition in LoopDefinitions)
            {
                var exit = RequireTransform(definition.ExitPath);
                var deadEnd = RequireTransform(definition.DeadEndPath);
                var sheave = RequireTransform(definition.SheavePath);
                var slot = RequireTransform(definition.SlotPath);
                var exitLeg = CreateVisualLeg(definition.Id, "ExitLeg", exit, sheave, slot);
                var deadEndLeg = CreateVisualLeg(definition.Id, "DeadEndLeg", deadEnd, sheave, slot);
                var initialLoopLength = Vector3.Distance(exit.position, sheave.position) +
                                        Vector3.Distance(deadEnd.position, sheave.position);

                loops.Add(new LoopRuntime(
                    definition,
                    exit,
                    deadEnd,
                    sheave,
                    headblockPhysics.InverseTransformPoint(sheave.position),
                    exitLeg,
                    deadEndLeg,
                    initialLoopLength,
                    Vector3.Distance(exit.position, sheave.position) -
                    Vector3.Distance(deadEnd.position, sheave.position)));
            }
        }

        private VisualLegRuntime CreateVisualLeg(
            string loopId,
            string legName,
            Transform fixedAnchor,
            Transform sheave,
            Transform slot)
        {
            var actorObject = new GameObject($"ObiRope_{loopId}_{legName}");
            actorObject.transform.SetParent(slot, false);
            var start = solver.transform.InverseTransformPoint(fixedAnchor.position);
            var end = solver.transform.InverseTransformPoint(sheave.position);
            var tangent = (end - start).normalized * 0.5f;
            var filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);

            var blueprint = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
            blueprint.name = $"Runtime_MAX_{loopId}_{legName}_Blueprint";
            blueprint.thickness = 0.028f;
            blueprint.resolution = 0.55f;
            blueprint.pooledParticles = 64;
            blueprint.path.Clear();
            blueprint.path.AddControlPoint(start, -tangent, tangent, Vector3.forward, 0.12f, 0.1f, 1f, filter, Color.white, "Fixed");
            blueprint.path.AddControlPoint(end, -tangent, tangent, Vector3.forward, 0.12f, 0.1f, 1f, filter, Color.white, "Sheave");
            blueprint.path.FlushEvents();
            blueprint.GenerateImmediate();

            var rope = actorObject.AddComponent<Obi.ObiRope>();
            rope.maxBending = 0.02f;
            rope.stretchCompliance = 0f;
            rope.maxCompression = 1f;
            rope.ropeBlueprint = blueprint;

            var renderer = actorObject.AddComponent<ObiRopeExtrudedRenderer>();
            renderer.section = Resources.Load<ObiRopeSection>("DefaultRopeSection");
            renderer.material = ropeMaterial;
            renderer.thicknessScale = 0.7f;
            renderer.uvScale = new Vector2(1f, 4f);

            var cursor = actorObject.AddComponent<ObiRopeCursor>();
            cursor.cursorMu = 0f;
            cursor.direction = true;

            var fixedAttachment = actorObject.AddComponent<ObiParticleAttachment>();
            fixedAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
            fixedAttachment.target = fixedAnchor;
            fixedAttachment.particleGroup = blueprint.groups[0];

            var sheaveAttachment = actorObject.AddComponent<ObiParticleAttachment>();
            sheaveAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
            sheaveAttachment.target = sheave;
            sheaveAttachment.particleGroup = blueprint.groups[1];
            sheaveAttachment.projection = true;

            return new VisualLegRuntime(
                actorObject,
                rope,
                cursor,
                blueprint,
                fixedAttachment,
                sheaveAttachment,
                Vector3.Distance(fixedAnchor.position, sheave.position));
        }

        private void TryFinishVisualInitialization()
        {
            foreach (var loop in loops)
            {
                if (!loop.ExitLeg.IsLoaded || !loop.DeadEndLeg.IsLoaded)
                {
                    return;
                }
            }

            foreach (var loop in loops)
            {
                InitializeVisualLeg(loop.ExitLeg, Vector3.Distance(loop.Exit.position, loop.Sheave.position));
                InitializeVisualLeg(loop.DeadEndLeg, Vector3.Distance(loop.DeadEnd.position, loop.Sheave.position));
                loop.InitialLoopLength = CalculateActualLoopLength(loop, headblockBody.position, headblockBody.rotation);
                loop.CommandedLoopLength = loop.InitialLoopLength;
                loop.LastCommandedLoopLength = loop.InitialLoopLength;
                loop.CommandedLegDifference = CalculateActualLegDifference(loop, headblockBody.position, headblockBody.rotation);
                loop.LastExitPosition = loop.Exit.position;
                loop.LastDeadEndPosition = loop.DeadEnd.position;
            }

            headblockBody.useGravity = false;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            loopsReady = true;
            startupPreTensioning = true;
            startupReleaseAt = Time.fixedTime + StartupPreTensionDurationSeconds;
            demoStartedAt = Time.fixedTime;
            runtimeStatus = "Pre-tensioning eight triangular rope loops";
            Debug.Log($"[YC_EIGHT_ROPE_MAX_READY] model={ImplementationVersion}, profile={TopologyProfile}, " +
                      $"drums=2, loops=8, visualLegs=16, drop={commandedSuspendedDrop:F3}m", this);
        }

        private static void InitializeVisualLeg(VisualLegRuntime leg, float targetLength)
        {
            leg.Rope.RecalculateRestLength();
            var correction = targetLength - leg.Rope.restLength;
            if (!Mathf.Approximately(correction, 0f))
            {
                leg.Cursor.ChangeLength(correction);
            }
            leg.CommandedVisualLength = targetLength;
        }

        private void HandleDeveloperShortcuts()
        {
#if UNITY_EDITOR
            if (UnityEngine.Input.GetKeyDown(KeyCode.G)) SetGrayboxVisible(!showGraybox);
            if (UnityEngine.Input.GetKeyDown(KeyCode.T)) BeginLoadedOperationRegression();
#endif
        }

        private void RunAutomatedDemo(float elapsed)
        {
            currentTrolleyVelocity = 0f;
            if (elapsed < 1f)
            {
                runtimeStatus = "Settling MAX loop constraints";
                return;
            }

            if (elapsed < 3f)
            {
                runtimeStatus = "MAX trolley S-curve acceleration";
                SetTrolleyX(trolleyHome.x + SmootherStep01((elapsed - 1f) / 2f) * 0.8f);
                return;
            }

            if (elapsed < 5f)
            {
                runtimeStatus = "MAX trolley S-curve deceleration";
                SetTrolleyX(trolleyHome.x + (1f - SmootherStep01((elapsed - 3f) / 2f)) * 0.8f);
                return;
            }

            if (elapsed < 7f)
            {
                runtimeStatus = "Dual-drum synchronized hoisting";
                ChangeSuspendedDrop(-HoistSpeedMetersPerSecond * Mathf.Sin(Mathf.PI * (elapsed - 5f) / 2f) * Time.fixedDeltaTime);
                return;
            }

            if (elapsed < 9f)
            {
                runtimeStatus = "Dual-drum synchronized lowering";
                ChangeSuspendedDrop(HoistSpeedMetersPerSecond * Mathf.Sin(Mathf.PI * (elapsed - 7f) / 2f) * Time.fixedDeltaTime);
                return;
            }

            if (elapsed < 12f)
            {
                if (!twistDisturbanceApplied)
                {
                    twistDisturbanceApplied = true;
                    headblockBody.AddTorque(Vector3.up * 0.5f, ForceMode.Impulse);
                }
                runtimeStatus = "Testing geared sheave anti-twist interlock";
                return;
            }

            if (elapsed < 13f)
            {
                if (demoGantryMaximumTravel <= 0f)
                {
                    demoGantryStartPosition = gantryDrive.PositionMeters;
                }
                gantryInput = 0.25f;
                UpdateManualGantryMotion();
                demoGantryMaximumTravel = Mathf.Max(
                    demoGantryMaximumTravel,
                    Mathf.Abs(gantryDrive.PositionMeters - demoGantryStartPosition));
                runtimeStatus = "Testing gantry acceleration and rope-anchor follow";
                return;
            }

            if (elapsed < 14f)
            {
                gantryInput = 0f;
                UpdateManualGantryMotion();
                runtimeStatus = "Testing gantry service braking";
                return;
            }

            if (elapsed < 15f)
            {
                if (!demoClampApplyRequested && gantryDrive.IsStationary)
                {
                    demoClampApplyRequested = gantryDrive.TryRequestWheelClamp(true, out _);
                }
                gantryInput = gantryDrive.WheelClampState == WheelClampState.Clamped ? 0.25f : 0f;
                UpdateManualGantryMotion();
                demoClampInhibitObserved |= gantryClampInhibitReported;
                runtimeStatus = "Testing wheel-clamp motion interlock";
                return;
            }

            if (elapsed < 16f)
            {
                gantryInput = 0f;
                if (!demoClampReleaseRequested && gantryDrive.WheelClampState == WheelClampState.Clamped)
                {
                    demoClampReleaseRequested = gantryDrive.TryRequestWheelClamp(false, out _);
                }
                UpdateManualGantryMotion();
                runtimeStatus = "Releasing wheel clamp";
                return;
            }

            if (elapsed < 17f)
            {
                gantryInput = -0.25f;
                UpdateManualGantryMotion();
                demoGantryMaximumTravel = Mathf.Max(
                    demoGantryMaximumTravel,
                    Mathf.Abs(gantryDrive.PositionMeters - demoGantryStartPosition));
                runtimeStatus = "Testing reverse gantry travel";
                return;
            }

            if (elapsed < 18f)
            {
                gantryInput = 0f;
                UpdateManualGantryMotion();
                runtimeStatus = "Settling gantry regression";
                return;
            }

            gantryInput = 0f;
            demoCompleted = true;
            runAutomatedDemo = false;
            var attachmentsBound = loops.TrueForAll(loop => loop.ExitLeg.AttachmentsBound && loop.DeadEndLeg.AttachmentsBound);
            var finiteState = IsFinite(headblockBody.position) && IsFinite(headblockBody.linearVelocity) &&
                              IsFinite(headblockBody.angularVelocity);
            residualTwistDegrees = Quaternion.Angle(headblockHomeRotation, headblockBody.rotation);
            var steelRopePassed = maximumObservedLoopExtension <= MaximumPermittedLoopExtensionMeters;
            var antiSwayPassed = maximumObservedSway <= PrototypeMaximumSwayMeters;
            var antiTwistPassed = residualTwistDegrees <= PrototypeMaximumResidualTwistDegrees;
            var gantryPassed = demoGantryMaximumTravel >= 0.05f &&
                               demoClampApplyRequested && demoClampReleaseRequested &&
                               demoClampInhibitObserved &&
                               gantryDrive.WheelClampState == WheelClampState.Released &&
                               gantryDrive.IsStationary;
            var passed = attachmentsBound && finiteState && headblockBody.position.y > 0.5f &&
                         steelRopePassed && antiSwayPassed && antiTwistPassed && gantryPassed;
            runtimeStatus = passed ? "PASS - MAX manual controls enabled" : "FAIL - inspect MAX topology";
            var message = $"model={ImplementationVersion}, profile={TopologyProfile}, attachmentsBound={attachmentsBound}, " +
                          $"finiteState={finiteState}, maxLoopExtension={maximumObservedLoopExtension * 1000f:F2}mm, " +
                          $"allowed={MaximumPermittedLoopExtensionMeters * 1000f:F2}mm, peakLoop={peakLoopId}, " +
                          $"rawDrift={maximumRawLoopDrift * 1000f:F2}mm, maxSway={maximumObservedSway:F3}m, " +
                          $"swayAllowed={PrototypeMaximumSwayMeters:F2}m, maxTwist={maximumObservedTwistDegrees:F2}deg, " +
                          $"residualTwist={residualTwistDegrees:F2}deg, twistAllowed={PrototypeMaximumResidualTwistDegrees:F2}deg, " +
                          $"gantryTravel={demoGantryMaximumTravel:F3}m, gantryPassed={gantryPassed}, " +
                          $"wheelClamp={gantryDrive.WheelClampState}, clampInhibit={demoClampInhibitObserved}, " +
                          $"headblockY={headblockBody.position.y:F3}m";
            if (passed)
            {
                Debug.Log(
                    $"[YC_GANTRY_MECHANISM_PASS] model={ImplementationVersion}, " +
                    $"travel={demoGantryMaximumTravel:F3}m, clampApply={demoClampApplyRequested}, " +
                    $"clampRelease={demoClampReleaseRequested}, clampInhibit={demoClampInhibitObserved}, " +
                    $"finalSpeed={gantryDrive.VelocityMetersPerSecond:F3}m/s",
                    this);
                Debug.Log($"[YC_EIGHT_ROPE_MAX_PASS] {message}", this);
            }
            else
            {
                Debug.LogError($"[YC_EIGHT_ROPE_MAX_FAIL] {message}", this);
            }
        }

        private void UpdateManualGantryMotion()
        {
            if (gantryDrive == null)
            {
                return;
            }

            var requestedGantryInput = gantryInput;
            var gantryAnchorInhibited = gantryAnchor != null && gantryAnchor.State != YardCraneAnchorState.Released;
            if (gantryAnchorInhibited)
            {
                requestedGantryInput = 0f;
                if (!Mathf.Approximately(gantryInput, 0f))
                {
                    runtimeStatus = "大车已锚定 · 请先释放大车锚定";
                }
            }

            if (gantryAnchorInhibited && !Mathf.Approximately(gantryInput, 0f) && !gantryAnchorInhibitReported)
            {
                gantryAnchorInhibitReported = true;
                Debug.LogWarning(
                    $"[YC_GANTRY_INHIBITED_ANCHOR] state={gantryAnchor.State}, input={gantryInput:F2}",
                    this);
            }
            else if (!gantryAnchorInhibited || Mathf.Approximately(gantryInput, 0f))
            {
                gantryAnchorInhibitReported = false;
            }

            var proximityInhibited = IsHorizontalTravelInhibitedByContainerProximity(
                requestedGantryInput,
                GantryAxisWorldDirection,
                gantryDrive.VelocityMetersPerSecond,
                GantryBrakingMetersPerSecondSquared,
                out var containerClearance,
                out var requiredStoppingMargin);
            gantryProximityInhibited = proximityInhibited;
            if (proximityInhibited)
            {
                requestedGantryInput = 0f;
                if (!Mathf.Approximately(gantryInput, 0f))
                {
                    runtimeStatus = containerLocked
                        ? $"大车低净空联锁 · 请将集装箱起升至 {LoadedTrolleyMinimumClearanceMeters:F2} m 以上"
                        : "大车防碰联锁 · 朝箱方向禁止，请起升或反向退出";
                }
            }

            if (proximityInhibited && !gantryClearanceInhibitReported)
            {
                gantryClearanceInhibitReported = true;
                Debug.LogWarning(
                    $"[YC_GANTRY_INHIBITED_CLEARANCE] surfaceGap={containerClearance:F3}m, " +
                    $"required={requiredStoppingMargin:F3}m, input={gantryInput:F2}, " +
                    $"loaded={containerLocked}",
                    this);
            }
            else if (!proximityInhibited)
            {
                gantryClearanceInhibitReported = false;
            }

            var previousVelocity = gantryDrive.VelocityMetersPerSecond;
            var previousClampState = gantryDrive.WheelClampState;
            var previousGantryAnchorState = gantryAnchor?.State ?? YardCraneAnchorState.Unknown;
            var step = gantryDrive.Step(requestedGantryInput, Time.fixedDeltaTime);
            ApplyGantryPosition(step.PositionMeters);
            gantryAnchor?.Advance(Time.fixedDeltaTime);
            if (gantryAnchor != null && previousGantryAnchorState != gantryAnchor.State)
            {
                Debug.Log(
                    $"[YC_GANTRY_ANCHOR_STATE] from={previousGantryAnchorState}, to={gantryAnchor.State}, " +
                    $"gantryPosition={step.PositionMeters:F3}m",
                    this);
            }

            if (previousClampState != step.WheelClampState)
            {
                Debug.Log(
                    $"[YC_WHEEL_CLAMP_STATE] from={previousClampState}, to={step.WheelClampState}, " +
                    $"gantryPosition={step.PositionMeters:F3}m",
                    this);
            }

            if (step.MotionInhibitedByClamp && !gantryClampInhibitReported)
            {
                gantryClampInhibitReported = true;
                runtimeStatus = "Gantry inhibited · release wheel clamp";
                Debug.LogWarning(
                    $"[YC_GANTRY_INHIBITED_CLAMP] state={step.WheelClampState}, input={gantryInput:F2}",
                    this);
            }
            else if (!step.MotionInhibitedByClamp)
            {
                gantryClampInhibitReported = false;
            }

            var limitActive = (step.ForwardLimitActive && gantryInput > 0f) ||
                              (step.BackwardLimitActive && gantryInput < 0f);
            if (limitActive && !gantryLimitReported)
            {
                gantryLimitReported = true;
                Debug.LogWarning(
                    $"[YC_GANTRY_LIMITED] position={step.PositionMeters:F3}m, input={gantryInput:F2}, " +
                    $"forward={step.ForwardLimitActive}, backward={step.BackwardLimitActive}, " +
                    $"travelFromHome={GantryTravelFromHomeMeters:F3}m",
                    this);
            }
            else if (!limitActive)
            {
                gantryLimitReported = false;
            }

            var moving = Mathf.Abs(step.VelocityMetersPerSecond) > MotionObservationSpeedThresholdMetersPerSecond;
            if (moving && !gantryMotionReported)
            {
                gantryMotionReported = true;
                Debug.Log(
                    $"[YC_GANTRY_STARTED] position={step.PositionMeters:F3}m, " +
                    $"speed={step.VelocityMetersPerSecond:F3}m/s, input={gantryInput:F2}, loaded={containerLocked}",
                    this);
            }
            else if (!moving && gantryMotionReported)
            {
                gantryMotionReported = false;
                Debug.Log(
                    $"[YC_GANTRY_STOPPED] position={step.PositionMeters:F3}m, loaded={containerLocked}",
                    this);
            }

            var braking = Mathf.Abs(step.VelocityMetersPerSecond) < Mathf.Abs(previousVelocity);
            if (containerLocked && braking && Mathf.Abs(previousVelocity) > 0.01f)
            {
                trolleyStopStabilizationReleaseAt = Mathf.Max(
                    trolleyStopStabilizationReleaseAt,
                    Time.fixedTime + LoadedTrolleyStopStabilizationSeconds);
            }
        }

        private void ApplyGantryPosition(float relativePositionMeters)
        {
            var gantryPosition = gantryMotion.localPosition;
            gantryPosition.z = gantryHome.z + relativePositionMeters;
            gantryMotion.localPosition = gantryPosition;

            var trolleyPosition = trolleyMotion.localPosition;
            trolleyPosition.z = trolleyHome.z + relativePositionMeters;
            trolleyMotion.localPosition = trolleyPosition;
        }

        private Vector3 GetTrolleyBoundaryVelocityWorld()
        {
            return TrolleyAxisWorldDirection * currentTrolleyVelocity +
                   GantryAxisWorldDirection * (gantryDrive?.VelocityMetersPerSecond ?? 0f);
        }

        private void UpdateManualTrolleyMotion()
        {
            var requestedInput = trolleyInput;
            var previousTrolleyAnchorState = trolleyAnchor?.State ?? YardCraneAnchorState.Unknown;
            trolleyAnchor?.Advance(Time.fixedDeltaTime);
            if (trolleyAnchor != null && previousTrolleyAnchorState != trolleyAnchor.State)
            {
                Debug.Log(
                    $"[YC_TROLLEY_ANCHOR_STATE] from={previousTrolleyAnchorState}, to={trolleyAnchor.State}, " +
                    $"trolleyX={trolleyMotion.localPosition.x:F3}m",
                    this);
            }

            var trolleyAnchorInhibited = trolleyAnchor != null && trolleyAnchor.State != YardCraneAnchorState.Released;
            if (trolleyAnchorInhibited)
            {
                requestedInput = 0f;
                if (!Mathf.Approximately(trolleyInput, 0f))
                {
                    runtimeStatus = "小车已锚定 · 请先释放小车锚定";
                }
            }

            if (trolleyAnchorInhibited && !Mathf.Approximately(trolleyInput, 0f) && !trolleyAnchorInhibitReported)
            {
                trolleyAnchorInhibitReported = true;
                Debug.LogWarning(
                    $"[YC_TROLLEY_INHIBITED_ANCHOR] state={trolleyAnchor.State}, input={trolleyInput:F2}",
                    this);
            }
            else if (!trolleyAnchorInhibited || Mathf.Approximately(trolleyInput, 0f))
            {
                trolleyAnchorInhibitReported = false;
            }

            var proximityInhibited = IsHorizontalTravelInhibitedByContainerProximity(
                requestedInput,
                TrolleyAxisWorldDirection,
                currentTrolleyVelocity,
                TrolleyBrakingMetersPerSecondSquared,
                out var containerClearance,
                out var requiredStoppingMargin);
            trolleyProximityInhibited = proximityInhibited;
            if (proximityInhibited)
            {
                requestedInput = 0f;
                if (!Mathf.Approximately(trolleyInput, 0f))
                {
                    runtimeStatus = containerLocked
                        ? $"小车低净空联锁 · 请将集装箱起升至 {LoadedTrolleyMinimumClearanceMeters:F2} m 以上"
                        : "小车防碰联锁 · 朝箱方向禁止，请起升或反向退出";
                }
            }

            if (proximityInhibited && !trolleyClearanceInhibitReported)
            {
                trolleyClearanceInhibitReported = true;
                Debug.LogWarning(
                    $"[YC_TROLLEY_INHIBITED_CLEARANCE] surfaceGap={containerClearance:F3}m, " +
                    $"required={requiredStoppingMargin:F3}m, input={trolleyInput:F0}, " +
                    $"loaded={containerLocked}, grossMass={containerGrossMassKg:F0}kg",
                    this);
            }
            else if (!proximityInhibited)
            {
                trolleyClearanceInhibitReported = false;
            }

            var loadRatio = containerLocked
                ? Mathf.Clamp01(containerGrossMassKg / MaximumContainerGrossMassKg)
                : 0f;
            var acceleration = TrolleyAccelerationMetersPerSecondSquared *
                               Mathf.Lerp(1f, FullyLoadedTrolleyAccelerationFactor, loadRatio);
            var braking = TrolleyBrakingMetersPerSecondSquared *
                          Mathf.Lerp(1f, FullyLoadedTrolleyBrakingFactor, loadRatio);
            var targetVelocity = requestedInput * TrolleySpeedMetersPerSecond;
            var travelLimited = false;

            // Start slowing before the travel stop instead of allowing the clamp to
            // change a moving trolley to zero velocity in one fixed step.
            if (!Mathf.Approximately(targetVelocity, 0f))
            {
                var minimumX = trolleyHome.x - TrolleyTravelFromHomeMeters;
                var maximumX = trolleyHome.x + TrolleyTravelFromHomeMeters;
                var remainingTravel = targetVelocity > 0f
                    ? maximumX - trolleyMotion.localPosition.x
                    : trolleyMotion.localPosition.x - minimumX;
                var safeTravel = Mathf.Max(0f, remainingTravel - TrolleyLimitSafetyMarginMeters);
                var limitSafeSpeed = Mathf.Sqrt(2f * braking * safeTravel);
                targetVelocity = Mathf.Sign(targetVelocity) *
                                 Mathf.Min(Mathf.Abs(targetVelocity), limitSafeSpeed);
                travelLimited = safeTravel <= 0.0001f;
            }

            if (travelLimited && !trolleyLimitReported)
            {
                trolleyLimitReported = true;
                Debug.LogWarning(
                    $"[YC_TROLLEY_LIMITED] x={trolleyMotion.localPosition.x:F3}m, " +
                    $"input={trolleyInput:F0}, travelFromHome={TrolleyTravelFromHomeMeters:F3}m",
                    this);
            }
            else if (!travelLimited)
            {
                trolleyLimitReported = false;
            }

            var previousVelocity = currentTrolleyVelocity;
            var isBraking = Mathf.Approximately(targetVelocity, 0f) ||
                            Mathf.Sign(targetVelocity) != Mathf.Sign(currentTrolleyVelocity) ||
                            Mathf.Abs(targetVelocity) < Mathf.Abs(currentTrolleyVelocity);
            currentTrolleyVelocity = Mathf.MoveTowards(
                currentTrolleyVelocity,
                targetVelocity,
                (isBraking ? braking : acceleration) * Time.fixedDeltaTime);

            if (containerLocked && isBraking && Mathf.Abs(previousVelocity) > 0.01f)
            {
                var startingStabilization = Time.fixedTime >= trolleyStopStabilizationReleaseAt;
                trolleyStopStabilizationReleaseAt =
                    Time.fixedTime + LoadedTrolleyStopStabilizationSeconds;
                if (startingStabilization)
                {
                    Debug.Log(
                        $"[YC_LOADED_TROLLEY_BRAKING] speed={previousVelocity:F3}m/s, " +
                        $"clearance={containerClearance:F3}m, grossMass={containerGrossMassKg:F0}kg, " +
                        $"currentSway={currentHorizontalSway:F3}m, " +
                        $"relativeSpeed={currentRelativeHorizontalSpeed:F3}m/s",
                        this);
                }
            }

            var previousX = trolleyMotion.localPosition.x;
            var requestedX = previousX + currentTrolleyVelocity * Time.fixedDeltaTime;
            SetTrolleyX(requestedX);
            if (!Mathf.Approximately(trolleyMotion.localPosition.x, requestedX))
            {
                currentTrolleyVelocity =
                    (trolleyMotion.localPosition.x - previousX) / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            }

            ObserveTrolleyMotion();
        }

        private void ApplyLoadedTrolleyStopStabilization()
        {
            if (!containerLocked || Time.fixedTime >= trolleyStopStabilizationReleaseAt)
            {
                return;
            }

            var boundaryVelocity = GetTrolleyBoundaryVelocityWorld();
            var relativeHorizontalVelocity = Vector3.ProjectOnPlane(
                headblockBody.linearVelocity - boundaryVelocity,
                Vector3.up);
            var linearBlend = 1f - Mathf.Exp(
                -LoadedTrolleyStopLinearDampingPerSecond * Time.fixedDeltaTime);
            headblockBody.linearVelocity -= relativeHorizontalVelocity * linearBlend;

            var angularBlend = 1f - Mathf.Exp(
                -LoadedTrolleyStopAngularDampingPerSecond * Time.fixedDeltaTime);
            headblockBody.angularVelocity = Vector3.Lerp(
                headblockBody.angularVelocity,
                Vector3.zero,
                angularBlend);
        }

        private void ApplyLoadedSuspensionDynamics()
        {
            if (!containerLocked || loadTransferPreTensioning)
            {
                return;
            }

            var trolleyVelocity = GetTrolleyBoundaryVelocityWorld();
            var horizontalDisplacement = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                Vector3.up) - loadedSuspensionEquilibriumOffset;
            var relativeHorizontalVelocity = Vector3.ProjectOnPlane(
                headblockBody.linearVelocity - trolleyVelocity,
                Vector3.up);
            var omega = LoadedSuspensionNaturalFrequencyRadiansPerSecond;
            var springAcceleration = -omega * omega * horizontalDisplacement;
            var dampingAcceleration = -2f * LoadedSuspensionDampingRatio * omega *
                                      relativeHorizontalVelocity;
            var suspensionAcceleration = Vector3.ClampMagnitude(
                springAcceleration + dampingAcceleration,
                MaximumLoadedSuspensionAccelerationMetersPerSecondSquared);
            headblockBody.linearVelocity += suspensionAcceleration * Time.fixedDeltaTime;
        }

        private void ApplyLoadedHoistSuspensionDynamics()
        {
            if (!containerLocked || loadTransferPreTensioning)
            {
                previousHoistSupportVelocity = currentHoistVelocity;
                previousHoistInput = hoistInput;
                return;
            }

            var deltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            var supportAcceleration =
                (currentHoistVelocity - previousHoistSupportVelocity) / deltaTime;
            previousHoistSupportVelocity = currentHoistVelocity;

            if (loadedHoistSettleObservationActive && !Mathf.Approximately(hoistInput, 0f))
            {
                CancelLoadedHoistSettleObservation();
            }

            if (!Mathf.Approximately(previousHoistInput, 0f) &&
                Mathf.Approximately(hoistInput, 0f))
            {
                loadedHoistStopPending = true;
                BeginLoadedHoistSettleObservation(GetLoadedStaticRopeDeflection());
                Debug.Log(
                    $"[YC_LOADED_HOIST_RELEASE] drumVelocity={currentHoistVelocity:F3}m/s, " +
                    $"deflection={loadedHoistDeflection * 1000f:F1}mm, " +
                    $"deflectionVelocity={loadedHoistDeflectionVelocity:F3}m/s",
                    this);
            }
            previousHoistInput = hoistInput;

            var staticDeflection = GetLoadedStaticRopeDeflection();
            var omega = LoadedHoistNaturalFrequencyRadiansPerSecond;
            var deflectionAcceleration =
                -2f * LoadedHoistDampingRatio * omega * loadedHoistDeflectionVelocity -
                omega * omega * (loadedHoistDeflection - staticDeflection) -
                supportAcceleration;
            loadedHoistDeflectionVelocity += deflectionAcceleration * deltaTime;
            var previousDeflection = loadedHoistDeflection;
            loadedHoistDeflection += loadedHoistDeflectionVelocity * deltaTime;
            loadedHoistDeflection = Mathf.Clamp(
                loadedHoistDeflection,
                0f,
                MaximumLoadedHoistDeflectionMeters);
            if ((Mathf.Approximately(loadedHoistDeflection, 0f) && loadedHoistDeflectionVelocity < 0f) ||
                (Mathf.Approximately(
                     loadedHoistDeflection,
                     MaximumLoadedHoistDeflectionMeters) && loadedHoistDeflectionVelocity > 0f))
            {
                loadedHoistDeflectionVelocity = 0f;
            }

            var deflectionDelta = loadedHoistDeflection - previousDeflection;
            if (!Mathf.Approximately(deflectionDelta, 0f))
            {
                // Every triangular loop is a 2:1 suspension. A vertical compliance
                // displacement changes its two visible/supporting legs by twice the
                // headblock deflection.
                foreach (var loop in loops)
                {
                    loop.CommandedLoopLength += deflectionDelta * 2f;
                }
            }

            if (loadedHoistStopPending && Mathf.Abs(currentHoistVelocity) <= 0.01f)
            {
                loadedHoistStopPending = false;
                loadedHoistSettleDrumStopped = true;
                loadedHoistSettleDrumStopAt = Time.fixedTime;
                Debug.Log(
                    $"[YC_LOADED_HOIST_DRUM_STOP] gravity={Physics.gravity.magnitude:F3}m/s2, " +
                    $"deflection={loadedHoistDeflection * 1000f:F1}mm, " +
                    $"deflectionVelocity={loadedHoistDeflectionVelocity:F3}m/s, " +
                    $"omega={LoadedHoistNaturalFrequencyRadiansPerSecond:F2}, " +
                    $"damping={LoadedHoistDampingRatio:F2}",
                    this);
            }

            ObserveLoadedHoistSettlement(staticDeflection);
        }

        private void BeginLoadedHoistSettleObservation(float staticDeflection)
        {
            loadedHoistSettleObservationActive = true;
            loadedHoistSettleDrumStopped = false;
            loadedHoistSettleReleaseAt = Time.fixedTime;
            loadedHoistSettleDrumStopAt = 0f;
            loadedHoistSettleStableSince = -1f;
            loadedHoistSettlePeakDeflection = loadedHoistDeflection;
            loadedHoistSettlePeakError = Mathf.Abs(loadedHoistDeflection - staticDeflection);
            loadedHoistSettlePeakVelocity = Mathf.Abs(loadedHoistDeflectionVelocity);
        }

        private void ObserveLoadedHoistSettlement(float staticDeflection)
        {
            if (!loadedHoistSettleObservationActive)
            {
                return;
            }

            loadedHoistSettlePeakDeflection = Mathf.Max(
                loadedHoistSettlePeakDeflection,
                loadedHoistDeflection);
            loadedHoistSettlePeakError = Mathf.Max(
                loadedHoistSettlePeakError,
                Mathf.Abs(loadedHoistDeflection - staticDeflection));
            loadedHoistSettlePeakVelocity = Mathf.Max(
                loadedHoistSettlePeakVelocity,
                Mathf.Abs(loadedHoistDeflectionVelocity));

            if (!loadedHoistSettleDrumStopped)
            {
                return;
            }

            var elapsed = Time.fixedTime - loadedHoistSettleReleaseAt;
            var withinDeflectionTolerance =
                Mathf.Abs(loadedHoistDeflection - staticDeflection) <=
                LoadedHoistSettleDeflectionToleranceMeters;
            var withinVelocityTolerance =
                Mathf.Abs(loadedHoistDeflectionVelocity) <=
                LoadedHoistSettleVelocityToleranceMetersPerSecond;
            var drumRemainsStopped = Mathf.Abs(currentHoistVelocity) <= 0.01f &&
                                     Mathf.Approximately(hoistInput, 0f);

            if (withinDeflectionTolerance && withinVelocityTolerance && drumRemainsStopped)
            {
                if (loadedHoistSettleStableSince < 0f)
                {
                    loadedHoistSettleStableSince = Time.fixedTime;
                }
                else if (Time.fixedTime - loadedHoistSettleStableSince >= LoadedHoistSettleHoldSeconds)
                {
                    Debug.Log(
                        $"[YC_LOADED_HOIST_SETTLED] gravity={Physics.gravity.magnitude:F3}m/s2, " +
                        $"settleTime={elapsed:F2}s, " +
                        $"drumStopToSettleTime={Time.fixedTime - loadedHoistSettleDrumStopAt:F2}s, " +
                        $"peakDeflection={loadedHoistSettlePeakDeflection * 1000f:F1}mm, " +
                        $"peakError={loadedHoistSettlePeakError * 1000f:F1}mm, " +
                        $"targetDeflection={staticDeflection * 1000f:F1}mm, " +
                        $"finalDeflection={loadedHoistDeflection * 1000f:F1}mm, " +
                        $"peakVelocity={loadedHoistSettlePeakVelocity:F3}m/s, " +
                        $"residualVelocity={loadedHoistDeflectionVelocity:F3}m/s, " +
                        $"hold={LoadedHoistSettleHoldSeconds:F2}s",
                        this);
                    CancelLoadedHoistSettleObservation();
                    return;
                }
            }
            else
            {
                loadedHoistSettleStableSince = -1f;
            }

            if (elapsed < LoadedHoistSettleTimeoutSeconds)
            {
                return;
            }

            Debug.LogWarning(
                $"[YC_LOADED_HOIST_SETTLE_TIMEOUT] elapsed={elapsed:F2}s, " +
                $"peakDeflection={loadedHoistSettlePeakDeflection * 1000f:F1}mm, " +
                $"peakError={loadedHoistSettlePeakError * 1000f:F1}mm, " +
                $"targetDeflection={staticDeflection * 1000f:F1}mm, " +
                $"finalDeflection={loadedHoistDeflection * 1000f:F1}mm, " +
                $"peakVelocity={loadedHoistSettlePeakVelocity:F3}m/s, " +
                $"residualVelocity={loadedHoistDeflectionVelocity:F3}m/s",
                this);
            CancelLoadedHoistSettleObservation();
        }

        private Vector3 GetHeadblockCenterOfMassWorld()
        {
            return headblockBody.position +
                   headblockBody.rotation * headblockBody.centerOfMass;
        }

        private float GetLoadedStaticRopeDeflection()
        {
            var loadRatio = Mathf.InverseLerp(
                2200f,
                MaximumContainerGrossMassKg,
                containerGrossMassKg);
            return Mathf.Lerp(
                LightLoadedStaticRopeDeflectionMeters,
                FullyLoadedStaticRopeDeflectionMeters,
                loadRatio);
        }

        private void ResetLoadedHoistDynamics()
        {
            loadedHoistDeflection = 0f;
            loadedHoistDeflectionVelocity = 0f;
            previousHoistSupportVelocity = currentHoistVelocity;
            previousHoistInput = hoistInput;
            loadedHoistStopPending = false;
            CancelLoadedHoistSettleObservation();
        }

        private void CancelLoadedHoistSettleObservation()
        {
            loadedHoistSettleObservationActive = false;
            loadedHoistSettleDrumStopped = false;
            loadedHoistSettleReleaseAt = 0f;
            loadedHoistSettleDrumStopAt = 0f;
            loadedHoistSettleStableSince = -1f;
            loadedHoistSettlePeakDeflection = 0f;
            loadedHoistSettlePeakError = 0f;
            loadedHoistSettlePeakVelocity = 0f;
        }

        private void UpdateManualHoistMotion()
        {
            var loadRatio = containerLocked
                ? Mathf.Clamp01(containerGrossMassKg / MaximumContainerGrossMassKg)
                : 0f;
            var loadedSpeedFactor = Mathf.Lerp(1f, FullyLoadedHoistSpeedFactor, loadRatio);
            var loadedAccelerationFactor = Mathf.Lerp(1f, FullyLoadedHoistAccelerationFactor, loadRatio);
            var targetVelocity = hoistInput * HoistSpeedMetersPerSecond * loadedSpeedFactor;
            var isBraking = Mathf.Approximately(targetVelocity, 0f) ||
                            Mathf.Sign(targetVelocity) != Mathf.Sign(currentHoistVelocity) ||
                            Mathf.Abs(targetVelocity) < Mathf.Abs(currentHoistVelocity);
            var acceleration = isBraking
                ? HoistBrakingMetersPerSecondSquared
                : UnloadedHoistAccelerationMetersPerSecondSquared * loadedAccelerationFactor;
            currentHoistVelocity = Mathf.MoveTowards(
                currentHoistVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);
            var requestedDropDelta = currentHoistVelocity * Time.fixedDeltaTime;
            var appliedDropDelta = ChangeSuspendedDrop(requestedDropDelta);
            var limitReached = !Mathf.Approximately(requestedDropDelta, 0f) &&
                               Mathf.Approximately(appliedDropDelta, 0f);
            if (limitReached)
            {
                currentHoistVelocity = 0f;
                if (!hoistLimitReported)
                {
                    hoistLimitReported = true;
                    Debug.LogWarning(
                        $"[YC_HOIST_LIMITED] drop={commandedSuspendedDrop:F3}m, input={hoistInput:F0}, " +
                        $"minimum={MinimumSuspendedDropMeters:F3}m, maximum={MaximumSuspendedDropMeters:F3}m",
                        this);
                }
            }
            else
            {
                hoistLimitReported = false;
            }

            ObserveHoistMotion();
        }

        /// <summary>
        /// Advances the command-domain rotation model and projects its angle onto
        /// the spreader-only mechanism nodes. The rope sheaves and headblock root
        /// deliberately stay under the eight-rope constraint solver's ownership.
        /// </summary>
        private void AdvanceSpreaderRotation()
        {
            if (spreaderRotation == null)
            {
                spreaderRotationAngularVelocityRadiansPerSecond = 0f;
                return;
            }

            var inputActive = Mathf.Abs(spreaderRotationInputAxis) > 0.0001f;
            if (!spreaderPumpOn && inputActive)
            {
                if (!spreaderRotationInhibitReported)
                {
                    spreaderRotationInhibitReported = true;
                    Debug.LogWarning(
                        $"[YC_SPREADER_ROTATION_INHIBITED_PUMP_OFF] angle={(spreaderRotation?.CurrentAngleDegrees ?? 0f):F2}deg",
                        this);
                }
            }
            else
            {
                spreaderRotationInhibitReported = false;
            }

            var previousAngle = spreaderRotation.CurrentAngleDegrees;
            var wasReturningToZero = spreaderRotation.IsReturningToZero;
            spreaderRotation.Advance(spreaderRotationInputAxis, spreaderPumpOn, Time.fixedDeltaTime);
            var currentAngle = spreaderRotation.CurrentAngleDegrees;
            spreaderRotationAngularVelocityRadiansPerSecond =
                (currentAngle - previousAngle) * Mathf.Deg2Rad / Mathf.Max(Time.fixedDeltaTime, 0.000001f);
            ApplySpreaderRotationPose(currentAngle);
            ObserveSpreaderRotationMotion(wasReturningToZero);
        }

        private void ApplySpreaderRotationPose(float angleDegrees, bool force = false)
        {
            if (!force && Mathf.Approximately(spreaderPhysicalRotationAngleDegrees, angleDegrees))
            {
                return;
            }

            // Pre-multiplication makes this a yaw about the headblock's local up
            // axis, while retaining any rest orientation authored for a real model.
            var localYaw = Quaternion.AngleAxis(angleDegrees, Vector3.up);
            spreaderRotationPivot.localRotation = localYaw * spreaderRotationPivotHomeLocalRotation;
            spreaderGrayboxVisualRoot.localRotation = localYaw * spreaderGrayboxVisualHomeLocalRotation;
            spreaderColliderRoot.localRotation = localYaw * spreaderColliderHomeLocalRotation;
            spreaderSensorRoot.localRotation = localYaw * spreaderSensorHomeLocalRotation;
            containerAttachmentRoot.localRotation = localYaw * containerAttachmentHomeLocalRotation;
            spreaderPhysicalRotationAngleDegrees = angleDegrees;
            headblockBody?.WakeUp();
        }

        private void ObserveSpreaderRotationMotion(bool wasReturningToZero)
        {
            var moving = Mathf.Abs(spreaderRotationAngularVelocityRadiansPerSecond) > 0.0001f;
            if (moving && !spreaderRotationMotionReported)
            {
                spreaderRotationMotionReported = true;
                Debug.Log(
                    $"[YC_SPREADER_ROTATION_STARTED] mode={(spreaderRotation.IsReturningToZero ? "ReturnToZero" : "Manual")}, " +
                    $"angle={spreaderRotation.CurrentAngleDegrees:F2}deg, " +
                    $"speed={spreaderRotationAngularVelocityRadiansPerSecond * Mathf.Rad2Deg:F2}deg/s",
                    this);
            }
            else if (!moving && spreaderRotationMotionReported)
            {
                spreaderRotationMotionReported = false;
                Debug.Log(
                    $"[YC_SPREADER_ROTATION_STOPPED] angle={spreaderRotation.CurrentAngleDegrees:F2}deg, " +
                    $"physicalAngle={spreaderPhysicalRotationAngleDegrees:F2}deg",
                    this);
            }

            if (wasReturningToZero && !spreaderRotation.IsReturningToZero &&
                Mathf.Approximately(spreaderRotation.CurrentAngleDegrees, 0f))
            {
                Debug.Log(
                    $"[YC_SPREADER_ROTATION_RETURNED_TO_ZERO] physicalAngle={spreaderPhysicalRotationAngleDegrees:F2}deg",
                    this);
            }
        }

        private void SetTrolleyX(float x)
        {
            var position = trolleyMotion.localPosition;
            position.x = Mathf.Clamp(
                x,
                trolleyHome.x - TrolleyTravelFromHomeMeters,
                trolleyHome.x + TrolleyTravelFromHomeMeters);
            trolleyMotion.localPosition = position;
        }

        private float ChangeSuspendedDrop(float requestedDropDelta)
        {
            if (Mathf.Approximately(requestedDropDelta, 0f))
            {
                return 0f;
            }

            var nextDrop = Mathf.Clamp(
                commandedSuspendedDrop + requestedDropDelta,
                MinimumSuspendedDropMeters,
                MaximumSuspendedDropMeters);
            var appliedDropDelta = nextDrop - commandedSuspendedDrop;
            if (Mathf.Approximately(appliedDropDelta, 0f))
            {
                return 0f;
            }

            // A 2:1 triangular loop changes total paid-out cable by twice the
            // requested vertical headblock travel.
            var loopLengthDelta = appliedDropDelta * 2f;
            foreach (var loop in loops)
            {
                loop.CommandedLoopLength += loopLengthDelta;
            }
            commandedSuspendedDrop = nextDrop;
            return appliedDropDelta;
        }

        private void ObserveTrolleyMotion()
        {
            var moving = Mathf.Abs(currentTrolleyVelocity) > MotionObservationSpeedThresholdMetersPerSecond;
            if (moving && !trolleyMotionReported)
            {
                trolleyMotionReported = true;
                Debug.Log(
                    $"[YC_TROLLEY_STARTED] x={trolleyMotion.localPosition.x:F3}m, " +
                    $"speed={currentTrolleyVelocity:F3}m/s, input={trolleyInput:F0}, loaded={containerLocked}",
                    this);
            }
            else if (!moving && trolleyMotionReported)
            {
                trolleyMotionReported = false;
                Debug.Log(
                    $"[YC_TROLLEY_STOPPED] x={trolleyMotion.localPosition.x:F3}m, " +
                    $"speed={currentTrolleyVelocity:F3}m/s, loaded={containerLocked}",
                    this);
            }
        }

        private void ObserveHoistMotion()
        {
            var moving = Mathf.Abs(currentHoistVelocity) > MotionObservationSpeedThresholdMetersPerSecond;
            if (moving && !hoistMotionReported)
            {
                hoistMotionReported = true;
                Debug.Log(
                    $"[YC_HOIST_STARTED] drop={commandedSuspendedDrop:F3}m, " +
                    $"speed={currentHoistVelocity:F3}m/s, input={hoistInput:F0}, loaded={containerLocked}",
                    this);
            }
            else if (!moving && hoistMotionReported)
            {
                hoistMotionReported = false;
                Debug.Log(
                    $"[YC_HOIST_STOPPED] drop={commandedSuspendedDrop:F3}m, " +
                    $"speed={currentHoistVelocity:F3}m/s, loaded={containerLocked}",
                    this);
            }
        }

        private void ProcessPendingContainerCommands()
        {
            if (containerLockRequested)
            {
                containerLockRequested = false;
                TryLockContainer();
            }

            if (containerUnlockRequested)
            {
                containerUnlockRequested = false;
                TryUnlockContainer();
            }
        }

        private void TryLockContainer()
        {
            if (!loopsReady || containerLocked)
            {
                return;
            }

            UpdateContainerAlignmentErrors();
            var speed = GetHeadblockPointVelocity().magnitude;
            var aligned = containerHorizontalErrorMeters <= ContainerHorizontalLockToleranceMeters &&
                          containerVerticalErrorMeters <= ContainerVerticalLockToleranceMeters &&
                          containerYawErrorDegrees <= ContainerYawLockToleranceDegrees;
            if (!aligned || speed > MaximumLockingSpeedMetersPerSecond)
            {
                twistlockStatus = "Lock rejected";
                runtimeStatus = "Container lock rejected · align and stop the spreader";
                Debug.LogWarning(
                    $"[YC_CONTAINER_LOCK_REJECTED] horizontal={containerHorizontalErrorMeters:F3}m, " +
                    $"vertical={containerVerticalErrorMeters:F3}m, yaw={containerYawErrorDegrees:F2}deg, " +
                    $"speed={speed:F3}m/s",
                    this);
                return;
            }

            // Perform the complete hand-over at the beginning of one fixed step.
            // The free container collider is suppressed while it is still touching
            // the yard surface so it cannot inject a contact impulse into the newly
            // created suspended rigid body.
            suspensionGravityActive = false;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;
            currentHoistVelocity = 0f;
            ResetLoadedHoistDynamics();
            containerCollider.enabled = false;
            containerColliderSuppressedForPickup = true;

            activeContainer.SetParent(containerAttachmentRoot, true);
            activeContainer.localPosition = containerLockedLocalPosition;
            activeContainer.localRotation = Quaternion.identity;
            containerLocked = true;
            ResetContainerLandingConfirmation();
            twistlockStatus = "Locked · 4/4";
            ApplySuspendedLoadProperties();
            loadedSuspensionEquilibriumOffset = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                Vector3.up);
            RebaselineLoopConstraintsFromCurrentPose();
            UpdateContainerLoadFeedback();
            loadTransferPreTensioning = true;
            loadTransferReleaseAt = Time.fixedTime + LoadTransferPreTensionDurationSeconds;
            runtimeStatus = $"Transferring {containerGrossMassKg / 1000f:F1} t load to eight ropes";
            Debug.Log(
                $"[YC_CONTAINER_LOCKED] grossMass={containerGrossMassKg:F0}kg, " +
                $"totalSuspendedMass={totalSuspendedMassKg:F0}kg, proxyMass={headblockBody.mass:F2}kg, " +
                $"averageLegTension={averageRopeTensionKn:F1}kN",
                this);
        }

        private void TryUnlockContainer()
        {
            if (!loopsReady || !containerLocked)
            {
                return;
            }

            if (!containerLandingConfirmed)
            {
                twistlockStatus = "Unlock inhibited";
                runtimeStatus = "Unlock inhibited · confirm position, attitude and stationary landing";
                var stableFor = containerLandingStableSince >= 0f
                    ? Time.fixedTime - containerLandingStableSince
                    : 0f;
                Debug.LogWarning(
                    $"[YC_CONTAINER_UNLOCK_REJECTED] confirmed={containerLandingConfirmed}, " +
                    $"horizontal={containerLandingHorizontalErrorMeters:F3}m, " +
                    $"vertical={containerLandingVerticalErrorMeters:F3}m, " +
                    $"yaw={containerLandingYawErrorDegrees:F2}deg, " +
                    $"speed={containerLandingSpeedMetersPerSecond:F3}m/s, " +
                    $"stableFor={stableFor:F2}s, required={ContainerLandingStableHoldSeconds:F2}s",
                    this);
                return;
            }

            suspensionGravityActive = false;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentHoistVelocity = 0f;
            ResetLoadedHoistDynamics();
            containerCollider.enabled = true;
            containerColliderSuppressedForPickup = false;
            activeContainer.SetParent(containerOriginalParent, true);
            containerLocked = false;
            ResetContainerLandingConfirmation();
            loadedSuspensionEquilibriumOffset = Vector3.zero;
            twistlockStatus = "Unlocked";
            ApplySuspendedLoadProperties();
            RebaselineLoopConstraintsFromCurrentPose();
            UpdateContainerLoadFeedback();
            loadTransferPreTensioning = true;
            loadTransferReleaseAt = Time.fixedTime + LoadTransferPreTensionDurationSeconds;
            runtimeStatus = "Container released on landing surface";
            Debug.Log("[YC_CONTAINER_UNLOCKED] load removed after landing confirmation", this);
        }

        private void ApplySuspendedLoadProperties()
        {
            var proxyContainerMass = containerLocked
                ? SimulationProxyMassKg * containerGrossMassKg / SpreaderRealMassKg
                : 0f;
            headblockBody.mass = SimulationProxyMassKg + proxyContainerMass;

            if (containerLocked)
            {
                var containerCenterLocal = headblockPhysics.InverseTransformPoint(activeContainer.position);
                var combinedCenterOfMass = containerCenterLocal *
                                           (proxyContainerMass / headblockBody.mass);
                headblockBody.centerOfMass = combinedCenterOfMass;

                // The carried collider is deliberately excluded during pickup.
                // Build the compound inertia explicitly so physical load feedback
                // remains present without depending on collider attach timing.
                var containerInertia = new Vector3(
                    proxyContainerMass * (ContainerColliderSizeMeters.y * ContainerColliderSizeMeters.y + ContainerColliderSizeMeters.z * ContainerColliderSizeMeters.z) / 12f,
                    proxyContainerMass * (ContainerColliderSizeMeters.x * ContainerColliderSizeMeters.x + ContainerColliderSizeMeters.z * ContainerColliderSizeMeters.z) / 12f,
                    proxyContainerMass * (ContainerColliderSizeMeters.x * ContainerColliderSizeMeters.x + ContainerColliderSizeMeters.y * ContainerColliderSizeMeters.y) / 12f);
                var spreaderOffset = -combinedCenterOfMass;
                var containerOffset = containerCenterLocal - combinedCenterOfMass;
                var compoundInertia = unloadedInertiaTensor + containerInertia;
                compoundInertia += ParallelAxisInertia(SimulationProxyMassKg, spreaderOffset);
                compoundInertia += ParallelAxisInertia(proxyContainerMass, containerOffset);
                headblockBody.inertiaTensorRotation = unloadedInertiaTensorRotation;
                headblockBody.inertiaTensor = new Vector3(
                    Mathf.Max(compoundInertia.x, 0.0001f),
                    Mathf.Max(compoundInertia.y, 0.0001f),
                    Mathf.Max(compoundInertia.z, 0.0001f));
            }
            else
            {
                headblockBody.ResetCenterOfMass();
                headblockBody.inertiaTensorRotation = unloadedInertiaTensorRotation;
                headblockBody.inertiaTensor = unloadedInertiaTensor;
            }

            headblockBody.angularDamping = containerLocked ? LoadedHeadblockAngularDamping : UnloadedHeadblockAngularDamping;
        }

        private static Vector3 ParallelAxisInertia(float mass, Vector3 offset)
        {
            return new Vector3(
                mass * (offset.y * offset.y + offset.z * offset.z),
                mass * (offset.x * offset.x + offset.z * offset.z),
                mass * (offset.x * offset.x + offset.y * offset.y));
        }

        private void UpdateContainerContactStatus()
        {
            UpdateContainerAlignmentErrors();
            if (runAutomatedDemo && !demoCompleted)
            {
                return;
            }

            if (!containerLocked &&
                containerHorizontalErrorMeters <= ContainerHorizontalLockToleranceMeters &&
                containerVerticalErrorMeters <= ContainerVerticalLockToleranceMeters &&
                containerYawErrorDegrees <= ContainerYawLockToleranceDegrees &&
                GetHeadblockPointVelocity().magnitude <= MaximumLockingSpeedMetersPerSecond)
            {
                twistlockStatus = "Landed · ready to lock";
                runtimeStatus = "Spreader landed on container · press L to lock";
            }
        }

        private void UpdateContainerLandingConfirmation()
        {
            if (!containerLocked)
            {
                ResetContainerLandingConfirmation();
                return;
            }

            var difference = activeContainer.position - containerHomePosition;
            containerLandingHorizontalErrorMeters = new Vector2(difference.x, difference.z).magnitude;
            containerLandingVerticalErrorMeters = Mathf.Abs(difference.y);
            containerLandingYawErrorDegrees = Mathf.Abs(Mathf.DeltaAngle(
                containerHomeRotation.eulerAngles.y,
                activeContainer.eulerAngles.y));
            containerLandingSpeedMetersPerSecond = GetHeadblockPointVelocity().magnitude;

            var stableLanding = containerLandingHorizontalErrorMeters <= ContainerHorizontalLockToleranceMeters &&
                                containerLandingVerticalErrorMeters <= ContainerLandingToleranceMeters &&
                                containerLandingYawErrorDegrees <= ContainerYawLockToleranceDegrees &&
                                containerLandingSpeedMetersPerSecond <= MaximumLockingSpeedMetersPerSecond;
            if (!stableLanding)
            {
                containerLandingStableSince = -1f;
                containerLandingConfirmed = false;
                containerLandingReported = false;
                return;
            }

            if (containerLandingStableSince < 0f)
            {
                containerLandingStableSince = Time.fixedTime;
            }

            containerLandingConfirmed =
                Time.fixedTime - containerLandingStableSince >= ContainerLandingStableHoldSeconds;
            if (!containerLandingConfirmed || containerLandingReported)
            {
                return;
            }

            containerLandingReported = true;
            twistlockStatus = "Landed · unlock enabled";
            runtimeStatus = "Container landing confirmed · press L to unlock";
            Debug.Log(
                $"[YC_CONTAINER_LANDED] horizontal={containerLandingHorizontalErrorMeters:F3}m, " +
                $"vertical={containerLandingVerticalErrorMeters:F3}m, " +
                $"yaw={containerLandingYawErrorDegrees:F2}deg, " +
                $"speed={containerLandingSpeedMetersPerSecond:F3}m/s, " +
                $"stableFor={Time.fixedTime - containerLandingStableSince:F2}s",
                this);
        }

        private void ResetContainerLandingConfirmation()
        {
            containerLandingHorizontalErrorMeters = 0f;
            containerLandingVerticalErrorMeters = 0f;
            containerLandingYawErrorDegrees = 0f;
            containerLandingSpeedMetersPerSecond = 0f;
            containerLandingStableSince = -1f;
            containerLandingConfirmed = false;
            containerLandingReported = false;
        }

        private void UpdateContainerAlignmentErrors()
        {
            if (containerLocked)
            {
                containerHorizontalErrorMeters = 0f;
                containerVerticalErrorMeters = 0f;
                containerYawErrorDegrees = 0f;
                return;
            }

            var desiredCenter = containerAttachmentRoot.TransformPoint(containerLockedLocalPosition);
            var difference = activeContainer.position - desiredCenter;
            containerHorizontalErrorMeters = new Vector2(difference.x, difference.z).magnitude;
            containerVerticalErrorMeters = Mathf.Abs(difference.y);
            containerYawErrorDegrees = Mathf.Abs(Mathf.DeltaAngle(
                containerAttachmentRoot.eulerAngles.y,
                activeContainer.eulerAngles.y));
        }

        private Vector3 GetHeadblockPointVelocity()
        {
            var attachmentPoint = containerAttachmentRoot.TransformPoint(containerLockedLocalPosition);
            var attachmentOffset = attachmentPoint - headblockBody.worldCenterOfMass;
            var velocity = headblockBody.linearVelocity +
                           Vector3.Cross(headblockBody.angularVelocity, attachmentOffset);

            // Child mechanisms do not contribute to Rigidbody.angularVelocity, so
            // add the driven yaw component explicitly for off-centre attachment
            // points and future twin-container layouts.
            if (Mathf.Abs(spreaderRotationAngularVelocityRadiansPerSecond) > 0.0001f)
            {
                var rotationAxis = headblockPhysics.TransformDirection(Vector3.up).normalized;
                var drivenOffset = attachmentPoint - containerAttachmentRoot.position;
                velocity += Vector3.Cross(
                    rotationAxis * spreaderRotationAngularVelocityRadiansPerSecond,
                    drivenOffset);
            }

            return velocity;
        }

        private void UpdateContainerLoadFeedback()
        {
            totalSuspendedMassKg = SpreaderRealMassKg + (containerLocked ? containerGrossMassKg : 0f);
            var verticalDirectionSum = 0f;
            foreach (var loop in loops)
            {
                var sheavePosition = headblockBody.position + headblockBody.rotation * loop.LocalSheaveAnchor;
                var exitDirection = (loop.Exit.position - sheavePosition).normalized;
                var deadEndDirection = (loop.DeadEnd.position - sheavePosition).normalized;
                verticalDirectionSum += Mathf.Max(0f, Vector3.Dot(exitDirection, Vector3.up));
                verticalDirectionSum += Mathf.Max(0f, Vector3.Dot(deadEndDirection, Vector3.up));
            }

            averageRopeTensionKn = totalSuspendedMassKg * Physics.gravity.magnitude /
                                   Mathf.Max(verticalDirectionSum, 0.1f) / 1000f;
        }

        private void CaptureBoundaryMotion()
        {
            var inverseDeltaTime = 1f / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            foreach (var loop in loops)
            {
                loop.ExitVelocity = (loop.Exit.position - loop.LastExitPosition) * inverseDeltaTime;
                loop.DeadEndVelocity = (loop.DeadEnd.position - loop.LastDeadEndPosition) * inverseDeltaTime;
                loop.LoopLengthRate = (loop.CommandedLoopLength - loop.LastCommandedLoopLength) * inverseDeltaTime;
                loop.LastExitPosition = loop.Exit.position;
                loop.LastDeadEndPosition = loop.DeadEnd.position;
                loop.LastCommandedLoopLength = loop.CommandedLoopLength;
            }
        }

        private void SolveEightLoopConstraints(bool applyPoseImmediately = false)
        {
            var startPosition = headblockBody.position;
            var startRotation = headblockBody.rotation;
            var localCenterOfMass = headblockBody.centerOfMass;
            var startCenterOfMassPosition = startPosition + startRotation * localCenterOfMass;
            var correctedRotation = applyPoseImmediately
                ? startRotation
                : IntegrateRotation(startRotation, headblockBody.angularVelocity, Time.fixedDeltaTime);
            var predictedCenterOfMassPosition = applyPoseImmediately
                ? startCenterOfMassPosition
                : startCenterOfMassPosition + headblockBody.linearVelocity * Time.fixedDeltaTime;
            var correctedPosition = predictedCenterOfMassPosition - correctedRotation * localCenterOfMass;

            foreach (var loop in loops)
            {
                var drift = Mathf.Max(0f, CalculateActualLoopLength(loop, correctedPosition, correctedRotation) - loop.CommandedLoopLength);
                maximumRawLoopDrift = Mathf.Max(maximumRawLoopDrift, drift);
            }

            for (var iteration = 0; iteration < PositionConstraintIterations; iteration++)
            {
                var correctionRequired = false;
                foreach (var loop in loops)
                {
                    if (ProjectLoopPosition(loop, ref correctedPosition, ref correctedRotation))
                    {
                        correctionRequired = true;
                    }
                }
                if (!correctionRequired) break;
            }

            // MAX headblock pulleys are mechanically interlocked. The total loop
            // constraint above models paid-out cable; this second constraint resists
            // free redistribution between both legs of a triangle, which is the
            // missing anti-sway/anti-twist mechanism in a free-running sheave model.
            for (var iteration = 0; iteration < InterlockPositionIterations; iteration++)
            {
                foreach (var loop in loops)
                {
                    ProjectInterlockedLegDifference(loop, ref correctedPosition, ref correctedRotation);
                }
            }

            // In normal operation the projected pose is the desired end pose of this
            // PhysX step. Convert it to COM velocity and let PhysX integrate exactly
            // once. Teleporting to the pose here and also keeping boundary/hoist
            // velocity would execute every command twice and create fall/correct
            // chatter. Startup/load-transfer holds still request immediate projection.
            var correctedCenterOfMassPosition =
                correctedPosition + correctedRotation * localCenterOfMass;
            var linearVelocity = applyPoseImmediately
                ? headblockBody.linearVelocity
                : (correctedCenterOfMassPosition - startCenterOfMassPosition) /
                  Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            var angularVelocity = applyPoseImmediately
                ? headblockBody.angularVelocity
                : CalculateAngularVelocity(startRotation, correctedRotation, Time.fixedDeltaTime);

            if (applyPoseImmediately)
            {
                headblockBody.position = correctedPosition;
                headblockBody.rotation = correctedRotation;
            }
            for (var iteration = 0; iteration < VelocityConstraintIterations; iteration++)
            {
                foreach (var loop in loops)
                {
                    ProjectLoopVelocity(loop, correctedPosition, correctedRotation, ref linearVelocity, ref angularVelocity);
                }
            }
            for (var iteration = 0; iteration < InterlockVelocityIterations; iteration++)
            {
                foreach (var loop in loops)
                {
                    ProjectInterlockedLegDifferenceVelocity(
                        loop,
                        correctedPosition,
                        correctedRotation,
                        ref linearVelocity,
                        ref angularVelocity);
                }
            }
            headblockBody.linearVelocity = linearVelocity;
            headblockBody.angularVelocity = angularVelocity;
        }

        private static Quaternion IntegrateRotation(
            Quaternion rotation,
            Vector3 angularVelocityRadiansPerSecond,
            float deltaTime)
        {
            var angularSpeed = angularVelocityRadiansPerSecond.magnitude;
            if (angularSpeed <= 0.000001f || deltaTime <= 0f)
            {
                return rotation;
            }

            var deltaRotation = Quaternion.AngleAxis(
                angularSpeed * deltaTime * Mathf.Rad2Deg,
                angularVelocityRadiansPerSecond / angularSpeed);
            return Quaternion.Normalize(deltaRotation * rotation);
        }

        private static Vector3 CalculateAngularVelocity(
            Quaternion startRotation,
            Quaternion endRotation,
            float deltaTime)
        {
            if (deltaTime <= 0.000001f)
            {
                return Vector3.zero;
            }

            var deltaRotation = Quaternion.Normalize(endRotation * Quaternion.Inverse(startRotation));
            if (deltaRotation.w < 0f)
            {
                deltaRotation = new Quaternion(
                    -deltaRotation.x,
                    -deltaRotation.y,
                    -deltaRotation.z,
                    -deltaRotation.w);
            }

            deltaRotation.ToAngleAxis(out var angleDegrees, out var axis);
            if (angleDegrees <= 0.0001f || !IsFinite(axis))
            {
                return Vector3.zero;
            }

            if (angleDegrees > 180f)
            {
                angleDegrees -= 360f;
            }

            return axis.normalized * (angleDegrees * Mathf.Deg2Rad / deltaTime);
        }

        private bool ProjectLoopPosition(
            LoopRuntime loop,
            ref Vector3 bodyPosition,
            ref Quaternion bodyRotation)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            var sheaveOffsetFromCenterOfMass = bodyRotation *
                                               (loop.LocalSheaveAnchor - headblockBody.centerOfMass);
            var exitVector = sheavePosition - loop.Exit.position;
            var deadEndVector = sheavePosition - loop.DeadEnd.position;
            var exitDistance = exitVector.magnitude;
            var deadEndDistance = deadEndVector.magnitude;
            if (exitDistance <= Mathf.Epsilon || deadEndDistance <= Mathf.Epsilon)
            {
                return false;
            }

            var excess = exitDistance + deadEndDistance - loop.CommandedLoopLength - LoopConstraintToleranceMeters;
            if (excess <= 0f)
            {
                return false;
            }

            var linearJacobian = exitVector / exitDistance + deadEndVector / deadEndDistance;
            var angularJacobian = Vector3.Cross(sheaveOffsetFromCenterOfMass, linearJacobian);
            var inverseMass = 1f / Mathf.Max(headblockBody.mass, 0.0001f);
            var inverseAngularMass = MultiplyInverseInertiaWorld(angularJacobian, bodyRotation);
            var effectiveInverseMass = inverseMass * linearJacobian.sqrMagnitude +
                                       Vector3.Dot(angularJacobian, inverseAngularMass);
            if (effectiveInverseMass <= 0.000001f)
            {
                return false;
            }

            var impulse = -excess / effectiveInverseMass;
            var centerOfMassPosition = bodyPosition + bodyRotation * headblockBody.centerOfMass;
            centerOfMassPosition += linearJacobian * (impulse * inverseMass);
            var angularCorrection = inverseAngularMass * impulse;
            ApplyAngularCorrection(ref bodyRotation, angularCorrection);
            bodyPosition = centerOfMassPosition - bodyRotation * headblockBody.centerOfMass;
            return true;
        }

        private void ProjectLoopVelocity(
            LoopRuntime loop,
            Vector3 bodyPosition,
            Quaternion bodyRotation,
            ref Vector3 linearVelocity,
            ref Vector3 angularVelocity)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            var sheaveOffsetFromCenterOfMass = bodyRotation *
                                               (loop.LocalSheaveAnchor - headblockBody.centerOfMass);
            var exitVector = sheavePosition - loop.Exit.position;
            var deadEndVector = sheavePosition - loop.DeadEnd.position;
            var exitDistance = exitVector.magnitude;
            var deadEndDistance = deadEndVector.magnitude;
            var actualLoopLength = exitDistance + deadEndDistance;
            if (exitDistance <= Mathf.Epsilon || deadEndDistance <= Mathf.Epsilon ||
                actualLoopLength < loop.CommandedLoopLength - LoopConstraintToleranceMeters)
            {
                return;
            }

            var exitDirection = exitVector / exitDistance;
            var deadEndDirection = deadEndVector / deadEndDistance;
            var linearJacobian = exitDirection + deadEndDirection;
            var angularJacobian = Vector3.Cross(sheaveOffsetFromCenterOfMass, linearJacobian);
            var sheaveVelocity = linearVelocity +
                                 Vector3.Cross(angularVelocity, sheaveOffsetFromCenterOfMass);
            var constraintRate = Vector3.Dot(exitDirection, sheaveVelocity - loop.ExitVelocity) +
                                 Vector3.Dot(deadEndDirection, sheaveVelocity - loop.DeadEndVelocity) -
                                 loop.LoopLengthRate;
            if (constraintRate <= 0f)
            {
                return;
            }

            var inverseMass = 1f / Mathf.Max(headblockBody.mass, 0.0001f);
            var inverseAngularMass = MultiplyInverseInertiaWorld(angularJacobian, bodyRotation);
            var effectiveInverseMass = inverseMass * linearJacobian.sqrMagnitude +
                                       Vector3.Dot(angularJacobian, inverseAngularMass);
            if (effectiveInverseMass <= 0.000001f)
            {
                return;
            }

            var impulse = -constraintRate / effectiveInverseMass;
            linearVelocity += linearJacobian * (impulse * inverseMass);
            angularVelocity += inverseAngularMass * impulse;
        }

        private void ProjectInterlockedLegDifference(
            LoopRuntime loop,
            ref Vector3 bodyPosition,
            ref Quaternion bodyRotation)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            var sheaveOffsetFromCenterOfMass = bodyRotation *
                                               (loop.LocalSheaveAnchor - headblockBody.centerOfMass);
            var exitVector = sheavePosition - loop.Exit.position;
            var deadEndVector = sheavePosition - loop.DeadEnd.position;
            var exitDistance = exitVector.magnitude;
            var deadEndDistance = deadEndVector.magnitude;
            if (exitDistance <= Mathf.Epsilon || deadEndDistance <= Mathf.Epsilon)
            {
                return;
            }

            var differenceError = exitDistance - deadEndDistance - loop.CommandedLegDifference;
            if (Mathf.Abs(differenceError) <= LoopConstraintToleranceMeters)
            {
                return;
            }

            var linearJacobian = exitVector / exitDistance - deadEndVector / deadEndDistance;
            var angularJacobian = Vector3.Cross(sheaveOffsetFromCenterOfMass, linearJacobian);
            var inverseMass = 1f / Mathf.Max(headblockBody.mass, 0.0001f);
            var inverseAngularMass = MultiplyInverseInertiaWorld(angularJacobian, bodyRotation);
            var effectiveInverseMass = inverseMass * linearJacobian.sqrMagnitude +
                                       Vector3.Dot(angularJacobian, inverseAngularMass);
            if (effectiveInverseMass <= 0.000001f)
            {
                return;
            }

            var impulse = -differenceError / effectiveInverseMass * GetInterlockPositionRelaxation();
            var centerOfMassPosition = bodyPosition + bodyRotation * headblockBody.centerOfMass;
            centerOfMassPosition += linearJacobian * (impulse * inverseMass);
            ApplyAngularCorrection(ref bodyRotation, inverseAngularMass * impulse);
            bodyPosition = centerOfMassPosition - bodyRotation * headblockBody.centerOfMass;
        }

        private void ProjectInterlockedLegDifferenceVelocity(
            LoopRuntime loop,
            Vector3 bodyPosition,
            Quaternion bodyRotation,
            ref Vector3 linearVelocity,
            ref Vector3 angularVelocity)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            var sheaveOffsetFromCenterOfMass = bodyRotation *
                                               (loop.LocalSheaveAnchor - headblockBody.centerOfMass);
            var exitVector = sheavePosition - loop.Exit.position;
            var deadEndVector = sheavePosition - loop.DeadEnd.position;
            var exitDistance = exitVector.magnitude;
            var deadEndDistance = deadEndVector.magnitude;
            if (exitDistance <= Mathf.Epsilon || deadEndDistance <= Mathf.Epsilon)
            {
                return;
            }

            var exitDirection = exitVector / exitDistance;
            var deadEndDirection = deadEndVector / deadEndDistance;
            var linearJacobian = exitDirection - deadEndDirection;
            var angularJacobian = Vector3.Cross(sheaveOffsetFromCenterOfMass, linearJacobian);
            var sheaveVelocity = linearVelocity +
                                 Vector3.Cross(angularVelocity, sheaveOffsetFromCenterOfMass);
            var differenceRate = Vector3.Dot(exitDirection, sheaveVelocity - loop.ExitVelocity) -
                                 Vector3.Dot(deadEndDirection, sheaveVelocity - loop.DeadEndVelocity);
            var inverseMass = 1f / Mathf.Max(headblockBody.mass, 0.0001f);
            var inverseAngularMass = MultiplyInverseInertiaWorld(angularJacobian, bodyRotation);
            var effectiveInverseMass = inverseMass * linearJacobian.sqrMagnitude +
                                       Vector3.Dot(angularJacobian, inverseAngularMass);
            if (effectiveInverseMass <= 0.000001f)
            {
                return;
            }

            var impulse = -differenceRate / effectiveInverseMass * GetInterlockVelocityRelaxation();
            linearVelocity += linearJacobian * (impulse * inverseMass);
            angularVelocity += inverseAngularMass * impulse;
        }

        private float GetInterlockPositionRelaxation()
        {
            if (!containerLocked)
            {
                return InterlockPositionRelaxation;
            }

            // A locked load uses the continuous suspension spring/damper below.
            // Re-applying the bilateral leg-difference projection eight times per
            // iteration would kinematically pin the load beneath the trolley and
            // remove all perceptible inertia.
            return 0f;
        }

        private float GetInterlockVelocityRelaxation()
        {
            if (!containerLocked)
            {
                return InterlockVelocityRelaxation;
            }

            return 0f;
        }

        private Vector3 MultiplyInverseInertiaWorld(Vector3 worldVector, Quaternion bodyRotation)
        {
            var inertiaRotation = bodyRotation * headblockBody.inertiaTensorRotation;
            var localVector = Quaternion.Inverse(inertiaRotation) * worldVector;
            var tensor = headblockBody.inertiaTensor;
            localVector = new Vector3(
                localVector.x / Mathf.Max(tensor.x, 0.0001f),
                localVector.y / Mathf.Max(tensor.y, 0.0001f),
                localVector.z / Mathf.Max(tensor.z, 0.0001f));
            return inertiaRotation * localVector;
        }

        private void ApplyAngularCorrection(ref Quaternion rotation, Vector3 angularCorrectionRadians)
        {
            var angleRadians = angularCorrectionRadians.magnitude;
            if (angleRadians <= 0.000001f)
            {
                return;
            }

            var angleDegrees = Mathf.Min(angleRadians * Mathf.Rad2Deg, MaximumAngularCorrectionDegrees);
            rotation = Quaternion.AngleAxis(angleDegrees, angularCorrectionRadians / angleRadians) * rotation;
            rotation = Quaternion.Normalize(rotation);
        }

        private void SynchronizeVisualLegLengths()
        {
            foreach (var loop in loops)
            {
                SetVisualLegLength(loop.ExitLeg, Vector3.Distance(loop.Exit.position, loop.Sheave.position));
                SetVisualLegLength(loop.DeadEndLeg, Vector3.Distance(loop.DeadEnd.position, loop.Sheave.position));
            }
        }

        private void ProjectVisualRopesToEngineeringGeometry()
        {
            foreach (var loop in loops)
            {
                ProjectVisualLegStraight(loop.ExitLeg, loop.Exit.position, loop.Sheave.position);
                ProjectVisualLegStraight(loop.DeadEndLeg, loop.DeadEnd.position, loop.Sheave.position);
            }
        }

        private static void ProjectVisualLegStraight(VisualLegRuntime leg, Vector3 startWorld, Vector3 endWorld)
        {
            var rope = leg.Rope;
            if (!rope.isLoaded || rope.solver == null || rope.elements.Count == 0)
            {
                return;
            }

            var start = rope.solver.transform.InverseTransformPoint(startWorld);
            var end = rope.solver.transform.InverseTransformPoint(endWorld);
            var totalRestLength = 0f;
            foreach (var element in rope.elements)
            {
                totalRestLength += Mathf.Max(0f, element.restLength);
            }

            if (totalRestLength <= Mathf.Epsilon)
            {
                return;
            }

            var accumulatedLength = 0f;
            foreach (var element in rope.elements)
            {
                SetVisualParticlePose(rope.solver, element.particle1, Vector3.Lerp(start, end, accumulatedLength / totalRestLength));
                accumulatedLength += Mathf.Max(0f, element.restLength);
                SetVisualParticlePose(rope.solver, element.particle2, Vector3.Lerp(start, end, accumulatedLength / totalRestLength));
            }
        }

        private static void SetVisualParticlePose(ObiSolver ropeSolver, int solverIndex, Vector3 position)
        {
            if (solverIndex < 0 || solverIndex >= ropeSolver.positions.count)
            {
                return;
            }

            ropeSolver.positions[solverIndex] = position;
            ropeSolver.startPositions[solverIndex] = position;
            ropeSolver.endPositions[solverIndex] = position;
            ropeSolver.renderablePositions[solverIndex] = position;
            ropeSolver.velocities[solverIndex] = Vector3.zero;
        }

        private static void SetVisualLegLength(VisualLegRuntime leg, float targetLength)
        {
            var delta = targetLength - leg.CommandedVisualLength;
            if (Mathf.Abs(delta) > 0.0001f)
            {
                leg.Cursor.ChangeLength(delta);
                leg.CommandedVisualLength = targetLength;
            }
        }

        private void RebaselineLoopConstraintsFromCurrentPose()
        {
            commandedSuspendedDrop = CalculateSuspendedDrop();
            foreach (var loop in loops)
            {
                var actualLoopLength = CalculateActualLoopLength(
                    loop,
                    headblockBody.position,
                    headblockBody.rotation);
                loop.CommandedLoopLength = actualLoopLength;
                loop.LastCommandedLoopLength = actualLoopLength;
                loop.CommandedLegDifference = CalculateActualLegDifference(
                    loop,
                    headblockBody.position,
                    headblockBody.rotation);
                loop.LastExitPosition = loop.Exit.position;
                loop.LastDeadEndPosition = loop.DeadEnd.position;
                loop.ExitVelocity = Vector3.zero;
                loop.DeadEndVelocity = Vector3.zero;
                loop.LoopLengthRate = 0f;
            }
        }

        private void RunLoadTransferPreTension()
        {
            CaptureBoundaryMotion();
            SolveEightLoopConstraints(applyPoseImmediately: true);
            SynchronizeVisualLegLengths();
            ProjectVisualRopesToEngineeringGeometry();
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;
            currentHoistVelocity = 0f;
            UpdateContainerLoadFeedback();

            if (Time.fixedTime < loadTransferReleaseAt)
            {
                runtimeStatus = containerLocked
                    ? $"Pre-tensioning eight ropes for {containerGrossMassKg / 1000f:F1} t load"
                    : "Rebalancing eight ropes after container release";
                return;
            }

            loadTransferPreTensioning = false;
            suspensionGravityActive = true;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            if (containerLocked)
            {
                ClearObservedMetrics();
            }
            runtimeStatus = containerLocked
                ? $"Container secured · {containerGrossMassKg / 1000f:F1} t · hoist enabled"
                : "Container released · hoist enabled";
            Debug.Log(
                $"[YC_CONTAINER_LOAD_TRANSFER_READY] locked={containerLocked}, " +
                $"hold={LoadTransferPreTensionDurationSeconds:F2}s, centerOfMass={headblockBody.centerOfMass}, " +
                $"proxyMass={headblockBody.mass:F2}kg, " +
                $"interlockPosition={GetInterlockPositionRelaxation():F3}, " +
                $"interlockVelocity={GetInterlockVelocityRelaxation():F3}, " +
                $"suspensionOmega={LoadedSuspensionNaturalFrequencyRadiansPerSecond:F2}, " +
                $"suspensionDamping={LoadedSuspensionDampingRatio:F2}, " +
                $"hoistOmega={LoadedHoistNaturalFrequencyRadiansPerSecond:F2}, " +
                $"hoistDamping={LoadedHoistDampingRatio:F2}, " +
                $"staticRopeDeflection={GetLoadedStaticRopeDeflection() * 1000f:F1}mm, " +
                $"headblockY={headblockBody.position.y:F3}m",
                this);
        }

        private void RestoreContainerColliderWhenClear()
        {
            if (!containerLocked || !containerColliderSuppressedForPickup ||
                activeContainer.position.y <= containerRestingCenterY + LoadedTrolleyMinimumClearanceMeters)
            {
                return;
            }

            containerCollider.enabled = true;
            containerColliderSuppressedForPickup = false;
            ApplySuspendedLoadProperties();
            Debug.Log(
                $"[YC_CONTAINER_COLLIDER_RESTORED] clearance=" +
                $"{activeContainer.position.y - containerRestingCenterY:F3}m",
                this);
        }

        private void RunStartupPreTension()
        {
            CaptureBoundaryMotion();
            SolveEightLoopConstraints(applyPoseImmediately: true);
            SynchronizeVisualLegLengths();
            ProjectVisualRopesToEngineeringGeometry();
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;

            if (Time.fixedTime < startupReleaseAt)
            {
                runtimeStatus = "Pre-tensioning eight triangular rope loops";
                return;
            }

            ClearObservedMetrics();
            demoStartedAt = Time.fixedTime;
            startupPreTensioning = false;
            headblockBody.useGravity = false;
            suspensionGravityActive = true;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            runtimeStatus = runAutomatedDemo ? "Automated MAX demo started" : "MAX manual operation ready";
            Debug.Log($"[YC_EIGHT_ROPE_MAX_PRETENSIONED] model={ImplementationVersion}, " +
                      $"hold={StartupPreTensionDurationSeconds:F2}s, headblockY={headblockBody.position.y:F3}m", this);
        }

        private void ObserveRuntimeState()
        {
            var trolleyCenter = trolleyMotion.position;
            var horizontalOffset = headblockBody.position - trolleyCenter;
            horizontalOffset.y = 0f;
            currentHorizontalSway = horizontalOffset.magnitude;
            maximumObservedSway = Mathf.Max(maximumObservedSway, currentHorizontalSway);
            var trolleyDirection = trolleyMotion.parent != null
                ? trolleyMotion.parent.TransformDirection(Vector3.right).normalized
                : Vector3.right;
            var trolleyVelocity = trolleyDirection * currentTrolleyVelocity;
            currentRelativeHorizontalSpeed = Vector3.ProjectOnPlane(
                headblockBody.linearVelocity - trolleyVelocity,
                Vector3.up).magnitude;
            maximumObservedTwistDegrees = Mathf.Max(
                maximumObservedTwistDegrees,
                Quaternion.Angle(headblockHomeRotation, headblockBody.rotation));

            foreach (var loop in loops)
            {
                var extension = Mathf.Max(
                    0f,
                    CalculateActualLoopLength(loop, headblockBody.position, headblockBody.rotation) - loop.CommandedLoopLength);
                if (extension > maximumObservedLoopExtension)
                {
                    maximumObservedLoopExtension = extension;
                    peakLoopId = loop.Definition.Id;
                }
            }
        }

        private static float CalculateActualLoopLength(LoopRuntime loop, Vector3 bodyPosition, Quaternion bodyRotation)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            return Vector3.Distance(loop.Exit.position, sheavePosition) +
                   Vector3.Distance(loop.DeadEnd.position, sheavePosition);
        }

        private static float CalculateActualLegDifference(LoopRuntime loop, Vector3 bodyPosition, Quaternion bodyRotation)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            return Vector3.Distance(loop.Exit.position, sheavePosition) -
                   Vector3.Distance(loop.DeadEnd.position, sheavePosition);
        }

        private float CalculateSuspendedDrop()
        {
            return Mathf.Max(0f, trolleyMotion.position.y - headblockPhysics.position.y);
        }

        private void ClearObservedMetrics()
        {
            maximumObservedSway = 0f;
            currentHorizontalSway = 0f;
            currentRelativeHorizontalSpeed = 0f;
            maximumObservedLoopExtension = 0f;
            maximumRawLoopDrift = 0f;
            maximumObservedTwistDegrees = 0f;
            residualTwistDegrees = 0f;
            peakLoopId = "-";
        }

        [ContextMenu("Reset MAX Prototype")]
        public void ResetPrototype()
        {
            TryResetPrototype(out _);
        }

        private bool TryResetPrototype(out string rejectionCode)
        {
            if (!loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                Debug.LogWarning("[YC_PROTOTYPE_RESET_REJECTED] reason=not-ready", this);
                return false;
            }

            var bodyLinearSpeed = headblockBody.linearVelocity.magnitude;
            var bodyAngularSpeed = headblockBody.angularVelocity.magnitude;
            var finiteState = IsFinite(headblockBody.position) &&
                              IsFinite(headblockBody.linearVelocity) &&
                              IsFinite(headblockBody.angularVelocity) &&
                              gantryDrive != null &&
                              float.IsFinite(gantryDrive.PositionMeters) &&
                              float.IsFinite(gantryDrive.VelocityMetersPerSecond) &&
                              float.IsFinite(currentTrolleyVelocity) &&
                              float.IsFinite(currentHoistVelocity);
            var busy = startupPreTensioning || loadTransferPreTensioning ||
                       containerLockRequested || containerUnlockRequested || loadedOperationRegressionActive ||
                       (runAutomatedDemo && !demoCompleted);
            var stationary = gantryDrive != null && gantryDrive.IsStationary &&
                             gantryDrive.WheelClampState != WheelClampState.Transitioning &&
                             (trolleyAnchor == null || trolleyAnchor.State != YardCraneAnchorState.Transitioning) &&
                             (gantryAnchor == null || gantryAnchor.State != YardCraneAnchorState.Transitioning) &&
                             (spreaderSize == null || spreaderSize.State != YardCraneSpreaderSizeMechanismState.Transitioning) &&
                             Mathf.Abs(currentTrolleyVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                             Mathf.Abs(currentHoistVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                             bodyLinearSpeed <= ResetSafeSpeedThresholdMetersPerSecond &&
                             bodyAngularSpeed <= ResetSafeSpeedThresholdMetersPerSecond;
            if (containerLocked || busy || !stationary || !finiteState)
            {
                rejectionCode = YardCraneOperationErrorCodes.ResetInterlockRejected;
                runtimeStatus = "Reset inhibited · unload and stop all mechanisms";
                Debug.LogWarning(
                    $"[YC_PROTOTYPE_RESET_REJECTED] locked={containerLocked}, busy={busy}, " +
                    $"finiteState={finiteState}, gantrySpeed={(gantryDrive?.VelocityMetersPerSecond ?? 0f):F3}m/s, " +
                    $"trolleySpeed={currentTrolleyVelocity:F3}m/s, " +
                    $"hoistSpeed={currentHoistVelocity:F3}m/s, bodySpeed={bodyLinearSpeed:F3}m/s, " +
                    $"angularSpeed={bodyAngularSpeed:F3}rad/s, phase={GetResetPhase()}",
                    this);
                return false;
            }

            gantryDrive.Reset();
            trolleyAnchor?.Reset();
            gantryAnchor?.Reset();
            spreaderSize?.Reset(YardCraneSpreaderSize.Ft40);
            spreaderPumpOn = false;
            spreaderRotation?.Reset();
            spreaderRotationInputAxis = 0f;
            spreaderRotationAngularVelocityRadiansPerSecond = 0f;
            ApplySpreaderRotationPose(0f, true);
            ApplyGantryPosition(0f);
            trolleyMotion.localPosition = trolleyHome;
            ResetContainerLoadState();
            headblockPhysics.SetPositionAndRotation(headblockHome, headblockHomeRotation);
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;
            currentHoistVelocity = 0f;
            gantryInput = 0f;
            SetHornActive(false);
            ResetLoadedHoistDynamics();
            trolleyStopStabilizationReleaseAt = 0f;
            twistDisturbanceApplied = false;
            commandedSuspendedDrop = initialSuspendedDrop;
            foreach (var loop in loops)
            {
                loop.CommandedLoopLength = loop.InitialLoopLength;
                loop.LastCommandedLoopLength = loop.InitialLoopLength;
                loop.CommandedLegDifference = loop.InitialLegDifference;
                loop.LastExitPosition = loop.Exit.position;
                loop.LastDeadEndPosition = loop.DeadEnd.position;
                loop.ExitVelocity = Vector3.zero;
                loop.DeadEndVelocity = Vector3.zero;
                loop.LoopLengthRate = 0f;
            }

            demoStartedAt = Time.fixedTime;
            demoCompleted = false;
            demoClampApplyRequested = false;
            demoClampReleaseRequested = false;
            demoClampInhibitObserved = false;
            demoGantryStartPosition = 0f;
            demoGantryMaximumTravel = 0f;
            ClearObservedMetrics();
            headblockBody.useGravity = false;
            suspensionGravityActive = false;
            containerLockRequested = false;
            containerUnlockRequested = false;
            loadTransferPreTensioning = false;
            ResetActionObservers();
            startupPreTensioning = true;
            startupReleaseAt = Time.fixedTime + StartupPreTensionDurationSeconds;
            runtimeStatus = "MAX prototype reset and pre-tensioning";
            Debug.Log(
                $"[YC_PROTOTYPE_RESET] model={ImplementationVersion}, gantry={gantryDrive.PositionMeters:F3}m, " +
                $"x={trolleyMotion.localPosition.x:F3}m, " +
                $"drop={commandedSuspendedDrop:F3}m, containerLocked={containerLocked}",
                this);
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        private void ResetContainerLoadState()
        {
            containerCollider.enabled = true;
            containerColliderSuppressedForPickup = false;
            if (activeContainer.parent != containerOriginalParent)
            {
                activeContainer.SetParent(containerOriginalParent, true);
            }
            activeContainer.SetPositionAndRotation(containerHomePosition, containerHomeRotation);
            containerLocked = false;
            ResetContainerLandingConfirmation();
            loadedSuspensionEquilibriumOffset = Vector3.zero;
            twistlockStatus = "Unlocked";
            ApplySuspendedLoadProperties();
            UpdateContainerLoadFeedback();
        }

        private string GetResetPhase()
        {
            if (startupPreTensioning) return "startup-pretension";
            if (loadTransferPreTensioning) return "load-transfer";
            if (runAutomatedDemo && !demoCompleted) return "automated-demo";
            if (containerLockRequested || containerUnlockRequested) return "container-command";
            if (containerLocked) return "loaded";
            return "manual";
        }

        private void ResetActionObservers()
        {
            gantryMotionReported = false;
            gantryLimitReported = false;
            gantryClampInhibitReported = false;
            gantryClearanceInhibitReported = false;
            gantryProximityInhibited = false;
            gantryAnchorInhibitReported = false;
            trolleyMotionReported = false;
            trolleyLimitReported = false;
            trolleyClearanceInhibitReported = false;
            trolleyProximityInhibited = false;
            trolleyAnchorInhibitReported = false;
            hoistMotionReported = false;
            hoistLimitReported = false;
            spreaderRotationInhibitReported = false;
            spreaderRotationMotionReported = false;
            ResetContainerLandingConfirmation();
        }

        [ContextMenu("Run Loaded Operation Regression")]
        public void BeginLoadedOperationRegression()
        {
            if (!loopsReady || setupFailed || runAutomatedDemo || !demoCompleted ||
                startupPreTensioning || loadTransferPreTensioning || containerLocked ||
                loadedOperationRegressionActive)
            {
                Debug.LogWarning(
                    $"[YC_LOADED_OPERATION_REGRESSION_REJECTED] loopsReady={loopsReady}, " +
                    $"setupFailed={setupFailed}, automatedDemo={runAutomatedDemo}, demoCompleted={demoCompleted}, " +
                    $"startupPreTensioning={startupPreTensioning}, loadTransfer={loadTransferPreTensioning}, " +
                    $"containerLocked={containerLocked}, alreadyRunning={loadedOperationRegressionActive}",
                    this);
                return;
            }

            trolleyInput = 0f;
            hoistInput = 0f;
            gantryInput = 0f;
            loadedOperationRegressionActive = true;
            loadedRegressionStartedAt = Time.fixedTime;
            loadedRegressionReferenceX = trolleyMotion.localPosition.x;
            SetLoadedRegressionStage(LoadedRegressionStage.AlignForLock);
            Debug.Log(
                $"[YC_LOADED_OPERATION_REGRESSION_STARTED] model={ImplementationVersion}, " +
                $"grossMass={containerGrossMassKg:F0}kg, x={trolleyMotion.localPosition.x:F3}m, " +
                $"drop={commandedSuspendedDrop:F3}m",
                this);
        }

        private void UpdateLoadedOperationRegression()
        {
            if (!loadedOperationRegressionActive)
            {
                return;
            }

            if (Time.fixedTime - loadedRegressionStartedAt > LoadedRegressionTotalTimeoutSeconds)
            {
                FailLoadedOperationRegression("total-timeout");
                return;
            }

            if (Time.fixedTime - loadedRegressionStageStartedAt > LoadedRegressionStageTimeoutSeconds)
            {
                FailLoadedOperationRegression($"stage-timeout:{loadedRegressionStage}");
                return;
            }

            trolleyInput = 0f;
            hoistInput = 0f;

            switch (loadedRegressionStage)
            {
                case LoadedRegressionStage.AlignForLock:
                    UpdateRegressionAlignmentForLock();
                    break;
                case LoadedRegressionStage.AwaitLock:
                    if (containerLocked)
                    {
                        SetLoadedRegressionStage(LoadedRegressionStage.AwaitLoadedReady);
                    }
                    else if (Time.fixedTime - loadedRegressionStageStartedAt >= 0.80f)
                    {
                        SetLoadedRegressionStage(LoadedRegressionStage.AlignForLock);
                    }
                    break;
                case LoadedRegressionStage.AwaitLoadedReady:
                    if (containerLocked && !loadTransferPreTensioning)
                    {
                        loadedRegressionReferenceX = trolleyMotion.localPosition.x;
                        SetLoadedRegressionStage(LoadedRegressionStage.VerifyLowClearanceInterlock);
                    }
                    break;
                case LoadedRegressionStage.VerifyLowClearanceInterlock:
                    UpdateRegressionLowClearanceInterlock();
                    break;
                case LoadedRegressionStage.HoistToSafeClearance:
                    UpdateRegressionHoistToSafeClearance();
                    break;
                case LoadedRegressionStage.RejectSuspendedUnlock:
                    UpdateRegressionSuspendedUnlockRejection();
                    break;
                case LoadedRegressionStage.TravelOutbound:
                    UpdateRegressionTrolleyTarget(trolleyHome.x + LoadedRegressionOutboundTravelMeters,
                        LoadedRegressionStage.ReturnHome);
                    break;
                case LoadedRegressionStage.ReturnHome:
                    UpdateRegressionTrolleyTarget(trolleyHome.x, LoadedRegressionStage.LowerToLanding);
                    break;
                case LoadedRegressionStage.LowerToLanding:
                    UpdateRegressionLowerToLanding();
                    break;
                case LoadedRegressionStage.AwaitLanding:
                    if (containerLandingConfirmed)
                    {
                        if (!loadedRegressionCommandIssued)
                        {
                            loadedRegressionCommandIssued = true;
                            RequestContainerUnlock();
                        }
                        SetLoadedRegressionStage(LoadedRegressionStage.AwaitUnlock);
                    }
                    break;
                case LoadedRegressionStage.AwaitUnlock:
                    if (!containerLocked && !loadTransferPreTensioning)
                    {
                        loadedRegressionReleasedContainerPosition = activeContainer.position;
                        loadedRegressionTargetDrop = Mathf.Max(
                            MinimumSuspendedDropMeters,
                            commandedSuspendedDrop - 0.50f);
                        SetLoadedRegressionStage(LoadedRegressionStage.EmptyHoist);
                    }
                    break;
                case LoadedRegressionStage.EmptyHoist:
                    UpdateRegressionEmptyHoist();
                    break;
            }
        }

        private void UpdateRegressionAlignmentForLock()
        {
            var desiredCenter = containerAttachmentRoot.TransformPoint(containerLockedLocalPosition);
            var verticalSignedError = activeContainer.position.y - desiredCenter.y;
            var horizontalError = new Vector2(
                activeContainer.position.x - desiredCenter.x,
                activeContainer.position.z - desiredCenter.z).magnitude;
            var yawError = Mathf.Abs(Mathf.DeltaAngle(
                containerAttachmentRoot.eulerAngles.y,
                activeContainer.eulerAngles.y));
            if (Mathf.Abs(verticalSignedError) > 0.040f)
            {
                hoistInput = Mathf.Clamp(-verticalSignedError * 3f, -1f, 1f);
                ResetLoadedRegressionStableHold();
                return;
            }

            var ready = horizontalError <= ContainerHorizontalLockToleranceMeters &&
                        Mathf.Abs(verticalSignedError) <= ContainerVerticalLockToleranceMeters &&
                        yawError <= ContainerYawLockToleranceDegrees &&
                        Mathf.Abs(currentHoistVelocity) <= MaximumLockingSpeedMetersPerSecond;
            if (!HoldLoadedRegressionStable(ready))
            {
                return;
            }

            if (!RequestContainerLock())
            {
                FailLoadedOperationRegression("lock-request-not-accepted");
                return;
            }
            SetLoadedRegressionStage(LoadedRegressionStage.AwaitLock);
        }

        private void UpdateRegressionLowClearanceInterlock()
        {
            var stageElapsed = Time.fixedTime - loadedRegressionStageStartedAt;
            if (stageElapsed < 0.50f)
            {
                trolleyInput = 1f;
                return;
            }

            var displacement = Mathf.Abs(trolleyMotion.localPosition.x - loadedRegressionReferenceX);
            if (displacement > 0.005f)
            {
                FailLoadedOperationRegression($"low-clearance-trolley-moved:{displacement:F4}m");
                return;
            }

            if (stageElapsed >= 0.65f)
            {
                SetLoadedRegressionStage(LoadedRegressionStage.HoistToSafeClearance);
            }
        }

        private void UpdateRegressionHoistToSafeClearance()
        {
            var clearance = activeContainer.position.y - containerRestingCenterY;
            var remaining = LoadedRegressionLiftClearanceMeters - clearance;
            if (remaining > 0.080f)
            {
                hoistInput = -Mathf.Clamp(remaining * 2f, 0.08f, 1f);
                ResetLoadedRegressionStableHold();
                return;
            }

            var stable = Mathf.Abs(currentHoistVelocity) <= 0.02f &&
                         !loadedHoistSettleObservationActive;
            if (HoldLoadedRegressionStable(stable))
            {
                SetLoadedRegressionStage(LoadedRegressionStage.RejectSuspendedUnlock);
            }
        }

        private void UpdateRegressionSuspendedUnlockRejection()
        {
            if (!loadedRegressionCommandIssued)
            {
                loadedRegressionCommandIssued = true;
                RequestContainerUnlock();
                return;
            }

            if (Time.fixedTime - loadedRegressionStageStartedAt < 0.40f)
            {
                return;
            }

            if (!containerLocked)
            {
                FailLoadedOperationRegression("suspended-unlock-was-not-rejected");
                return;
            }
            SetLoadedRegressionStage(LoadedRegressionStage.TravelOutbound);
        }

        private void UpdateRegressionTrolleyTarget(float targetX, LoadedRegressionStage nextStage)
        {
            var error = targetX - trolleyMotion.localPosition.x;
            if (Mathf.Abs(error) > 0.025f)
            {
                trolleyInput = Mathf.Clamp(error * 1.5f, -1f, 1f);
                ResetLoadedRegressionStableHold();
                return;
            }

            var stable = Mathf.Abs(currentTrolleyVelocity) <= MotionObservationSpeedThresholdMetersPerSecond &&
                         currentRelativeHorizontalSpeed <= 0.08f;
            if (HoldLoadedRegressionStable(stable))
            {
                SetLoadedRegressionStage(nextStage);
            }
        }

        private void UpdateRegressionLowerToLanding()
        {
            if (containerLandingConfirmed)
            {
                SetLoadedRegressionStage(LoadedRegressionStage.AwaitLanding);
                return;
            }

            var horizontalError = new Vector2(
                activeContainer.position.x - containerHomePosition.x,
                activeContainer.position.z - containerHomePosition.z).magnitude;
            if (horizontalError > ContainerHorizontalLockToleranceMeters)
            {
                FailLoadedOperationRegression($"landing-horizontal-error:{horizontalError:F3}m");
                return;
            }

            var verticalSignedError = activeContainer.position.y - containerHomePosition.y;
            if (Mathf.Abs(verticalSignedError) > 0.025f)
            {
                hoistInput = Mathf.Clamp(verticalSignedError * 2f, -1f, 1f);
                ResetLoadedRegressionStableHold();
                return;
            }

            var stable = Mathf.Abs(currentHoistVelocity) <= MotionObservationSpeedThresholdMetersPerSecond &&
                         GetHeadblockPointVelocity().magnitude <= MaximumLockingSpeedMetersPerSecond;
            if (HoldLoadedRegressionStable(stable))
            {
                SetLoadedRegressionStage(LoadedRegressionStage.AwaitLanding);
            }
        }

        private void UpdateRegressionEmptyHoist()
        {
            if (commandedSuspendedDrop - loadedRegressionTargetDrop > 0.025f)
            {
                hoistInput = -0.65f;
                ResetLoadedRegressionStableHold();
                return;
            }

            // The final empty-hoist assertion is about command completion and load separation.
            // The unloaded headblock is still free to carry a small residual rope-mode velocity,
            // so using its point velocity here can keep the regression open after the drum has
            // stopped and the released container has demonstrably remained on the landing surface.
            var stable = Mathf.Abs(currentHoistVelocity) <= MotionObservationSpeedThresholdMetersPerSecond;
            if (!HoldLoadedRegressionStable(stable))
            {
                return;
            }

            var containerDrift = Vector3.Distance(
                activeContainer.position,
                loadedRegressionReleasedContainerPosition);
            var finiteState = IsFinite(headblockBody.position) && IsFinite(headblockBody.linearVelocity) &&
                              IsFinite(headblockBody.angularVelocity);
            if (containerLocked || containerDrift > 0.01f || !finiteState ||
                maximumObservedLoopExtension > MaximumPermittedLoopExtensionMeters ||
                maximumObservedSway > PrototypeMaximumSwayMeters)
            {
                FailLoadedOperationRegression(
                    $"final-check:locked={containerLocked},containerDrift={containerDrift:F4}m," +
                    $"finite={finiteState},loopExtension={maximumObservedLoopExtension:F4}m," +
                    $"sway={maximumObservedSway:F3}m");
                return;
            }

            loadedOperationRegressionActive = false;
            trolleyInput = 0f;
            hoistInput = 0f;
            loadedRegressionStage = LoadedRegressionStage.Complete;
            runtimeStatus = "PASS - loaded operation regression complete";
            Debug.Log(
                $"[YC_LOADED_OPERATION_REGRESSION_PASS] model={ImplementationVersion}, " +
                $"duration={Time.fixedTime - loadedRegressionStartedAt:F2}s, " +
                $"maxLoopExtension={maximumObservedLoopExtension * 1000f:F2}mm, " +
                $"maxSway={maximumObservedSway:F3}m, maxTwist={maximumObservedTwistDegrees:F2}deg, " +
                $"containerDrift={containerDrift * 1000f:F2}mm",
                this);
        }

        private bool HoldLoadedRegressionStable(bool stable)
        {
            if (!stable)
            {
                ResetLoadedRegressionStableHold();
                return false;
            }
            if (loadedRegressionStableSince < 0f)
            {
                loadedRegressionStableSince = Time.fixedTime;
            }
            return Time.fixedTime - loadedRegressionStableSince >= LoadedRegressionStableHoldSeconds;
        }

        private void ResetLoadedRegressionStableHold()
        {
            loadedRegressionStableSince = -1f;
        }

        private void SetLoadedRegressionStage(LoadedRegressionStage stage)
        {
            loadedRegressionStage = stage;
            loadedRegressionStageStartedAt = Time.fixedTime;
            loadedRegressionCommandIssued = false;
            ResetLoadedRegressionStableHold();
            runtimeStatus = $"Loaded regression · {stage}";
            Debug.Log(
                $"[YC_LOADED_OPERATION_REGRESSION_STAGE] stage={stage}, " +
                $"elapsed={Time.fixedTime - loadedRegressionStartedAt:F2}s, " +
                $"x={trolleyMotion.localPosition.x:F3}m, drop={commandedSuspendedDrop:F3}m, " +
                $"locked={containerLocked}",
                this);
        }

        private void FailLoadedOperationRegression(string reason)
        {
            loadedOperationRegressionActive = false;
            trolleyInput = 0f;
            hoistInput = 0f;
            loadedRegressionStage = LoadedRegressionStage.Failed;
            runtimeStatus = $"FAIL - loaded regression · {reason}";
            Debug.LogError(
                $"[YC_LOADED_OPERATION_REGRESSION_FAIL] model={ImplementationVersion}, " +
                $"reason={reason}, duration={Time.fixedTime - loadedRegressionStartedAt:F2}s, " +
                $"x={trolleyMotion.localPosition.x:F3}m, drop={commandedSuspendedDrop:F3}m, " +
                $"locked={containerLocked}, landingConfirmed={containerLandingConfirmed}, " +
                $"maxLoopExtension={maximumObservedLoopExtension * 1000f:F2}mm, " +
                $"maxSway={maximumObservedSway:F3}m",
                this);
        }

        [ContextMenu("Toggle Graybox Visuals")]
        public void ToggleGrayboxVisuals()
        {
            SetGrayboxVisible(!showGraybox);
        }

        public void SetGrayboxVisible(bool visible)
        {
            showGraybox = visible;
            ApplyGrayboxVisibility();
        }

        private void ApplyGrayboxVisibility()
        {
            foreach (var child in GetComponentsInChildren<Transform>(true))
            {
                if (child != transform && child.name == "Graybox_Placeholder")
                {
                    child.gameObject.SetActive(showGraybox);
                }
            }
        }

        private void EnsurePrototypeView()
        {
            if (Camera.main == null)
            {
                var cameraMount = RequireTransform("70_Cameras/LocalCabinCameraMount");
                var cameraObject = new GameObject("MAX Prototype Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(cameraMount, false);
                cameraObject.transform.position = transform.TransformPoint(new Vector3(12f, 7f, -15f));
                cameraObject.transform.LookAt(transform.TransformPoint(new Vector3(0f, 3.8f, 0f)));
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 48f;
                cameraObject.AddComponent<AudioListener>();
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("MAX Prototype Directional Light");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
            }
        }

        private void CreateRuntimeMaterials()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "YC_MAX_RuntimeMaterialProbe";
            var template = probe.GetComponent<MeshRenderer>().sharedMaterial;
            ropeMaterial = new Material(template) { color = new Color(0.08f, 0.10f, 0.14f) };
            runtimeMaterials.Add(ropeMaterial);
            Destroy(probe);
        }

        private const string KeyStatusColorGreen = "3ECF57";
        private const string KeyStatusColorAmber = "F2B233";
        private const string KeyStatusColorRed = "E5484D";
        private const string KeyStatusColorNeutral = "B9C2CC";
        private const string OperationGuideColorCyan = "58C7FF";

        private GUIStyle keyStatusHeaderStyle;
        private GUIStyle keyStatusValueStyle;
        private GUIStyle operationGuideHeaderStyle;
        private GUIStyle operationGuideValueStyle;

        private const float DesiredRuntimeUiScale = 2f;
        private const float MinimumRuntimeUiScale = 1f;

        private void OnGUI()
        {
            if (!showRuntimePanel)
            {
                return;
            }

            var runtimeUiScale = CalculateRuntimeUiScale();
            var previousGuiMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(runtimeUiScale, runtimeUiScale, 1f)) * previousGuiMatrix;

            try
            {
                GUILayout.BeginArea(new Rect(MainPanelX, MainPanelY, MainPanelWidth, MainPanelHeight), GUI.skin.box);
                GUILayout.Label("YC RUNTIME ROOT · 新八绳 MAX 灰盒");
                GUILayout.Space(6f);
                GUILayout.Label($"状态：{runtimeStatus}");
                GUILayout.Label($"拓扑：双卷筒 × 4回路 · 8个三角回路 · 16个Obi可视绳段");
                GUILayout.Label($"绳索：{(loopsReady ? "8/8 Ready" : $"{loops.Count}/8 Loading")} · Obi仅负责视觉");
                GUILayout.Label($"吊具位置：{FormatVector(headblockBody != null ? headblockBody.position : Vector3.zero)} · " +
                                $"小车速度：{currentTrolleyVelocity:F2} m/s · 起升速度：{currentHoistVelocity:F2} m/s");
                GUILayout.Label($"大车位置：{(gantryDrive?.PositionMeters ?? 0f):F2} m · " +
                                $"大车速度：{(gantryDrive?.VelocityMetersPerSecond ?? 0f):F2} m/s · " +
                                $"夹轮器：{TranslateWheelClampState(gantryDrive?.WheelClampState ?? WheelClampState.Unknown)} · " +
                                $"鸣笛：{(hornActive ? "ON" : "OFF")}");
                GUILayout.Label($"吊高：{commandedSuspendedDrop:F2} m · 当前摆移：{currentHorizontalSway:F3} m · " +
                                $"最大摆移：{maximumObservedSway:F3} m");
                GUILayout.Label($"相对小车速度：{currentRelativeHorizontalSpeed:F3} m/s · " +
                                $"扭转：{maximumObservedTwistDegrees:F2}°");
                GUILayout.Label($"重载垂向挠度：{loadedHoistDeflection * 1000f:F1} mm · " +
                                $"挠度速度：{loadedHoistDeflectionVelocity:F3} m/s");
                GUILayout.Label($"回路伸长：{maximumObservedLoopExtension * 1000f:F1} mm · " +
                                $"求解前残差：{maximumRawLoopDrift * 1000f:F1} mm");
                GUILayout.Space(4f);
                GUILayout.Label($"锁销：{twistlockStatus} · 箱重：{containerGrossMassKg / 1000f:F1} t · " +
                                $"悬挂总重：{totalSuspendedMassKg / 1000f:F1} t");
                GUILayout.Label($"平均单绳张力：{averageRopeTensionKn:F1} kN · 物理代理质量：" +
                                $"{(headblockBody != null ? headblockBody.mass : 0f):F2} kg");
                if (containerLocked)
                {
                    GUILayout.Label($"落箱确认：{(containerLandingConfirmed ? "已确认" : "未确认")} · " +
                                    $"水平 {containerLandingHorizontalErrorMeters:F2} m · " +
                                    $"垂直 {containerLandingVerticalErrorMeters:F2} m · " +
                                    $"偏航 {containerLandingYawErrorDegrees:F1}° · " +
                                    $"速度 {containerLandingSpeedMetersPerSecond:F2} m/s");
                }
                else
                {
                    GUILayout.Label($"对位误差：水平 {containerHorizontalErrorMeters:F2} m · " +
                                    $"垂直 {containerVerticalErrorMeters:F2} m · 偏航 {containerYawErrorDegrees:F1}°");
                }
                GUILayout.EndArea();

                DrawKeyOperationStatusPanel(Screen.width / runtimeUiScale);
                DrawOperationGuidePanel(Screen.width / runtimeUiScale);
            }
            finally
            {
                GUI.matrix = previousGuiMatrix;
            }
        }

        // Bounds of the main runtime panel above (Rect(24,24,610,355)), needed to decide whether
        // the key-status panel can sit beside it or must stack below it on narrow screens.
        private const float MainPanelX = 24f;
        private const float MainPanelY = 24f;
        private const float MainPanelWidth = 610f;
        private const float MainPanelHeight = 355f;
        private const float KeyStatusPanelWidth = 250f;
        // Row heights are calibrated from the last Play-verified layout (136 for header + 4
        // status rows, 207 for header + 8 guide rows), which implies a shared box/header
        // overhead of ~48px plus ~22px per status row and ~20px per guide row. Sized for the
        // current header + 12 status rows / header + 18 guide rows (added spreader rotation
        // angle status and its two keys on top of the earlier pump/size and anchor rows)
        // without shrinking either font. 24 + 312 + 12 + 408 + 24 = 780 > 540, so this no
        // longer fits the exact-2x beside layout at 1920x1080 - the fallback branch in
        // CalculateRuntimeUiScale computes a slightly reduced scale instead, which is the
        // intended degrade path, not a bug. Still needs Play confirmation that the resulting
        // scale keeps both panels legible.
        private const float KeyStatusPanelHeight = 312f;
        private const float KeyStatusPanelGap = 24f;
        private const float OperationGuidePanelWidth = 250f;
        private const float OperationGuidePanelHeight = 408f;
        private const float OperationPanelInnerGap = 12f;

        private static float CalculateRuntimeUiScale()
        {
            // Prefer the requested exact 2× scale. Wide screens keep both panels side by side;
            // portrait/narrow screens stack them and reduce only when 2× would leave the canvas.
            var besideRequiredWidth = MainPanelX + MainPanelWidth + KeyStatusPanelGap +
                                      KeyStatusPanelWidth + KeyStatusPanelGap;
            var besideRequiredHeight = Mathf.Max(MainPanelY + MainPanelHeight,
                MainPanelY + KeyStatusPanelHeight + OperationPanelInnerGap + OperationGuidePanelHeight) +
                KeyStatusPanelGap;
            if (Screen.width >= besideRequiredWidth * DesiredRuntimeUiScale &&
                Screen.height >= besideRequiredHeight * DesiredRuntimeUiScale)
            {
                return DesiredRuntimeUiScale;
            }

            var stackedOperationPanelsWidth = KeyStatusPanelWidth + OperationPanelInnerGap +
                                              OperationGuidePanelWidth;
            var stackedRequiredWidth = MainPanelX + Mathf.Max(MainPanelWidth, stackedOperationPanelsWidth) +
                                       KeyStatusPanelGap;
            var stackedRequiredHeight = MainPanelY + MainPanelHeight + 12f +
                                        Mathf.Max(KeyStatusPanelHeight, OperationGuidePanelHeight) +
                                        KeyStatusPanelGap;
            return Mathf.Clamp(Mathf.Min(
                    DesiredRuntimeUiScale,
                    Screen.width / stackedRequiredWidth,
                    Screen.height / stackedRequiredHeight),
                MinimumRuntimeUiScale,
                DesiredRuntimeUiScale);
        }

        /// <summary>
        /// Compact, high-visibility status block for brake/clamp/grab state. Docks beside the
        /// main panel's upper-right when the screen is wide enough for both without overlap;
        /// otherwise stacks below the main panel, still within the upper half of the screen.
        /// </summary>
        private void DrawKeyOperationStatusPanel(float logicalScreenWidth)
        {
            EnsureKeyStatusStyles();

            var canFitBesideMainPanel = logicalScreenWidth >=
                MainPanelX + MainPanelWidth + KeyStatusPanelGap + KeyStatusPanelWidth + KeyStatusPanelGap;
            var rect = canFitBesideMainPanel
                ? new Rect(logicalScreenWidth - KeyStatusPanelWidth - KeyStatusPanelGap, MainPanelY,
                    KeyStatusPanelWidth, KeyStatusPanelHeight)
                : new Rect(MainPanelX, MainPanelY + MainPanelHeight + 12f,
                    KeyStatusPanelWidth, KeyStatusPanelHeight);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("关键操作状态", keyStatusHeaderStyle);
            GUILayout.Label(BuildGantryBrakeStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildWheelClampStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildTrolleyAnchorStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildGantryAnchorStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSpreaderPumpStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSpreaderSizeStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSpreaderRotationStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildContainerGrabStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildSafetyInterlockStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildControlPowerStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildEmergencyStopStatusLine(), keyStatusValueStyle);
            GUILayout.Label(BuildFaultResetStatusLine(), keyStatusValueStyle);
            GUILayout.EndArea();
        }

        /// <summary>
        /// Persistent keyboard/Xbox mapping. It shares the right-hand HUD column on wide screens
        /// and sits beside the key-status card below the main panel on portrait/narrow screens.
        /// </summary>
        private void DrawOperationGuidePanel(float logicalScreenWidth)
        {
            EnsureOperationGuideStyles();

            var canFitBesideMainPanel = logicalScreenWidth >=
                MainPanelX + MainPanelWidth + KeyStatusPanelGap + OperationGuidePanelWidth +
                KeyStatusPanelGap;
            var rect = canFitBesideMainPanel
                ? new Rect(logicalScreenWidth - OperationGuidePanelWidth - KeyStatusPanelGap,
                    MainPanelY + KeyStatusPanelHeight + OperationPanelInnerGap,
                    OperationGuidePanelWidth, OperationGuidePanelHeight)
                : new Rect(MainPanelX + KeyStatusPanelWidth + OperationPanelInnerGap,
                    MainPanelY + MainPanelHeight + 12f,
                    OperationGuidePanelWidth, OperationGuidePanelHeight);

            GUILayout.BeginArea(rect, GUI.skin.box);
            GUILayout.Label("操作指引", operationGuideHeaderStyle);
            GUILayout.Label(BuildOperationGuideLine("大车行走", "J / L", "左摇杆左右"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("小车行走", "A / D", "右摇杆左右"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具升降", "W / S", "左摇杆上下"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("鸣笛", "Space", "LB"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("锁箱", "E", "X"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("解锁", "Q", "B"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("夹轮器", "C", "Y"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("小车锚定", "X", "D-Pad↓"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("大车锚定", "Z", "RB"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具泵", "P", "LT"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具20尺", "1", "D-Pad←"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具40尺", "2", "D-Pad→"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具旋转", "[ / ]", "右摇杆Y"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("吊具回零", "0", "R3"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("暂停", "Esc", "Menu"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("控制电源", "F8", "D-Pad↑"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("急停", "F9", "View"), operationGuideValueStyle);
            GUILayout.Label(BuildOperationGuideLine("故障复位", "R", "A"), operationGuideValueStyle);
            GUILayout.EndArea();
        }

        private void EnsureKeyStatusStyles()
        {
            if (keyStatusHeaderStyle != null)
            {
                return;
            }

            keyStatusHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                richText = true
            };
            keyStatusValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 13,
                richText = true
            };
        }

        private void EnsureOperationGuideStyles()
        {
            if (operationGuideHeaderStyle != null)
            {
                return;
            }

            operationGuideHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                fontSize = 14,
                richText = true
            };
            operationGuideValueStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Normal,
                fontSize = 12,
                richText = true
            };
        }

        private static string BuildOperationGuideLine(string operation, string keyboard, string xbox)
        {
            return $"{operation}  {Colorize($"[{keyboard}]", OperationGuideColorCyan)} · " +
                   $"{Colorize($"[{xbox}]", OperationGuideColorCyan)}";
        }

        private string BuildGantryBrakeStatusLine()
        {
            var brakeReleased = gantryDrive != null && !gantryDrive.IsBrakeApplied;
            var label = brakeReleased ? "已松闸" : "已制动";
            var color = brakeReleased ? KeyStatusColorGreen : KeyStatusColorNeutral;
            return $"大车制动：{Colorize(label, color)}";
        }

        private string BuildWheelClampStatusLine()
        {
            var state = gantryDrive?.WheelClampState ?? WheelClampState.Unknown;
            var color = WheelClampStatusColor(state);
            return $"夹轮器：{Colorize(TranslateWheelClampState(state), color)}";
        }

        /// <summary>
        /// Single source of truth for the Chinese label of each <see cref="WheelClampState"/>,
        /// shared by the main debug panel and the key-status panel so no English enum value is
        /// ever shown to the user.
        /// </summary>
        private static string TranslateWheelClampState(WheelClampState state)
        {
            switch (state)
            {
                case WheelClampState.Released:
                    return "已松开";
                case WheelClampState.Transitioning:
                    return "动作中";
                case WheelClampState.Clamped:
                    return "已夹紧";
                case WheelClampState.Fault:
                    return "故障";
                default:
                    return "状态未知";
            }
        }

        private static string WheelClampStatusColor(WheelClampState state)
        {
            switch (state)
            {
                case WheelClampState.Released:
                    return KeyStatusColorGreen;
                case WheelClampState.Transitioning:
                    return KeyStatusColorAmber;
                case WheelClampState.Clamped:
                    return KeyStatusColorNeutral;
                case WheelClampState.Fault:
                    return KeyStatusColorRed;
                default:
                    return KeyStatusColorRed;
            }
        }

        private string BuildTrolleyAnchorStatusLine()
        {
            var state = trolleyAnchor?.State ?? YardCraneAnchorState.Unknown;
            var color = AnchorStatusColor(state);
            return $"小车锚定：{Colorize(TranslateAnchorState(state), color)}";
        }

        private string BuildGantryAnchorStatusLine()
        {
            var state = gantryAnchor?.State ?? YardCraneAnchorState.Unknown;
            var color = AnchorStatusColor(state);
            return $"大车锚定：{Colorize(TranslateAnchorState(state), color)}";
        }

        /// <summary>
        /// Single source of truth for the Chinese label of each <see cref="YardCraneAnchorState"/>,
        /// mirroring TranslateWheelClampState - no English enum value is ever shown to the user.
        /// </summary>
        private static string TranslateAnchorState(YardCraneAnchorState state)
        {
            switch (state)
            {
                case YardCraneAnchorState.Released:
                    return "已释放";
                case YardCraneAnchorState.Transitioning:
                    return "动作中";
                case YardCraneAnchorState.Anchored:
                    return "已锚定";
                case YardCraneAnchorState.Fault:
                    return "故障";
                default:
                    return "状态未知";
            }
        }

        private static string AnchorStatusColor(YardCraneAnchorState state)
        {
            switch (state)
            {
                case YardCraneAnchorState.Released:
                    return KeyStatusColorGreen;
                case YardCraneAnchorState.Transitioning:
                    return KeyStatusColorAmber;
                case YardCraneAnchorState.Anchored:
                    return KeyStatusColorNeutral;
                case YardCraneAnchorState.Fault:
                    return KeyStatusColorRed;
                default:
                    return KeyStatusColorRed;
            }
        }

        private string BuildSpreaderPumpStatusLine()
        {
            var label = spreaderPumpOn ? "已启动" : "已停止";
            var color = spreaderPumpOn ? KeyStatusColorGreen : KeyStatusColorNeutral;
            return $"吊具泵：{Colorize(label, color)}";
        }

        private string BuildSpreaderSizeStatusLine()
        {
            var mechanismState = spreaderSize?.State ?? YardCraneSpreaderSizeMechanismState.Idle;
            if (mechanismState == YardCraneSpreaderSizeMechanismState.Transitioning)
            {
                return $"吊具尺寸：{Colorize("动作中", KeyStatusColorAmber)}";
            }

            var size = spreaderSize?.CurrentSize ?? YardCraneSpreaderSize.Unknown;
            var color = mechanismState == YardCraneSpreaderSizeMechanismState.Fault
                ? KeyStatusColorRed
                : KeyStatusColorNeutral;
            return $"吊具尺寸：{Colorize(TranslateSpreaderSize(size), color)}";
        }

        /// <summary>
        /// Single source of truth for the Chinese label of each <see cref="YardCraneSpreaderSize"/>,
        /// mirroring TranslateAnchorState - no English enum value is ever shown to the user.
        /// </summary>
        private static string TranslateSpreaderSize(YardCraneSpreaderSize size)
        {
            switch (size)
            {
                case YardCraneSpreaderSize.Ft20:
                    return "20尺";
                case YardCraneSpreaderSize.Ft40:
                    return "40尺";
                case YardCraneSpreaderSize.Twin20:
                    return "双20尺";
                default:
                    return "状态未知";
            }
        }

        private string BuildSpreaderRotationStatusLine()
        {
            var angle = spreaderRotation?.CurrentAngleDegrees ?? 0f;
            if (spreaderRotation?.IsReturningToZero ?? false)
            {
                return $"吊具角度：{Colorize($"回零中 {angle:+0.0;-0.0;0.0}°", KeyStatusColorAmber)}";
            }

            var color = Mathf.Approximately(angle, 0f) ? KeyStatusColorGreen : KeyStatusColorNeutral;
            return $"吊具角度：{Colorize($"{angle:+0.0;-0.0;0.0}°", color)}";
        }

        private string BuildContainerGrabStatusLine()
        {
            // Unlock-in-progress must win over the stable "locked" reading: containerLocked
            // stays true for the whole unlock request/execution window, so checking it first
            // would permanently hide the "解锁中" transition state.
            string label;
            string color;
            if (containerUnlockRequested)
            {
                label = "解锁中";
                color = KeyStatusColorAmber;
            }
            else if (containerLocked)
            {
                label = "已抓箱";
                color = KeyStatusColorGreen;
            }
            else if (containerLockRequested)
            {
                label = "锁箱中";
                color = KeyStatusColorAmber;
            }
            else
            {
                label = "未抓箱";
                color = KeyStatusColorNeutral;
            }

            return $"抓箱：{Colorize(label, color)}";
        }

        private string BuildSafetyInterlockStatusLine()
        {
            if (trolleyProximityInhibited && gantryProximityInhibited)
            {
                return $"安全联锁：{Colorize("大车/小车已限制", KeyStatusColorAmber)}";
            }

            if (trolleyProximityInhibited)
            {
                var label = containerLocked ? "小车低净空禁止" : "小车朝箱侧禁止";
                return $"安全联锁：{Colorize(label, KeyStatusColorAmber)}";
            }

            if (gantryProximityInhibited)
            {
                var label = containerLocked ? "大车低净空禁止" : "大车朝箱侧禁止";
                return $"安全联锁：{Colorize(label, KeyStatusColorAmber)}";
            }

            return $"安全联锁：{Colorize("正常", KeyStatusColorGreen)}";
        }

        private string BuildControlPowerStatusLine()
        {
            if (standaloneOperationHost == null)
            {
                return $"控制电源：{Colorize("未知", KeyStatusColorNeutral)}";
            }

            var on = standaloneOperationHost.IsControlPowerOn;
            var label = on ? "已合" : "已断";
            var color = on ? KeyStatusColorGreen : KeyStatusColorRed;
            return $"控制电源：{Colorize(label, color)}";
        }

        private string BuildEmergencyStopStatusLine()
        {
            if (standaloneOperationHost == null)
            {
                return $"急停：{Colorize("未知", KeyStatusColorNeutral)}";
            }

            var released = standaloneOperationHost.IsEmergencyStopReleased;
            var label = released ? "已释放" : "已按下";
            var color = released ? KeyStatusColorGreen : KeyStatusColorRed;
            return $"急停：{Colorize(label, color)}";
        }

        private string BuildFaultResetStatusLine()
        {
            var awaitingReset = standaloneOperationHost != null
                ? standaloneOperationHost.IsAwaitingFaultReset
                : operationController != null && operationController.Phase == YardCraneOperationPhase.SafeHold;
            var label = awaitingReset ? "等待复位" : "正常";
            var color = awaitingReset ? KeyStatusColorRed : KeyStatusColorGreen;
            return $"安全复位：{Colorize(label, color)}";
        }

        private static string Colorize(string text, string hexColor) => $"<color=#{hexColor}>{text}</color>";

        private void OnDestroy()
        {
            foreach (var loop in loops)
            {
                DestroyVisualLeg(loop.ExitLeg);
                DestroyVisualLeg(loop.DeadEndLeg);
            }
            foreach (var material in runtimeMaterials)
            {
                if (material != null) Destroy(material);
            }
            if (runtimeHornClip != null)
            {
                Destroy(runtimeHornClip);
            }
        }

        private static void DestroyVisualLeg(VisualLegRuntime leg)
        {
            if (leg?.Blueprint != null) Destroy(leg.Blueprint);
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }

        private static float SmootherStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private readonly struct LoopDefinition
        {
            public LoopDefinition(string id, string drum, string antiSwayAxis, string exitPath, string deadEndPath, string sheavePath, string slotPath)
            {
                Id = id;
                Drum = drum;
                AntiSwayAxis = antiSwayAxis;
                ExitPath = exitPath;
                DeadEndPath = deadEndPath;
                SheavePath = sheavePath;
                SlotPath = slotPath;
            }

            public string Id { get; }
            public string Drum { get; }
            public string AntiSwayAxis { get; }
            public string ExitPath { get; }
            public string DeadEndPath { get; }
            public string SheavePath { get; }
            public string SlotPath { get; }
        }

        private sealed class LoopRuntime
        {
            public LoopRuntime(
                LoopDefinition definition,
                Transform exit,
                Transform deadEnd,
                Transform sheave,
                Vector3 localSheaveAnchor,
                VisualLegRuntime exitLeg,
                VisualLegRuntime deadEndLeg,
                float initialLoopLength,
                float initialLegDifference)
            {
                Definition = definition;
                Exit = exit;
                DeadEnd = deadEnd;
                Sheave = sheave;
                LocalSheaveAnchor = localSheaveAnchor;
                ExitLeg = exitLeg;
                DeadEndLeg = deadEndLeg;
                InitialLoopLength = initialLoopLength;
                CommandedLoopLength = initialLoopLength;
                LastCommandedLoopLength = initialLoopLength;
                InitialLegDifference = initialLegDifference;
                CommandedLegDifference = initialLegDifference;
                LastExitPosition = exit.position;
                LastDeadEndPosition = deadEnd.position;
            }

            public LoopDefinition Definition { get; }
            public Transform Exit { get; }
            public Transform DeadEnd { get; }
            public Transform Sheave { get; }
            public Vector3 LocalSheaveAnchor { get; }
            public VisualLegRuntime ExitLeg { get; }
            public VisualLegRuntime DeadEndLeg { get; }
            public float InitialLoopLength { get; set; }
            public float CommandedLoopLength { get; set; }
            public float LastCommandedLoopLength { get; set; }
            public float InitialLegDifference { get; }
            public float CommandedLegDifference { get; set; }
            public Vector3 LastExitPosition { get; set; }
            public Vector3 LastDeadEndPosition { get; set; }
            public Vector3 ExitVelocity { get; set; }
            public Vector3 DeadEndVelocity { get; set; }
            public float LoopLengthRate { get; set; }
        }

        private sealed class VisualLegRuntime
        {
            public VisualLegRuntime(
                GameObject actorObject,
                Obi.ObiRope rope,
                ObiRopeCursor cursor,
                ObiRopeBlueprint blueprint,
                ObiParticleAttachment fixedAttachment,
                ObiParticleAttachment sheaveAttachment,
                float commandedVisualLength)
            {
                ActorObject = actorObject;
                Rope = rope;
                Cursor = cursor;
                Blueprint = blueprint;
                FixedAttachment = fixedAttachment;
                SheaveAttachment = sheaveAttachment;
                CommandedVisualLength = commandedVisualLength;
            }

            public GameObject ActorObject { get; }
            public Obi.ObiRope Rope { get; }
            public ObiRopeCursor Cursor { get; }
            public ObiRopeBlueprint Blueprint { get; }
            public ObiParticleAttachment FixedAttachment { get; }
            public ObiParticleAttachment SheaveAttachment { get; }
            public float CommandedVisualLength { get; set; }
            public bool IsLoaded => Rope.isLoaded && Rope.elements.Count > 0;
            public bool AttachmentsBound => FixedAttachment.isBound && SheaveAttachment.isBound;
        }
    }
}
