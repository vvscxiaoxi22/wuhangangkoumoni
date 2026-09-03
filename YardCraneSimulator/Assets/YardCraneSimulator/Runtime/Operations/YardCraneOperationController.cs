using System;
using System.Collections.Generic;

namespace PortTraining.YardCraneSimulator.Operations
{
    /// <summary>
    /// Scene-local operation phases. These phases describe mechanism execution and
    /// deliberately do not replace the process lifecycle in YardCraneSceneStateMachine.
    /// </summary>
    public enum YardCraneOperationPhase
    {
        WaitingForRun = 0,
        Initializing = 1,
        PreTensioning = 2,
        ReadyEmpty = 3,
        AlignedEmpty = 4,
        MovingEmpty = 5,
        LockPending = 6,
        LoadTransfer = 7,
        ReadyLoaded = 8,
        HoistingLoaded = 9,
        TrolleyTravelLoaded = 10,
        LandingConfirmed = 11,
        UnlockPending = 12,
        SafeHold = 13,
        Faulted = 14,
        WheelClampTransition = 15,
        AnchorTransition = 16,
        SpreaderSizeTransition = 17
    }

    public enum YardCraneControlMode
    {
        Local = 0,
        Remote = 1
    }

    public enum YardCraneInputQuality
    {
        Good = 0,
        Stale = 1,
        Invalid = 2
    }

    public enum YardCraneOperationActionType
    {
        SpreaderLock = 0,
        SpreaderUnlock = 1,
        SpreaderSizeChange = 2,
        GuidePlateChange = 3,
        SpreaderPumpChange = 4,
        Reset = 5,
        WheelClampChange = 6,
        TrolleyAnchorChange = 7,
        GantryAnchorChange = 8,
        SpreaderRotationReturnToZero = 9
    }

    public enum YardCraneActionAckStatus
    {
        Accepted = 0,
        Rejected = 1,
        Duplicate = 2
    }

    public static class YardCraneOperationErrorCodes
    {
        public const string None = "";
        public const string ContractValidationFailed = "CONTRACT_VALIDATION_FAILED";
        public const string ActionIdConflict = "ACTION_ID_CONFLICT";
        public const string ControlLeaseConflict = "CONTROL_LEASE_CONFLICT";
        public const string ControlModeMismatch = "CONTROL_MODE_MISMATCH";
        public const string PlcOffline = "PLC_OFFLINE";
        public const string InputSourceUnavailable = "INPUT_SOURCE_UNAVAILABLE";
        public const string HeartbeatTimeout = "HEARTBEAT_TIMEOUT";
        public const string OperationRunMismatch = "OPERATION_RUN_MISMATCH";
        public const string InputStreamMismatch = "INPUT_STREAM_MISMATCH";
        public const string InputSequenceRejected = "INPUT_SEQUENCE_REJECTED";
        public const string SafeHoldActive = "SAFE_HOLD_ACTIVE";
        public const string MechanismNotReady = "MECHANISM_NOT_READY";
        public const string MechanismBusy = "MECHANISM_BUSY";
        public const string LockInterlockRejected = "LOCK_INTERLOCK_REJECTED";
        public const string UnlockInterlockRejected = "UNLOCK_INTERLOCK_REJECTED";
        public const string ResetInterlockRejected = "RESET_INTERLOCK_REJECTED";
        public const string WheelClampInterlockRejected = "WHEEL_CLAMP_INTERLOCK_REJECTED";
        public const string AnchorInterlockRejected = "ANCHOR_INTERLOCK_REJECTED";
        public const string SpreaderSizeInterlockRejected = "SPREADER_SIZE_INTERLOCK_REJECTED";
        public const string SpreaderPumpInterlockRejected = "SPREADER_PUMP_INTERLOCK_REJECTED";
        public const string SpreaderRotationInterlockRejected = "SPREADER_ROTATION_INTERLOCK_REJECTED";
    }

    /// <summary>
    /// Internal scene context mapped from the signed RunTicket and active Gateway lease.
    /// It is not a second wire-contract DTO.
    /// </summary>
    public readonly struct YardCraneOperationContext
    {
        public YardCraneOperationContext(
            Guid operationRunId,
            Guid leaseId,
            long leaseEpoch,
            Guid sceneInstanceId,
            string stationId,
            YardCraneControlMode controlMode,
            bool isAuthoritative)
        {
            OperationRunId = operationRunId;
            LeaseId = leaseId;
            LeaseEpoch = leaseEpoch;
            SceneInstanceId = sceneInstanceId;
            StationId = stationId ?? string.Empty;
            ControlMode = controlMode;
            IsAuthoritative = isAuthoritative;
        }

        public Guid OperationRunId { get; }
        public Guid LeaseId { get; }
        public long LeaseEpoch { get; }
        public Guid SceneInstanceId { get; }
        public string StationId { get; }
        public YardCraneControlMode ControlMode { get; }
        public bool IsAuthoritative { get; }

        public bool IsValid =>
            OperationRunId != Guid.Empty &&
            LeaseId != Guid.Empty &&
            LeaseEpoch > 0 &&
            SceneInstanceId != Guid.Empty &&
            !string.IsNullOrWhiteSpace(StationId);
    }

    public readonly struct YardCraneRealtimeInput
    {
        public YardCraneRealtimeInput(
            Guid operationRunId,
            Guid leaseId,
            long leaseEpoch,
            Guid streamId,
            long sequence,
            int dataAgeMs,
            YardCraneInputQuality quality,
            YardCraneControlMode controlMode,
            bool plcOnline,
            bool emergencyStopReleased,
            bool controlPowerOn,
            float gantryCommand,
            float trolleyCommand,
            float hoistCommand,
            bool hornPressed = false,
            float rotationCommand = 0f)
        {
            OperationRunId = operationRunId;
            LeaseId = leaseId;
            LeaseEpoch = leaseEpoch;
            StreamId = streamId;
            Sequence = sequence;
            DataAgeMs = dataAgeMs;
            Quality = quality;
            ControlMode = controlMode;
            PlcOnline = plcOnline;
            EmergencyStopReleased = emergencyStopReleased;
            ControlPowerOn = controlPowerOn;
            GantryCommand = gantryCommand;
            TrolleyCommand = trolleyCommand;
            HoistCommand = hoistCommand;
            HornPressed = hornPressed;
            RotationCommand = rotationCommand;
        }

        public Guid OperationRunId { get; }
        public Guid LeaseId { get; }
        public long LeaseEpoch { get; }
        public Guid StreamId { get; }
        public long Sequence { get; }
        public int DataAgeMs { get; }
        public YardCraneInputQuality Quality { get; }
        public YardCraneControlMode ControlMode { get; }
        public bool PlcOnline { get; }
        public bool EmergencyStopReleased { get; }
        public bool ControlPowerOn { get; }
        public float GantryCommand { get; }
        public float TrolleyCommand { get; }
        public float HoistCommand { get; }
        public bool HornPressed { get; }
        public float RotationCommand { get; }
    }

    public readonly struct YardCraneOperationAction
    {
        public YardCraneOperationAction(
            Guid operationRunId,
            Guid streamId,
            Guid actionId,
            long actionSequence,
            YardCraneOperationActionType actionType,
            string targetState)
        {
            OperationRunId = operationRunId;
            StreamId = streamId;
            ActionId = actionId;
            ActionSequence = actionSequence;
            ActionType = actionType;
            TargetState = targetState ?? string.Empty;
        }

        public Guid OperationRunId { get; }
        public Guid StreamId { get; }
        public Guid ActionId { get; }
        public long ActionSequence { get; }
        public YardCraneOperationActionType ActionType { get; }
        public string TargetState { get; }
    }

    public readonly struct YardCraneActionAck
    {
        public YardCraneActionAck(
            Guid operationRunId,
            Guid actionId,
            YardCraneActionAckStatus status,
            string errorCode,
            YardCraneOperationPhase phase,
            string detail)
            : this(operationRunId, actionId, status, status, errorCode, phase, detail)
        {
        }

        public YardCraneActionAck(
            Guid operationRunId,
            Guid actionId,
            YardCraneActionAckStatus status,
            YardCraneActionAckStatus originalStatus,
            string errorCode,
            YardCraneOperationPhase phase,
            string detail)
        {
            OperationRunId = operationRunId;
            ActionId = actionId;
            Status = status;
            OriginalStatus = originalStatus;
            ErrorCode = errorCode ?? string.Empty;
            Phase = phase;
            Detail = detail ?? string.Empty;
        }

        public Guid OperationRunId { get; }
        public Guid ActionId { get; }
        public YardCraneActionAckStatus Status { get; }
        public YardCraneActionAckStatus OriginalStatus { get; }
        public string ErrorCode { get; }
        public YardCraneOperationPhase Phase { get; }
        public string Detail { get; }
        public bool IsAccepted => Status == YardCraneActionAckStatus.Accepted ||
                                  (Status == YardCraneActionAckStatus.Duplicate &&
                                   OriginalStatus == YardCraneActionAckStatus.Accepted);
    }

    public interface IYardCraneOperationActuator
    {
        bool IsInitialized { get; }
        bool IsPreTensioning { get; }
        bool IsLoadTransferActive { get; }
        bool IsMechanismBusy { get; }
        bool IsFaulted { get; }
        bool IsStationary { get; }
        bool IsContainerLocked { get; }
        bool IsContainerLandingConfirmed { get; }
        bool IsAlignedForContainerLock { get; }
        bool IsWheelClampClamped { get; }
        bool IsWheelClampTransitioning { get; }
        bool IsTrolleyAnchorApplied { get; }
        bool IsTrolleyAnchorTransitioning { get; }
        bool IsGantryAnchorApplied { get; }
        bool IsGantryAnchorTransitioning { get; }
        bool IsSpreaderPumpOn { get; }
        bool IsSpreaderSizeTransitioning { get; }

        bool TryQueueContainerLock(out string rejectionCode);
        bool TryQueueContainerUnlock(out string rejectionCode);
        bool TryQueueWheelClamp(bool clampApplied, out string rejectionCode);
        bool TryQueueTrolleyAnchor(bool anchorApplied, out string rejectionCode);
        bool TryQueueGantryAnchor(bool anchorApplied, out string rejectionCode);
        bool TryQueueSpreaderPump(bool pumpOn, out string rejectionCode);
        bool TryQueueSpreaderSize(string targetSize, out string rejectionCode);
        bool TryQueueSpreaderRotationReturnToZero(out string rejectionCode);
        bool TryReset(out string rejectionCode);
        void SetMotionInputs(float gantry, float trolley, float hoist);
        void SetHornActive(bool active);
        void SetSpreaderRotationInput(float rotation);
        void ClearMotionInputs();
    }

    /// <summary>
    /// Owns the scene-side execution authority for one operation run. Transport and
    /// JSON serialization remain the responsibility of the future PortTraining.Contracts adapter.
    /// </summary>
    public sealed class YardCraneOperationController
    {
        public const int StaleWarningThresholdMs = 100;
        public const int SafeHoldThresholdMs = 250;
        public const int OwnerHeartbeatTimeoutMs = 2000;
        public const int CoordinatorHeartbeatTimeoutMs = 5000;

        private readonly IYardCraneOperationActuator actuator;
        private readonly Dictionary<Guid, ProcessedAction> processedActions = new();
        private YardCraneOperationContext context;
        private Guid activeStreamId;
        private long lastInputSequence = -1;
        private long lastActionSequence = -1;
        private long lastInputReceivedAtMs = -1;
        private long lastOwnerHeartbeatAtMs = -1;
        private long lastCoordinatorHeartbeatAtMs = -1;
        private float lastGantryCommand;
        private float lastTrolleyCommand;
        private float lastHoistCommand;
        private string safeHoldReason = string.Empty;
        private bool active;
        private bool inputStreamOpen;
        private bool lastInputIsStale;

        public YardCraneOperationController(IYardCraneOperationActuator actuator)
        {
            this.actuator = actuator ?? throw new ArgumentNullException(nameof(actuator));
        }

        public YardCraneOperationPhase Phase { get; private set; } = YardCraneOperationPhase.WaitingForRun;
        public YardCraneOperationContext Context => context;
        public Guid ActiveStreamId => activeStreamId;
        public long LastInputSequence => lastInputSequence;
        public string SafeHoldReason => safeHoldReason;
        public bool IsActive => active;
        public bool IsInputStreamOpen => inputStreamOpen;
        public bool LastInputIsStale => lastInputIsStale;
        public bool IsWheelClampClamped => actuator.IsWheelClampClamped;
        public bool IsWheelClampTransitioning => actuator.IsWheelClampTransitioning;
        public bool IsTrolleyAnchorApplied => actuator.IsTrolleyAnchorApplied;
        public bool IsTrolleyAnchorTransitioning => actuator.IsTrolleyAnchorTransitioning;
        public bool IsGantryAnchorApplied => actuator.IsGantryAnchorApplied;
        public bool IsGantryAnchorTransitioning => actuator.IsGantryAnchorTransitioning;
        public bool IsSpreaderPumpOn => actuator.IsSpreaderPumpOn;

        public event Action<YardCraneOperationPhase, YardCraneOperationPhase, string> PhaseChanged;
        public event Action<int, YardCraneInputQuality> InputBecameStale;

        public bool TryActivateRun(YardCraneOperationContext next, long monotonicNowMs, out string errorCode)
        {
            if (!next.IsValid || monotonicNowMs < 0)
            {
                errorCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                return false;
            }

            if (active)
            {
                errorCode = context.OperationRunId == next.OperationRunId &&
                            context.LeaseId == next.LeaseId &&
                            context.LeaseEpoch == next.LeaseEpoch &&
                            context.SceneInstanceId == next.SceneInstanceId &&
                            string.Equals(context.StationId, next.StationId, StringComparison.Ordinal) &&
                            context.ControlMode == next.ControlMode
                    ? YardCraneOperationErrorCodes.None
                    : YardCraneOperationErrorCodes.ControlLeaseConflict;
                return errorCode.Length == 0;
            }

            context = next;
            active = true;
            inputStreamOpen = false;
            activeStreamId = Guid.Empty;
            lastInputSequence = -1;
            lastActionSequence = -1;
            lastInputReceivedAtMs = monotonicNowMs;
            lastOwnerHeartbeatAtMs = monotonicNowMs;
            lastCoordinatorHeartbeatAtMs = monotonicNowMs;
            lastGantryCommand = 0f;
            lastTrolleyCommand = 0f;
            lastHoistCommand = 0f;
            safeHoldReason = string.Empty;
            lastInputIsStale = false;
            processedActions.Clear();
            actuator.ClearMotionInputs();
            SetPhase(actuator.IsInitialized ? YardCraneOperationPhase.PreTensioning : YardCraneOperationPhase.Initializing, "run-activated");
            Tick();
            errorCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        public void ReleaseRun()
        {
            actuator.ClearMotionInputs();
            context = default;
            active = false;
            inputStreamOpen = false;
            activeStreamId = Guid.Empty;
            lastInputSequence = -1;
            lastActionSequence = -1;
            lastInputReceivedAtMs = -1;
            lastOwnerHeartbeatAtMs = -1;
            lastCoordinatorHeartbeatAtMs = -1;
            lastGantryCommand = 0f;
            lastTrolleyCommand = 0f;
            lastHoistCommand = 0f;
            safeHoldReason = string.Empty;
            lastInputIsStale = false;
            processedActions.Clear();
            SetPhase(YardCraneOperationPhase.WaitingForRun, "run-released");
        }

        public bool TryOpenInputStream(
            Guid operationRunId,
            Guid leaseId,
            long leaseEpoch,
            Guid streamId,
            long monotonicNowMs,
            out string errorCode)
        {
            if (!active || operationRunId != context.OperationRunId)
            {
                errorCode = YardCraneOperationErrorCodes.OperationRunMismatch;
                return false;
            }

            if (leaseId != context.LeaseId || leaseEpoch != context.LeaseEpoch)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.ControlLeaseConflict);
                errorCode = YardCraneOperationErrorCodes.ControlLeaseConflict;
                return false;
            }

            if (streamId == Guid.Empty || monotonicNowMs < 0)
            {
                errorCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                return false;
            }

            activeStreamId = streamId;
            inputStreamOpen = true;
            lastInputSequence = -1;
            lastActionSequence = -1;
            lastInputReceivedAtMs = monotonicNowMs;
            actuator.ClearMotionInputs();
            errorCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        public bool TryApplyRealtimeInput(
            YardCraneRealtimeInput input,
            long monotonicNowMs,
            out string errorCode)
        {
            if (!TryValidateInputContext(input, out errorCode))
            {
                return false;
            }

            if (input.Sequence <= lastInputSequence)
            {
                errorCode = YardCraneOperationErrorCodes.InputSequenceRejected;
                return false;
            }

            if (monotonicNowMs < 0 || input.DataAgeMs < 0 ||
                input.Quality == YardCraneInputQuality.Invalid)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.ContractValidationFailed);
                errorCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                return false;
            }

            if (input.DataAgeMs >= SafeHoldThresholdMs)
            {
                lastInputIsStale = true;
                EnterSafeHold(YardCraneOperationErrorCodes.HeartbeatTimeout);
                errorCode = YardCraneOperationErrorCodes.HeartbeatTimeout;
                return false;
            }

            if (!input.PlcOnline)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.PlcOffline);
                errorCode = YardCraneOperationErrorCodes.PlcOffline;
                return false;
            }

            if (!input.EmergencyStopReleased || !input.ControlPowerOn)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.SafeHoldActive);
                errorCode = YardCraneOperationErrorCodes.SafeHoldActive;
                return false;
            }

            if (!IsNormalized(input.GantryCommand) || !IsNormalized(input.TrolleyCommand) ||
                !IsNormalized(input.HoistCommand) || !IsNormalized(input.RotationCommand))
            {
                EnterSafeHold(YardCraneOperationErrorCodes.ContractValidationFailed);
                errorCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                return false;
            }

            if (Phase == YardCraneOperationPhase.SafeHold || Phase == YardCraneOperationPhase.Faulted)
            {
                actuator.ClearMotionInputs();
                errorCode = YardCraneOperationErrorCodes.SafeHoldActive;
                return false;
            }

            lastInputSequence = input.Sequence;
            lastInputReceivedAtMs = monotonicNowMs;
            var inputIsStale = input.DataAgeMs >= StaleWarningThresholdMs ||
                               input.Quality == YardCraneInputQuality.Stale;
            if (inputIsStale && !lastInputIsStale)
            {
                InputBecameStale?.Invoke(input.DataAgeMs, input.Quality);
            }
            lastInputIsStale = inputIsStale;
            if (!actuator.IsInitialized || actuator.IsPreTensioning || actuator.IsMechanismBusy)
            {
                lastGantryCommand = 0f;
                lastTrolleyCommand = 0f;
                lastHoistCommand = 0f;
                actuator.ClearMotionInputs();
                Tick();
                errorCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            lastGantryCommand = input.GantryCommand;
            lastTrolleyCommand = input.TrolleyCommand;
            lastHoistCommand = input.HoistCommand;
            actuator.SetHornActive(input.HornPressed);
            actuator.SetMotionInputs(input.GantryCommand, input.TrolleyCommand, input.HoistCommand);
            actuator.SetSpreaderRotationInput(input.RotationCommand);
            Tick();
            errorCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        public YardCraneActionAck SubmitAction(YardCraneOperationAction action)
        {
            if (!active || action.OperationRunId != context.OperationRunId)
            {
                return Reject(action, YardCraneOperationErrorCodes.OperationRunMismatch, "operation-run-mismatch");
            }

            if (!inputStreamOpen || action.StreamId != activeStreamId)
            {
                return Reject(action, YardCraneOperationErrorCodes.InputStreamMismatch, "input-stream-mismatch");
            }

            if (action.ActionId != Guid.Empty && processedActions.TryGetValue(action.ActionId, out var processed))
            {
                if (!processed.Matches(action))
                {
                    if (action.ActionSequence > lastActionSequence)
                    {
                        lastActionSequence = action.ActionSequence;
                    }
                    return Reject(action, YardCraneOperationErrorCodes.ActionIdConflict, "action-id-content-conflict");
                }

                var original = processed.Ack;
                return new YardCraneActionAck(
                    original.OperationRunId,
                    original.ActionId,
                    YardCraneActionAckStatus.Duplicate,
                    original.Status,
                    original.ErrorCode,
                    Phase,
                    $"duplicate-of-{original.Status}");
            }

            var validationError = ValidateAction(action);
            if (validationError.Length > 0)
            {
                return Remember(action, Reject(action, validationError, "action-validation-rejected"));
            }

            lastActionSequence = action.ActionSequence;

            if (Phase == YardCraneOperationPhase.SafeHold || Phase == YardCraneOperationPhase.Faulted)
            {
                return Remember(action, Reject(action, YardCraneOperationErrorCodes.SafeHoldActive, "operation-not-authorized"));
            }

            bool accepted;
            var rejectionCode = YardCraneOperationErrorCodes.None;
            YardCraneOperationPhase acceptedPhase;
            var motionCommandsCentered = Math.Abs(lastGantryCommand) <= 0.0001f &&
                                         Math.Abs(lastTrolleyCommand) <= 0.0001f &&
                                         Math.Abs(lastHoistCommand) <= 0.0001f;
            switch (action.ActionType)
            {
                case YardCraneOperationActionType.SpreaderLock:
                    accepted = motionCommandsCentered && actuator.TryQueueContainerLock(out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.LockInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.LockPending;
                    break;
                case YardCraneOperationActionType.SpreaderUnlock:
                    accepted = motionCommandsCentered && actuator.TryQueueContainerUnlock(out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.UnlockInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.UnlockPending;
                    break;
                case YardCraneOperationActionType.Reset:
                    accepted = motionCommandsCentered && actuator.TryReset(out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.ResetInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.PreTensioning;
                    break;
                case YardCraneOperationActionType.WheelClampChange:
                    accepted = motionCommandsCentered &&
                               actuator.TryQueueWheelClamp(action.TargetState == "Clamped", out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.WheelClampInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.WheelClampTransition;
                    break;
                case YardCraneOperationActionType.TrolleyAnchorChange:
                    accepted = motionCommandsCentered &&
                               actuator.TryQueueTrolleyAnchor(action.TargetState == "Anchored", out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.AnchorTransition;
                    break;
                case YardCraneOperationActionType.GantryAnchorChange:
                    accepted = motionCommandsCentered &&
                               actuator.TryQueueGantryAnchor(action.TargetState == "Anchored", out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.AnchorInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.AnchorTransition;
                    break;
                case YardCraneOperationActionType.SpreaderPumpChange:
                    accepted = motionCommandsCentered &&
                               actuator.TryQueueSpreaderPump(action.TargetState == "On", out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.SpreaderPumpInterlockRejected;
                    }
                    acceptedPhase = Phase;
                    break;
                case YardCraneOperationActionType.SpreaderSizeChange:
                    accepted = motionCommandsCentered &&
                               actuator.TryQueueSpreaderSize(action.TargetState, out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.SpreaderSizeInterlockRejected;
                    }
                    acceptedPhase = YardCraneOperationPhase.SpreaderSizeTransition;
                    break;
                case YardCraneOperationActionType.SpreaderRotationReturnToZero:
                    accepted = motionCommandsCentered &&
                               actuator.TryQueueSpreaderRotationReturnToZero(out rejectionCode);
                    if (!motionCommandsCentered)
                    {
                        rejectionCode = YardCraneOperationErrorCodes.SpreaderRotationInterlockRejected;
                    }
                    acceptedPhase = Phase;
                    break;
                default:
                    accepted = false;
                    rejectionCode = YardCraneOperationErrorCodes.ContractValidationFailed;
                    acceptedPhase = Phase;
                    break;
            }

            if (!accepted)
            {
                var code = string.IsNullOrWhiteSpace(rejectionCode)
                    ? YardCraneOperationErrorCodes.MechanismBusy
                    : rejectionCode;
                return Remember(action, Reject(action, code, "mechanism-interlock-rejected"));
            }

            SetPhase(acceptedPhase, $"action-{action.ActionType}-accepted");
            return Remember(
                action,
                new YardCraneActionAck(
                    context.OperationRunId,
                    action.ActionId,
                    YardCraneActionAckStatus.Accepted,
                    YardCraneOperationErrorCodes.None,
                    Phase,
                    "queued-for-fixed-step-execution"));
        }

        public void RecordOwnerHeartbeat(long monotonicNowMs)
        {
            if (active && monotonicNowMs >= 0)
            {
                lastOwnerHeartbeatAtMs = monotonicNowMs;
            }
        }

        public void RecordCoordinatorHeartbeat(long monotonicNowMs)
        {
            if (active && monotonicNowMs >= 0)
            {
                lastCoordinatorHeartbeatAtMs = monotonicNowMs;
            }
        }

        public bool EvaluateWatchdogs(long monotonicNowMs, out string errorCode)
        {
            errorCode = YardCraneOperationErrorCodes.None;
            if (!active || Phase == YardCraneOperationPhase.Faulted)
            {
                return true;
            }

            var inputTimedOut = lastInputReceivedAtMs < 0 ||
                                monotonicNowMs - lastInputReceivedAtMs >= SafeHoldThresholdMs;
            if (inputTimedOut ||
                lastOwnerHeartbeatAtMs < 0 || monotonicNowMs - lastOwnerHeartbeatAtMs > OwnerHeartbeatTimeoutMs ||
                lastCoordinatorHeartbeatAtMs < 0 || monotonicNowMs - lastCoordinatorHeartbeatAtMs > CoordinatorHeartbeatTimeoutMs)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.HeartbeatTimeout);
                errorCode = YardCraneOperationErrorCodes.HeartbeatTimeout;
                return false;
            }

            return true;
        }

        public bool NotifyInputUnavailable(
            Guid operationRunId,
            Guid leaseId,
            long leaseEpoch,
            string detail,
            out string errorCode)
        {
            if (!active || operationRunId != context.OperationRunId)
            {
                errorCode = YardCraneOperationErrorCodes.OperationRunMismatch;
                return false;
            }

            if (leaseId != context.LeaseId || leaseEpoch != context.LeaseEpoch)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.ControlLeaseConflict);
                errorCode = YardCraneOperationErrorCodes.ControlLeaseConflict;
                return false;
            }

            EnterSafeHold(YardCraneOperationErrorCodes.InputSourceUnavailable);
            errorCode = YardCraneOperationErrorCodes.InputSourceUnavailable;
            return true;
        }

        public bool TryClearSafeHold(
            Guid operationRunId,
            Guid leaseId,
            long leaseEpoch,
            YardCraneControlMode controlMode,
            bool plcOnline,
            bool emergencyStopReleased,
            bool controlPowerOn,
            bool handlesCentered,
            long monotonicNowMs,
            out string errorCode)
        {
            if (!active || operationRunId != context.OperationRunId)
            {
                errorCode = YardCraneOperationErrorCodes.OperationRunMismatch;
                return false;
            }

            if (leaseId != context.LeaseId || leaseEpoch != context.LeaseEpoch)
            {
                errorCode = YardCraneOperationErrorCodes.ControlLeaseConflict;
                return false;
            }

            if (controlMode != context.ControlMode)
            {
                errorCode = YardCraneOperationErrorCodes.ControlModeMismatch;
                return false;
            }

            if (Phase != YardCraneOperationPhase.SafeHold || !plcOnline || !emergencyStopReleased ||
                !controlPowerOn || !handlesCentered || !actuator.IsStationary || monotonicNowMs < 0)
            {
                errorCode = YardCraneOperationErrorCodes.SafeHoldActive;
                return false;
            }

            lastInputReceivedAtMs = monotonicNowMs;
            lastOwnerHeartbeatAtMs = monotonicNowMs;
            lastCoordinatorHeartbeatAtMs = monotonicNowMs;
            lastGantryCommand = 0f;
            lastTrolleyCommand = 0f;
            lastHoistCommand = 0f;
            safeHoldReason = string.Empty;
            lastInputIsStale = false;
            actuator.ClearMotionInputs();
            SetPhase(YardCraneOperationPhase.Initializing, "safe-hold-cleared");
            Tick();
            errorCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        public void Tick()
        {
            if (!active || Phase == YardCraneOperationPhase.SafeHold || Phase == YardCraneOperationPhase.Faulted)
            {
                return;
            }

            if (actuator.IsFaulted)
            {
                actuator.ClearMotionInputs();
                SetPhase(YardCraneOperationPhase.Faulted, "actuator-fault");
                return;
            }

            if (!actuator.IsInitialized)
            {
                SetPhase(YardCraneOperationPhase.Initializing, "actuator-initializing");
                return;
            }

            if (actuator.IsPreTensioning)
            {
                SetPhase(YardCraneOperationPhase.PreTensioning, "rope-pre-tensioning");
                return;
            }

            if (actuator.IsLoadTransferActive)
            {
                SetPhase(YardCraneOperationPhase.LoadTransfer, "load-transfer");
                return;
            }

            if (actuator.IsWheelClampTransitioning)
            {
                SetPhase(YardCraneOperationPhase.WheelClampTransition, "wheel-clamp-transition");
                return;
            }

            if (Phase == YardCraneOperationPhase.LockPending && actuator.IsMechanismBusy)
            {
                return;
            }

            if (actuator.IsMechanismBusy)
            {
                actuator.ClearMotionInputs();
                SetPhase(YardCraneOperationPhase.Initializing, "mechanism-busy");
                return;
            }

            if (Phase == YardCraneOperationPhase.UnlockPending && actuator.IsMechanismBusy)
            {
                return;
            }

            if (actuator.IsContainerLocked)
            {
                if (actuator.IsContainerLandingConfirmed)
                {
                    SetPhase(YardCraneOperationPhase.LandingConfirmed, "landing-confirmed");
                }
                else if (Math.Abs(lastHoistCommand) > 0.0001f)
                {
                    SetPhase(YardCraneOperationPhase.HoistingLoaded, "loaded-hoist-command");
                }
                else if (Math.Abs(lastGantryCommand) > 0.0001f || Math.Abs(lastTrolleyCommand) > 0.0001f)
                {
                    SetPhase(YardCraneOperationPhase.TrolleyTravelLoaded, "loaded-trolley-command");
                }
                else
                {
                    SetPhase(YardCraneOperationPhase.ReadyLoaded, "loaded-stationary");
                }
                return;
            }

            if (Math.Abs(lastGantryCommand) > 0.0001f ||
                Math.Abs(lastTrolleyCommand) > 0.0001f ||
                Math.Abs(lastHoistCommand) > 0.0001f)
            {
                SetPhase(YardCraneOperationPhase.MovingEmpty, "empty-motion-command");
            }
            else if (actuator.IsAlignedForContainerLock)
            {
                SetPhase(YardCraneOperationPhase.AlignedEmpty, "container-aligned");
            }
            else
            {
                SetPhase(YardCraneOperationPhase.ReadyEmpty, "empty-stationary");
            }
        }

        private bool TryValidateInputContext(YardCraneRealtimeInput input, out string errorCode)
        {
            if (!active || input.OperationRunId != context.OperationRunId)
            {
                errorCode = YardCraneOperationErrorCodes.OperationRunMismatch;
                return false;
            }

            if (input.LeaseId != context.LeaseId || input.LeaseEpoch != context.LeaseEpoch)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.ControlLeaseConflict);
                errorCode = YardCraneOperationErrorCodes.ControlLeaseConflict;
                return false;
            }

            if (input.ControlMode != context.ControlMode)
            {
                EnterSafeHold(YardCraneOperationErrorCodes.ControlModeMismatch);
                errorCode = YardCraneOperationErrorCodes.ControlModeMismatch;
                return false;
            }

            if (!inputStreamOpen || input.StreamId != activeStreamId)
            {
                errorCode = YardCraneOperationErrorCodes.InputStreamMismatch;
                return false;
            }

            errorCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        private string ValidateAction(YardCraneOperationAction action)
        {
            if (action.ActionId == Guid.Empty ||
                action.ActionSequence < 0 || action.ActionSequence <= lastActionSequence)
            {
                return YardCraneOperationErrorCodes.ContractValidationFailed;
            }

            return action.ActionType switch
            {
                YardCraneOperationActionType.SpreaderLock when action.TargetState == "Locked" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.SpreaderUnlock when action.TargetState == "Unlocked" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.Reset when action.TargetState.Length == 0 => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.SpreaderSizeChange when action.TargetState is "FT20" or "FT40" or "Twin20" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.GuidePlateChange when action.TargetState.Length > 0 => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.SpreaderPumpChange when action.TargetState is "On" or "Off" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.WheelClampChange when action.TargetState is "Released" or "Clamped" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.TrolleyAnchorChange when action.TargetState is "Released" or "Anchored" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.GantryAnchorChange when action.TargetState is "Released" or "Anchored" => YardCraneOperationErrorCodes.None,
                YardCraneOperationActionType.SpreaderRotationReturnToZero when action.TargetState.Length == 0 => YardCraneOperationErrorCodes.None,
                _ => YardCraneOperationErrorCodes.ContractValidationFailed
            };
        }

        private void EnterSafeHold(string reason)
        {
            lastGantryCommand = 0f;
            lastTrolleyCommand = 0f;
            lastHoistCommand = 0f;
            safeHoldReason = reason;
            actuator.ClearMotionInputs();
            SetPhase(YardCraneOperationPhase.SafeHold, reason);
        }

        private YardCraneActionAck Remember(YardCraneOperationAction action, YardCraneActionAck ack)
        {
            if (action.ActionId != Guid.Empty)
            {
                processedActions[action.ActionId] = new ProcessedAction(action, ack);
            }
            return ack;
        }

        private YardCraneActionAck Reject(YardCraneOperationAction action, string errorCode, string detail)
        {
            return new YardCraneActionAck(
                action.OperationRunId,
                action.ActionId,
                YardCraneActionAckStatus.Rejected,
                errorCode,
                Phase,
                detail);
        }

        private void SetPhase(YardCraneOperationPhase next, string reason)
        {
            if (Phase == next)
            {
                return;
            }

            var previous = Phase;
            Phase = next;
            PhaseChanged?.Invoke(previous, next, reason ?? string.Empty);
        }

        private static bool IsNormalized(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value >= -1f && value <= 1f;
        }

        private readonly struct ProcessedAction
        {
            private readonly YardCraneOperationActionType actionType;
            private readonly string targetState;

            public ProcessedAction(YardCraneOperationAction action, YardCraneActionAck ack)
            {
                actionType = action.ActionType;
                targetState = action.TargetState;
                Ack = ack;
            }

            public YardCraneActionAck Ack { get; }

            public bool Matches(YardCraneOperationAction action)
            {
                return actionType == action.ActionType &&
                       string.Equals(targetState, action.TargetState, StringComparison.Ordinal);
            }
        }
    }
}
