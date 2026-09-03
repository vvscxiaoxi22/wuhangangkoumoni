using System;
using PortTraining.YardCraneSimulator.Operations;
using UnityEditor;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    public static class YardCraneOperationControllerSelfTest
    {
        private const string AutomaticRunKey = "PortTraining.YC.OperationSelfTest.v16";

        [InitializeOnLoadMethod]
        private static void RunOnceAfterReload()
        {
            if (SessionState.GetBool(AutomaticRunKey, false))
            {
                return;
            }

            SessionState.SetBool(AutomaticRunKey, true);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/PortTraining/Run Operation State Machine Self-Test")]
        public static void Run()
        {
            var actuator = new FakeActuator();
            var controller = new YardCraneOperationController(actuator);
            var runId = Guid.NewGuid();
            var leaseId = Guid.NewGuid();
            var sceneId = Guid.NewGuid();
            var streamId = Guid.NewGuid();
            var context = new YardCraneOperationContext(
                runId,
                leaseId,
                9,
                sceneId,
                "YC-SELFTEST-01",
                YardCraneControlMode.Local,
                false);

            Require(controller.TryActivateRun(context, 0, out var error), $"activate:{error}");
            Require(controller.TryOpenInputStream(runId, leaseId, 9, streamId, 0, out error), $"stream:{error}");
            Require(controller.Phase == YardCraneOperationPhase.AlignedEmpty, $"initial-phase:{controller.Phase}");

            var staleWarningInput = CreateInput(runId, leaseId, streamId, 1, 150, 0.5f, 0f);
            Require(controller.TryApplyRealtimeInput(staleWarningInput, 10, out error), $"stale-warning:{error}");
            Require(controller.LastInputIsStale, "stale-warning-not-observable");
            Require(controller.Phase == YardCraneOperationPhase.MovingEmpty, $"moving-empty:{controller.Phase}");
            Require(Math.Abs(actuator.TrolleyInput - 0.5f) < 0.0001f, "motion-not-forwarded");

            Require(!controller.TryApplyRealtimeInput(staleWarningInput, 11, out error), "duplicate-sequence-accepted");
            Require(error == YardCraneOperationErrorCodes.InputSequenceRejected, $"duplicate-sequence:{error}");

            var movingLockAction = new YardCraneOperationAction(
                runId, streamId, Guid.NewGuid(), 1, YardCraneOperationActionType.SpreaderLock, "Locked");
            var movingLock = controller.SubmitAction(movingLockAction);
            Require(movingLock.Status == YardCraneActionAckStatus.Rejected, "moving-lock-accepted");
            Require(movingLock.ErrorCode == YardCraneOperationErrorCodes.LockInterlockRejected,
                $"moving-lock:{movingLock.ErrorCode}");
            Require(actuator.LockRequestCount == 0, "moving-lock-reached-actuator");
            var movingLockDuplicate = controller.SubmitAction(movingLockAction);
            Require(movingLockDuplicate.Status == YardCraneActionAckStatus.Duplicate,
                "rejected-duplicate-not-detected");
            Require(movingLockDuplicate.OriginalStatus == YardCraneActionAckStatus.Rejected &&
                    !movingLockDuplicate.IsAccepted,
                $"rejected-duplicate-origin:{movingLockDuplicate.OriginalStatus}");

            var zeroInput = CreateInput(runId, leaseId, streamId, 2, 0, 0f, 0f);
            Require(controller.TryApplyRealtimeInput(zeroInput, 12, out error), $"center-input:{error}");
            Require(!controller.LastInputIsStale, "fresh-input-did-not-clear-warning");

            actuator.Stationary = true;
            var actionId = Guid.NewGuid();
            var lockAction = new YardCraneOperationAction(
                runId,
                streamId,
                actionId,
                2,
                YardCraneOperationActionType.SpreaderLock,
                "Locked");
            var firstAck = controller.SubmitAction(lockAction);
            Require(firstAck.Status == YardCraneActionAckStatus.Accepted, $"lock:{firstAck.ErrorCode}");
            Require(actuator.LockRequestCount == 1, $"lock-count:{actuator.LockRequestCount}");

            var duplicateAck = controller.SubmitAction(lockAction);
            Require(duplicateAck.Status == YardCraneActionAckStatus.Duplicate, $"duplicate:{duplicateAck.Status}");
            Require(duplicateAck.OriginalStatus == YardCraneActionAckStatus.Accepted && duplicateAck.IsAccepted,
                $"accepted-duplicate-origin:{duplicateAck.OriginalStatus}");
            Require(actuator.LockRequestCount == 1, $"duplicate-executed:{actuator.LockRequestCount}");

            var conflictAction = new YardCraneOperationAction(
                runId,
                streamId,
                actionId,
                3,
                YardCraneOperationActionType.SpreaderLock,
                "Unlocked");
            var conflictAck = controller.SubmitAction(conflictAction);
            Require(conflictAck.Status == YardCraneActionAckStatus.Rejected, "conflict-not-rejected");
            Require(conflictAck.ErrorCode == YardCraneOperationErrorCodes.ActionIdConflict, $"conflict:{conflictAck.ErrorCode}");
            Require(actuator.LockRequestCount == 1, $"conflict-executed:{actuator.LockRequestCount}");

            actuator.ContainerLocked = true;
            actuator.Busy = false;
            controller.Tick();
            Require(controller.Phase == YardCraneOperationPhase.ReadyLoaded, $"ready-loaded:{controller.Phase}");

            var invalidRangeInput = CreateInput(runId, leaseId, streamId, 3, 0, 1.01f, 0f);
            Require(!controller.TryApplyRealtimeInput(invalidRangeInput, 20, out error), "invalid-range-accepted");
            Require(error == YardCraneOperationErrorCodes.ContractValidationFailed, $"invalid-range:{error}");
            Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"safe-hold:{controller.Phase}");
            Require(actuator.ClearCount > 0 && actuator.TrolleyInput == 0f && actuator.HoistInput == 0f, "safe-hold-output-not-zero");

            Require(!controller.TryClearSafeHold(
                    runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, false, 30, out error),
                "safe-hold-cleared-with-handles-active");
            Require(error == YardCraneOperationErrorCodes.SafeHoldActive, $"center-interlock:{error}");
            Require(controller.TryClearSafeHold(
                    runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 31, out error),
                $"safe-hold-recovery:{error}");

            var dangerousBoundary = CreateInput(runId, leaseId, streamId, 3, 250, 0f, 0f);
            Require(!controller.TryApplyRealtimeInput(dangerousBoundary, 32, out error),
                "250ms-boundary-accepted");
            Require(error == YardCraneOperationErrorCodes.HeartbeatTimeout, $"250ms-boundary:{error}");
            Require(controller.TryClearSafeHold(
                    runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 35, out error),
                $"boundary-recovery:{error}");

            controller.RecordOwnerHeartbeat(35);
            controller.RecordCoordinatorHeartbeat(35);
            var modeMismatchInput = new YardCraneRealtimeInput(
                runId, leaseId, 9, streamId, 3, 0, YardCraneInputQuality.Good,
                YardCraneControlMode.Remote, true, true, true, 0f, 0f, 0f);
            Require(!controller.TryApplyRealtimeInput(modeMismatchInput, 36, out error), "mode-mismatch-accepted");
            Require(error == YardCraneOperationErrorCodes.ControlModeMismatch, $"mode-mismatch:{error}");
            Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"mode-safe-hold:{controller.Phase}");

            Require(controller.TryClearSafeHold(
                    runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 40, out error),
                $"second-recovery:{error}");

            var wrongLeaseInput = new YardCraneRealtimeInput(
                runId, Guid.NewGuid(), 9, streamId, 3, 0, YardCraneInputQuality.Good,
                YardCraneControlMode.Local, true, true, true, 0f, 0f, 0f);
            Require(!controller.TryApplyRealtimeInput(wrongLeaseInput, 41, out error), "wrong-lease-accepted");
            Require(error == YardCraneOperationErrorCodes.ControlLeaseConflict, $"lease:{error}");
            Require(controller.TryClearSafeHold(
                    runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 50, out error),
                $"lease-recovery:{error}");

            var plcOfflineInput = new YardCraneRealtimeInput(
                runId, leaseId, 9, streamId, 3, 0, YardCraneInputQuality.Good,
                YardCraneControlMode.Local, false, true, true, 0f, 0f, 0f);
            Require(!controller.TryApplyRealtimeInput(plcOfflineInput, 51, out error), "plc-offline-accepted");
            Require(error == YardCraneOperationErrorCodes.PlcOffline, $"plc:{error}");
            Require(controller.TryClearSafeHold(
                    runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 60, out error),
                $"plc-recovery:{error}");

            controller.RecordOwnerHeartbeat(60);
            controller.RecordCoordinatorHeartbeat(60);
            Require(!controller.EvaluateWatchdogs(311, out error), "stale-input-did-not-safe-hold");
            Require(error == YardCraneOperationErrorCodes.HeartbeatTimeout, $"watchdog:{error}");
            Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"watchdog-phase:{controller.Phase}");

            controller.ReleaseRun();
            Require(controller.Phase == YardCraneOperationPhase.WaitingForRun, $"release:{controller.Phase}");
            Require(!controller.IsActive && !controller.IsInputStreamOpen, "release-authority-not-cleared");

            Debug.Log(
                "[YC_OPERATION_STATE_MACHINE_SELFTEST_PASS] " +
                "accepted=True, duplicateExecutedOnce=True, rejectedDuplicatePreserved=True, " +
                "actionConflict=True, sequenceGuard=True, dangerBoundary250ms=True, " +
                "centeredActionInterlock=True, safeHoldZero=True, centeredRecovery=True, " +
                "modeMismatch=True, leaseInvalid=True, plcOffline=True, watchdog=True");
        }

        private static YardCraneRealtimeInput CreateInput(
            Guid runId,
            Guid leaseId,
            Guid streamId,
            long sequence,
            int dataAgeMs,
            float trolley,
            float hoist)
        {
            return new YardCraneRealtimeInput(
                runId,
                leaseId,
                9,
                streamId,
                sequence,
                dataAgeMs,
                dataAgeMs > YardCraneOperationController.StaleWarningThresholdMs
                    ? YardCraneInputQuality.Stale
                    : YardCraneInputQuality.Good,
                YardCraneControlMode.Local,
                true,
                true,
                true,
                0f,
                trolley,
                hoist);
        }

        private static void Require(bool condition, string failure)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"[YC_OPERATION_STATE_MACHINE_SELFTEST_FAIL] {failure}");
            }
        }

        private sealed class FakeActuator : IYardCraneOperationActuator
        {
            public bool Initialized = true;
            public bool PreTensioning;
            public bool LoadTransfer;
            public bool Busy;
            public bool Faulted;
            public bool Stationary = true;
            public bool ContainerLocked;
            public bool LandingConfirmed;
            public bool Aligned = true;
            public int LockRequestCount;
            public int UnlockRequestCount;
            public int ResetCount;
            public int ClearCount;
            public float GantryInput;
            public float TrolleyInput;
            public float HoistInput;

            bool IYardCraneOperationActuator.IsInitialized => Initialized;
            bool IYardCraneOperationActuator.IsPreTensioning => PreTensioning;
            bool IYardCraneOperationActuator.IsLoadTransferActive => LoadTransfer;
            bool IYardCraneOperationActuator.IsMechanismBusy => Busy;
            bool IYardCraneOperationActuator.IsFaulted => Faulted;
            bool IYardCraneOperationActuator.IsStationary => Stationary;
            bool IYardCraneOperationActuator.IsContainerLocked => ContainerLocked;
            bool IYardCraneOperationActuator.IsContainerLandingConfirmed => LandingConfirmed;
            bool IYardCraneOperationActuator.IsAlignedForContainerLock => Aligned;
            bool IYardCraneOperationActuator.IsWheelClampClamped => false;
            bool IYardCraneOperationActuator.IsWheelClampTransitioning => false;
            bool IYardCraneOperationActuator.IsTrolleyAnchorApplied => false;
            bool IYardCraneOperationActuator.IsTrolleyAnchorTransitioning => false;
            bool IYardCraneOperationActuator.IsGantryAnchorApplied => false;
            bool IYardCraneOperationActuator.IsGantryAnchorTransitioning => false;
            bool IYardCraneOperationActuator.IsSpreaderPumpOn => false;
            bool IYardCraneOperationActuator.IsSpreaderSizeTransitioning => false;

            bool IYardCraneOperationActuator.TryQueueSpreaderRotationReturnToZero(out string rejectionCode)
            {
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            void IYardCraneOperationActuator.SetSpreaderRotationInput(float rotation)
            {
            }

            bool IYardCraneOperationActuator.TryQueueContainerLock(out string rejectionCode)
            {
                LockRequestCount++;
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryQueueContainerUnlock(out string rejectionCode)
            {
                UnlockRequestCount++;
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryReset(out string rejectionCode)
            {
                ResetCount++;
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryQueueWheelClamp(bool clampApplied, out string rejectionCode)
            {
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryQueueTrolleyAnchor(bool anchorApplied, out string rejectionCode)
            {
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryQueueGantryAnchor(bool anchorApplied, out string rejectionCode)
            {
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryQueueSpreaderPump(bool pumpOn, out string rejectionCode)
            {
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            bool IYardCraneOperationActuator.TryQueueSpreaderSize(string targetSize, out string rejectionCode)
            {
                rejectionCode = YardCraneOperationErrorCodes.None;
                return true;
            }

            void IYardCraneOperationActuator.SetMotionInputs(float gantry, float trolley, float hoist)
            {
                GantryInput = gantry;
                TrolleyInput = trolley;
                HoistInput = hoist;
            }

            void IYardCraneOperationActuator.SetHornActive(bool active)
            {
            }

            void IYardCraneOperationActuator.ClearMotionInputs()
            {
                ClearCount++;
                GantryInput = 0f;
                TrolleyInput = 0f;
                HoistInput = 0f;
            }
        }
    }
}
