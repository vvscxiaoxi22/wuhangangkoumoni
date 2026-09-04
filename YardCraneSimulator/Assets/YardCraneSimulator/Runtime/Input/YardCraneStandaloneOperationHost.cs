using System;
using System.Collections.Generic;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Controls
{
    /// <summary>
    /// Standalone authority adapter. It translates local Input System devices into
    /// the same operation controller contract that managed IPC/PLC adapters use.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class YardCraneStandaloneOperationHost : MonoBehaviour
    {
        private readonly List<YardCraneInputSnapshot> snapshots = new(2);
        private readonly YardCraneInputRouter router = new();
        private readonly IYardCraneInputSource keyboard = new YardCraneKeyboardInputSource();
        private readonly IYardCraneInputSource xbox = new YardCraneXboxInputSource();

        private YardCraneOperationController controller;
        private YardCraneInputProfile profile;
        private YardCraneOperatorCommand latestCommand;
        private Guid runId;
        private Guid leaseId;
        private Guid sceneInstanceId;
        private Guid streamId;
        private long inputSequence;
        private long actionSequence;
        private bool configured;
        private bool ownsRuntimeProfile;
        private bool pendingLock;
        private bool pendingUnlock;
        private bool pendingWheelClampToggle;
        private bool pendingFaultReset;
        private bool pendingTrolleyAnchorToggle;
        private bool pendingGantryAnchorToggle;
        private bool pendingSpreaderPumpToggle;
        private bool pendingSpreaderSizeFt20;
        private bool pendingSpreaderSizeFt40;
        private bool pendingSpreaderReturnToZero;
        private bool paused;
        private bool focusAvailable = true;
        private bool sourceLost;
        private bool hasFreshDeviceSample;
        private bool controlPowerOn = true;
        private bool emergencyStopReleased = true;

        public string ActiveSourceId => router.ActiveSourceId;
        public YardCraneOperatorCommand LatestCommand => latestCommand;
        public bool IsPaused => paused;
        public bool IsHornActive => latestCommand.HornHeld;

        /// <summary>
        /// Standalone-simulated panel state. These are keyboard/Xbox adapter states, not a
        /// real hardware safety circuit; a future Gateway/PLC adapter supplies the real signal.
        /// </summary>
        public bool IsControlPowerOn => controlPowerOn;
        public bool IsEmergencyStopReleased => emergencyStopReleased;
        public bool IsAwaitingFaultReset =>
            configured && controller != null && controller.Phase == YardCraneOperationPhase.SafeHold;

        public void Configure(
            YardCraneOperationController operationController,
            string stationId,
            YardCraneInputProfile inputProfile = null)
        {
            if (configured)
            {
                throw new InvalidOperationException("Standalone operation host is already configured.");
            }

            controller = operationController ?? throw new ArgumentNullException(nameof(operationController));
            profile = inputProfile;
            if (profile == null)
            {
                profile = YardCraneInputProfile.CreateRuntimeDefault();
                ownsRuntimeProfile = true;
            }

            runId = Guid.NewGuid();
            leaseId = Guid.NewGuid();
            sceneInstanceId = Guid.NewGuid();
            streamId = Guid.NewGuid();
            var nowMs = MonotonicMilliseconds();
            var context = new YardCraneOperationContext(
                runId,
                leaseId,
                1,
                sceneInstanceId,
                string.IsNullOrWhiteSpace(stationId) ? "YC-STANDALONE-01" : stationId,
                YardCraneControlMode.Local,
                false);

            var activated = controller.TryActivateRun(context, nowMs, out var activationError);
            var streamOpened = false;
            var streamError = YardCraneOperationErrorCodes.None;
            if (activated)
            {
                streamOpened = controller.TryOpenInputStream(runId, leaseId, 1, streamId, nowMs, out streamError);
            }

            if (!activated || !streamOpened)
            {
                throw new InvalidOperationException(
                    $"Unable to start standalone input authority: activation={activationError}, stream={streamError}");
            }

            latestCommand = YardCraneOperatorCommand.Zero(0, nowMs);
            focusAvailable = Application.isFocused;
            configured = true;
            Debug.Log(
                $"[YC_STANDALONE_INPUT_READY] run={runId:D}, station={context.StationId}, " +
                "keyboard=true, xbox=true, authoritative=false",
                this);
        }

        private void Update()
        {
            if (!configured || !focusAvailable)
            {
                return;
            }

            snapshots.Clear();
            snapshots.Add(keyboard.Sample(profile));
            snapshots.Add(xbox.Sample(profile));
            var route = router.Route(snapshots, MonotonicMilliseconds());
            latestCommand = route.Command;
            hasFreshDeviceSample = true;
            sourceLost |= route.ActiveSourceLost;
            pendingLock |= latestCommand.LockPressed;
            pendingUnlock |= latestCommand.UnlockPressed;
            pendingWheelClampToggle |= latestCommand.WheelClampTogglePressed;
            pendingFaultReset |= latestCommand.FaultResetPressed;
            pendingTrolleyAnchorToggle |= latestCommand.TrolleyAnchorTogglePressed;
            pendingGantryAnchorToggle |= latestCommand.GantryAnchorTogglePressed;
            pendingSpreaderPumpToggle |= latestCommand.SpreaderPumpTogglePressed;
            pendingSpreaderSizeFt20 |= latestCommand.SpreaderSizeFt20Pressed;
            pendingSpreaderSizeFt40 |= latestCommand.SpreaderSizeFt40Pressed;
            pendingSpreaderReturnToZero |= latestCommand.SpreaderReturnToZeroPressed;

            if (latestCommand.ControlPowerTogglePressed)
            {
                controlPowerOn = !controlPowerOn;
                Debug.Log($"[YC_STANDALONE_CONTROL_POWER] on={controlPowerOn}", this);
            }

            if (latestCommand.EmergencyStopTogglePressed)
            {
                emergencyStopReleased = !emergencyStopReleased;
                Debug.Log($"[YC_STANDALONE_EMERGENCY_STOP] released={emergencyStopReleased}", this);
            }

            if (latestCommand.PausePressed)
            {
                paused = !paused;
                Debug.Log($"[YC_STANDALONE_PAUSE] paused={paused}", this);
            }

        }

        private void FixedUpdate()
        {
            if (!configured)
            {
                return;
            }

            var nowMs = MonotonicMilliseconds();
            controller.RecordOwnerHeartbeat(nowMs);
            controller.RecordCoordinatorHeartbeat(nowMs);

            var mustHold = paused || !focusAvailable || sourceLost;
            if (mustHold)
            {
                controller.NotifyInputUnavailable(
                    runId,
                    leaseId,
                    1,
                    paused ? "standalone-paused" : !focusAvailable ? "application-focus-lost" : "active-device-disconnected",
                    out _);
                pendingLock = false;
                pendingUnlock = false;
                pendingWheelClampToggle = false;
                pendingFaultReset = false;
                pendingTrolleyAnchorToggle = false;
                pendingGantryAnchorToggle = false;
                pendingSpreaderPumpToggle = false;
                pendingSpreaderSizeFt20 = false;
                pendingSpreaderSizeFt40 = false;
                pendingSpreaderReturnToZero = false;
                sourceLost = false;
                return;
            }

            // Safety trip has priority over any queued business action: control power off or
            // emergency stop pressed must drop lock/unlock/wheel-clamp/anchor/spreader intents on
            // this exact frame, not just rely on the controller rejecting them once SafeHold is entered.
            if (!controlPowerOn || !emergencyStopReleased)
            {
                pendingLock = false;
                pendingUnlock = false;
                pendingWheelClampToggle = false;
                pendingTrolleyAnchorToggle = false;
                pendingGantryAnchorToggle = false;
                pendingSpreaderPumpToggle = false;
                pendingSpreaderSizeFt20 = false;
                pendingSpreaderSizeFt40 = false;
                pendingSpreaderReturnToZero = false;
            }

            if (controller.Phase == YardCraneOperationPhase.SafeHold)
            {
                if (!pendingFaultReset)
                {
                    return;
                }

                pendingFaultReset = false;

                if (!controller.TryClearSafeHold(
                        runId,
                        leaseId,
                        1,
                        YardCraneControlMode.Local,
                        true,
                        emergencyStopReleased,
                        controlPowerOn,
                        latestCommand.HandlesCentered,
                        nowMs,
                        out var recoveryError))
                {
                    Debug.LogWarning(
                        $"[YC_STANDALONE_RESET_REJECTED] error={recoveryError}, " +
                        $"controlPowerOn={controlPowerOn}, emergencyStopReleased={emergencyStopReleased}, " +
                        $"handlesCentered={latestCommand.HandlesCentered}",
                        this);
                    return;
                }

                Debug.Log("[YC_STANDALONE_RECOVERED] handlesCentered=true", this);
            }

            var mechanismStarting = controller.Phase == YardCraneOperationPhase.Initializing ||
                                    controller.Phase == YardCraneOperationPhase.PreTensioning;
            var commandForStep = !hasFreshDeviceSample || mechanismStarting
                ? YardCraneOperatorCommand.Zero(latestCommand.Sequence, nowMs)
                : latestCommand;
            if (mechanismStarting)
            {
                pendingLock = false;
                pendingUnlock = false;
                pendingWheelClampToggle = false;
                pendingTrolleyAnchorToggle = false;
                pendingGantryAnchorToggle = false;
                pendingSpreaderPumpToggle = false;
                pendingSpreaderSizeFt20 = false;
                pendingSpreaderSizeFt40 = false;
                pendingSpreaderReturnToZero = false;
            }

            var dataAge = (int)Math.Max(0, Math.Min(int.MaxValue, nowMs - commandForStep.CapturedAtMs));
            var input = new YardCraneRealtimeInput(
                runId,
                leaseId,
                1,
                streamId,
                ++inputSequence,
                dataAge,
                dataAge >= YardCraneOperationController.StaleWarningThresholdMs
                    ? YardCraneInputQuality.Stale
                    : YardCraneInputQuality.Good,
                YardCraneControlMode.Local,
                true,
                emergencyStopReleased,
                controlPowerOn,
                commandForStep.GantryAxis,
                commandForStep.TrolleyAxis,
                commandForStep.HoistAxis,
                commandForStep.HornHeld,
                commandForStep.SpreaderRotationAxis);

            if (!controller.TryApplyRealtimeInput(input, nowMs, out var inputError))
            {
                Debug.LogWarning(
                    $"[YC_STANDALONE_INPUT_REJECTED] sequence={inputSequence}, error={inputError}, " +
                    $"phase={controller.Phase}",
                    this);
                return;
            }

            var pendingActionCount = (pendingLock ? 1 : 0) +
                                     (pendingUnlock ? 1 : 0) +
                                     (pendingWheelClampToggle ? 1 : 0) +
                                     (pendingFaultReset ? 1 : 0) +
                                     (pendingTrolleyAnchorToggle ? 1 : 0) +
                                     (pendingGantryAnchorToggle ? 1 : 0) +
                                     (pendingSpreaderPumpToggle ? 1 : 0) +
                                     (pendingSpreaderSizeFt20 ? 1 : 0) +
                                     (pendingSpreaderSizeFt40 ? 1 : 0) +
                                     (pendingSpreaderReturnToZero ? 1 : 0);
            if (pendingActionCount > 1)
            {
                pendingLock = false;
                pendingUnlock = false;
                pendingWheelClampToggle = false;
                pendingFaultReset = false;
                pendingTrolleyAnchorToggle = false;
                pendingGantryAnchorToggle = false;
                pendingSpreaderPumpToggle = false;
                pendingSpreaderSizeFt20 = false;
                pendingSpreaderSizeFt40 = false;
                pendingSpreaderReturnToZero = false;
                Debug.LogWarning("[YC_STANDALONE_ACTION_REJECTED] reason=conflicting-discrete-actions", this);
            }
            else if (pendingLock)
            {
                SubmitAction(YardCraneOperationActionType.SpreaderLock, "Locked");
                pendingLock = false;
            }
            else if (pendingUnlock)
            {
                SubmitAction(YardCraneOperationActionType.SpreaderUnlock, "Unlocked");
                pendingUnlock = false;
            }
            else if (pendingWheelClampToggle)
            {
                SubmitAction(
                    YardCraneOperationActionType.WheelClampChange,
                    controller.IsWheelClampClamped ? "Released" : "Clamped");
                pendingWheelClampToggle = false;
            }
            else if (pendingTrolleyAnchorToggle)
            {
                SubmitAction(
                    YardCraneOperationActionType.TrolleyAnchorChange,
                    controller.IsTrolleyAnchorApplied ? "Released" : "Anchored");
                pendingTrolleyAnchorToggle = false;
            }
            else if (pendingGantryAnchorToggle)
            {
                SubmitAction(
                    YardCraneOperationActionType.GantryAnchorChange,
                    controller.IsGantryAnchorApplied ? "Released" : "Anchored");
                pendingGantryAnchorToggle = false;
            }
            else if (pendingSpreaderPumpToggle)
            {
                SubmitAction(
                    YardCraneOperationActionType.SpreaderPumpChange,
                    controller.IsSpreaderPumpOn ? "Off" : "On");
                pendingSpreaderPumpToggle = false;
            }
            else if (pendingSpreaderSizeFt20)
            {
                SubmitAction(YardCraneOperationActionType.SpreaderSizeChange, "FT20");
                pendingSpreaderSizeFt20 = false;
            }
            else if (pendingSpreaderSizeFt40)
            {
                SubmitAction(YardCraneOperationActionType.SpreaderSizeChange, "FT40");
                pendingSpreaderSizeFt40 = false;
            }
            else if (pendingSpreaderReturnToZero)
            {
                SubmitAction(YardCraneOperationActionType.SpreaderRotationReturnToZero, string.Empty);
                pendingSpreaderReturnToZero = false;
            }
            else if (pendingFaultReset)
            {
                // Reached only when Phase != SafeHold: routes through the existing physical
                // reset interlock (motion-centered + actuator.TryReset) instead of the
                // SafeHold-clear path handled above.
                SubmitAction(YardCraneOperationActionType.Reset, string.Empty);
                pendingFaultReset = false;
            }

            controller.EvaluateWatchdogs(nowMs, out _);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focusAvailable = hasFocus;
            if (!hasFocus)
            {
                latestCommand = YardCraneOperatorCommand.Zero(latestCommand.Sequence + 1, MonotonicMilliseconds());
                router.Reset();
            }
        }

        private void OnDestroy()
        {
            if (configured && controller.IsActive)
            {
                controller.ReleaseRun();
            }

            if (ownsRuntimeProfile && profile != null)
            {
                Destroy(profile);
            }
        }

        private void SubmitAction(YardCraneOperationActionType actionType, string targetState)
        {
            var action = new YardCraneOperationAction(
                runId,
                streamId,
                Guid.NewGuid(),
                ++actionSequence,
                actionType,
                targetState);
            var ack = controller.SubmitAction(action);
            var log =
                $"[YC_STANDALONE_ACTION_ACK] action={actionType}, status={ack.Status}, " +
                $"error={ack.ErrorCode}, phase={ack.Phase}";
            if (ack.Status == YardCraneActionAckStatus.Rejected)
            {
                Debug.LogWarning(log, this);
            }
            else
            {
                Debug.Log(log, this);
            }
        }

        private static long MonotonicMilliseconds()
        {
            return (long)(Time.realtimeSinceStartupAsDouble * 1000d);
        }
    }
}
