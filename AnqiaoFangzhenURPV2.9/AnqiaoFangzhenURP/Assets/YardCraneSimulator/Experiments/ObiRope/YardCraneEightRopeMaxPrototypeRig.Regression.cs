using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    // Automated dev/QA sequencers only - the startup empty-load smoke demo and the 30-ton loaded
    // operation regression driven by Tools/Yard Crane/Tests menu items and the Editor 'T' shortcut.
    // Neither runs during normal student operation. Split into its own partial-class file (not a
    // separate class) because these methods reach directly into dozens of the Rig's private physics
    // fields with no clean interface boundary - unlike the mechanism Presenters extracted earlier,
    // there is no actuator-forwarding seam here to extract behind, so a partial class keeps full
    // private access with zero visibility promotion and zero behavior change.
    public sealed partial class YardCraneEightRopeMaxPrototypeRig
    {
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

        private float LoadedRegressionStageTimeoutSeconds => activeProfile.regression.loadedStageTimeoutSeconds;
        private float LoadedRegressionTotalTimeoutSeconds => activeProfile.regression.loadedTotalTimeoutSeconds;
        private float LoadedRegressionStableHoldSeconds => activeProfile.regression.loadedStableHoldSeconds;
        private float LoadedRegressionLiftClearanceMeters => activeProfile.regression.loadedLiftClearanceMeters;
        private float LoadedRegressionOutboundTravelMeters => activeProfile.regression.loadedOutboundTravelMeters;

        private float loadedRegressionStartedAt;
        private float loadedRegressionStageStartedAt;
        private float loadedRegressionStableSince = -1f;
        private float loadedRegressionReferenceX;
        private float loadedRegressionTargetDrop;
        private Vector3 loadedRegressionReleasedContainerPosition;
        private bool loadedRegressionCommandIssued;
        private LoadedRegressionStage loadedRegressionStage;

        private bool twistDisturbanceApplied;
        private bool demoClampApplyRequested;
        private bool demoClampReleaseRequested;
        private bool demoClampInhibitObserved;
        private float demoGantryStartPosition;
        private float demoGantryMaximumTravel;
        private bool demoPumpOnRequested;
        private bool demoPumpOffRequested;
        private bool demoPumpCycleObserved;
        private bool demoTrolleyAnchorApplyRequested;
        private bool demoTrolleyAnchorReleaseRequested;
        private bool demoTrolleyAnchorInhibitObserved;
        private bool demoGantryAnchorApplyRequested;
        private bool demoGantryAnchorReleaseRequested;
        private bool demoGantryAnchorInhibitObserved;

        private void HandleDeveloperShortcuts()
        {
#if UNITY_EDITOR
            // Legacy UnityEngine.Input throws InvalidOperationException once Player Settings'
            // Active Input Handling is set to "Input System Package (New)" only - match the
            // Keyboard.current pattern already used everywhere else in the input layer
            // (YardCraneKeyboardInputSource) instead of the old polling API.
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.gKey.wasPressedThisFrame) SetGrayboxVisible(!showGraybox);
            if (keyboard.tKey.wasPressedThisFrame) BeginLoadedOperationRegression();
#endif
        }

        public bool BeginEmptyMechanicalRegression()
        {
            if (!loopsReady || setupFailed || startupPreTensioning || loadTransferPreTensioning ||
                containerLocked || loadedOperationRegressionActive || (runAutomatedDemo && !demoCompleted))
            {
                Debug.LogWarning(
                    $"[YC_EMPTY_MECHANICAL_REGRESSION_REJECTED] loopsReady={loopsReady}, " +
                    $"setupFailed={setupFailed}, preTension={startupPreTensioning}, " +
                    $"loadTransfer={loadTransferPreTensioning}, locked={containerLocked}, " +
                    $"loadedRegression={loadedOperationRegressionActive}, demoRunning={runAutomatedDemo && !demoCompleted}",
                    this);
                return false;
            }

            runAutomatedDemo = true;
            demoCompleted = false;
            demoStartedAt = Time.fixedTime;
            twistDisturbanceApplied = false;
            demoClampApplyRequested = false;
            demoClampReleaseRequested = false;
            demoClampInhibitObserved = false;
            demoGantryStartPosition = gantryDrive?.PositionMeters ?? 0f;
            demoGantryMaximumTravel = 0f;
            demoPumpOnRequested = false;
            demoPumpOffRequested = false;
            demoPumpCycleObserved = false;
            demoTrolleyAnchorApplyRequested = false;
            demoTrolleyAnchorReleaseRequested = false;
            demoTrolleyAnchorInhibitObserved = false;
            demoGantryAnchorApplyRequested = false;
            demoGantryAnchorReleaseRequested = false;
            demoGantryAnchorInhibitObserved = false;
            ClearObservedMetrics();
            Debug.Log($"[YC_EMPTY_MECHANICAL_REGRESSION_STARTED] model={ImplementationVersion}", this);
            return true;
        }

        private void RunAutomatedDemo(float elapsed)
        {
            currentTrolleyVelocity = 0f;
            if (elapsed < 1f)
            {
                runtimeStatus = "Settling eight straight-rope constraints";
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
                runtimeStatus = "Testing eight-rope geometric anti-twist response";
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

            var actuator = (IYardCraneOperationActuator)this;
            if (elapsed < 19f)
            {
                if (!demoPumpOnRequested)
                {
                    demoPumpOnRequested = actuator.TryQueueSpreaderPump(true, out _);
                }
                demoPumpCycleObserved |= SpreaderPumpOn;
                runtimeStatus = "Testing spreader hydraulic pump on";
                return;
            }

            if (elapsed < 20f)
            {
                if (!demoPumpOffRequested && SpreaderPumpOn)
                {
                    demoPumpOffRequested = actuator.TryQueueSpreaderPump(false, out _);
                }
                demoPumpCycleObserved &= !SpreaderPumpOn;
                runtimeStatus = "Testing spreader hydraulic pump off";
                return;
            }

            if (elapsed < 21f)
            {
                if (!demoTrolleyAnchorApplyRequested)
                {
                    demoTrolleyAnchorApplyRequested = actuator.TryQueueTrolleyAnchor(true, out _);
                }
                trolleyInput = CurrentTrolleyAnchorState == YardCraneAnchorState.Anchored ? 0.25f : 0f;
                UpdateManualTrolleyMotion();
                demoTrolleyAnchorInhibitObserved |= trolleyAnchorInhibitReported;
                runtimeStatus = "Testing trolley anchor motion interlock";
                return;
            }

            if (elapsed < 22f)
            {
                trolleyInput = 0f;
                if (!demoTrolleyAnchorReleaseRequested &&
                    CurrentTrolleyAnchorState == YardCraneAnchorState.Anchored)
                {
                    demoTrolleyAnchorReleaseRequested = actuator.TryQueueTrolleyAnchor(false, out _);
                }
                UpdateManualTrolleyMotion();
                runtimeStatus = "Releasing trolley anchor";
                return;
            }

            if (elapsed < 23f)
            {
                if (!demoGantryAnchorApplyRequested)
                {
                    demoGantryAnchorApplyRequested = actuator.TryQueueGantryAnchor(true, out _);
                }
                gantryInput = CurrentGantryAnchorState == YardCraneAnchorState.Anchored ? 0.25f : 0f;
                UpdateManualGantryMotion();
                demoGantryAnchorInhibitObserved |= gantryAnchorInhibitReported;
                runtimeStatus = "Testing gantry anchor motion interlock";
                return;
            }

            if (elapsed < 24f)
            {
                gantryInput = 0f;
                if (!demoGantryAnchorReleaseRequested &&
                    CurrentGantryAnchorState == YardCraneAnchorState.Anchored)
                {
                    demoGantryAnchorReleaseRequested = actuator.TryQueueGantryAnchor(false, out _);
                }
                UpdateManualGantryMotion();
                runtimeStatus = "Releasing gantry anchor";
                return;
            }

            if (elapsed < 25f)
            {
                trolleyInput = 0f;
                gantryInput = 0f;
                UpdateManualTrolleyMotion();
                UpdateManualGantryMotion();
                runtimeStatus = "Settling pump and anchor regression";
                return;
            }

            gantryInput = 0f;
            trolleyInput = 0f;
            demoCompleted = true;
            runAutomatedDemo = false;
            var attachmentsBound = loops.TrueForAll(loop => loop.VisualRope.AttachmentsBound);
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
            var pumpPassed = demoPumpOnRequested && demoPumpOffRequested && demoPumpCycleObserved &&
                             !SpreaderPumpOn;
            var anchorPassed = demoTrolleyAnchorApplyRequested && demoTrolleyAnchorReleaseRequested &&
                               demoTrolleyAnchorInhibitObserved &&
                               CurrentTrolleyAnchorState == YardCraneAnchorState.Released &&
                               demoGantryAnchorApplyRequested && demoGantryAnchorReleaseRequested &&
                               demoGantryAnchorInhibitObserved &&
                               CurrentGantryAnchorState == YardCraneAnchorState.Released;
            var passed = attachmentsBound && finiteState && headblockBody.position.y > 0.5f &&
                         steelRopePassed && antiSwayPassed && antiTwistPassed && gantryPassed &&
                         pumpPassed && anchorPassed;
            runtimeStatus = passed ? "PASS - MAX manual controls enabled" : "FAIL - inspect MAX topology";
            var message = $"model={ImplementationVersion}, profile={TopologyProfile}, attachmentsBound={attachmentsBound}, " +
                          $"finiteState={finiteState}, maxLoopExtension={maximumObservedLoopExtension * 1000f:F2}mm, " +
                          $"allowed={MaximumPermittedLoopExtensionMeters * 1000f:F2}mm, peakLoop={peakLoopId}, " +
                          $"rawDrift={maximumRawLoopDrift * 1000f:F2}mm, maxSway={maximumObservedSway:F3}m, " +
                          $"swayAllowed={PrototypeMaximumSwayMeters:F2}m, maxTwist={maximumObservedTwistDegrees:F2}deg, " +
                          $"residualTwist={residualTwistDegrees:F2}deg, twistAllowed={PrototypeMaximumResidualTwistDegrees:F2}deg, " +
                          $"gantryTravel={demoGantryMaximumTravel:F3}m, gantryPassed={gantryPassed}, " +
                          $"pumpPassed={pumpPassed}, anchorPassed={anchorPassed}, " +
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
                Debug.Log(
                    $"[YC_PUMP_ANCHOR_MECHANISM_PASS] pumpCycle={pumpPassed}, " +
                    $"trolleyAnchor={demoTrolleyAnchorInhibitObserved}, " +
                    $"gantryAnchor={demoGantryAnchorInhibitObserved}",
                    this);
                Debug.Log($"[YC_EIGHT_ROPE_MAX_PASS] {message}", this);
            }
            else
            {
                Debug.LogError($"[YC_EIGHT_ROPE_MAX_FAIL] {message}", this);
            }
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
    }
}
