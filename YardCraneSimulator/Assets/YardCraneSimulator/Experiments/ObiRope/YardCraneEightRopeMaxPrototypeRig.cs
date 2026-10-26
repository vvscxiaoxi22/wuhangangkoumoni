using System;
using System.Collections.Generic;
using Obi;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Configuration;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Operations;
using PortTraining.YardCraneSimulator.Training;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Qingdao-port yard-crane greybox using eight independent straight ropes.
    /// Every rope connects one trolley-side top anchor to one headblock-side lower anchor.
    /// The engineering distance constraints carry the headblock; Obi renders exactly the same
    /// eight rope lines and never acts as a second suspension model.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    [DisallowMultipleComponent]
    public sealed partial class YardCraneEightRopeMaxPrototypeRig : MonoBehaviour, IYardCraneOperationActuator
    {
        private const string ImplementationVersion = "2026-09-17-eight-straight-rope-v34-yard-container-cycle";
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
        private float LoadTransferPreTensionDurationSeconds => activeProfile.load.loadTransferPreTensionDurationSeconds;
        private float LoadedTrolleyMinimumClearanceMeters => activeProfile.geometryAndSafety.loadedTrolleyMinimumClearanceMeters;

        private float LoopConstraintToleranceMeters => activeProfile.ropePhysics.loopConstraintToleranceMeters;
        private float MaximumPermittedLoopExtensionMeters => activeProfile.acceptance.maximumPermittedLoopExtensionMeters;
        private float StartupPreTensionDurationSeconds => activeProfile.ropePhysics.startupPreTensionDurationSeconds;
        private int PositionConstraintIterations => activeProfile.ropePhysics.positionConstraintIterations;
        private int VelocityConstraintIterations => activeProfile.ropePhysics.velocityConstraintIterations;
        private float MaximumAngularCorrectionDegrees => activeProfile.ropePhysics.maximumAngularCorrectionDegrees;
        private float LoadedHeadblockAngularDamping => activeProfile.ropePhysics.loadedHeadblockAngularDamping;
        private float PrototypeMaximumSwayMeters => activeProfile.acceptance.prototypeMaximumSwayMeters;
        private float PrototypeMaximumResidualTwistDegrees => activeProfile.acceptance.prototypeMaximumResidualTwistDegrees;
        private float LoadedGantrySpeedFactor => activeProfile.training.loadedGantrySpeedFactor;
        private float LoadedTrolleySpeedFactor => activeProfile.training.loadedTrolleySpeedFactor;
        private float TrainingSwayThresholdMeters => activeProfile.training.trainingSwayThresholdMeters;
        private string TopologyProfile => activeProfile.identity.topologyProfileId;
        private int solverSubsteps => activeProfile.ropePhysics.obiSolverSubsteps;
        private int distanceIterations => activeProfile.ropePhysics.obiDistanceIterations;
        private Vector3 containerLockedLocalPosition => activeProfile.load.containerLockedLocalPosition;
        private LoopDefinition[] LoopDefinitions => loopDefinitions;

        [Header("Versioned Configuration")]
        [SerializeField] private YardCraneEightRopeMaxProfile profileAsset;

        [Header("MAX Prototype")]
        [SerializeField] private bool showGraybox = true;
        [SerializeField] private bool runAutomatedDemo = true;
        // internal (not private): read by YardCraneDebugHudPresenter, the temporary IMGUI test
        // display extracted out of this file - see that file's class comment for why these
        // fields are exposed this way instead of a bundled snapshot type.
        [SerializeField] internal bool showRuntimePanel = true;
        [SerializeField] private YardCraneInputProfile standaloneInputProfile;
        // 2026-09-16: opt-in Inspector toggle to test the S7-link-required input path (see
        // YardCraneStandaloneOperationHost.Configure's requireS7Link parameter) without a real
        // PLC - S7Interface can stay disabled on the standalone YardCraneS7Integration player;
        // this only exercises "keyboard/Xbox are excluded" and "no link => refuse to configure".
        // Defaults false so every existing keyboard/Xbox test session is unaffected.
        [SerializeField] private bool requireS7Link;

        [Header("Container Load")]
        [SerializeField, Min(0f)] internal float containerGrossMassKg = 30000f;

        private readonly List<LoopRuntime> loops = new();

        private Transform gantryMotion;
        private Transform trolleyMotion;
        private Transform headblockPhysics;
        private Transform spreaderRotationPivot;
        private Transform spreaderGrayboxVisualRoot;
        private Transform spreaderColliderRoot;
        private Transform spreaderSensorRoot;
        private Transform containerAttachmentRoot;
        private Transform activeContainer;
        private Transform containerFreeBody;
        private Transform containerOriginalParent;
        internal Rigidbody headblockBody;
        private BoxCollider headblockCollider;
        private Rigidbody containerBody;
        private BoxCollider containerCollider;
        private YardCraneContainerImpactSensor containerImpactSensor;
        private YardCraneContainer container;
        private readonly List<YardCraneYardSlot> yardSlots = new();
        private YardCraneYardSlot pickedUpSourceSlot;
        private bool releasedContainerSettlementPending;
        private float releasedContainerStableSince = -1f;
        private Transform twistlockFrontLeft;
        private Transform twistlockFrontRight;
        private Transform twistlockRearLeft;
        private Transform twistlockRearRight;
        // 2026-09-17: which container the raycast sensors currently see under all four
        // twistlock points - null unless every point independently confirms the same instance
        // (see ComputeContainerCornerAlignment). Read by IsContainerAlignedForLock's
        // container-type/spreader-size interlock; not a substitute for containerCornerAlignment.
        private YardCraneContainer detectedPickupContainer;
        private ObiSolver solver;
        private YardCraneEightRopeMaxProfile activeProfile;
        internal YardCraneGantryDriveModel gantryDrive;
        internal YardCraneWheelClampPresenter wheelClampPresenter;
        private YardCraneAnchorInterlock trolleyAnchor;
        private YardCraneAnchorInterlock gantryAnchor;
        internal YardCraneAnchorPresenter trolleyAnchorPresenter;
        internal YardCraneAnchorPresenter gantryAnchorPresenter;
        internal YardCraneSpreaderSizeInterlock spreaderSize;
        internal YardCraneSpreaderSizePresenter spreaderSizePresenter;
        internal YardCraneSpreaderPumpPresenter spreaderPump;
        internal YardCraneSpreaderRotationModel spreaderRotation;
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
        internal float commandedSuspendedDrop;
        internal float currentTrolleyVelocity;
        internal float currentHoistVelocity;
        private float gantryInput;
        private float trolleyInput;
        private float hoistInput;
        internal YardCraneOperationController operationController;
        internal YardCraneStandaloneOperationHost standaloneOperationHost;
        private YardCraneDebugHudPresenter debugHudPresenter;
        private float demoStartedAt;
        private float startupReleaseAt;
        private float loadTransferReleaseAt;
        private float trolleyStopStabilizationReleaseAt;
        internal float maximumObservedSway;
        internal float currentHorizontalSway;
        internal float currentRelativeHorizontalSpeed;
        internal float loadedHoistDeflection;
        internal float loadedHoistDeflectionVelocity;
        private float previousHoistSupportVelocity;
        private float previousHoistInput;
        private float loadedHoistSettleReleaseAt;
        private float loadedHoistSettleDrumStopAt;
        private float loadedHoistSettleStableSince = -1f;
        private float loadedHoistSettlePeakDeflection;
        private float loadedHoistSettlePeakError;
        private float loadedHoistSettlePeakVelocity;
        internal float maximumObservedLoopExtension;
        internal float maximumRawLoopDrift;
        internal float maximumObservedTwistDegrees;
        private float residualTwistDegrees;
        internal float averageRopeTensionKn;
        internal float totalSuspendedMassKg;
        internal float containerHorizontalErrorMeters;
        internal float containerVerticalErrorMeters;
        internal float containerYawErrorDegrees;
        private YardCraneContainerCornerState containerCornerAlignment;
        internal float containerLandingHorizontalErrorMeters;
        internal float containerLandingVerticalErrorMeters;
        internal float containerLandingYawErrorDegrees;
        internal float containerLandingSpeedMetersPerSecond;
        private float containerLandingStableSince = -1f;
        private Vector3 containerHomePosition;
        private Quaternion containerHomeRotation;
        private Vector3 nominalSuspensionBodyHorizontalOffset;
        private Quaternion nominalSuspensionBodyRotation;
        private Vector3 unloadedSuspensionEquilibriumOffset;
        private Vector3 loadedSuspensionEquilibriumOffset;
        private Vector3 currentRawHorizontalOffset;
        private Vector3 currentSwayError;
        private float trolleyMotionPeakSway;
        private int resetEpoch;
        private Vector3 unloadedInertiaTensor;
        private Quaternion unloadedInertiaTensorRotation;
        private Vector3 sceneAuthoredHeadblockCenterOfMass;
        private float sceneAuthoredHeadblockMassKg;
        private float sceneAuthoredHeadblockAngularDamping;
        private float containerRestingCenterY;
        private string peakLoopId = "-";
        internal string twistlockStatus = "Unlocked";
        private bool suspensionGravityActive;
        internal bool containerLocked;
        internal bool containerLockRequested;
        internal bool containerUnlockRequested;
        private bool loadTransferPreTensioning;
        private bool containerColliderSuppressedForPickup;
        private bool containerHeadblockCollisionIgnored;
        private bool loadedHoistStopPending;
        private bool loadedHoistSettleObservationActive;
        private bool loadedHoistSettleDrumStopped;
        internal bool containerLandingConfirmed;
        internal bool spreaderContactConfirmed;
        internal bool trolleyBrakeApplied = true;
        private readonly YardCraneTrainingScoreTracker trainingScore = new();
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
        internal bool gantryProximityInhibited;
        internal bool trolleyProximityInhibited;
        private bool hoistMotionReported;
        private bool hoistLimitReported;
        private bool loadedOperationRegressionActive;
        internal bool loopsReady;
        private bool startupPreTensioning;
        private bool demoCompleted;
        private bool setupFailed;
        internal bool hornActive;
        private AudioSource hornSource;
        private AudioClip runtimeHornClip;
        internal string runtimeStatus = "Initializing eight-rope MAX prototype";

        public bool ShowGraybox => showGraybox;
        public bool LoopsReady => loopsReady;
        // internal (not public): loops itself stays private (its element type LoopRuntime is a
        // private nested type), but YardCraneDebugHudPresenter only ever needs the count.
        internal int LoopCount => loops.Count;
        public string RuntimeStatus => runtimeStatus;
        public bool ContainerLocked => containerLocked;
        public float ContainerGrossMassKg => containerGrossMassKg;
        // Optional: null on scenes that have not yet added a YardCraneContainer data component
        // to ActiveContainer. Locked/unlocked state stays owned by the rig (containerLocked
        // above), not by this reference, since the spreader can only hold one container at a
        // time and a single source of truth avoids the two states drifting apart.
        public YardCraneContainer AttachedContainer => container;
        public float AverageRopeTensionKn => averageRopeTensionKn;
        public bool ContainerLandingConfirmed => containerLandingConfirmed;
        // Live pre-lock: which of the four corners is currently within tolerance. Snaps to
        // all-true once containerLocked, since TryLockContainer now refuses to lock unless every
        // corner was aligned at that instant - see ComputeContainerCornerAlignment.
        public YardCraneContainerCornerState ContainerCornerAlignment => containerCornerAlignment;
        public bool SpreaderContactConfirmed => spreaderContactConfirmed;
        public bool LoadedOperationRegressionActive => loadedOperationRegressionActive;
        public YardCraneTrainingScoreTracker TrainingScore => trainingScore;
        public YardCraneOperationController OperationController => operationController;
        public YardCraneOperationPhase OperationPhase => operationController?.Phase ?? YardCraneOperationPhase.WaitingForRun;
        public float GantryPositionMeters => gantryDrive?.PositionMeters ?? 0f;
        public float GantrySpeedMetersPerSecondActual => gantryDrive?.VelocityMetersPerSecond ?? 0f;
        public bool GantryBrakeKnown => gantryDrive != null;
        public bool GantryBrakeApplied => gantryDrive?.IsBrakeApplied ?? false;
        public bool TrolleyBrakeKnown => loopsReady && trolleyMotion != null;
        public bool TrolleyBrakeApplied => trolleyBrakeApplied;
        public WheelClampState CurrentWheelClampState => wheelClampPresenter?.State ?? WheelClampState.Unknown;
        public bool HornActive => hornActive;
        public YardCraneAnchorState CurrentTrolleyAnchorState =>
            trolleyAnchorPresenter?.State ?? YardCraneAnchorState.Unknown;
        public YardCraneAnchorState CurrentGantryAnchorState =>
            gantryAnchorPresenter?.State ?? YardCraneAnchorState.Unknown;
        public bool SpreaderPumpOn => spreaderPump?.IsOn ?? false;
        public YardCraneSpreaderSize CurrentSpreaderSize => spreaderSizePresenter?.CurrentSize ?? YardCraneSpreaderSize.Unknown;
        public YardCraneSpreaderSizeMechanismState CurrentSpreaderSizeState =>
            spreaderSizePresenter?.State ?? YardCraneSpreaderSizeMechanismState.Idle;
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
        bool IYardCraneOperationActuator.IsWheelClampClamped => wheelClampPresenter != null && wheelClampPresenter.IsClamped;
        bool IYardCraneOperationActuator.IsWheelClampTransitioning =>
            wheelClampPresenter != null && wheelClampPresenter.IsTransitioning;
        bool IYardCraneOperationActuator.IsTrolleyAnchorApplied =>
            trolleyAnchorPresenter != null && trolleyAnchorPresenter.IsApplied;
        bool IYardCraneOperationActuator.IsTrolleyAnchorTransitioning =>
            trolleyAnchorPresenter != null && trolleyAnchorPresenter.IsTransitioning;
        bool IYardCraneOperationActuator.IsGantryAnchorApplied =>
            gantryAnchorPresenter != null && gantryAnchorPresenter.IsApplied;
        bool IYardCraneOperationActuator.IsGantryAnchorTransitioning =>
            gantryAnchorPresenter != null && gantryAnchorPresenter.IsTransitioning;
        bool IYardCraneOperationActuator.IsSpreaderPumpOn => spreaderPump?.IsOn ?? false;
        bool IYardCraneOperationActuator.IsSpreaderSizeTransitioning =>
            spreaderSizePresenter != null && spreaderSizePresenter.IsTransitioning;

        public bool TrySetContainerGrossMass(float grossMassKg)
        {
            if (activeProfile == null || containerLocked || grossMassKg < MinimumContainerGrossMassKg ||
                grossMassKg > MaximumContainerGrossMassKg)
            {
                return false;
            }

            containerGrossMassKg = grossMassKg;
            if (containerBody != null)
            {
                containerBody.mass = grossMassKg;
            }
            container?.SetGrossMassKg(grossMassKg);
            UpdateContainerLoadFeedback();
            return true;
        }

        public bool RequestContainerLock()
        {
            if (!loopsReady || containerLocked || containerLockRequested || releasedContainerSettlementPending)
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
                releasedContainerSettlementPending ||
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

            if (!containerLocked)
            {
                rejectionCode = YardCraneOperationErrorCodes.UnlockInterlockRejected;
                return false;
            }

            // Real yard cranes gate the unlock output on a load-cell "landed" signal AND the
            // mechanism being stationary, so an operator physically cannot unlock in mid-air
            // (see PortTraining/LM-Wiki 详细设计与接口契约.md). This shell deliberately does
            // NOT hard-block that anymore: the point of this training scenario is to let a
            // student attempt an unsafe unlock and be scored for it, not be prevented the way
            // real hardware prevents it. Record it as a severe safety violation instead.
            var isLanded = containerLandingConfirmed;
            var isStationary = IsMechanismStationary();
            if (!isLanded || !isStationary)
            {
                trainingScore.RecordViolation(
                    YardCraneViolationCategory.Safety, YardCraneViolationType.UnsafeUnlock,
                    activeProfile.training.unsafeUnlockPenaltyPoints, Time.time,
                    $"landed={isLanded}, stationary={isStationary}");
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
            if (wheelClampPresenter == null)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            return wheelClampPresenter.TryQueue(clampApplied, !setupFailed && loopsReady, out rejectionCode);
        }

        bool IYardCraneOperationActuator.TryQueueTrolleyAnchor(bool anchorApplied, out string rejectionCode)
        {
            if (trolleyAnchorPresenter == null)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            var stationary = Mathf.Abs(currentTrolleyVelocity) <= ResetSafeSpeedThresholdMetersPerSecond;
            return trolleyAnchorPresenter.TryQueue(
                anchorApplied, stationary, !setupFailed && loopsReady, currentTrolleyVelocity, out rejectionCode);
        }

        bool IYardCraneOperationActuator.TryQueueGantryAnchor(bool anchorApplied, out string rejectionCode)
        {
            if (gantryAnchorPresenter == null || gantryDrive == null)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            return gantryAnchorPresenter.TryQueue(
                anchorApplied,
                gantryDrive.IsStationary,
                !setupFailed && loopsReady,
                gantryDrive.VelocityMetersPerSecond,
                out rejectionCode);
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderPump(bool pumpOn, out string rejectionCode)
        {
            if (spreaderPump == null)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            return spreaderPump.TryQueue(pumpOn, !setupFailed && loopsReady, out rejectionCode);
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderSize(string targetSize, out string rejectionCode)
        {
            if (spreaderSizePresenter == null)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            return spreaderSizePresenter.TryQueue(
                targetSize,
                spreaderPump?.IsOn ?? false,
                IsMechanismStationary(),
                containerLocked,
                !setupFailed && loopsReady,
                out rejectionCode);
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderRotationReturnToZero(out string rejectionCode)
        {
            if (spreaderRotation == null || setupFailed || !loopsReady)
            {
                rejectionCode = YardCraneOperationErrorCodes.MechanismNotReady;
                return false;
            }

            if (!spreaderRotation.TryRequestReturnToZero(spreaderPump?.IsOn ?? false, out var driveError))
            {
                rejectionCode = YardCraneOperationErrorCodes.SpreaderRotationInterlockRejected;
                Debug.LogWarning(
                    $"[YC_SPREADER_ROTATION_RETURN_TO_ZERO_REJECTED] angle={spreaderRotation.CurrentAngleDegrees:F2}deg, " +
                    $"pumpOn={spreaderPump?.IsOn ?? false}, reason={driveError}",
                    this);
                return false;
            }

            rejectionCode = YardCraneOperationErrorCodes.None;
            Debug.Log(
                $"[YC_SPREADER_ROTATION_RETURN_TO_ZERO_REQUESTED] angle={spreaderRotation.CurrentAngleDegrees:F2}deg",
                this);
            return true;
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
            // Real spreaders cannot resize while sizing gear is mid-travel; a lock attempted
            // during that window would be reaching for geometry that is still moving under it.
            if (spreaderSize != null && spreaderSize.State == YardCraneSpreaderSizeMechanismState.Transitioning)
            {
                return false;
            }

            // All four corners, not the old center+yaw aggregate - see
            // ComputeContainerCornerAlignment for why this is intentionally stricter.
            if (!containerCornerAlignment.AllAligned)
            {
                return false;
            }

            return IsDetectedContainerTypeCompatibleWithSpreaderSize();
        }

        // 2026-09-17: explicit, named rejection reason for a container/spreader size mismatch,
        // even though ApplyTwistlockPositionsForSpreaderSize already makes a mismatched pickup
        // geometrically unlikely to pass the corner sensors in the first place - per user
        // decision, this must not rely purely on "the geometry happened to miss".
        private bool IsDetectedContainerTypeCompatibleWithSpreaderSize()
        {
            if (detectedPickupContainer == null)
            {
                return false;
            }

            var expectedContainerType = CurrentSpreaderSize == YardCraneSpreaderSize.Ft20
                ? YardCraneContainerType.Ft20
                : YardCraneContainerType.Ft40;
            if (detectedPickupContainer.ContainerType != expectedContainerType)
            {
                Debug.LogWarning(
                    $"[YC_CONTAINER_TYPE_SPREADER_SIZE_MISMATCH] container={detectedPickupContainer.ContainerType}, " +
                    $"spreaderSize={CurrentSpreaderSize}",
                    this);
                return false;
            }

            return true;
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

            var freeContainerStationary = containerBody == null || containerBody.isKinematic ||
                                          (containerBody.linearVelocity.magnitude <= ResetSafeSpeedThresholdMetersPerSecond &&
                                           containerBody.angularVelocity.magnitude <= ResetSafeSpeedThresholdMetersPerSecond);
            return Mathf.Abs(currentTrolleyVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                   (gantryDrive == null || gantryDrive.IsStationary) &&
                   (gantryDrive == null || gantryDrive.WheelClampState != WheelClampState.Transitioning) &&
                   Mathf.Abs(currentHoistVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                   headblockBody.linearVelocity.magnitude <= ResetSafeSpeedThresholdMetersPerSecond &&
                   headblockBody.angularVelocity.magnitude <= ResetSafeSpeedThresholdMetersPerSecond &&
                   freeContainerStationary;
        }

        // Speed limits step down when a container is locked on, mirroring how a real
        // operator is expected to run slower under load. Only hoist currently has a
        // physics-enforced loaded-speed factor (FullyLoadedHoistSpeedFactor); gantry and
        // trolley are not physically slowed when loaded, so their loaded limits here are
        // purely a training/scoring threshold, not a physics change.
        private void UpdateTrainingScoreChecks()
        {
            var loaded = containerLocked;
            var now = Time.time;

            var gantrySpeed = gantryDrive != null ? Mathf.Abs(gantryDrive.VelocityMetersPerSecond) : 0f;
            var gantryLimit = loaded ? GantrySpeedMetersPerSecond * LoadedGantrySpeedFactor : GantrySpeedMetersPerSecond;
            trainingScore.UpdateGantrySpeed(gantrySpeed > gantryLimit, activeProfile.training.overspeedPenaltyPoints, now,
                $"loaded={loaded}, gantrySpeed={gantrySpeed:F3}m/s, limit={gantryLimit:F3}m/s");

            var trolleySpeed = Mathf.Abs(currentTrolleyVelocity);
            var trolleyLimit = loaded ? TrolleySpeedMetersPerSecond * LoadedTrolleySpeedFactor : TrolleySpeedMetersPerSecond;
            trainingScore.UpdateTrolleySpeed(trolleySpeed > trolleyLimit, activeProfile.training.overspeedPenaltyPoints, now,
                $"loaded={loaded}, trolleySpeed={trolleySpeed:F3}m/s, limit={trolleyLimit:F3}m/s");

            var hoistSpeed = Mathf.Abs(currentHoistVelocity);
            var hoistLimit = loaded ? HoistSpeedMetersPerSecond * FullyLoadedHoistSpeedFactor : HoistSpeedMetersPerSecond;
            trainingScore.UpdateHoistSpeed(hoistSpeed > hoistLimit, activeProfile.training.overspeedPenaltyPoints, now,
                $"loaded={loaded}, hoistSpeed={hoistSpeed:F3}m/s, limit={hoistLimit:F3}m/s");

            trainingScore.UpdateSway(currentHorizontalSway > TrainingSwayThresholdMeters,
                activeProfile.training.excessiveSwayPenaltyPoints, now,
                $"sway={currentHorizontalSway:F3}m, limit={TrainingSwayThresholdMeters:F3}m");
        }

        private void InitializeOperationController()
        {
            operationController = new YardCraneOperationController(this);
            operationController.PhaseChanged += OnOperationPhaseChanged;
            operationController.InputBecameStale += OnOperationInputBecameStale;

            var bootstrap = YardCraneBootstrap.Active;
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
            standaloneOperationHost.Configure(
                operationController,
                stationId,
                standaloneInputProfile,
                requireS7Link);
        }

        // Runs regardless of Standalone/Managed launch mode - the debug HUD is a read-only
        // overlay unrelated to input authority, temporary until UI Toolkit replaces it (see
        // YardCraneDebugHudPresenter's class comment).
        private void InitializeDebugHud()
        {
            debugHudPresenter = GetComponent<YardCraneDebugHudPresenter>();
            if (debugHudPresenter == null)
            {
                debugHudPresenter = gameObject.AddComponent<YardCraneDebugHudPresenter>();
            }

            debugHudPresenter.Initialize(this);
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
                ConfigureGantryDrive();
                ConfigureHeadblockPhysics();
                InitializePhysicalSpreaderRotation();
                ConfigureContainerLoad();
                RefreshYardSlots();
                ConfigureHorn();
                BindSceneAuthoredSolver();
                BindEightSceneAuthoredStraightRopes();
                EnsurePrototypeView();

                gantryHome = gantryMotion.localPosition;
                trolleyHome = trolleyMotion.localPosition;
                headblockHome = headblockPhysics.position;
                headblockHomeRotation = headblockPhysics.rotation;
                unloadedSuspensionEquilibriumOffset = Vector3.ProjectOnPlane(
                    GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                    Vector3.up);
                CaptureNominalSuspensionPose();
                initialSuspendedDrop = CalculateSuspendedDrop();
                commandedSuspendedDrop = initialSuspendedDrop;
                // When the optional startup demo is disabled there is no demo phase to
                // complete. Mark it complete immediately so developer regressions and
                // manual verification are not permanently rejected by a stale flag.
                demoCompleted = !runAutomatedDemo;
                InitializeOperationController();
                InitializeDebugHud();
                runtimeStatus = "Waiting for 8 Obi visual ropes";
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
                    binding.topAnchorPath,
                    binding.sheavePath,
                    binding.slotPath);
            }

            Debug.Log(
                $"[YC_PHYSICS_PROFILE_LOADED] schema={activeProfile.identity.schemaVersion}, " +
                $"profile={activeProfile.identity.profileId}, topology={TopologyProfile}, " +
                $"source={activeProfileSource}, ropes={loopDefinitions.Length}, " +
                $"calibration={activeProfile.identity.calibrationStatus}",
                this);
        }

        private void Update()
        {
            // 2026-09-11: pushed unconditionally, before every early-return below, so the
            // Training layer's static holder always has this frame's score by the time Unity
            // might destroy this component - mirrors YardCraneLocalTrainingModeCoordinator's
            // identical ElapsedSeconds push (see YardCraneTrainingSessionIdentity.CurrentScore
            // for why a push, not a pull through a live component reference, is required, and
            // why this keeps Training from having to reference this Experiments-layer type).
            YardCraneTrainingSessionIdentity.RecordScore(trainingScore.TotalScore);

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
            UpdateContainerHeadblockCollisionSuppression();
            UpdateReleasedContainerSettlement();
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
            // gravity enabled would apply it later inside PhysX, after our straight-rope
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
            ApplyTrolleyAntiSwayDynamics();
            ApplyLoadedTrolleyStopStabilization();
            CaptureBoundaryMotion();
            SolveEightRopeConstraints();
            SynchronizeVisualRopeLengths();
            ProjectVisualRopesToEngineeringGeometry();
            UpdateContainerLoadFeedback();
            UpdateContainerLandingConfirmation();
            UpdateContainerContactStatus();
            RestoreContainerColliderWhenClear();
            UpdateTrainingScoreChecks();
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
            // every taut MAX rope follow the current engineering anchors exactly.
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
            twistlockFrontLeft = RequireTransform("30_SpreaderPhysics/ContainerAttachment/Twistlock_FL");
            twistlockFrontRight = RequireTransform("30_SpreaderPhysics/ContainerAttachment/Twistlock_FR");
            twistlockRearLeft = RequireTransform("30_SpreaderPhysics/ContainerAttachment/Twistlock_RL");
            twistlockRearRight = RequireTransform("30_SpreaderPhysics/ContainerAttachment/Twistlock_RR");
            activeContainer = RequireTransform("50_ContainerWorkspace/ActiveContainer");
            containerFreeBody = RequireTransform(YardCraneSceneSimulationContract.ContainerFreeBodyPath);

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
                RequireTransform(definition.TopAnchorPath);
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
            wheelClampPresenter = new YardCraneWheelClampPresenter(gantryDrive, this);
            trolleyAnchor = new YardCraneAnchorInterlock(AnchorTransitionSeconds);
            gantryAnchor = new YardCraneAnchorInterlock(AnchorTransitionSeconds);
            trolleyAnchorPresenter = new YardCraneAnchorPresenter(trolleyAnchor, "小车锚定", "TROLLEY", this);
            gantryAnchorPresenter = new YardCraneAnchorPresenter(gantryAnchor, "大车锚定", "GANTRY", this);
            spreaderSize = new YardCraneSpreaderSizeInterlock(SpreaderSizeTransitionSeconds, YardCraneSpreaderSize.Ft40);
            spreaderSizePresenter = new YardCraneSpreaderSizePresenter(spreaderSize, this);
            spreaderPump = new YardCraneSpreaderPumpPresenter(this);
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
                throw MissingSceneComponent<Rigidbody>(YardCraneSceneSimulationContract.HeadblockPath);
            }

            if (!float.IsFinite(headblockBody.mass) || headblockBody.mass <= 0f ||
                !float.IsFinite(headblockBody.linearDamping) || headblockBody.linearDamping < 0f ||
                !float.IsFinite(headblockBody.angularDamping) || headblockBody.angularDamping < 0f ||
                headblockBody.solverIterations < 1 || headblockBody.solverVelocityIterations < 1 ||
                headblockBody.isKinematic || headblockBody.useGravity)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {YardCraneSceneSimulationContract.HeadblockPath} has invalid " +
                    "Rigidbody settings. Use a positive mass, non-negative damping/iterations, Dynamic mode, " +
                    "and disable Use Gravity (the rope solver applies gravity explicitly).");
            }

            // Runtime load transfer may temporarily change mass/COM/inertia, but the unloaded
            // baseline comes from the authored Rigidbody so Inspector edits remain authoritative.
            sceneAuthoredHeadblockMassKg = headblockBody.mass;
            sceneAuthoredHeadblockAngularDamping = headblockBody.angularDamping;

            if (headblockPhysics.GetComponent<ObiRigidbody>() == null)
            {
                throw MissingSceneComponent<ObiRigidbody>(YardCraneSceneSimulationContract.HeadblockPath);
            }

            var colliderTransform = RequireTransform(YardCraneSceneSimulationContract.HeadblockColliderPath);
            headblockCollider = colliderTransform.GetComponent<BoxCollider>();
            if (headblockCollider == null)
            {
                throw MissingSceneComponent<BoxCollider>(YardCraneSceneSimulationContract.HeadblockColliderPath);
            }
            sceneAuthoredHeadblockCenterOfMass = headblockBody.centerOfMass;
            unloadedInertiaTensor = headblockBody.inertiaTensor;
            unloadedInertiaTensorRotation = headblockBody.inertiaTensorRotation;
        }

        private void ConfigureContainerLoad()
        {
            containerOriginalParent = activeContainer.parent;
            containerHomePosition = activeContainer.position;
            containerHomeRotation = activeContainer.rotation;
            containerRestingCenterY = activeContainer.position.y;

            containerBody = containerFreeBody.GetComponent<Rigidbody>();
            if (containerBody == null)
            {
                throw MissingSceneComponent<Rigidbody>(YardCraneSceneSimulationContract.ContainerFreeBodyPath);
            }

            containerCollider = activeContainer.GetComponent<BoxCollider>();
            if (containerCollider == null)
            {
                throw MissingSceneComponent<BoxCollider>(YardCraneSceneSimulationContract.ActiveContainerPath);
            }

            containerImpactSensor = containerFreeBody.GetComponent<YardCraneContainerImpactSensor>();
            if (containerImpactSensor == null)
            {
                throw MissingSceneComponent<YardCraneContainerImpactSensor>(
                    YardCraneSceneSimulationContract.ContainerFreeBodyPath);
            }

            // Optional: a YardCraneContainer data component on ActiveContainer gives the box its
            // own id/type/tare-mass identity and becomes the authoritative gross mass source
            // (synced onto containerBody.mass below). It is absent from today's saved scene, so
            // this stays backward compatible - Rigidbody.mass keeps being authoritative until a
            // YardCraneContainer is added there.
            container = activeContainer.GetComponent<YardCraneContainer>();
            if (container != null)
            {
                if (!container.TryValidate(activeProfile, out var containerError))
                {
                    throw new InvalidOperationException(
                        $"[YC_SCENE_AUTHORING_INVALID] {YardCraneSceneSimulationContract.ActiveContainerPath} " +
                        $"YardCraneContainer is invalid: {containerError}");
                }

                containerBody.mass = container.GrossMassKg;
                container.EnsureLockHoleColliders();
            }
            else
            {
                // 2026-09-17: the raycast-based lock sensors (ComputeContainerCornerAlignment)
                // can only ever detect a YardCraneContainer's own lock-hole colliders - there is
                // no equivalent fallback for a bare graybox with no such component, unlike
                // GrossMassKg's fallback to containerBody.mass above. Locking will not succeed
                // (containerCornerAlignment will never read as aligned) until one is added to
                // ActiveContainer in the Editor. This is a genuine behavior change from this
                // component's original optional/backward-compatible design, not an oversight.
                Debug.LogWarning(
                    $"[YC_CONTAINER_LOCK_HOLES_MISSING] {YardCraneSceneSimulationContract.ActiveContainerPath} " +
                    "has no YardCraneContainer component, so it has no lock-hole colliders for the " +
                    "twistlock raycast sensors to find. Locking will always be rejected until one is added.",
                    this);
            }

            if (activeContainer.GetComponentInChildren<Rigidbody>(true) != null)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {YardCraneSceneSimulationContract.ActiveContainerPath} " +
                    "must not contain a Rigidbody. Its collider becomes part of the headblock compound body while locked.");
            }
            if (containerFreeBody.GetComponents<Collider>().Length != 0 || containerFreeBody.childCount != 0)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {YardCraneSceneSimulationContract.ContainerFreeBodyPath} " +
                    "must be an empty Rigidbody proxy in the saved scene. Keep ActiveContainer as its sibling.");
            }

            if (containerBody.mass < MinimumContainerGrossMassKg ||
                containerBody.mass > MaximumContainerGrossMassKg)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {YardCraneSceneSimulationContract.ContainerFreeBodyPath} " +
                    $"Rigidbody.mass={containerBody.mass:F1}kg is outside the supported " +
                    $"[{MinimumContainerGrossMassKg:F1}, {MaximumContainerGrossMassKg:F1}]kg range.");
            }

            // The scene-authored free-body proxy owns the authoritative container mass, unless a
            // YardCraneContainer data component on ActiveContainer overrides it above. Runtime
            // only switches state on either; it never replaces or recreates the components.
            containerGrossMassKg = containerBody.mass;
            containerImpactSensor.ImpactDetected -= HandleContainerImpact;
            containerImpactSensor.ImpactDetected += HandleContainerImpact;
            ParkContainerFreeBody();
            SetContainerHeadblockCollisionIgnored(false);
            containerCollider.enabled = true;

            containerLocked = false;
            twistlockStatus = "Unlocked";
            ApplySuspendedLoadProperties();
            UpdateContainerLoadFeedback();
        }

        private void RefreshYardSlots()
        {
            yardSlots.Clear();
            foreach (var slot in FindObjectsByType<YardCraneYardSlot>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (slot != null && slot.gameObject.scene == gameObject.scene)
                {
                    yardSlots.Add(slot);
                }
            }

            yardSlots.Sort((left, right) => string.CompareOrdinal(left.SlotId, right.SlotId));
            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            var initialOccupants = new HashSet<YardCraneContainer>();
            foreach (var slot in yardSlots)
            {
                if (string.IsNullOrWhiteSpace(slot.SlotId) || !slotIds.Add(slot.SlotId))
                {
                    Debug.LogError(
                        $"[YC_YARD_SLOT_ID_INVALID] slot={slot.name}, id='{slot.SlotId}', " +
                        "slot ids must be non-empty and unique inside the YardCrane scene.",
                        slot);
                }

                if (slot.InitialOccupant != null && !initialOccupants.Add(slot.InitialOccupant))
                {
                    Debug.LogError(
                        $"[YC_YARD_SLOT_DUPLICATE_INITIAL_OCCUPANT] slot={slot.SlotId}, " +
                        $"container={slot.InitialOccupant.ContainerId}",
                        slot);
                }
            }

            Debug.Log($"[YC_YARD_SLOTS_READY] count={yardSlots.Count}", this);
        }

        private bool TryBindDetectedContainerForPickup(out string rejectionReason)
        {
            var candidate = detectedPickupContainer;
            if (candidate == null)
            {
                rejectionReason = "no detected pickup container";
                return false;
            }

            if (releasedContainerSettlementPending)
            {
                rejectionReason = "the previous released container is still settling";
                return false;
            }

            if (!candidate.TryValidate(activeProfile, out var validationError))
            {
                rejectionReason = validationError;
                return false;
            }

            var candidateTransform = candidate.transform;
            var candidateCollider = candidate.GetComponent<BoxCollider>();
            if (candidateCollider == null || !candidateCollider.enabled || candidateCollider.isTrigger)
            {
                rejectionReason = "the detected container requires one enabled, non-trigger BoxCollider on its semantic root";
                return false;
            }

            if (candidateTransform.GetComponentInChildren<Rigidbody>(true) != null)
            {
                rejectionReason = "the detected static yard container must not own a Rigidbody";
                return false;
            }

            if (containerFreeBody.childCount != 0 && candidateTransform.parent != containerFreeBody)
            {
                rejectionReason = "the shared ContainerFreeBody is still occupied by another container";
                return false;
            }

            if (containerHeadblockCollisionIgnored && containerCollider != null)
            {
                SetContainerHeadblockCollisionIgnored(false);
            }

            activeContainer = candidateTransform;
            container = candidate;
            containerCollider = candidateCollider;
            containerOriginalParent = candidateTransform.parent;
            containerHomePosition = candidateTransform.position;
            containerHomeRotation = candidateTransform.rotation;
            containerRestingCenterY = candidateTransform.position.y;
            containerGrossMassKg = candidate.GrossMassKg;
            containerBody.mass = containerGrossMassKg;
            container.EnsureLockHoleColliders();
            containerImpactSensor.Disarm();
            pickedUpSourceSlot = FindYardSlotContaining(candidate);
            rejectionReason = null;

            Debug.Log(
                $"[YC_CONTAINER_BOUND] container={candidate.ContainerId}, type={candidate.ContainerType}, " +
                $"grossMass={candidate.GrossMassKg:F0}kg, " +
                $"sourceSlot={(pickedUpSourceSlot != null ? pickedUpSourceSlot.SlotId : "<unassigned>")}",
                candidate);
            return true;
        }

        private YardCraneYardSlot FindYardSlotContaining(YardCraneContainer occupant)
        {
            foreach (var slot in yardSlots)
            {
                if (slot != null && slot.CurrentOccupant == occupant)
                {
                    return slot;
                }
            }

            return null;
        }

        private YardCraneYardSlot FindBestAvailableYardSlot(
            YardCraneContainer candidate,
            out float horizontalError,
            out float verticalError,
            out float yawError)
        {
            var bestSlot = default(YardCraneYardSlot);
            var bestScore = float.PositiveInfinity;
            horizontalError = float.PositiveInfinity;
            verticalError = float.PositiveInfinity;
            yawError = float.PositiveInfinity;

            if (candidate == null)
            {
                return null;
            }

            foreach (var slot in yardSlots)
            {
                if (slot == null || !slot.CanAccept(candidate))
                {
                    continue;
                }

                var difference = candidate.transform.position - slot.transform.position;
                var slotHorizontalError = new Vector2(difference.x, difference.z).magnitude;
                var slotVerticalError = Mathf.Abs(difference.y);
                // Occupancy is stricter than the spreader's yaw-only lock interlock: a box that
                // has tipped onto its side must never be recorded as a healthy occupied slot just
                // because its centre and heading still happen to be close to the marker.
                var slotYawError = Quaternion.Angle(
                    slot.transform.rotation,
                    candidate.transform.rotation);
                if (slotHorizontalError > ContainerHorizontalLockToleranceMeters ||
                    slotVerticalError > ContainerVerticalLockToleranceMeters ||
                    slotYawError > ContainerYawLockToleranceDegrees)
                {
                    continue;
                }

                var score = slotHorizontalError + slotVerticalError + Mathf.Deg2Rad * slotYawError;
                if (score >= bestScore)
                {
                    continue;
                }

                bestSlot = slot;
                bestScore = score;
                horizontalError = slotHorizontalError;
                verticalError = slotVerticalError;
                yawError = slotYawError;
            }

            return bestSlot;
        }

        private InvalidOperationException MissingSceneComponent<T>(string path) where T : Component
        {
            return new InvalidOperationException(
                $"[YC_SCENE_AUTHORING_MISSING] {path} requires {typeof(T).Name}. " +
                $"Run '{YardCraneSceneSimulationContract.AuthoringMenuPath}' in Edit Mode, inspect the result, then save the scene.");
        }

        private void ParkContainerFreeBody()
        {
            if (!containerBody.isKinematic)
            {
                containerBody.linearVelocity = Vector3.zero;
                containerBody.angularVelocity = Vector3.zero;
            }

            containerBody.useGravity = false;
            containerBody.isKinematic = true;
            containerBody.detectCollisions = true;
            containerImpactSensor.Disarm();
        }

        private void SetContainerHeadblockCollisionIgnored(bool ignored)
        {
            if (containerCollider == null || headblockCollider == null)
            {
                return;
            }

            Physics.IgnoreCollision(containerCollider, headblockCollider, ignored);
            containerHeadblockCollisionIgnored = ignored;
        }

        private void UpdateContainerHeadblockCollisionSuppression()
        {
            if (!containerHeadblockCollisionIgnored || containerLocked ||
                containerCollider == null || headblockCollider == null || !containerCollider.enabled)
            {
                return;
            }

            var containerBounds = containerCollider.bounds;
            var headblockBounds = headblockCollider.bounds;
            var centerDelta = containerBounds.center - headblockBounds.center;
            var separation = Mathf.Max(
                Mathf.Abs(centerDelta.x) - containerBounds.extents.x - headblockBounds.extents.x,
                Mathf.Max(
                    Mathf.Abs(centerDelta.y) - containerBounds.extents.y - headblockBounds.extents.y,
                    Mathf.Abs(centerDelta.z) - containerBounds.extents.z - headblockBounds.extents.z));
            if (separation > ContainerContactEnvelopeMarginMeters)
            {
                SetContainerHeadblockCollisionIgnored(false);
            }
        }

        private void BindSceneAuthoredSolver()
        {
            var ropeRig = RequireTransform(YardCraneSceneSimulationContract.RopeRigPath);
            solver = ropeRig.GetComponent<ObiSolver>();
            if (solver == null)
            {
                throw MissingSceneComponent<ObiSolver>(YardCraneSceneSimulationContract.RopeRigPath);
            }

            var sceneInfrastructure = RequireTransform(YardCraneSceneSimulationContract.ObiSceneComponentsPath);
            if (sceneInfrastructure.GetComponentInChildren<BurstColliderWorld>(true) == null)
            {
                throw MissingSceneComponent<BurstColliderWorld>(
                    YardCraneSceneSimulationContract.ObiSceneComponentsPath);
            }
        }

        private void BindEightSceneAuthoredStraightRopes()
        {
            loops.Clear();
            foreach (var definition in LoopDefinitions)
            {
                var topAnchor = RequireTransform(definition.TopAnchorPath);
                var sheave = RequireTransform(definition.SheavePath);
                var slot = RequireTransform(definition.SlotPath);
                var visualRope = BindSceneAuthoredVisualRope(definition.Id, topAnchor, sheave, slot);
                var initialRopeLength = Vector3.Distance(topAnchor.position, sheave.position);

                loops.Add(new LoopRuntime(
                    definition,
                    topAnchor,
                    sheave,
                    headblockPhysics.InverseTransformPoint(sheave.position),
                    visualRope,
                    initialRopeLength));
            }

            if (loops.Count != 8)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] Expected exactly 8 authored ropes, found {loops.Count}.");
            }
        }

        private VisualLegRuntime BindSceneAuthoredVisualRope(
            string ropeId,
            Transform fixedAnchor,
            Transform sheave,
            Transform slot)
        {
            var actorName = YardCraneSceneSimulationContract.GetRopeActorName(ropeId);
            var actorTransform = slot.Find(actorName);
            if (actorTransform == null)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_MISSING] {definitionPath(slot, actorName)} is missing. " +
                    $"Run '{YardCraneSceneSimulationContract.AuthoringMenuPath}' in Edit Mode.");
            }

            var actorObject = actorTransform.gameObject;
            var rope = actorObject.GetComponent<Obi.ObiRope>();
            if (rope == null) throw MissingSceneComponent<Obi.ObiRope>(definitionPath(slot, actorName));

            var blueprint = rope.ropeBlueprint;
            if (blueprint == null || blueprint.groups == null || blueprint.groups.Count < 2)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {definitionPath(slot, actorName)} requires a persistent " +
                    "ObiRopeBlueprint with Fixed and Sheave particle groups.");
            }

            var renderer = actorObject.GetComponent<ObiRopeExtrudedRenderer>();
            if (renderer == null) throw MissingSceneComponent<ObiRopeExtrudedRenderer>(definitionPath(slot, actorName));
            if (renderer.section == null || renderer.material == null)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {definitionPath(slot, actorName)} renderer requires " +
                    "a rope section and a persistent material.");
            }

            var cursor = actorObject.GetComponent<ObiRopeCursor>();
            if (cursor == null) throw MissingSceneComponent<ObiRopeCursor>(definitionPath(slot, actorName));

            ObiParticleAttachment fixedAttachment = null;
            ObiParticleAttachment sheaveAttachment = null;
            var attachments = actorObject.GetComponents<ObiParticleAttachment>();
            foreach (var attachment in attachments)
            {
                if (attachment.target == fixedAnchor) fixedAttachment = attachment;
                if (attachment.target == sheave) sheaveAttachment = attachment;
            }

            if (attachments.Length != 2 || fixedAttachment == null || sheaveAttachment == null ||
                fixedAttachment == sheaveAttachment)
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {definitionPath(slot, actorName)} must contain exactly two " +
                    $"attachments bound to '{fixedAnchor.name}' and '{sheave.name}'.");
            }

            if (fixedAttachment.particleGroup != blueprint.groups[0] ||
                sheaveAttachment.particleGroup != blueprint.groups[1])
            {
                throw new InvalidOperationException(
                    $"[YC_SCENE_AUTHORING_INVALID] {definitionPath(slot, actorName)} attachments must use " +
                    "blueprint groups Fixed and Sheave respectively.");
            }

            return new VisualLegRuntime(
                actorObject,
                rope,
                cursor,
                blueprint,
                fixedAttachment,
                sheaveAttachment,
                Vector3.Distance(fixedAnchor.position, sheave.position));
        }

        private string definitionPath(Transform slot, string actorName)
        {
            return $"{slot.name}/{actorName}";
        }

        private void TryFinishVisualInitialization()
        {
            foreach (var loop in loops)
            {
                if (!loop.VisualRope.IsLoaded)
                {
                    return;
                }
            }

            foreach (var loop in loops)
            {
                InitializeVisualLeg(loop.VisualRope, Vector3.Distance(loop.TopAnchor.position, loop.Sheave.position));
                loop.InitialRopeLength = CalculateActualRopeLength(loop, headblockBody.position, headblockBody.rotation);
                loop.CommandedRopeLength = loop.InitialRopeLength;
                loop.LastCommandedRopeLength = loop.InitialRopeLength;
                loop.LastTopAnchorPosition = loop.TopAnchor.position;
            }

            headblockBody.useGravity = false;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            loopsReady = true;
            startupPreTensioning = true;
            startupReleaseAt = Time.fixedTime + StartupPreTensionDurationSeconds;
            demoStartedAt = Time.fixedTime;
            runtimeStatus = "Pre-tensioning eight independent straight ropes";
            var activeRigCount = FindObjectsByType<YardCraneEightRopeMaxPrototypeRig>(
                FindObjectsSortMode.None).Length;
            Debug.Log($"[YC_EIGHT_ROPE_MAX_READY] model={ImplementationVersion}, profile={TopologyProfile}, " +
                      $"drums=2, ropes=8, visualRopes=8, drop={commandedSuspendedDrop:F3}m, " +
                      $"antiSway=empty+loaded, omega={LoadedSuspensionNaturalFrequencyRadiansPerSecond:F2}rad/s, " +
                      $"dampingRatio={LoadedSuspensionDampingRatio:F2}, " +
                      $"maxAcceleration={MaximumLoadedSuspensionAccelerationMetersPerSecondSquared:F2}m/s2, " +
                      $"instance={GetInstanceID()}, activeRigCount={activeRigCount}, " +
                      $"scene={gameObject.scene.name}, resetEpoch={resetEpoch}", this);
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
            var isBraking = RequiresBraking(currentTrolleyVelocity, targetVelocity);
            trolleyBrakeApplied = isBraking &&
                                   (Mathf.Approximately(targetVelocity, 0f) ||
                                    Mathf.Abs(currentTrolleyVelocity) > 0.0001f);
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

        private void ApplyTrolleyAntiSwayDynamics()
        {
            if (loadTransferPreTensioning)
            {
                return;
            }

            var trolleyVelocity = GetTrolleyBoundaryVelocityWorld();
            // The previous implementation only ran this relative-motion controller while a
            // container was locked. An empty spreader therefore had almost no damping relative
            // to the moving trolley and repeatedly crossed the 0.30 m training sway limit.
            // Keep the load-transfer equilibrium offset for a locked container, but stabilize
            // the empty spreader around the trolley centre as well.
            var equilibriumOffset = GetCurrentSuspensionEquilibriumOffset();
            var horizontalDisplacement = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                Vector3.up) - equilibriumOffset;
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
                RefreshCommandedRopeLengthsForNominalPose();
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
            var isBraking = RequiresBraking(currentHoistVelocity, targetVelocity);
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

            var pumpOn = spreaderPump?.IsOn ?? false;
            var inputActive = Mathf.Abs(spreaderRotationInputAxis) > 0.0001f;
            if (!pumpOn && inputActive)
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
            spreaderRotation.Advance(spreaderRotationInputAxis, pumpOn, Time.fixedDeltaTime);
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

            commandedSuspendedDrop = nextDrop;
            RefreshCommandedRopeLengthsForNominalPose();
            return appliedDropDelta;
        }

        private void RefreshCommandedRopeLengthsForNominalPose()
        {
            // Never derive a new hoist command from the dynamically swinging body pose.
            // Doing so gives every rope a different incremental length while the spreader is
            // tilted, permanently baking that transient sway into the unilateral constraints.
            // Recompute all eight lengths from the last explicitly rebaselined equilibrium pose
            // and the absolute commanded drop instead.
            var nominalDrop = Mathf.Max(0.01f, commandedSuspendedDrop + loadedHoistDeflection);
            var nominalBodyPosition = trolleyMotion.position + nominalSuspensionBodyHorizontalOffset;
            nominalBodyPosition.y = trolleyMotion.position.y - nominalDrop;
            foreach (var loop in loops)
            {
                var nominalSheavePosition = nominalBodyPosition +
                                             nominalSuspensionBodyRotation * loop.LocalSheaveAnchor;
                loop.CommandedRopeLength = Vector3.Distance(
                    loop.TopAnchor.position,
                    nominalSheavePosition);
            }
        }

        private void CaptureNominalSuspensionPose()
        {
            nominalSuspensionBodyHorizontalOffset = Vector3.ProjectOnPlane(
                headblockBody.position - trolleyMotion.position,
                Vector3.up);
            nominalSuspensionBodyRotation = headblockBody.rotation;
        }

        private Vector3 GetCurrentSuspensionEquilibriumOffset()
        {
            return containerLocked
                ? loadedSuspensionEquilibriumOffset
                : unloadedSuspensionEquilibriumOffset;
        }

        private static bool RequiresBraking(float currentVelocity, float targetVelocity)
        {
            if (Mathf.Approximately(targetVelocity, 0f))
            {
                return true;
            }

            // Mathf.Sign(0) is +1. Comparing signs directly made a negative command from rest
            // use the stronger braking acceleration while a positive command used normal
            // acceleration. Only treat direction as a reversal once motion actually exists.
            return Mathf.Abs(currentVelocity) > 0.0001f &&
                   (currentVelocity * targetVelocity < 0f ||
                    Mathf.Abs(targetVelocity) < Mathf.Abs(currentVelocity));
        }

        private void ObserveTrolleyMotion()
        {
            var moving = Mathf.Abs(currentTrolleyVelocity) > MotionObservationSpeedThresholdMetersPerSecond;
            if (moving && !trolleyMotionReported)
            {
                trolleyMotionReported = true;
                trolleyMotionPeakSway = currentHorizontalSway;
                Debug.Log(
                    $"[YC_TROLLEY_STARTED] x={trolleyMotion.localPosition.x:F3}m, " +
                    $"speed={currentTrolleyVelocity:F3}m/s, input={trolleyInput:F0}, loaded={containerLocked}, " +
                    $"instance={GetInstanceID()}, resetEpoch={resetEpoch}",
                    this);
            }
            else if (!moving && trolleyMotionReported)
            {
                trolleyMotionReported = false;
                trolleyMotionPeakSway = Mathf.Max(trolleyMotionPeakSway, currentHorizontalSway);
                var equilibrium = GetCurrentSuspensionEquilibriumOffset();
                var operationRunId = operationController != null
                    ? operationController.Context.OperationRunId
                    : Guid.Empty;
                Debug.Log(
                    $"[YC_TROLLEY_STOPPED] x={trolleyMotion.localPosition.x:F3}m, " +
                    $"speed={currentTrolleyVelocity:F3}m/s, loaded={containerLocked}, " +
                    $"swayError={currentHorizontalSway:F3}m, segmentPeakSway={trolleyMotionPeakSway:F3}m, " +
                    $"maxObservedSway={maximumObservedSway:F3}m, relativeSpeed={currentRelativeHorizontalSpeed:F3}m/s, " +
                    $"rawOffset=({currentRawHorizontalOffset.x:F3},{currentRawHorizontalOffset.z:F3})m, " +
                    $"error=({currentSwayError.x:F3},{currentSwayError.z:F3})m, " +
                    $"equilibrium=({equilibrium.x:F3},{equilibrium.z:F3})m, " +
                    $"ropeCommandSpread={GetCommandedRopeLengthSpread():F4}m, " +
                    $"instance={GetInstanceID()}, scene={gameObject.scene.name}, " +
                    $"run={operationRunId:D}, resetEpoch={resetEpoch}",
                    this);
            }
        }

        private float GetCommandedRopeLengthSpread()
        {
            if (loops.Count == 0)
            {
                return 0f;
            }

            var minimum = float.PositiveInfinity;
            var maximum = float.NegativeInfinity;
            foreach (var loop in loops)
            {
                minimum = Mathf.Min(minimum, loop.CommandedRopeLength);
                maximum = Mathf.Max(maximum, loop.CommandedRopeLength);
            }
            return maximum - minimum;
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
            if (!loopsReady || containerLocked || releasedContainerSettlementPending)
            {
                return;
            }

            UpdateContainerAlignmentErrors();
            var speed = GetRelativeContainerLockSpeed();
            // Delegates to the same interlock RequestContainerLock already gated on, instead of
            // re-testing containerCornerAlignment directly here - keeps this method's own
            // spreader-size-transitioning and container-type checks from drifting out of sync
            // with each other now that there are three conditions instead of one.
            var aligned = IsContainerAlignedForLock();
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

            if (!TryBindDetectedContainerForPickup(out var bindError))
            {
                twistlockStatus = "Lock rejected";
                runtimeStatus = "Container lock rejected · invalid yard container state";
                Debug.LogWarning($"[YC_CONTAINER_BIND_REJECTED] reason={bindError}", this);
                return;
            }

            // Perform the complete hand-over at the beginning of one fixed step.
            // The free container collider is suppressed while it is still touching
            // the yard surface so it cannot inject a contact impulse into the
            // scene-authored suspended rigid body.
            suspensionGravityActive = false;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;
            currentHoistVelocity = 0f;
            ResetLoadedHoistDynamics();
            containerCollider.enabled = false;
            containerColliderSuppressedForPickup = true;

            // The free-body proxy stays in the scene for editing and inspection. Park it before
            // moving the collider back under the headblock so there is never a nested Rigidbody
            // and the lifted container remains part of the headblock compound body.
            ParkContainerFreeBody();

            activeContainer.SetParent(containerAttachmentRoot, true);
            activeContainer.localPosition = containerLockedLocalPosition;
            activeContainer.localRotation = Quaternion.identity;
            SetContainerHeadblockCollisionIgnored(false);
            containerLocked = true;
            if (pickedUpSourceSlot != null)
            {
                var clearedSlotId = pickedUpSourceSlot.SlotId;
                pickedUpSourceSlot.Clear();
                Debug.Log(
                    $"[YC_YARD_SOURCE_CLEARED] slot={clearedSlotId}, container={container.ContainerId}",
                    this);
            }
            ResetContainerLandingConfirmation();
            twistlockStatus = "Locked · 4/4";
            ApplySuspendedLoadProperties();
            loadedSuspensionEquilibriumOffset = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                Vector3.up);
            RebaselineRopeConstraintsFromCurrentPose();
            UpdateContainerLoadFeedback();
            loadTransferPreTensioning = true;
            loadTransferReleaseAt = Time.fixedTime + LoadTransferPreTensionDurationSeconds;
            runtimeStatus = $"Transferring {containerGrossMassKg / 1000f:F1} t load to eight ropes";
            Debug.Log(
                $"[YC_CONTAINER_LOCKED] grossMass={containerGrossMassKg:F0}kg, " +
                $"totalSuspendedMass={totalSuspendedMassKg:F0}kg, proxyMass={headblockBody.mass:F2}kg, " +
                $"averageRopeTension={averageRopeTensionKn:F1}kN",
                this);
        }

        private void TryUnlockContainer()
        {
            if (!loopsReady || !containerLocked)
            {
                return;
            }

            // Capture the full point velocity before the headblock is stopped so the existing
            // scene-authored free-body proxy inherits both translation and rotation at release.
            var releaseVelocity = GetHeadblockPointVelocity();
            var releaseAngularVelocity = headblockBody.angularVelocity;
            if (Mathf.Abs(spreaderRotationAngularVelocityRadiansPerSecond) > 0.0001f)
            {
                releaseAngularVelocity += headblockPhysics.TransformDirection(Vector3.up).normalized *
                                          spreaderRotationAngularVelocityRadiansPerSecond;
            }
            var unsafeUnlock = !containerLandingConfirmed;
            if (unsafeUnlock && loadedOperationRegressionActive)
            {
                // The legacy 30 t Full Play regression explicitly verifies that a suspended
                // unlock request is rejected before it continues to the real landing phase.
                // Keep that harness-only contract without changing the product path below,
                // where an operator can still perform an unsafe unlock and receive the
                // corresponding training violation plus real PhysX fall.
                twistlockStatus = "Unlock rejected";
                runtimeStatus = "Regression interlock · land the container before unlocking";
                Debug.Log(
                    "[YC_CONTAINER_UNLOCK_REJECTED_REGRESSION] suspended unlock retained lock state",
                    this);
                return;
            }

            if (unsafeUnlock)
            {
                // Real yard cranes cannot physically unlock here - see the comment on
                // TryQueueContainerUnlock. This method is the single point every unlock
                // path converges on (player input via TryQueueContainerUnlock, and the
                // developer regression/demo harness calling RequestContainerUnlock()
                // directly), so the interlock has to be relaxed here too, not only at the
                // player-facing action-acceptance gate, or the physical unlock never
                // actually happens even after that gate lets the request through. The
                // training-violation record itself is written once, at TryQueueContainerUnlock
                // (the player-facing gate) - not here, so the regression/demo harness path
                // does not generate scoring records for a developer test sequence.
                var stableFor = containerLandingStableSince >= 0f
                    ? Time.fixedTime - containerLandingStableSince
                    : 0f;
                Debug.LogWarning(
                    $"[YC_CONTAINER_UNLOCK_UNSAFE] confirmed={containerLandingConfirmed}, " +
                    $"horizontal={containerLandingHorizontalErrorMeters:F3}m, " +
                    $"vertical={containerLandingVerticalErrorMeters:F3}m, " +
                    $"yaw={containerLandingYawErrorDegrees:F2}deg, " +
                    $"speed={containerLandingSpeedMetersPerSecond:F3}m/s, " +
                    $"stableFor={stableFor:F2}s, required={ContainerLandingStableHoldSeconds:F2}s",
                    this);
            }

            suspensionGravityActive = false;
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentHoistVelocity = 0f;
            ResetLoadedHoistDynamics();
            containerCollider.enabled = true;
            containerColliderSuppressedForPickup = false;
            var releasePosition = activeContainer.position;
            var releaseRotation = activeContainer.rotation;
            containerFreeBody.SetPositionAndRotation(releasePosition, releaseRotation);
            activeContainer.SetParent(containerFreeBody, true);
            activeContainer.localPosition = Vector3.zero;
            activeContainer.localRotation = Quaternion.identity;
            SetContainerHeadblockCollisionIgnored(true);

            // Activate the scene-authored container Rigidbody from this point on, so an unsafe (mid-air)
            // unlock actually falls under gravity instead of freezing in place as a static
            // collider - see the known-gap note this replaces in the 2026-09-09 discussion
            // record. A safe unlock (already resting at/near landing height) just lets it
            // settle naturally, which is also more correct than leaving it a static leftover.
            // Gated off during an automated regression run: loadedOperationRegressionActive is
            // only true for the .Regression.cs harness's own RequestContainerUnlock() calls,
            // and that harness's existing assertions were validated against a container that
            // stays a static, motionless collider after unlock - giving it new physics there
            // would be an unrelated behavior change to an already-verified test, not a fix.
            if (!loadedOperationRegressionActive)
            {
                containerBody.mass = containerGrossMassKg;
                containerBody.useGravity = true;
                containerBody.isKinematic = false;
                containerBody.detectCollisions = true;
                containerBody.linearVelocity = releaseVelocity;
                containerBody.angularVelocity = releaseAngularVelocity;

                containerImpactSensor.Arm();
                releasedContainerSettlementPending = true;
                releasedContainerStableSince = -1f;
                Debug.Log(
                    $"[YC_CONTAINER_SETTLEMENT_STARTED] container={container.ContainerId}, " +
                    $"linearSpeed={containerBody.linearVelocity.magnitude:F3}m/s, " +
                    $"angularSpeed={containerBody.angularVelocity.magnitude:F3}rad/s",
                    this);
            }
            else
            {
                // The regression intentionally keeps the released test box static, but it must
                // still leave the shared free-body proxy empty so a later real yard pickup is
                // not rejected as "proxy occupied".
                activeContainer.SetParent(containerOriginalParent, true);
                containerHomePosition = activeContainer.position;
                containerHomeRotation = activeContainer.rotation;
                containerRestingCenterY = activeContainer.position.y;
                ParkContainerFreeBody();
                releasedContainerSettlementPending = false;
                releasedContainerStableSince = -1f;
            }

            containerLocked = false;
            ResetContainerLandingConfirmation();
            twistlockStatus = unsafeUnlock ? "Unlocked · UNSAFE" : "Unlocked";
            ApplySuspendedLoadProperties();
            unloadedSuspensionEquilibriumOffset = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                Vector3.up);
            loadedSuspensionEquilibriumOffset = Vector3.zero;
            RebaselineRopeConstraintsFromCurrentPose();
            UpdateContainerLoadFeedback();
            loadTransferPreTensioning = true;
            loadTransferReleaseAt = Time.fixedTime + LoadTransferPreTensionDurationSeconds;
            runtimeStatus = unsafeUnlock
                ? "Container released WITHOUT confirmed landing - unsafe unlock"
                : "Container released on landing surface";
            Debug.Log(
                unsafeUnlock
                    ? "[YC_CONTAINER_UNLOCKED] load removed WITHOUT landing confirmation (unsafe)"
                    : "[YC_CONTAINER_UNLOCKED] load removed after landing confirmation",
                this);
        }

        private void UpdateReleasedContainerSettlement()
        {
            if (!releasedContainerSettlementPending)
            {
                return;
            }

            if (container == null || activeContainer == null || containerBody == null)
            {
                Debug.LogError(
                    "[YC_CONTAINER_SETTLEMENT_ABORTED] released container state is incomplete; " +
                    "the rig is disabled fail-closed so another box cannot reuse an unknown free-body state.",
                    this);
                enabled = false;
                return;
            }

            if (containerBody.isKinematic)
            {
                // Recover an externally interrupted release without permanently occupying the
                // one shared ContainerFreeBody. Do not infer a slot occupancy from this fault.
                activeContainer.SetParent(containerOriginalParent, true);
                if (containerCollider != null)
                {
                    containerCollider.enabled = true;
                }
                ParkContainerFreeBody();
                releasedContainerSettlementPending = false;
                releasedContainerStableSince = -1f;
                pickedUpSourceSlot = null;
                Debug.LogError(
                    "[YC_CONTAINER_SETTLEMENT_ABORTED] free body became kinematic before settlement; " +
                    "container was detached and left unassigned.",
                    this);
                return;
            }

            var linearSpeed = containerBody.linearVelocity.magnitude;
            var angularSpeed = containerBody.angularVelocity.magnitude;
            if (!float.IsFinite(linearSpeed) || !float.IsFinite(angularSpeed) ||
                !IsContainerPhysicallySupported() ||
                linearSpeed > ResetSafeSpeedThresholdMetersPerSecond ||
                angularSpeed > ResetSafeSpeedThresholdMetersPerSecond)
            {
                releasedContainerStableSince = -1f;
                return;
            }

            if (releasedContainerStableSince < 0f)
            {
                releasedContainerStableSince = Time.fixedTime;
                return;
            }

            var stableFor = Time.fixedTime - releasedContainerStableSince;
            if (stableFor < ContainerLandingStableHoldSeconds)
            {
                return;
            }

            var sourceSlotId = pickedUpSourceSlot != null ? pickedUpSourceSlot.SlotId : "<unassigned>";
            var targetSlot = FindBestAvailableYardSlot(
                container,
                out var horizontalError,
                out var verticalError,
                out var yawError);

            foreach (var slot in yardSlots)
            {
                if (slot != null && slot.CurrentOccupant == container)
                {
                    slot.Clear();
                }
            }

            activeContainer.SetParent(containerOriginalParent, true);
            containerCollider.enabled = true;
            ParkContainerFreeBody();

            if (targetSlot != null)
            {
                targetSlot.SetOccupant(container);
                Debug.Log(
                    $"[YC_YARD_SLOT_OCCUPIED] slot={targetSlot.SlotId}, container={container.ContainerId}, " +
                    $"type={container.ContainerType}, sourceSlot={sourceSlotId}, " +
                    $"horizontal={horizontalError:F3}m, vertical={verticalError:F3}m, yaw={yawError:F2}deg",
                    this);
            }
            else
            {
                Debug.Log(
                    $"[YC_YARD_SLOT_UNASSIGNED] container={container.ContainerId}, sourceSlot={sourceSlotId}, " +
                    "released container settled outside every compatible empty yard slot.",
                    this);
            }

            containerHomePosition = activeContainer.position;
            containerHomeRotation = activeContainer.rotation;
            containerRestingCenterY = activeContainer.position.y;
            releasedContainerSettlementPending = false;
            releasedContainerStableSince = -1f;
            pickedUpSourceSlot = null;
            runtimeStatus = targetSlot != null
                ? $"Container settled · slot {targetSlot.SlotId} occupied"
                : "Container settled outside assigned yard slots";
            Debug.Log(
                $"[YC_CONTAINER_SETTLED] container={container.ContainerId}, " +
                $"targetSlot={(targetSlot != null ? targetSlot.SlotId : "<unassigned>")}, " +
                $"linearSpeed={linearSpeed:F3}m/s, angularSpeed={angularSpeed:F3}rad/s, " +
                $"stableFor={stableFor:F2}s",
                this);
        }

        // Called by YardCraneContainerImpactSensor.ImpactDetected once per unlock cycle, for
        // the first collision the now-freely-falling container has. Independent of the
        // UnsafeUnlock violation recorded in TryQueueContainerUnlock: choosing to unlock
        // unsafely and the resulting impact are two separate real-world facts (an incident
        // report would cite both), so a bad unlock that also lands hard can record both
        // violations. Not recorded at all for a normal demo/regression drop test outside a
        // scored session, same as every other trainingScore.RecordViolation call in this file.
        private void HandleContainerImpact(float impactSpeedMetersPerSecond)
        {
            if (impactSpeedMetersPerSecond <= activeProfile.training.hardImpactSpeedThresholdMetersPerSecond) return;
            containerImpactSensor.Disarm();
            trainingScore.RecordViolation(
                YardCraneViolationCategory.Skill, YardCraneViolationType.HardImpact,
                activeProfile.training.hardImpactPenaltyPoints, Time.time,
                $"impactSpeed={impactSpeedMetersPerSecond:F3}m/s, limit={activeProfile.training.hardImpactSpeedThresholdMetersPerSecond:F3}m/s");
            Debug.LogWarning(
                $"[YC_TRAINING_VIOLATION_HARD_IMPACT] impactSpeed={impactSpeedMetersPerSecond:F3}m/s, " +
                $"limit={activeProfile.training.hardImpactSpeedThresholdMetersPerSecond:F3}m/s", this);
        }

        private void ApplySuspendedLoadProperties()
        {
            var proxyContainerMass = containerLocked
                ? sceneAuthoredHeadblockMassKg * containerGrossMassKg / SpreaderRealMassKg
                : 0f;
            headblockBody.mass = sceneAuthoredHeadblockMassKg + proxyContainerMass;

            if (containerLocked)
            {
                var containerCenterLocal = headblockPhysics.InverseTransformPoint(activeContainer.position);
                var combinedCenterOfMass = containerCenterLocal *
                                           (proxyContainerMass / headblockBody.mass);
                headblockBody.centerOfMass = combinedCenterOfMass;

                // The carried collider is deliberately excluded during pickup.
                // Build the compound inertia explicitly so physical load feedback
                // remains present without depending on collider attach timing.
                var containerSize = containerCollider.size;
                var containerInertia = new Vector3(
                    proxyContainerMass * (containerSize.y * containerSize.y + containerSize.z * containerSize.z) / 12f,
                    proxyContainerMass * (containerSize.x * containerSize.x + containerSize.z * containerSize.z) / 12f,
                    proxyContainerMass * (containerSize.x * containerSize.x + containerSize.y * containerSize.y) / 12f);
                var spreaderOffset = -combinedCenterOfMass;
                var containerOffset = containerCenterLocal - combinedCenterOfMass;
                var compoundInertia = unloadedInertiaTensor + containerInertia;
                compoundInertia += ParallelAxisInertia(sceneAuthoredHeadblockMassKg, spreaderOffset);
                compoundInertia += ParallelAxisInertia(proxyContainerMass, containerOffset);
                headblockBody.inertiaTensorRotation = unloadedInertiaTensorRotation;
                headblockBody.inertiaTensor = new Vector3(
                    Mathf.Max(compoundInertia.x, 0.0001f),
                    Mathf.Max(compoundInertia.y, 0.0001f),
                    Mathf.Max(compoundInertia.z, 0.0001f));
            }
            else
            {
                headblockBody.centerOfMass = sceneAuthoredHeadblockCenterOfMass;
                headblockBody.inertiaTensorRotation = unloadedInertiaTensorRotation;
                headblockBody.inertiaTensor = unloadedInertiaTensor;
            }

            headblockBody.angularDamping = containerLocked
                ? LoadedHeadblockAngularDamping
                : sceneAuthoredHeadblockAngularDamping;
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
                spreaderContactConfirmed = false;
                return;
            }

            spreaderContactConfirmed =
                !containerLocked &&
                IsContainerAlignedForLock() &&
                GetRelativeContainerLockSpeed() <= MaximumLockingSpeedMetersPerSecond;

            if (spreaderContactConfirmed)
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

            var landingSlot = FindBestAvailableYardSlot(
                container,
                out var slotHorizontalError,
                out var slotVerticalError,
                out var slotYawError);
            var difference = activeContainer.position - containerHomePosition;
            containerLandingHorizontalErrorMeters = landingSlot != null
                ? slotHorizontalError
                : new Vector2(difference.x, difference.z).magnitude;
            containerLandingVerticalErrorMeters = landingSlot != null
                ? slotVerticalError
                : Mathf.Abs(difference.y);
            containerLandingYawErrorDegrees = landingSlot != null
                ? slotYawError
                : Mathf.Abs(Mathf.DeltaAngle(
                    containerHomeRotation.eulerAngles.y,
                    activeContainer.eulerAngles.y));
            containerLandingSpeedMetersPerSecond = GetHeadblockPointVelocity().magnitude;

            var physicallySupported = IsContainerPhysicallySupported();
            // Pose matching only classifies the eventual destination. It cannot prove that a
            // box is actually resting on anything: a container can be motionless while hovering
            // inside the slot tolerance. Physical bottom support is therefore a hard unlock
            // requirement, matching the project's "let PhysX decide whether placement is stable"
            // rule.
            var stableLanding = physicallySupported &&
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
                $"stableFor={Time.fixedTime - containerLandingStableSince:F2}s, " +
                $"support={physicallySupported}, " +
                $"slot={(landingSlot != null ? landingSlot.SlotId : "<free-ground>")}",
                this);
        }

        private bool IsContainerPhysicallySupported()
        {
            if (activeContainer == null || containerCollider == null || !containerCollider.enabled)
            {
                return false;
            }

            var scale = containerCollider.transform.lossyScale;
            var halfExtents = Vector3.Scale(
                containerCollider.size * 0.5f,
                new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            var probeDepth = Mathf.Max(ContainerLandingToleranceMeters, 0.01f);
            halfExtents.x = Mathf.Max(halfExtents.x - ContainerContactEnvelopeMarginMeters, 0.01f);
            halfExtents.z = Mathf.Max(halfExtents.z - ContainerContactEnvelopeMarginMeters, 0.01f);
            halfExtents.y += probeDepth * 0.5f;
            var probeCenter = containerCollider.transform.TransformPoint(containerCollider.center) +
                              Vector3.down * (probeDepth * 0.5f);
            var containerBounds = containerCollider.bounds;

            foreach (var overlap in Physics.OverlapBox(
                         probeCenter,
                         halfExtents,
                         containerCollider.transform.rotation,
                         ~0,
                         QueryTriggerInteraction.Ignore))
            {
                if (overlap == null || overlap == containerCollider || overlap == headblockCollider ||
                    overlap.transform.IsChildOf(activeContainer) ||
                    (headblockPhysics != null && overlap.transform.IsChildOf(headblockPhysics)))
                {
                    continue;
                }

                var supportBounds = overlap.bounds;
                var topIsAtContainerBase =
                    supportBounds.max.y >= containerBounds.min.y - probeDepth &&
                    supportBounds.max.y <= containerBounds.center.y;
                var overlapsHorizontally =
                    supportBounds.max.x >= containerBounds.min.x + ContainerContactEnvelopeMarginMeters &&
                    supportBounds.min.x <= containerBounds.max.x - ContainerContactEnvelopeMarginMeters &&
                    supportBounds.max.z >= containerBounds.min.z + ContainerContactEnvelopeMarginMeters &&
                    supportBounds.min.z <= containerBounds.max.z - ContainerContactEnvelopeMarginMeters;
                if (topIsAtContainerBase && overlapsHorizontally)
                {
                    return true;
                }
            }

            return false;
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
                containerCornerAlignment = new YardCraneContainerCornerState(true, true, true, true);
                return;
            }

            containerCornerAlignment = ComputeContainerCornerAlignment();
            var alignmentTarget = detectedPickupContainer != null
                ? detectedPickupContainer.transform
                : activeContainer;
            var desiredCenter = containerAttachmentRoot.TransformPoint(containerLockedLocalPosition);
            var difference = alignmentTarget.position - desiredCenter;
            containerHorizontalErrorMeters = new Vector2(difference.x, difference.z).magnitude;
            containerVerticalErrorMeters = Mathf.Abs(difference.y);
            containerYawErrorDegrees = Mathf.Abs(Mathf.DeltaAngle(
                containerAttachmentRoot.eulerAngles.y,
                alignmentTarget.eulerAngles.y));
        }

        // 2026-09-17: replaced the earlier pure-geometry version (compared computed corner
        // positions against a computed target) with real Physics.Raycast queries against small
        // trigger colliders authored at a YardCraneContainer's true corners
        // (EnsureLockHoleColliders validates them). Per user decision: lock/pick detection must come from the
        // physics engine sensing an actual lock hole, not from us calculating whether it should
        // be there - this also gives "which container is under the spreader" for free (the yard
        // pick/place loop), instead of needing a separate nearest-slot distance lookup.
        //
        // The four twistlock points are repositioned every call (ApplyTwistlockPositionsForSpreaderSize)
        // to match the current spreader size (Ft20/Ft40), so a container whose own type does not
        // match the spreader's current size will generally miss geometrically before
        // IsContainerAlignedForLock's explicit type check ever runs.
        private YardCraneContainerCornerState ComputeContainerCornerAlignment()
        {
            ApplyTwistlockPositionsForSpreaderSize();

            var frontLeftHit = RaycastLockHole(twistlockFrontLeft);
            var frontRightHit = RaycastLockHole(twistlockFrontRight);
            var rearLeftHit = RaycastLockHole(twistlockRearLeft);
            var rearRightHit = RaycastLockHole(twistlockRearRight);

            // All four points have to agree on the same container - a real spreader cannot have
            // its four twistlocks straddling two different boxes. Front-left is the arbitrary
            // tie-breaker that decides which candidate the other three must match.
            detectedPickupContainer = frontLeftHit;

            return new YardCraneContainerCornerState(
                frontLeft: frontLeftHit != null,
                frontRight: frontRightHit != null && frontRightHit == detectedPickupContainer,
                rearLeft: rearLeftHit != null && rearLeftHit == detectedPickupContainer,
                rearRight: rearRightHit != null && rearRightHit == detectedPickupContainer);
        }

        private const float TwistlockSensorRangeMeters = 0.35f;

        private YardCraneContainer RaycastLockHole(Transform twistlockPoint)
        {
            if (twistlockPoint == null)
            {
                return null;
            }

            var lockHoleLayer = LayerMask.NameToLayer(YardCraneContainer.LockHoleLayerName);
            if (lockHoleLayer < 0)
            {
                return null;
            }

            // Query every layer and walk the hits nearest-first. Looking only at the lock-hole
            // layer lets a sensor see through a physical container stacked above the requested
            // box, which is impossible in the real mechanism. Rig-owned colliders and unrelated
            // trigger volumes are transparent; the first foreign solid collider blocks the ray.
            var hits = Physics.RaycastAll(
                twistlockPoint.position,
                -twistlockPoint.up,
                TwistlockSensorRangeMeters,
                ~0,
                QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                var hitCollider = hit.collider;
                if (hitCollider == null ||
                    hitCollider == headblockCollider ||
                    (headblockPhysics != null && hitCollider.transform.IsChildOf(headblockPhysics)))
                {
                    continue;
                }

                var detectedContainer = hitCollider.gameObject.layer == lockHoleLayer
                    ? hitCollider.GetComponentInParent<YardCraneContainer>()
                    : null;
                if (detectedContainer != null)
                {
                    return IsLockHolePathObstructed(
                        twistlockPoint.position,
                        hit.point,
                        detectedContainer)
                        ? null
                        : detectedContainer;
                }

                if (hitCollider.isTrigger)
                {
                    continue;
                }

                return null;
            }

            return null;
        }

        private bool IsLockHolePathObstructed(
            Vector3 sensorOrigin,
            Vector3 lockHolePoint,
            YardCraneContainer detectedContainer)
        {
            const float pathProbeRadiusMeters = 0.0125f;
            foreach (var overlap in Physics.OverlapCapsule(
                         sensorOrigin,
                         lockHolePoint,
                         pathProbeRadiusMeters,
                         ~0,
                         QueryTriggerInteraction.Ignore))
            {
                if (overlap == null ||
                    overlap == headblockCollider ||
                    overlap.transform.IsChildOf(detectedContainer.transform) ||
                    (headblockPhysics != null && overlap.transform.IsChildOf(headblockPhysics)))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        // Container-local axes: X = length, Z = width (matches the existing graybox collider's
        // ContainerColliderSizeMeters X-is-length convention); +X is labeled "front", +Z is
        // labeled "left" - an internally consistent choice for this codebase, not one checked
        // against a real spreader's own front/left naming, and it does not affect which
        // container the sensors find, only the corner names in status/HUD text.
        //
        // Positions the four points at the spreader's own fixed geometry (not any specific
        // container's), so a mismatched-size container's real corners generally fall outside
        // TwistlockSensorRangeMeters here. Runs every alignment check rather than only on a
        // spreader-size-change event - four Vector3 writes is cheap enough that keeping this
        // always in sync is simpler than wiring a change notification.
        private void ApplyTwistlockPositionsForSpreaderSize()
        {
            var containerType = CurrentSpreaderSize == YardCraneSpreaderSize.Ft20
                ? YardCraneContainerType.Ft20
                : YardCraneContainerType.Ft40;
            var dims = YardCraneContainerDimensions.For(containerType);
            var halfLength = dims.x * 0.5f;
            var halfWidth = dims.y * 0.5f;
            var halfHeight = dims.z * 0.5f;
            var baseLocalPosition = containerLockedLocalPosition + new Vector3(0f, halfHeight, 0f);

            twistlockFrontLeft.localPosition = baseLocalPosition + new Vector3(halfLength, 0f, halfWidth);
            twistlockFrontRight.localPosition = baseLocalPosition + new Vector3(halfLength, 0f, -halfWidth);
            twistlockRearLeft.localPosition = baseLocalPosition + new Vector3(-halfLength, 0f, halfWidth);
            twistlockRearRight.localPosition = baseLocalPosition + new Vector3(-halfLength, 0f, -halfWidth);
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

        private Vector3 GetContainerPointVelocity()
        {
            if (containerBody == null || containerBody.isKinematic)
            {
                return Vector3.zero;
            }

            return containerBody.GetPointVelocity(activeContainer.position);
        }

        private float GetRelativeContainerLockSpeed()
        {
            return (GetHeadblockPointVelocity() - GetContainerPointVelocity()).magnitude;
        }

        private void UpdateContainerLoadFeedback()
        {
            totalSuspendedMassKg = SpreaderRealMassKg + (containerLocked ? containerGrossMassKg : 0f);
            var verticalDirectionSum = 0f;
            foreach (var loop in loops)
            {
                var sheavePosition = headblockBody.position + headblockBody.rotation * loop.LocalSheaveAnchor;
                var ropeDirection = (loop.TopAnchor.position - sheavePosition).normalized;
                verticalDirectionSum += Mathf.Max(0f, Vector3.Dot(ropeDirection, Vector3.up));
            }

            averageRopeTensionKn = totalSuspendedMassKg * Physics.gravity.magnitude /
                                   Mathf.Max(verticalDirectionSum, 0.1f) / 1000f;
        }

        private void CaptureBoundaryMotion()
        {
            var inverseDeltaTime = 1f / Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            foreach (var loop in loops)
            {
                loop.TopAnchorVelocity =
                    (loop.TopAnchor.position - loop.LastTopAnchorPosition) * inverseDeltaTime;
                loop.RopeLengthRate =
                    (loop.CommandedRopeLength - loop.LastCommandedRopeLength) * inverseDeltaTime;
                loop.LastTopAnchorPosition = loop.TopAnchor.position;
                loop.LastCommandedRopeLength = loop.CommandedRopeLength;
            }
        }

        private void SolveEightRopeConstraints(bool applyPoseImmediately = false)
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
                var drift = Mathf.Max(
                    0f,
                    CalculateActualRopeLength(loop, correctedPosition, correctedRotation) - loop.CommandedRopeLength);
                maximumRawLoopDrift = Mathf.Max(maximumRawLoopDrift, drift);
            }

            for (var iteration = 0; iteration < PositionConstraintIterations; iteration++)
            {
                var correctionRequired = false;
                foreach (var loop in loops)
                {
                    if (ProjectRopePosition(loop, ref correctedPosition, ref correctedRotation))
                    {
                        correctionRequired = true;
                    }
                }
                if (!correctionRequired) break;
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
                    ProjectRopeVelocity(loop, correctedPosition, correctedRotation, ref linearVelocity, ref angularVelocity);
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

        private bool ProjectRopePosition(
            LoopRuntime loop,
            ref Vector3 bodyPosition,
            ref Quaternion bodyRotation)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            var sheaveOffsetFromCenterOfMass = bodyRotation *
                                               (loop.LocalSheaveAnchor - headblockBody.centerOfMass);
            var ropeVector = sheavePosition - loop.TopAnchor.position;
            var ropeDistance = ropeVector.magnitude;
            if (ropeDistance <= Mathf.Epsilon)
            {
                return false;
            }

            var excess = ropeDistance - loop.CommandedRopeLength - LoopConstraintToleranceMeters;
            if (excess <= 0f)
            {
                return false;
            }

            var linearJacobian = ropeVector / ropeDistance;
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

        private void ProjectRopeVelocity(
            LoopRuntime loop,
            Vector3 bodyPosition,
            Quaternion bodyRotation,
            ref Vector3 linearVelocity,
            ref Vector3 angularVelocity)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            var sheaveOffsetFromCenterOfMass = bodyRotation *
                                               (loop.LocalSheaveAnchor - headblockBody.centerOfMass);
            var ropeVector = sheavePosition - loop.TopAnchor.position;
            var ropeDistance = ropeVector.magnitude;
            if (ropeDistance <= Mathf.Epsilon ||
                ropeDistance < loop.CommandedRopeLength - LoopConstraintToleranceMeters)
            {
                return;
            }

            var ropeDirection = ropeVector / ropeDistance;
            var linearJacobian = ropeDirection;
            var angularJacobian = Vector3.Cross(sheaveOffsetFromCenterOfMass, linearJacobian);
            var sheaveVelocity = linearVelocity +
                                 Vector3.Cross(angularVelocity, sheaveOffsetFromCenterOfMass);
            var constraintRate = Vector3.Dot(
                                     ropeDirection,
                                     sheaveVelocity - loop.TopAnchorVelocity) -
                                 loop.RopeLengthRate;
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

        private void SynchronizeVisualRopeLengths()
        {
            foreach (var loop in loops)
            {
                SetVisualLegLength(
                    loop.VisualRope,
                    Vector3.Distance(loop.TopAnchor.position, loop.Sheave.position));
            }
        }

        private void ProjectVisualRopesToEngineeringGeometry()
        {
            foreach (var loop in loops)
            {
                ProjectVisualLegStraight(
                    loop.VisualRope,
                    loop.TopAnchor.position,
                    loop.Sheave.position);
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

        private void RebaselineRopeConstraintsFromCurrentPose()
        {
            commandedSuspendedDrop = CalculateSuspendedDrop();
            CaptureNominalSuspensionPose();
            foreach (var loop in loops)
            {
                var actualRopeLength = CalculateActualRopeLength(
                    loop,
                    headblockBody.position,
                    headblockBody.rotation);
                loop.CommandedRopeLength = actualRopeLength;
                loop.LastCommandedRopeLength = actualRopeLength;
                loop.LastTopAnchorPosition = loop.TopAnchor.position;
                loop.TopAnchorVelocity = Vector3.zero;
                loop.RopeLengthRate = 0f;
            }
        }

        private void RunLoadTransferPreTension()
        {
            CaptureBoundaryMotion();
            SolveEightRopeConstraints(applyPoseImmediately: true);
            SynchronizeVisualRopeLengths();
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
                $"proxyMass={headblockBody.mass:F2}kg, topology=8-straight-rope, " +
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
            SolveEightRopeConstraints(applyPoseImmediately: true);
            SynchronizeVisualRopeLengths();
            ProjectVisualRopesToEngineeringGeometry();
            headblockBody.linearVelocity = Vector3.zero;
            headblockBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;

            if (Time.fixedTime < startupReleaseAt)
            {
                runtimeStatus = "Pre-tensioning eight independent straight ropes";
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
            currentRawHorizontalOffset = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyCenter,
                Vector3.up);
            currentSwayError = currentRawHorizontalOffset -
                               GetCurrentSuspensionEquilibriumOffset();
            currentHorizontalSway = currentSwayError.magnitude;
            maximumObservedSway = Mathf.Max(maximumObservedSway, currentHorizontalSway);
            if (trolleyMotionReported)
            {
                trolleyMotionPeakSway = Mathf.Max(
                    trolleyMotionPeakSway,
                    currentHorizontalSway);
            }
            var trolleyVelocity = GetTrolleyBoundaryVelocityWorld();
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
                    CalculateActualRopeLength(loop, headblockBody.position, headblockBody.rotation) -
                    loop.CommandedRopeLength);
                if (extension > maximumObservedLoopExtension)
                {
                    maximumObservedLoopExtension = extension;
                    peakLoopId = loop.Definition.Id;
                }
            }
        }

        private static float CalculateActualRopeLength(LoopRuntime loop, Vector3 bodyPosition, Quaternion bodyRotation)
        {
            var sheavePosition = bodyPosition + bodyRotation * loop.LocalSheaveAnchor;
            return Vector3.Distance(loop.TopAnchor.position, sheavePosition);
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
            currentRawHorizontalOffset = Vector3.zero;
            currentSwayError = Vector3.zero;
            trolleyMotionPeakSway = 0f;
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
            var freeContainerDynamic = containerBody != null && !containerBody.isKinematic;
            var freeContainerLinearSpeed = freeContainerDynamic ? containerBody.linearVelocity.magnitude : 0f;
            var freeContainerAngularSpeed = freeContainerDynamic ? containerBody.angularVelocity.magnitude : 0f;
            var finiteState = IsFinite(headblockBody.position) &&
                               IsFinite(headblockBody.linearVelocity) &&
                               IsFinite(headblockBody.angularVelocity) &&
                               (!freeContainerDynamic ||
                                (IsFinite(containerBody.position) &&
                                 IsFinite(containerBody.linearVelocity) &&
                                 IsFinite(containerBody.angularVelocity))) &&
                              gantryDrive != null &&
                              float.IsFinite(gantryDrive.PositionMeters) &&
                              float.IsFinite(gantryDrive.VelocityMetersPerSecond) &&
                              float.IsFinite(currentTrolleyVelocity) &&
                              float.IsFinite(currentHoistVelocity);
            var busy = startupPreTensioning || loadTransferPreTensioning ||
                       containerLockRequested || containerUnlockRequested || releasedContainerSettlementPending ||
                       loadedOperationRegressionActive ||
                       (runAutomatedDemo && !demoCompleted);
            var stationary = gantryDrive != null && gantryDrive.IsStationary &&
                             gantryDrive.WheelClampState != WheelClampState.Transitioning &&
                             (trolleyAnchor == null || trolleyAnchor.State != YardCraneAnchorState.Transitioning) &&
                             (gantryAnchor == null || gantryAnchor.State != YardCraneAnchorState.Transitioning) &&
                             (spreaderSize == null || spreaderSize.State != YardCraneSpreaderSizeMechanismState.Transitioning) &&
                             Mathf.Abs(currentTrolleyVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                              Mathf.Abs(currentHoistVelocity) <= ResetSafeSpeedThresholdMetersPerSecond &&
                              bodyLinearSpeed <= ResetSafeSpeedThresholdMetersPerSecond &&
                              bodyAngularSpeed <= ResetSafeSpeedThresholdMetersPerSecond &&
                              freeContainerLinearSpeed <= ResetSafeSpeedThresholdMetersPerSecond &&
                              freeContainerAngularSpeed <= ResetSafeSpeedThresholdMetersPerSecond;
            if (containerLocked || busy || !stationary || !finiteState)
            {
                rejectionCode = YardCraneOperationErrorCodes.ResetInterlockRejected;
                runtimeStatus = "Reset inhibited · unload and stop all mechanisms";
                Debug.LogWarning(
                    $"[YC_PROTOTYPE_RESET_REJECTED] locked={containerLocked}, busy={busy}, " +
                    $"finiteState={finiteState}, gantrySpeed={(gantryDrive?.VelocityMetersPerSecond ?? 0f):F3}m/s, " +
                    $"trolleySpeed={currentTrolleyVelocity:F3}m/s, " +
                    $"hoistSpeed={currentHoistVelocity:F3}m/s, bodySpeed={bodyLinearSpeed:F3}m/s, " +
                    $"angularSpeed={bodyAngularSpeed:F3}rad/s, " +
                    $"containerSpeed={freeContainerLinearSpeed:F3}m/s, " +
                    $"containerAngularSpeed={freeContainerAngularSpeed:F3}rad/s, phase={GetResetPhase()}",
                    this);
                return false;
            }

            resetEpoch++;
            gantryDrive.Reset();
            trolleyAnchorPresenter?.Reset();
            gantryAnchorPresenter?.Reset();
            spreaderSizePresenter?.Reset(YardCraneSpreaderSize.Ft40);
            spreaderPump?.Reset();
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
            unloadedSuspensionEquilibriumOffset = Vector3.ProjectOnPlane(
                GetHeadblockCenterOfMassWorld() - trolleyMotion.position,
                Vector3.up);
            CaptureNominalSuspensionPose();
            currentTrolleyVelocity = 0f;
            currentHoistVelocity = 0f;
            gantryInput = 0f;
            trolleyInput = 0f;
            hoistInput = 0f;
            SetHornActive(false);
            ResetLoadedHoistDynamics();
            trolleyStopStabilizationReleaseAt = 0f;
            twistDisturbanceApplied = false;
            commandedSuspendedDrop = initialSuspendedDrop;
            foreach (var loop in loops)
            {
                loop.CommandedRopeLength = loop.InitialRopeLength;
                loop.LastCommandedRopeLength = loop.InitialRopeLength;
                loop.LastTopAnchorPosition = loop.TopAnchor.position;
                loop.TopAnchorVelocity = Vector3.zero;
                loop.RopeLengthRate = 0f;
            }

            demoStartedAt = Time.fixedTime;
            demoCompleted = !runAutomatedDemo;
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
            runtimeStatus = "Eight-rope prototype reset and pre-tensioning";
            Debug.Log(
                $"[YC_PROTOTYPE_RESET] model={ImplementationVersion}, gantry={gantryDrive.PositionMeters:F3}m, " +
                $"x={trolleyMotion.localPosition.x:F3}m, " +
                $"drop={commandedSuspendedDrop:F3}m, containerLocked={containerLocked}, " +
                $"instance={GetInstanceID()}, scene={gameObject.scene.name}, resetEpoch={resetEpoch}",
                this);
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        private void ResetContainerLoadState()
        {
            var sourceSlotToRestore = pickedUpSourceSlot;
            ParkContainerFreeBody();
            containerCollider.enabled = true;
            containerColliderSuppressedForPickup = false;
            if (activeContainer.parent != containerOriginalParent)
            {
                activeContainer.SetParent(containerOriginalParent, true);
            }
            activeContainer.SetPositionAndRotation(containerHomePosition, containerHomeRotation);
            containerFreeBody.SetPositionAndRotation(containerHomePosition, containerHomeRotation);
            SetContainerHeadblockCollisionIgnored(false);
            if (sourceSlotToRestore != null && container != null && sourceSlotToRestore.CanAccept(container))
            {
                sourceSlotToRestore.SetOccupant(container);
                Debug.Log(
                    $"[YC_YARD_SOURCE_RESTORED_ON_RESET] slot={sourceSlotToRestore.SlotId}, " +
                    $"container={container.ContainerId}",
                    this);
            }
            containerLocked = false;
            releasedContainerSettlementPending = false;
            releasedContainerStableSince = -1f;
            pickedUpSourceSlot = null;
            detectedPickupContainer = null;
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

        // internal (not private): shared with per-mechanism presenter classes extracted out of
        // this file, e.g. YardCraneWheelClampPresenter, so HUD colors stay defined in one place.
        internal const string KeyStatusColorGreen = "3ECF57";
        internal const string KeyStatusColorAmber = "F2B233";
        internal const string KeyStatusColorRed = "E5484D";
        internal const string KeyStatusColorNeutral = "B9C2CC";
        internal static string Colorize(string text, string hexColor) => $"<color=#{hexColor}>{text}</color>";

        private void OnDestroy()
        {
            // Saved here (rather than hooked into the Bootstrap scene-state machine's
            // terminal states) because the Rig only exists in the formal YardCrane scene while
            // Bootstrap is a separate, persistent DontDestroyOnLoad object across scene
            // switches - tying this to OnDestroy avoids fragile cross-scene handoff logic
            // for what is deliberately a v1/local-only persistence pass. loopsReady guards
            // against writing an empty record when setup failed before anything ran.
            if (loopsReady)
            {
                try
                {
                    var save = YardCraneTrainingScoreRecorder.SaveWithResult(trainingScore);
                    Debug.Log(
                        $"[YC_TRAINING_RUN_SAVED] path={save.Path}, outcome={save.Outcome}, " +
                        $"scoreTrust={save.ScoreTrustLevel}, hasScore={save.HasScore}, " +
                        $"identityContextMatched={save.IdentityContextMatched}, " +
                        $"countsTowardResults={save.CountsTowardResults}, " +
                        $"totalScore={save.TotalScore}, violations={trainingScore.Violations.Count}", this);
                }
                catch (Exception error)
                {
                    Debug.LogError($"[YC_TRAINING_SCORE_SAVE_FAILED] {error}", this);
                }
            }

            if (containerImpactSensor != null)
            {
                containerImpactSensor.ImpactDetected -= HandleContainerImpact;
            }
            SetContainerHeadblockCollisionIgnored(false);
            if (runtimeHornClip != null)
            {
                Destroy(runtimeHornClip);
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static float SmootherStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

    }
}
