using System;
using System.Collections.Generic;
using System.Linq;
using PortTraining.YardCraneSimulator.Operations;
using PortTraining.YardCraneSimulator.SharedNet;
using PortTraining.YardCraneSimulator.Training;
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
        private YardCraneS7InputSource s7;
        private bool s7Exclusive;

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

        // Every discrete action that queues through FixedUpdate's mutual-exclusion dispatch.
        // FaultReset is deliberately excluded from SafetyTripClearedIntents: it must survive a
        // control-power/e-stop trip so the operator can still press reset afterward, matching
        // the original per-field behavior exactly (see the dedicated SafeHold branch below,
        // which also special-cases FaultReset).
        private static readonly YardCraneOperatorIntent[] QueuedIntents =
        {
            YardCraneOperatorIntent.ContainerLock,
            YardCraneOperatorIntent.ContainerUnlock,
            YardCraneOperatorIntent.WheelClampToggle,
            YardCraneOperatorIntent.TrolleyAnchorToggle,
            YardCraneOperatorIntent.GantryAnchorToggle,
            YardCraneOperatorIntent.SpreaderPumpToggle,
            YardCraneOperatorIntent.SpreaderSizeFt20,
            YardCraneOperatorIntent.SpreaderSizeFt40,
            YardCraneOperatorIntent.SpreaderReturnToZero,
            YardCraneOperatorIntent.FaultReset
        };

        private static readonly YardCraneOperatorIntent[] SafetyTripClearedIntents =
        {
            YardCraneOperatorIntent.ContainerLock,
            YardCraneOperatorIntent.ContainerUnlock,
            YardCraneOperatorIntent.WheelClampToggle,
            YardCraneOperatorIntent.TrolleyAnchorToggle,
            YardCraneOperatorIntent.GantryAnchorToggle,
            YardCraneOperatorIntent.SpreaderPumpToggle,
            YardCraneOperatorIntent.SpreaderSizeFt20,
            YardCraneOperatorIntent.SpreaderSizeFt40,
            YardCraneOperatorIntent.SpreaderReturnToZero
        };

        // A brief, real focus blip (e.g. clicking the Console to read a log line, an OS/remote
        // desktop notification momentarily stealing focus) should not force a full SafeHold that
        // then requires an explicit fault reset to leave - that turned every short glance away
        // from the Game view into a "keyboard stopped working" dead end. Motion is still zeroed
        // immediately on any focus loss (see OnApplicationFocus); only the point at which a
        // sustained loss escalates into NotifyInputUnavailable/SafeHold is delayed by this
        // window. A device actually disconnecting (sourceLost) or an explicit pause still trips
        // immediately - only the focus-loss condition gets this tolerance.
        private const long FocusLossGraceMs = 400;
        private long focusLostAtMs = -1;

        private readonly HashSet<YardCraneOperatorIntent> pendingIntents = new();
        private bool paused;
        private bool focusAvailable = true;
        private bool sourceLost;
        private bool hasFreshDeviceSample;
        private bool controlPowerOn = true;
        private bool emergencyStopReleased = true;
        private bool trainingInputEnabled = true;

        public string ActiveSourceId => router.ActiveSourceId;
        public YardCraneOperatorCommand LatestCommand => latestCommand;
        public bool IsPaused => paused;
        public bool IsHornActive => latestCommand.HornHeld;
        public bool IsTrainingInputEnabled => trainingInputEnabled;

        /// <summary>
        /// Standalone-simulated panel state. These are keyboard/Xbox adapter states, not a
        /// real hardware safety circuit; a future Gateway/PLC adapter supplies the real signal.
        /// </summary>
        public bool IsControlPowerOn => controlPowerOn;
        public bool IsEmergencyStopReleased => emergencyStopReleased;
        public bool IsAwaitingFaultReset =>
            configured && controller != null && controller.Phase == YardCraneOperationPhase.SafeHold;

        /// <summary>
        /// Gates business input during a real training countdown or after local evaluation
        /// finishes. Heartbeats and zero realtime samples continue, while control-power,
        /// emergency-stop and fault-reset safety paths remain available. Closing a visual
        /// prompt alone is never treated as an input interlock.
        /// </summary>
        public void SetTrainingInputEnabled(bool enabled)
        {
            if (trainingInputEnabled == enabled) return;
            trainingInputEnabled = enabled;
            pendingIntents.Clear();
            hasFreshDeviceSample = false;
            sourceLost = false;
            var nowMs = MonotonicMilliseconds();
            latestCommand = YardCraneOperatorCommand.Zero(latestCommand.Sequence + 1, nowMs);
            router.Reset();
            Debug.Log($"[YC_STANDALONE_TRAINING_INPUT] enabled={enabled}", this);
        }

        /// <summary>
        /// requireS7Link=true switches this host to the production PLC-only configuration: the
        /// keyboard/Xbox adapters (test-only, per the operator's own instruction) are not
        /// sampled at all, and a failed link to the independent YardCraneS7Integration player is
        /// fatal to Configure - the operator's explicit choice was to refuse training entry
        /// rather than silently fall back to a device that could mask a real PLC-link outage.
        /// </summary>
        public void Configure(
            YardCraneOperationController operationController,
            string stationId,
            YardCraneInputProfile inputProfile = null,
            bool requireS7Link = false,
            string s7LinkHost = "127.0.0.1",
            int s7LinkPort = YardCraneS7LinkProtocol.DefaultPort,
            int s7LinkConnectTimeoutMs = 3000)
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

            // A StudentHome-launched local run already owns a frozen SessionId. Reuse it as
            // the operation run id so input authority, HUD, lifecycle and the persisted audit
            // record can be correlated without an informal second identifier. Direct editor/
            // standalone boots have no local context and keep their independent generated id.
            var trainingContext = YardCraneLocalTrainingSession.CurrentContext;
            runId = trainingContext?.SessionId ?? Guid.NewGuid();
            leaseId = Guid.NewGuid();
            sceneInstanceId = Guid.NewGuid();
            streamId = Guid.NewGuid();
            var nowMs = MonotonicMilliseconds();
            var operationContext = new YardCraneOperationContext(
                runId,
                leaseId,
                1,
                sceneInstanceId,
                string.IsNullOrWhiteSpace(stationId) ? "YC-STANDALONE-01" : stationId,
                YardCraneControlMode.Local,
                false);

            var activated = controller.TryActivateRun(operationContext, nowMs, out var activationError);
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

            if (requireS7Link)
            {
                var link = new YardCraneS7InputSource();
                if (!link.TryConnect(s7LinkHost, s7LinkPort, s7LinkConnectTimeoutMs, out var linkError))
                {
                    if (controller.IsActive)
                    {
                        controller.ReleaseRun();
                    }

                    throw new InvalidOperationException(
                        $"S7 link required but unavailable at {s7LinkHost}:{s7LinkPort} - {linkError}");
                }

                s7 = link;
                s7Exclusive = true;
            }

            latestCommand = YardCraneOperatorCommand.Zero(0, nowMs);
            focusAvailable = Application.isFocused;
            configured = true;
            Debug.Log(
                $"[YC_STANDALONE_INPUT_READY] run={runId:D}, station={operationContext.StationId}, " +
                $"keyboard={!s7Exclusive}, xbox={!s7Exclusive}, s7Link={s7Exclusive}, authoritative=false",
                this);
        }

        private void Update()
        {
            if (!configured || !focusAvailable)
            {
                return;
            }

            snapshots.Clear();
            if (s7Exclusive)
            {
                snapshots.Add(s7.Sample(profile));
            }
            else
            {
                snapshots.Add(keyboard.Sample(profile));
                snapshots.Add(xbox.Sample(profile));
            }

            var nowMs = MonotonicMilliseconds();
            var route = router.Route(snapshots, nowMs);
            var sampledCommand = route.Command;
            latestCommand = trainingInputEnabled
                ? sampledCommand
                : new YardCraneOperatorCommand(
                    sampledCommand.SourceId,
                    sampledCommand.Sequence,
                    nowMs,
                    0f,
                    0f,
                    0f,
                    false,
                    0f,
                    sampledCommand.Has(YardCraneOperatorIntent.FaultReset)
                        ? new[] { YardCraneOperatorIntent.FaultReset }
                        : null);
            hasFreshDeviceSample = true;
            sourceLost |= route.ActiveSourceLost;
            foreach (var intent in QueuedIntents)
            {
                if (latestCommand.Has(intent))
                {
                    pendingIntents.Add(intent);
                }
            }

            if (sampledCommand.Has(YardCraneOperatorIntent.ControlPowerToggle))
            {
                controlPowerOn = !controlPowerOn;
                Debug.Log($"[YC_STANDALONE_CONTROL_POWER] on={controlPowerOn}", this);
            }

            if (s7Exclusive)
            {
                // Emergency stop over the S7 link is a maintained level, not a button - it is
                // read fresh every Update() and directly overrides emergencyStopReleased,
                // rather than going through the toggle-on-press path below (which S7 never
                // produces an intent for anyway; YardCraneS7InputSource keeps
                // EmergencyStopToggle out of its momentary signal set on purpose). This also
                // means a dropped WebSocket frame or link loss cannot leave a stale "released"
                // state - YardCraneS7InputSource.EmergencyStopEngaged itself fails toward true.
                var s7EmergencyStopReleased = !s7.EmergencyStopEngaged;
                if (emergencyStopReleased != s7EmergencyStopReleased)
                {
                    emergencyStopReleased = s7EmergencyStopReleased;
                    Debug.Log($"[YC_STANDALONE_EMERGENCY_STOP] released={emergencyStopReleased}, source=s7Link", this);
                }
            }
            else if (sampledCommand.Has(YardCraneOperatorIntent.EmergencyStopToggle))
            {
                emergencyStopReleased = !emergencyStopReleased;
                Debug.Log($"[YC_STANDALONE_EMERGENCY_STOP] released={emergencyStopReleased}, source=toggle", this);
            }

            if (trainingInputEnabled && sampledCommand.Has(YardCraneOperatorIntent.Pause))
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

            var focusLossWithinGrace = !focusAvailable && focusLostAtMs >= 0 &&
                                       nowMs - focusLostAtMs < FocusLossGraceMs;
            var focusLossPastGrace = !focusAvailable && !focusLossWithinGrace;
            var mustHold = paused || focusLossPastGrace || sourceLost;
            if (mustHold)
            {
                controller.NotifyInputUnavailable(
                    runId,
                    leaseId,
                    1,
                    paused ? "standalone-paused" : focusLossPastGrace ? "application-focus-lost" : "active-device-disconnected",
                    out _);
                pendingIntents.Clear();
                sourceLost = false;
                return;
            }

            // Safety trip has priority over any queued business action: control power off or
            // emergency stop pressed must drop lock/unlock/wheel-clamp/anchor/spreader intents on
            // this exact frame, not just rely on the controller rejecting them once SafeHold is entered.
            if (!controlPowerOn || !emergencyStopReleased)
            {
                pendingIntents.ExceptWith(SafetyTripClearedIntents);
            }

            if (controller.Phase == YardCraneOperationPhase.SafeHold)
            {
                if (!pendingIntents.Contains(YardCraneOperatorIntent.FaultReset))
                {
                    return;
                }

                if (!hasFreshDeviceSample)
                {
                    // latestCommand.HandlesCentered below would read true off a focus-loss
                    // synthetic zero command even if the real stick is still deflected, letting
                    // SafeHold clear invisibly and then jump into motion once focus returns and
                    // sampling resumes. Leave FaultReset pending - it's re-evaluated once a
                    // genuine post-focus sample lands, not dropped. (YC-P0-FOCUS-01)
                    return;
                }

                pendingIntents.Remove(YardCraneOperatorIntent.FaultReset);

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
            // Re-stamped with nowMs (not the original loss time) so the controller's own
            // independent input-staleness check never trips while still inside the grace window.
            var commandForStep = !trainingInputEnabled || !hasFreshDeviceSample || mechanismStarting || focusLossWithinGrace
                ? YardCraneOperatorCommand.Zero(latestCommand.Sequence, nowMs)
                : latestCommand;
            if (mechanismStarting)
            {
                // Matches the original per-field behavior exactly: FaultReset is intentionally
                // NOT cleared here (unlike mustHold), since a fault reset requested while the
                // mechanism is still initializing/pre-tensioning must still be honored once it
                // finishes, not silently dropped.
                pendingIntents.ExceptWith(SafetyTripClearedIntents);
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

            if (pendingIntents.Count > 1)
            {
                pendingIntents.Clear();
                Debug.LogWarning("[YC_STANDALONE_ACTION_REJECTED] reason=conflicting-discrete-actions", this);
            }
            else if (pendingIntents.Count == 1)
            {
                var intent = pendingIntents.First();
                pendingIntents.Remove(intent);
                DispatchIntent(intent);
            }

            controller.EvaluateWatchdogs(nowMs, out _);
        }

        /// <summary>
        /// Turns exactly one pending discrete intent into the matching controller action. The
        /// dispatch order that used to be encoded by an if/else-if chain no longer matters: by
        /// the time this runs, the pendingIntents.Count == 1 check above already guarantees at
        /// most one intent is pending, so which case a switch checks first is irrelevant - only
        /// the one actually pending intent can ever match.
        /// </summary>
        private void DispatchIntent(YardCraneOperatorIntent intent)
        {
            switch (intent)
            {
                case YardCraneOperatorIntent.ContainerLock:
                    SubmitAction(YardCraneOperationActionType.SpreaderLock, "Locked");
                    break;
                case YardCraneOperatorIntent.ContainerUnlock:
                    SubmitAction(YardCraneOperationActionType.SpreaderUnlock, "Unlocked");
                    break;
                case YardCraneOperatorIntent.WheelClampToggle:
                    SubmitAction(
                        YardCraneOperationActionType.WheelClampChange,
                        controller.IsWheelClampClamped ? "Released" : "Clamped");
                    break;
                case YardCraneOperatorIntent.TrolleyAnchorToggle:
                    SubmitAction(
                        YardCraneOperationActionType.TrolleyAnchorChange,
                        controller.IsTrolleyAnchorApplied ? "Released" : "Anchored");
                    break;
                case YardCraneOperatorIntent.GantryAnchorToggle:
                    SubmitAction(
                        YardCraneOperationActionType.GantryAnchorChange,
                        controller.IsGantryAnchorApplied ? "Released" : "Anchored");
                    break;
                case YardCraneOperatorIntent.SpreaderPumpToggle:
                    SubmitAction(
                        YardCraneOperationActionType.SpreaderPumpChange,
                        controller.IsSpreaderPumpOn ? "Off" : "On");
                    break;
                case YardCraneOperatorIntent.SpreaderSizeFt20:
                    SubmitAction(YardCraneOperationActionType.SpreaderSizeChange, "FT20");
                    break;
                case YardCraneOperatorIntent.SpreaderSizeFt40:
                    SubmitAction(YardCraneOperationActionType.SpreaderSizeChange, "FT40");
                    break;
                case YardCraneOperatorIntent.SpreaderReturnToZero:
                    SubmitAction(YardCraneOperationActionType.SpreaderRotationReturnToZero, string.Empty);
                    break;
                case YardCraneOperatorIntent.FaultReset:
                    // Reached only when Phase != SafeHold: routes through the existing physical
                    // reset interlock (motion-centered + actuator.TryReset) instead of the
                    // SafeHold-clear path handled above.
                    SubmitAction(YardCraneOperationActionType.Reset, string.Empty);
                    break;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            focusAvailable = hasFocus;
            if (hasFocus)
            {
                focusLostAtMs = -1;
            }
            else
            {
                focusLostAtMs = MonotonicMilliseconds();
                latestCommand = YardCraneOperatorCommand.Zero(latestCommand.Sequence + 1, MonotonicMilliseconds());
                // Queued discrete actions (lock/unlock/wheel-clamp/anchor/pump/size) are
                // deliberately NOT cancelled here: each re-validates real physical state
                // (alignment, stationary, etc.) at dispatch time, so a brief focus blip doesn't
                // need to punish an action the operator genuinely requested. Only the
                // sample-freshness flag is invalidated - see the FixedUpdate SafeHold branch,
                // which uses it to stop a pending FaultReset from reading this call's synthetic
                // zero command as a genuine "handles centered" confirmation. (YC-P0-FOCUS-01)
                hasFreshDeviceSample = false;
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

            s7?.Dispose();
            s7 = null;
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
