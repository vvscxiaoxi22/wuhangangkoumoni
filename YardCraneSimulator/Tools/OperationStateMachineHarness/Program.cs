using System;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;

internal static class Program
{
    private static int Main()
    {
        try
        {
            Run();
            Console.WriteLine(
                "[YC_OPERATION_STATE_MACHINE_SELFTEST_PASS] " +
                "accepted=True duplicateExecutedOnce=True rejectedDuplicatePreserved=True " +
                "actionConflict=True runIsolation=True " +
                "sequenceGuard=True staleWarningAccepted=True dangerBoundary250ms=True " +
                "centeredActionInterlock=True " +
                "safeHoldZero=True centeredRecovery=True modeMismatch=True leaseInvalid=True " +
                "plcOffline=True watchdog=True inputSourceLoss=True " +
                "inputAuthorityLock=True centeredDeviceSwitch=True activeDisconnectZero=True " +
                "standaloneLaunch=True managedNoFallback=True gantryInterlock=True " +
                "hornRealtime=True wheelClampAction=True gantryDrive=True gantryLimit=True " +
                "gantryBrake=True containerContactInterlock=True cabinSafetyChain=True " +
                "activeSourceIntentPreserved=True anchorInterlock=True cabinAnchorAction=True " +
                "spreaderSizeInterlock=True spreaderPumpAndSizeAction=True " +
                "spreaderRotationModel=True spreaderRotationAction=True");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"[YC_OPERATION_STATE_MACHINE_SELFTEST_FAIL] {exception.Message}");
            return 1;
        }
    }

    private static void Run()
    {
        var actuator = new FakeActuator();
        VerifyLaunchModes();
        VerifyInputRouting();
        VerifyActiveSourceSafetyIntentPreserved();
        VerifyGantryDrive();
        VerifyGantryBrakeState();
        VerifyAnchorInterlock();
        VerifySpreaderSizeInterlock();
        VerifySpreaderRotationModel();
        VerifyContainerApproachInterlock();
        VerifyControlPowerAndEmergencyStopSafety();
        var controller = new YardCraneOperationController(actuator);
        var runId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var context = new YardCraneOperationContext(
            runId, leaseId, 9, sceneId, "YC-SELFTEST-01", YardCraneControlMode.Local, false);

        Require(controller.TryActivateRun(context, 0, out var error), $"activate:{error}");
        Require(controller.TryOpenInputStream(runId, leaseId, 9, streamId, 0, out error), $"stream:{error}");
        Require(controller.Phase == YardCraneOperationPhase.AlignedEmpty, $"initial:{controller.Phase}");

        var staleWarningInput = Input(runId, leaseId, streamId, 1, 150, 0.5f, 0f, YardCraneControlMode.Local);
        Require(controller.TryApplyRealtimeInput(staleWarningInput, 10, out error), $"stale-warning:{error}");
        Require(controller.LastInputIsStale, "stale-warning-not-observable");
        Require(controller.Phase == YardCraneOperationPhase.MovingEmpty, $"moving:{controller.Phase}");
        Require(actuator.TrolleyInput == 0.5f, "motion-not-forwarded");
        Require(!controller.TryApplyRealtimeInput(staleWarningInput, 11, out error), "duplicate-sequence-accepted");
        Require(error == YardCraneOperationErrorCodes.InputSequenceRejected, $"sequence:{error}");

        var movingLockAction = new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 1, YardCraneOperationActionType.SpreaderLock, "Locked");
        var movingLock = controller.SubmitAction(movingLockAction);
        Require(movingLock.Status == YardCraneActionAckStatus.Rejected, "moving-lock-accepted");
        Require(movingLock.ErrorCode == YardCraneOperationErrorCodes.LockInterlockRejected,
            $"moving-lock:{movingLock.ErrorCode}");
        Require(actuator.LockCount == 0, "moving-lock-reached-actuator");
        var movingLockDuplicate = controller.SubmitAction(movingLockAction);
        Require(movingLockDuplicate.Status == YardCraneActionAckStatus.Duplicate, "rejected-duplicate-not-detected");
        Require(movingLockDuplicate.OriginalStatus == YardCraneActionAckStatus.Rejected,
            $"rejected-duplicate-origin:{movingLockDuplicate.OriginalStatus}");
        Require(!movingLockDuplicate.IsAccepted, "rejected-duplicate-reported-accepted");

        var zeroInput = Input(runId, leaseId, streamId, 2, 0, 0f, 0f, YardCraneControlMode.Local);
        Require(controller.TryApplyRealtimeInput(zeroInput, 12, out error), $"center-input:{error}");
        Require(!controller.LastInputIsStale, "fresh-input-did-not-clear-warning");

        var actionId = Guid.NewGuid();
        var lockAction = new YardCraneOperationAction(
            runId, streamId, actionId, 2, YardCraneOperationActionType.SpreaderLock, "Locked");
        var accepted = controller.SubmitAction(lockAction);
        Require(accepted.Status == YardCraneActionAckStatus.Accepted, $"lock:{accepted.ErrorCode}");
        var duplicate = controller.SubmitAction(lockAction);
        Require(duplicate.Status == YardCraneActionAckStatus.Duplicate, $"duplicate:{duplicate.Status}");
        Require(duplicate.OriginalStatus == YardCraneActionAckStatus.Accepted && duplicate.IsAccepted,
            $"accepted-duplicate-origin:{duplicate.OriginalStatus}");
        Require(actuator.LockCount == 1, $"duplicate-executed:{actuator.LockCount}");

        var conflict = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, actionId, 3, YardCraneOperationActionType.SpreaderLock, "Unlocked"));
        Require(conflict.ErrorCode == YardCraneOperationErrorCodes.ActionIdConflict, $"conflict:{conflict.ErrorCode}");
        Require(actuator.LockCount == 1, $"conflict-executed:{actuator.LockCount}");

        var wrongRunDuplicate = controller.SubmitAction(new YardCraneOperationAction(
            Guid.NewGuid(), streamId, actionId, 1, YardCraneOperationActionType.SpreaderLock, "Locked"));
        Require(wrongRunDuplicate.Status == YardCraneActionAckStatus.Rejected, "cross-run-duplicate-accepted");
        Require(wrongRunDuplicate.ErrorCode == YardCraneOperationErrorCodes.OperationRunMismatch,
            $"cross-run:{wrongRunDuplicate.ErrorCode}");

        actuator.ContainerLocked = true;
        controller.Tick();
        Require(controller.Phase == YardCraneOperationPhase.ReadyLoaded, $"loaded:{controller.Phase}");

        var invalidRange = Input(runId, leaseId, streamId, 3, 0, 1.01f, 0f, YardCraneControlMode.Local);
        Require(!controller.TryApplyRealtimeInput(invalidRange, 20, out error), "invalid-range-accepted");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"range-safe-hold:{controller.Phase}");
        Require(actuator.ClearCount > 0 && actuator.TrolleyInput == 0f && actuator.HoistInput == 0f,
            "safe-hold-output-not-zero");

        Require(!controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, false, 30, out error),
            "recovered-with-active-handles");
        Require(controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 31, out error),
            $"recovery:{error}");

        var dangerousBoundary = Input(runId, leaseId, streamId, 3, 250, 0f, 0f, YardCraneControlMode.Local);
        Require(!controller.TryApplyRealtimeInput(dangerousBoundary, 32, out error), "250ms-boundary-accepted");
        Require(error == YardCraneOperationErrorCodes.HeartbeatTimeout, $"250ms-boundary:{error}");
        Require(controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 35, out error),
            $"boundary-recovery:{error}");

        controller.RecordOwnerHeartbeat(35);
        controller.RecordCoordinatorHeartbeat(35);
        var modeMismatch = Input(runId, leaseId, streamId, 3, 0, 0f, 0f, YardCraneControlMode.Remote);
        Require(!controller.TryApplyRealtimeInput(modeMismatch, 36, out error), "mode-mismatch-accepted");
        Require(error == YardCraneOperationErrorCodes.ControlModeMismatch, $"mode:{error}");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"mode-safe-hold:{controller.Phase}");

        Require(controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 40, out error),
            $"second-recovery:{error}");

        var wrongLease = new YardCraneRealtimeInput(
            runId, Guid.NewGuid(), 9, streamId, 3, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0f, 0f);
        Require(!controller.TryApplyRealtimeInput(wrongLease, 41, out error), "wrong-lease-accepted");
        Require(error == YardCraneOperationErrorCodes.ControlLeaseConflict, $"lease:{error}");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"lease-safe-hold:{controller.Phase}");
        Require(controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 50, out error),
            $"lease-recovery:{error}");

        var plcOffline = new YardCraneRealtimeInput(
            runId, leaseId, 9, streamId, 3, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, false, true, true, 0f, 0f, 0f);
        Require(!controller.TryApplyRealtimeInput(plcOffline, 51, out error), "plc-offline-accepted");
        Require(error == YardCraneOperationErrorCodes.PlcOffline, $"plc:{error}");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"plc-safe-hold:{controller.Phase}");
        Require(controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 60, out error),
            $"plc-recovery:{error}");

        Require(controller.NotifyInputUnavailable(runId, leaseId, 9, "device-disconnected", out error),
            $"input-source-loss:{error}");
        Require(error == YardCraneOperationErrorCodes.InputSourceUnavailable,
            $"input-source-loss-code:{error}");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, "input-source-loss-not-safe-hold");
        Require(controller.TryClearSafeHold(
            runId, leaseId, 9, YardCraneControlMode.Local, true, true, true, true, 61, out error),
            $"input-source-recovery:{error}");

        var gantryInput = new YardCraneRealtimeInput(
            runId, leaseId, 9, streamId, 3, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 1f, 0f, 0f, true);
        Require(controller.TryApplyRealtimeInput(gantryInput, 62, out error), $"gantry-input:{error}");
        Require(actuator.GantryInput == 1f, "gantry-not-forwarded");
        Require(actuator.HornActive, "horn-not-forwarded");
        var gantryReset = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 4, YardCraneOperationActionType.Reset, string.Empty));
        Require(gantryReset.Status == YardCraneActionAckStatus.Rejected, "gantry-moving-reset-accepted");
        Require(gantryReset.ErrorCode == YardCraneOperationErrorCodes.ResetInterlockRejected,
            $"gantry-reset:{gantryReset.ErrorCode}");
        var finalZero = new YardCraneRealtimeInput(
            runId, leaseId, 9, streamId, 4, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0f, 0f, false);
        Require(controller.TryApplyRealtimeInput(finalZero, 63, out error), $"final-zero:{error}");
        Require(!actuator.HornActive, "horn-not-cleared");

        var clampAction = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 5, YardCraneOperationActionType.WheelClampChange, "Clamped"));
        Require(clampAction.Status == YardCraneActionAckStatus.Accepted,
            $"wheel-clamp:{clampAction.ErrorCode}");
        Require(actuator.WheelClampCount == 1 && actuator.ClampApplied,
            "wheel-clamp-not-forwarded");

        var trolleyAnchorAction = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 6, YardCraneOperationActionType.TrolleyAnchorChange, "Anchored"));
        Require(trolleyAnchorAction.Status == YardCraneActionAckStatus.Accepted,
            $"trolley-anchor:{trolleyAnchorAction.ErrorCode}");
        Require(actuator.TrolleyAnchorCount == 1 && actuator.TrolleyAnchorApplied,
            "trolley-anchor-not-forwarded");
        Require(controller.IsTrolleyAnchorApplied, "trolley-anchor-not-reflected-on-controller");

        var gantryAnchorAction = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 7, YardCraneOperationActionType.GantryAnchorChange, "Anchored"));
        Require(gantryAnchorAction.Status == YardCraneActionAckStatus.Accepted,
            $"gantry-anchor:{gantryAnchorAction.ErrorCode}");
        Require(actuator.GantryAnchorCount == 1 && actuator.GantryAnchorApplied,
            "gantry-anchor-not-forwarded");
        Require(controller.IsGantryAnchorApplied, "gantry-anchor-not-reflected-on-controller");

        var pumpAction = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 8, YardCraneOperationActionType.SpreaderPumpChange, "On"));
        Require(pumpAction.Status == YardCraneActionAckStatus.Accepted,
            $"spreader-pump:{pumpAction.ErrorCode}");
        Require(actuator.PumpRequestCount == 1 && actuator.PumpOn, "spreader-pump-not-forwarded");
        Require(controller.IsSpreaderPumpOn, "spreader-pump-not-reflected-on-controller");

        var sizeAction = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 9, YardCraneOperationActionType.SpreaderSizeChange, "FT20"));
        Require(sizeAction.Status == YardCraneActionAckStatus.Accepted,
            $"spreader-size:{sizeAction.ErrorCode}");
        Require(actuator.SizeRequestCount == 1 && actuator.LastRequestedSize == "FT20",
            "spreader-size-not-forwarded");

        var rotationInput = new YardCraneRealtimeInput(
            runId, leaseId, 9, streamId, 5, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0f, 0f, false, 1f);
        Require(controller.TryApplyRealtimeInput(rotationInput, 63, out error), $"rotation-input:{error}");
        Require(actuator.RotationInput == 1f, "rotation-input-not-forwarded");
        var rotationZeroInput = new YardCraneRealtimeInput(
            runId, leaseId, 9, streamId, 6, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0f, 0f, false, 0f);
        Require(controller.TryApplyRealtimeInput(rotationZeroInput, 63, out error), $"rotation-zero-input:{error}");

        var rotationReturnAction = controller.SubmitAction(new YardCraneOperationAction(
            runId, streamId, Guid.NewGuid(), 10, YardCraneOperationActionType.SpreaderRotationReturnToZero,
            string.Empty));
        Require(rotationReturnAction.Status == YardCraneActionAckStatus.Accepted,
            $"spreader-rotation-return:{rotationReturnAction.ErrorCode}");
        Require(actuator.ReturnToZeroRequestCount == 1, "spreader-rotation-return-not-forwarded");

        controller.RecordOwnerHeartbeat(63);
        controller.RecordCoordinatorHeartbeat(63);
        Require(!controller.EvaluateWatchdogs(314, out error), "watchdog-did-not-trip");
        Require(error == YardCraneOperationErrorCodes.HeartbeatTimeout, $"watchdog:{error}");

        controller.ReleaseRun();
        Require(controller.Phase == YardCraneOperationPhase.WaitingForRun, $"release:{controller.Phase}");
        Require(!controller.IsActive && !controller.IsInputStreamOpen, "release-authority-retained");
    }

    private static void VerifyLaunchModes()
    {
        Require(SceneLaunchOptions.TryParseRuntime(
                new[] { "YardCraneSimulator.exe" },
                _ => string.Empty,
                "YC-EIGHT-ROPE-MAX-V1",
                false,
                out var standalone,
                out var error),
            $"standalone-launch:{error}");
        Require(standalone.LaunchMode == YardCraneLaunchMode.Standalone, $"standalone-mode:{standalone.LaunchMode}");

        Require(!SceneLaunchOptions.TryParseRuntime(
                new[] { "YardCraneSimulator.exe", "--operation-run-id", Guid.NewGuid().ToString("D") },
                _ => string.Empty,
                "YC-EIGHT-ROPE-MAX-V1",
                false,
                out _,
                out error),
            "incomplete-managed-launch-fell-back-to-standalone");
        Require(error.Contains("Required startup argument", StringComparison.Ordinal), $"managed-error:{error}");
    }

    private static void VerifyInputRouting()
    {
        var router = new YardCraneInputRouter();
        var keyboardMoving = new YardCraneInputSnapshot(
            "KEYBOARD", true, 0f, 1f, 0f, false, false, false, false);
        var keyboardCentered = new YardCraneInputSnapshot(
            "KEYBOARD", true, 0f, 0f, 0f, false, false, false, false);
        var gamepadMoving = new YardCraneInputSnapshot(
            "XBOX_GAMEPAD", true, -0.5f, 0f, 0f, false, false, false, false);

        var first = router.Route(new[] { keyboardMoving, gamepadMoving }, 1);
        Require(first.Command.SourceId == "KEYBOARD", $"initial-source:{first.Command.SourceId}");
        var locked = router.Route(new[] { keyboardMoving, gamepadMoving }, 2);
        Require(locked.Command.SourceId == "KEYBOARD", $"authority-stolen:{locked.Command.SourceId}");
        var switched = router.Route(new[] { keyboardCentered, gamepadMoving }, 3);
        Require(switched.Command.SourceId == "XBOX_GAMEPAD", $"centered-switch:{switched.Command.SourceId}");
        var disconnected = router.Route(new[]
        {
            keyboardCentered,
            new YardCraneInputSnapshot("XBOX_GAMEPAD", false, 0f, 0f, 0f, false, false, false, false)
        }, 4);
        Require(disconnected.ActiveSourceLost, "active-disconnect-not-detected");
        Require(disconnected.Command.HandlesCentered, "active-disconnect-not-zero");

        Require(!YardCraneOperatorCommand.Zero(1, 0).HasIntent, "zero-command-has-intent");

        // The three new discrete cabin-safety intents must survive routing unchanged, and
        // must be able to establish input authority like the existing lock/unlock/pause keys.
        var cabinSafetyIntents = new YardCraneInputSnapshot(
            "KEYBOARD", true, 0f, 0f, 0f, false, false, false, false,
            false, true, true, true);
        var cabinSafetyRoute = router.Route(new[] { cabinSafetyIntents }, 5);
        Require(cabinSafetyRoute.Command.SourceId == "KEYBOARD",
            $"cabin-safety-intent-source:{cabinSafetyRoute.Command.SourceId}");
        Require(cabinSafetyRoute.Command.ControlPowerTogglePressed &&
                cabinSafetyRoute.Command.EmergencyStopTogglePressed &&
                cabinSafetyRoute.Command.FaultResetPressed,
            "cabin-safety-intents-lost-in-routing");
    }

    /// <summary>
    /// Regression for the R1 review finding: a discrete safety intent (e-stop toggle, control
    /// power toggle, fault reset) pending on the currently active, axis-centered source must
    /// not be dropped just because another source has motion this same frame. Before the fix,
    /// <see cref="YardCraneInputRouter"/> handed authority away whenever
    /// <c>active.HandlesCentered</c> was true, ignoring any pending discrete intent; authority
    /// may only move once the active source has zero intent at all (<c>HasIntent == false</c>).
    /// </summary>
    private static void VerifyActiveSourceSafetyIntentPreserved()
    {
        var router = new YardCraneInputRouter();
        var keyboardMoving = new YardCraneInputSnapshot(
            "KEYBOARD", true, 0f, 1f, 0f, false, false, false, false);
        var established = router.Route(new[] { keyboardMoving }, 1);
        Require(established.Command.SourceId == "KEYBOARD",
            $"safety-preempt-setup:{established.Command.SourceId}");

        // Keyboard handles are centered but an e-stop toggle is pending; Xbox has a nonzero
        // motion axis this same frame. Before the fix, HandlesCentered alone would hand
        // authority to Xbox and silently drop the keyboard's e-stop intent.
        var keyboardEstopCentered = new YardCraneInputSnapshot(
            "KEYBOARD", true, 0f, 0f, 0f, false, false, false, false,
            false, false, true, false);
        var gamepadMoving = new YardCraneInputSnapshot(
            "XBOX_GAMEPAD", true, -0.6f, 0f, 0f, false, false, false, false);
        var preempted = router.Route(new[] { keyboardEstopCentered, gamepadMoving }, 2);
        Require(preempted.Command.SourceId == "KEYBOARD",
            $"safety-intent-preempted-source:{preempted.Command.SourceId}");
        Require(preempted.Command.EmergencyStopTogglePressed, "safety-intent-preempted-estop-lost");
        Require(preempted.Command.GantryAxis == 0f && preempted.Command.TrolleyAxis == 0f &&
                preempted.Command.HoistAxis == 0f,
            "safety-intent-preempted-xbox-motion-leaked");

        // Next frame: keyboard is fully idle (centered and no discrete intent at all). Only
        // now may authority move to the still-moving Xbox source.
        var keyboardIdle = new YardCraneInputSnapshot(
            "KEYBOARD", true, 0f, 0f, 0f, false, false, false, false);
        var released = router.Route(new[] { keyboardIdle, gamepadMoving }, 3);
        Require(released.Command.SourceId == "XBOX_GAMEPAD",
            $"safety-intent-idle-switch:{released.Command.SourceId}");
        Require(released.Command.GantryAxis == -0.6f, "safety-intent-idle-switch-axis-lost");
    }

    private static void VerifyGantryDrive()
    {
        var configuration = new YardCraneGantryDriveConfiguration(
            2f, 1f, 2f, 3f, 0.1f, 0.2f, 0.01f);
        var drive = new YardCraneGantryDriveModel(configuration);

        var step = drive.Step(1f, 0.02f);
        for (var index = 0; index < 40; index++)
        {
            step = drive.Step(1f, 0.02f);
        }
        Require(step.PositionMeters > 0f && step.VelocityMetersPerSecond > 0f,
            "gantry-drive-did-not-accelerate");
        Require(!drive.TryRequestWheelClamp(true, out var clampError),
            "moving-wheel-clamp-accepted");
        Require(clampError == "WHEEL_CLAMP_MOVING_INTERLOCK", $"moving-clamp:{clampError}");

        for (var index = 0; index < 80; index++)
        {
            step = drive.Step(0f, 0.02f);
        }
        Require(drive.IsStationary, "gantry-drive-did-not-brake");
        Require(drive.TryRequestWheelClamp(true, out clampError), $"clamp-request:{clampError}");
        for (var index = 0; index < 11; index++)
        {
            step = drive.Step(0f, 0.02f);
        }
        Require(step.WheelClampState == WheelClampState.Clamped, $"clamp-state:{step.WheelClampState}");
        var clampedPosition = step.PositionMeters;
        step = drive.Step(1f, 0.02f);
        Require(step.MotionInhibitedByClamp && Math.Abs(step.PositionMeters - clampedPosition) < 0.0001f,
            "clamped-gantry-moved");

        Require(drive.TryRequestWheelClamp(false, out clampError), $"release-request:{clampError}");
        for (var index = 0; index < 11; index++)
        {
            drive.Step(0f, 0.02f);
        }
        Require(drive.WheelClampState == WheelClampState.Released, "wheel-clamp-did-not-release");

        drive.Reset(2.8f);
        for (var index = 0; index < 150; index++)
        {
            step = drive.Step(1f, 0.02f);
        }
        Require(step.PositionMeters <= 3f && step.ForwardLimitActive &&
                Math.Abs(step.VelocityMetersPerSecond) <= 0.0001f,
            $"gantry-forward-limit:{step.PositionMeters:F3}/{step.VelocityMetersPerSecond:F3}");
    }

    /// <summary>
    /// Pure-logic coverage for the storm-anchor state machine shared by the trolley and
    /// gantry anchors (YC-P0-05-ANCHOR-01). Mirrors the wheel-clamp portion of
    /// VerifyGantryDrive: reject while moving, accept and transition while stationary,
    /// stay released for a redundant release request, and support a fresh Reset.
    /// </summary>
    private static void VerifyAnchorInterlock()
    {
        var anchor = new YardCraneAnchorInterlock(0.2f);
        Require(anchor.State == YardCraneAnchorState.Released, $"anchor-initial-state:{anchor.State}");

        Require(!anchor.TryRequestAnchor(true, false, out var anchorError),
            "moving-anchor-request-accepted");
        Require(anchorError == "ANCHOR_MOVING_INTERLOCK", $"moving-anchor:{anchorError}");

        Require(anchor.TryRequestAnchor(true, true, out anchorError), $"anchor-request:{anchorError}");
        Require(anchor.State == YardCraneAnchorState.Transitioning, $"anchor-not-transitioning:{anchor.State}");
        Require(!anchor.TryRequestAnchor(false, true, out anchorError),
            "anchor-busy-request-accepted");
        Require(anchorError == "ANCHOR_BUSY", $"anchor-busy:{anchorError}");

        anchor.Advance(0.1f);
        Require(anchor.State == YardCraneAnchorState.Transitioning, "anchor-completed-early");
        anchor.Advance(0.2f);
        Require(anchor.State == YardCraneAnchorState.Anchored, $"anchor-did-not-complete:{anchor.State}");

        Require(anchor.TryRequestAnchor(false, true, out anchorError), $"anchor-release-request:{anchorError}");
        anchor.Advance(0.3f);
        Require(anchor.State == YardCraneAnchorState.Released, $"anchor-did-not-release:{anchor.State}");

        anchor.Reset(true);
        Require(anchor.State == YardCraneAnchorState.Anchored, $"anchor-reset-did-not-apply:{anchor.State}");
        anchor.Reset();
        Require(anchor.State == YardCraneAnchorState.Released, $"anchor-reset-default-not-released:{anchor.State}");
    }

    private static void VerifySpreaderSizeInterlock()
    {
        var size = new YardCraneSpreaderSizeInterlock(0.2f, YardCraneSpreaderSize.Ft40);
        Require(size.CurrentSize == YardCraneSpreaderSize.Ft40, $"size-initial:{size.CurrentSize}");
        Require(size.State == YardCraneSpreaderSizeMechanismState.Idle, $"size-initial-state:{size.State}");

        Require(!size.TryRequestSize(YardCraneSpreaderSize.Ft20, false, true, false, out var sizeError),
            "pump-off-request-accepted");
        Require(sizeError == "SPREADER_SIZE_PUMP_OFF", $"pump-off:{sizeError}");

        Require(!size.TryRequestSize(YardCraneSpreaderSize.Ft20, true, true, true, out sizeError),
            "loaded-request-accepted");
        Require(sizeError == "SPREADER_SIZE_LOADED_INTERLOCK", $"loaded:{sizeError}");

        Require(!size.TryRequestSize(YardCraneSpreaderSize.Ft20, true, false, false, out sizeError),
            "moving-request-accepted");
        Require(sizeError == "SPREADER_SIZE_MOVING_INTERLOCK", $"moving:{sizeError}");

        Require(size.TryRequestSize(YardCraneSpreaderSize.Ft40, true, true, false, out sizeError),
            $"same-size-request:{sizeError}");
        Require(size.State == YardCraneSpreaderSizeMechanismState.Idle, "same-size-request-started-transition");

        Require(size.TryRequestSize(YardCraneSpreaderSize.Ft20, true, true, false, out sizeError),
            $"size-request:{sizeError}");
        Require(size.State == YardCraneSpreaderSizeMechanismState.Transitioning,
            $"size-not-transitioning:{size.State}");
        Require(!size.TryRequestSize(YardCraneSpreaderSize.Ft40, true, true, false, out sizeError),
            "size-busy-request-accepted");
        Require(sizeError == "SPREADER_SIZE_BUSY", $"size-busy:{sizeError}");

        size.Advance(0.1f);
        Require(size.State == YardCraneSpreaderSizeMechanismState.Transitioning, "size-completed-early");
        size.Advance(0.2f);
        Require(size.State == YardCraneSpreaderSizeMechanismState.Idle &&
                size.CurrentSize == YardCraneSpreaderSize.Ft20,
            $"size-did-not-complete:{size.State}/{size.CurrentSize}");

        size.Reset(YardCraneSpreaderSize.Ft40);
        Require(size.CurrentSize == YardCraneSpreaderSize.Ft40 &&
                size.State == YardCraneSpreaderSizeMechanismState.Idle,
            $"size-reset-did-not-apply:{size.CurrentSize}/{size.State}");
    }

    private static void VerifySpreaderRotationModel()
    {
        var rotation = new YardCraneSpreaderRotationModel(3f, 1.5f, 1.0f);
        Require(rotation.CurrentAngleDegrees == 0f, $"rotation-initial:{rotation.CurrentAngleDegrees}");
        Require(!rotation.IsReturningToZero, "rotation-initial-returning");

        // Pump off: held input must not move the angle.
        rotation.Advance(1f, false, 0.5f);
        Require(rotation.CurrentAngleDegrees == 0f, $"rotation-moved-with-pump-off:{rotation.CurrentAngleDegrees}");

        // Pump on: holding right ramps the angle up, clamped at the configured max.
        rotation.Advance(1f, true, 1f);
        Require(Math.Abs(rotation.CurrentAngleDegrees - 1.5f) < 0.001f,
            $"rotation-rate-mismatch:{rotation.CurrentAngleDegrees}");
        rotation.Advance(1f, true, 10f);
        Require(rotation.CurrentAngleDegrees == 3f, $"rotation-not-clamped:{rotation.CurrentAngleDegrees}");

        // Pump-off request for return-to-zero must be rejected with the documented code.
        Require(!rotation.TryRequestReturnToZero(false, out var rotationError),
            "rotation-return-to-zero-accepted-with-pump-off");
        Require(rotationError == "SPREADER_ROTATION_PUMP_OFF", $"rotation-pump-off:{rotationError}");

        Require(rotation.TryRequestReturnToZero(true, out rotationError), $"rotation-return-request:{rotationError}");
        Require(rotation.IsReturningToZero, "rotation-not-returning");
        rotation.Advance(0f, true, 1f);
        Require(Math.Abs(rotation.CurrentAngleDegrees - 2f) < 0.001f,
            $"rotation-return-rate-mismatch:{rotation.CurrentAngleDegrees}");

        // Resuming a held direction mid-return must cancel the auto-return.
        rotation.Advance(-1f, true, 0.5f);
        Require(!rotation.IsReturningToZero, "rotation-return-not-cancelled-by-held-input");

        Require(rotation.TryRequestReturnToZero(true, out rotationError), $"rotation-return-request-2:{rotationError}");
        for (var index = 0; index < 20; index++)
        {
            rotation.Advance(0f, true, 0.5f);
        }
        Require(rotation.CurrentAngleDegrees == 0f && !rotation.IsReturningToZero,
            $"rotation-did-not-settle-at-zero:{rotation.CurrentAngleDegrees}/{rotation.IsReturningToZero}");

        rotation.Advance(1f, true, 1f);
        rotation.Reset();
        Require(rotation.CurrentAngleDegrees == 0f && !rotation.IsReturningToZero,
            $"rotation-reset-did-not-apply:{rotation.CurrentAngleDegrees}/{rotation.IsReturningToZero}");
    }

    private static void VerifyGantryBrakeState()
    {
        var configuration = new YardCraneGantryDriveConfiguration(
            2f, 1f, 2f, 3f, 0.1f, 0.2f, 0.01f);
        var drive = new YardCraneGantryDriveModel(configuration);

        Require(drive.IsBrakeApplied, "reset-not-braked");

        var step = drive.Step(1f, 0.02f);
        Require(!step.IsBrakeApplied && !drive.IsBrakeApplied, "valid-startup-still-braked");

        step = drive.Step(0f, 0.02f);
        Require(step.IsBrakeApplied && drive.IsBrakeApplied, "released-input-not-braked");

        for (var index = 0; index < 30; index++)
        {
            step = drive.Step(1f, 0.02f);
        }
        Require(!step.IsBrakeApplied && Math.Abs(step.VelocityMetersPerSecond) > 0.01f,
            "cruise-drive-still-braked");

        step = drive.Step(-1f, 0.02f);
        Require(step.IsBrakeApplied && drive.IsBrakeApplied, "reversal-not-braked");

        for (var index = 0; index < 200; index++)
        {
            step = drive.Step(0f, 0.02f);
        }
        Require(drive.IsStationary && step.IsBrakeApplied, "stationary-hold-not-braked");

        Require(drive.TryRequestWheelClamp(true, out var clampError), $"clamp-request:{clampError}");
        step = drive.Step(0f, 0.02f);
        Require(step.WheelClampState == WheelClampState.Transitioning && step.IsBrakeApplied,
            "clamp-transitioning-not-braked");

        for (var index = 0; index < 10; index++)
        {
            step = drive.Step(0f, 0.02f);
        }
        Require(step.WheelClampState == WheelClampState.Clamped && step.IsBrakeApplied,
            "clamp-applied-not-braked");

        step = drive.Step(1f, 0.02f);
        Require(step.MotionInhibitedByClamp && step.IsBrakeApplied, "clamp-inhibited-not-braked");

        Require(drive.TryRequestWheelClamp(false, out clampError), $"release-request:{clampError}");
        for (var index = 0; index < 11; index++)
        {
            step = drive.Step(0f, 0.02f);
        }
        Require(step.WheelClampState == WheelClampState.Released && step.IsBrakeApplied,
            "clamp-released-idle-not-braked");

        step = drive.Step(1f, 0.02f);
        Require(!step.IsBrakeApplied && !drive.IsBrakeApplied, "post-clamp-restart-still-braked");

        // Regression for the R1 P0 fix: holding a command into an already-reached travel limit
        // must not read as brake-released just because the (already zero) velocity has not
        // decreased any further. The effective target velocity must be the gate, not just the
        // velocity delta.
        drive.Reset(2.8f);
        for (var index = 0; index < 150; index++)
        {
            step = drive.Step(1f, 0.02f);
        }
        Require(step.ForwardLimitActive && Math.Abs(step.VelocityMetersPerSecond) <= 0.0001f,
            "gantry-brake-limit-setup-not-stopped");

        for (var index = 0; index < 20; index++)
        {
            step = drive.Step(1f, 0.02f);
        }
        Require(step.ForwardLimitActive &&
                Math.Abs(step.VelocityMetersPerSecond) <= 0.0001f &&
                step.IsBrakeApplied && drive.IsBrakeApplied,
            "gantry-brake-limit-hold-released");
    }

    private static void VerifyContainerApproachInterlock()
    {
        // Regression for the reported dead zone: the spreader can be horizontally near the
        // pickup center while still 0.285m above the box. That is alignment airspace, not contact.
        Require(!YardCraneContainerApproachInterlock.ShouldInhibit(
                -1f, -0.20f, -4.0f, -2.0f, 0.285f, 0.35f, 0.03f),
            "container-interlock-blocked-above-box-alignment");

        Require(YardCraneContainerApproachInterlock.ShouldInhibit(
                -1f, -4.42f, 0.02f, -0.10f, -0.05f, 0.35f, 0.03f),
            "container-interlock-missed-side-approach");
        Require(!YardCraneContainerApproachInterlock.ShouldInhibit(
                1f, -4.42f, 0.02f, -0.10f, -0.05f, 0.35f, 0.03f),
            "container-interlock-blocked-retreat");
        Require(!YardCraneContainerApproachInterlock.ShouldInhibit(
                -1f, -4.80f, 0.40f, -0.10f, -0.05f, 0.35f, 0.03f),
            "container-interlock-braked-before-stopping-envelope");
        Require(!YardCraneContainerApproachInterlock.ShouldInhibit(
                -1f, -4.42f, 0.02f, 0.10f, -0.05f, 0.35f, 0.03f),
            "container-interlock-blocked-clear-orthogonal-path");
        Require(YardCraneContainerApproachInterlock.ShouldInhibit(
                1f, 0f, -4.0f, -2.0f, 0.01f, 0.35f, 0.03f) &&
                YardCraneContainerApproachInterlock.ShouldInhibit(
                    -1f, 0f, -4.0f, -2.0f, 0.01f, 0.35f, 0.03f),
            "container-interlock-centered-landing-not-held");
        Require(!YardCraneContainerApproachInterlock.ShouldInhibit(
                0f, 0f, -4.0f, -2.0f, 0f, 0.35f, 0.03f),
            "container-interlock-zero-input-inhibited");
        Require(YardCraneContainerApproachInterlock.ShouldInhibit(
                1f, float.NaN, 0f, 0f, 0f, 0.35f, 0.03f),
            "container-interlock-nonfinite-not-failsafe");
    }

    /// <summary>
    /// Controller-level coverage for YC-P0-03-CABIN-01: control power and emergency stop are
    /// persistent safety signals (not a momentary pulse), SafeHold clearing must be rejected
    /// while either condition is unmet, and a successful explicit recovery must not resurrect
    /// the motion command that was active at the moment of the trip. This exercises
    /// YardCraneOperationController directly; it cannot exercise the Unity-only Standalone host
    /// input adapter, so keyboard/Xbox device behavior remains Unity Play-verification scope.
    /// </summary>
    private static void VerifyControlPowerAndEmergencyStopSafety()
    {
        var actuator = new FakeActuator();
        var controller = new YardCraneOperationController(actuator);
        var runId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var sceneId = Guid.NewGuid();
        var streamId = Guid.NewGuid();
        var context = new YardCraneOperationContext(
            runId, leaseId, 1, sceneId, "YC-SELFTEST-CABIN-01", YardCraneControlMode.Local, false);

        Require(controller.TryActivateRun(context, 0, out var error), $"cabin-activate:{error}");
        Require(controller.TryOpenInputStream(runId, leaseId, 1, streamId, 0, out error), $"cabin-stream:{error}");

        var moving = new YardCraneRealtimeInput(
            runId, leaseId, 1, streamId, 1, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0.6f, 0f);
        Require(controller.TryApplyRealtimeInput(moving, 10, out error), $"cabin-moving:{error}");
        Require(actuator.TrolleyInput == 0.6f, "cabin-moving-not-forwarded");

        // Control power disconnected must trip SafeHold and zero every axis and the horn
        // on the same frame, not just refuse the next command.
        var clearCountBeforePowerOff = actuator.ClearCount;
        var powerOff = new YardCraneRealtimeInput(
            runId, leaseId, 1, streamId, 2, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, false, 0f, 0.6f, 0f, true);
        Require(!controller.TryApplyRealtimeInput(powerOff, 11, out error), "cabin-power-off-accepted");
        Require(error == YardCraneOperationErrorCodes.SafeHoldActive, $"cabin-power-off-error:{error}");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"cabin-power-off-phase:{controller.Phase}");
        Require(actuator.ClearCount > clearCountBeforePowerOff, "cabin-power-off-not-cleared");
        Require(actuator.TrolleyInput == 0f && actuator.GantryInput == 0f && actuator.HoistInput == 0f &&
                !actuator.HornActive, "cabin-power-off-axes-not-zero");

        // Reset attempted while control power is still off must be rejected and stay in SafeHold.
        Require(!controller.TryClearSafeHold(
                runId, leaseId, 1, YardCraneControlMode.Local, true, true, false, true, 12, out error),
            "cabin-reset-while-power-off-accepted");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold,
            "cabin-reset-while-power-off-left-safe-hold");

        // Explicit reset with control power restored must succeed and must not resurrect the
        // 0.6 command that was active when the trip happened.
        Require(controller.TryClearSafeHold(
                runId, leaseId, 1, YardCraneControlMode.Local, true, true, true, true, 13, out error),
            $"cabin-reset-power-restored:{error}");
        Require(controller.Phase != YardCraneOperationPhase.SafeHold, "cabin-reset-power-restored-still-safe-hold");
        Require(actuator.TrolleyInput == 0f && actuator.GantryInput == 0f && actuator.HoistInput == 0f,
            "cabin-reset-replayed-old-motion");

        var afterRecovery = new YardCraneRealtimeInput(
            runId, leaseId, 1, streamId, 3, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0f, 0f);
        Require(controller.TryApplyRealtimeInput(afterRecovery, 14, out error), $"cabin-after-recovery:{error}");
        Require(actuator.TrolleyInput == 0f, "cabin-after-recovery-old-motion-replayed");

        // Emergency stop pressed (not released) must independently trip SafeHold even while
        // control power stays on.
        var estopMoving = new YardCraneRealtimeInput(
            runId, leaseId, 1, streamId, 4, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, true, true, 0f, 0.4f, 0f);
        Require(controller.TryApplyRealtimeInput(estopMoving, 15, out error), $"cabin-estop-setup:{error}");
        Require(actuator.TrolleyInput == 0.4f, "cabin-estop-setup-not-forwarded");

        var estopPressed = new YardCraneRealtimeInput(
            runId, leaseId, 1, streamId, 5, 0, YardCraneInputQuality.Good,
            YardCraneControlMode.Local, true, false, true, 0f, 0.4f, 0f);
        Require(!controller.TryApplyRealtimeInput(estopPressed, 16, out error), "cabin-estop-accepted");
        Require(error == YardCraneOperationErrorCodes.SafeHoldActive, $"cabin-estop-error:{error}");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, $"cabin-estop-phase:{controller.Phase}");
        Require(actuator.TrolleyInput == 0f, "cabin-estop-not-cleared");

        // Reset attempted while emergency stop is still pressed must be rejected.
        Require(!controller.TryClearSafeHold(
                runId, leaseId, 1, YardCraneControlMode.Local, true, false, true, true, 17, out error),
            "cabin-reset-while-estop-accepted");
        Require(controller.Phase == YardCraneOperationPhase.SafeHold, "cabin-reset-while-estop-left-safe-hold");

        // Explicit reset with emergency stop released succeeds.
        Require(controller.TryClearSafeHold(
                runId, leaseId, 1, YardCraneControlMode.Local, true, true, true, true, 18, out error),
            $"cabin-reset-estop-released:{error}");
        Require(controller.Phase != YardCraneOperationPhase.SafeHold, "cabin-reset-estop-released-still-safe-hold");

        controller.ReleaseRun();
    }

    private static YardCraneRealtimeInput Input(
        Guid runId,
        Guid leaseId,
        Guid streamId,
        long sequence,
        int dataAgeMs,
        float trolley,
        float hoist,
        YardCraneControlMode mode)
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
            mode,
            true,
            true,
            true,
            0f,
            trolley,
            hoist);
    }

    private static void Require(bool value, string message)
    {
        if (!value)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeActuator : IYardCraneOperationActuator
    {
        public bool ContainerLocked;
        public int LockCount;
        public int ClearCount;
        public float GantryInput;
        public float TrolleyInput;
        public float HoistInput;
        public bool HornActive;
        public bool ClampApplied;
        public int WheelClampCount;
        public bool TrolleyAnchorApplied;
        public int TrolleyAnchorCount;
        public bool GantryAnchorApplied;
        public int GantryAnchorCount;
        public bool PumpOn;
        public int PumpRequestCount;
        public string LastRequestedSize = string.Empty;
        public int SizeRequestCount;
        public float RotationInput;
        public int ReturnToZeroRequestCount;

        bool IYardCraneOperationActuator.IsInitialized => true;
        bool IYardCraneOperationActuator.IsPreTensioning => false;
        bool IYardCraneOperationActuator.IsLoadTransferActive => false;
        bool IYardCraneOperationActuator.IsMechanismBusy => false;
        bool IYardCraneOperationActuator.IsFaulted => false;
        bool IYardCraneOperationActuator.IsStationary => true;
        bool IYardCraneOperationActuator.IsContainerLocked => ContainerLocked;
        bool IYardCraneOperationActuator.IsContainerLandingConfirmed => false;
        bool IYardCraneOperationActuator.IsAlignedForContainerLock => true;
        bool IYardCraneOperationActuator.IsWheelClampClamped => ClampApplied;
        bool IYardCraneOperationActuator.IsWheelClampTransitioning => false;
        bool IYardCraneOperationActuator.IsTrolleyAnchorApplied => TrolleyAnchorApplied;
        bool IYardCraneOperationActuator.IsTrolleyAnchorTransitioning => false;
        bool IYardCraneOperationActuator.IsGantryAnchorApplied => GantryAnchorApplied;
        bool IYardCraneOperationActuator.IsGantryAnchorTransitioning => false;
        bool IYardCraneOperationActuator.IsSpreaderPumpOn => PumpOn;
        bool IYardCraneOperationActuator.IsSpreaderSizeTransitioning => false;

        bool IYardCraneOperationActuator.TryQueueSpreaderRotationReturnToZero(out string rejectionCode)
        {
            ReturnToZeroRequestCount++;
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        void IYardCraneOperationActuator.SetSpreaderRotationInput(float rotation)
        {
            RotationInput = rotation;
        }

        bool IYardCraneOperationActuator.TryQueueContainerLock(out string rejectionCode)
        {
            LockCount++;
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueContainerUnlock(out string rejectionCode)
        {
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryReset(out string rejectionCode)
        {
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueWheelClamp(bool clampApplied, out string rejectionCode)
        {
            ClampApplied = clampApplied;
            WheelClampCount++;
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueTrolleyAnchor(bool anchorApplied, out string rejectionCode)
        {
            TrolleyAnchorApplied = anchorApplied;
            TrolleyAnchorCount++;
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueGantryAnchor(bool anchorApplied, out string rejectionCode)
        {
            GantryAnchorApplied = anchorApplied;
            GantryAnchorCount++;
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderPump(bool pumpOn, out string rejectionCode)
        {
            PumpOn = pumpOn;
            PumpRequestCount++;
            rejectionCode = YardCraneOperationErrorCodes.None;
            return true;
        }

        bool IYardCraneOperationActuator.TryQueueSpreaderSize(string targetSize, out string rejectionCode)
        {
            LastRequestedSize = targetSize;
            SizeRequestCount++;
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
            HornActive = active;
        }

        void IYardCraneOperationActuator.ClearMotionInputs()
        {
            ClearCount++;
            GantryInput = 0f;
            TrolleyInput = 0f;
            HoistInput = 0f;
            HornActive = false;
        }
    }
}
