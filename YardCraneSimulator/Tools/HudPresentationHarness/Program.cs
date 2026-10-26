using System;
using PortTraining.YardCraneSimulator.Presentation;
using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;
using PortTraining.YardCraneSimulator.Training;

internal static class HudPresentationTests
{
    private static int checks;
    private static void Expect(bool condition, string name)
    { if (!condition) throw new InvalidOperationException("[YC_HUD_TEST_FAIL] " + name); checks++; Console.WriteLine("PASS " + name); }

    private static YardCraneHudStatus Status(YardCraneHudSignals signals = YardCraneHudSignals.Ready,
        YardCraneOperationPhase phase = YardCraneOperationPhase.ReadyEmpty) =>
        new(phase, signals, WheelClampState.Released, YardCraneAnchorState.Released,
            YardCraneAnchorState.Released, YardCraneSpreaderSize.Ft40, YardCraneSpreaderSizeMechanismState.Idle, "KEYBOARD");
    private static YardCraneHudSnapshot Sample(double time, YardCraneHudStatus status) =>
        new(time, status, new YardCraneHudTelemetry(2, 1, 3, 0, 4, -1, 2));
    private static YardCraneHudSnapshot Sample(
        double time,
        YardCraneHudStatus status,
        float gantry,
        float trolley,
        float height,
        float rotation) =>
        new(time, status, new YardCraneHudTelemetry(gantry, 0, trolley, 0, height, 0, rotation));

    private static void Main()
    {
        var feed = new YardCraneHudFeed();
        var states = 0;
        var numbers = 0;
        feed.StatusChanged += () => states++;
        feed.TelemetryChanged += () => numbers++;
        Expect(feed.IsStale && !feed.HasSample, "initial-data-is-unknown");
        feed.Publish(Sample(1, Status()), 1);
        Expect(states == 1 && numbers == 1 && !feed.IsStale, "first-sample-publishes-both");
        feed.Publish(Sample(1.01, Status()), 1.01);
        Expect(states == 1 && numbers == 1, "unchanged-status-no-events-and-numbers-throttled");
        feed.Publish(Sample(1.02, Status(YardCraneHudSignals.Ready | YardCraneHudSignals.SafetyKnown)), 1.02);
        Expect(states == 2 && numbers == 1, "safety-change-bypasses-numeric-throttle");
        feed.Publish(Sample(1.11, Status(YardCraneHudSignals.Ready | YardCraneHudSignals.SafetyKnown)), 1.11);
        Expect(numbers == 2 && states == 2, "ten-hz-numeric-update");
        feed.Publish(Sample(1.0, Status()), 1.2);
        Expect(feed.Latest.SampledAtSeconds == 1.11, "old-sample-cannot-overwrite-newer-state");
        feed.Publish(Sample(99, Status()), 1.3);
        Expect(feed.Latest.SampledAtSeconds == 1.11, "future-sample-rejected");
        feed.Tick(1.7);
        Expect(feed.IsStale && states == 3 && numbers == 3, "stale-clears-status-and-numbers");
        feed.Publish(Sample(1.11, Status()), 1.71);
        Expect(feed.IsStale, "replayed-sample-cannot-restore-healthy");
        feed.Publish(Sample(1.72, Status()), 1.72);
        Expect(!feed.IsStale && states == 4 && numbers == 4, "fresh-sample-recovers-with-immediate-numbers");
        feed.Invalidate();
        Expect(feed.IsStale && states == 5 && numbers == 5, "destroyed-or-disabled-source-clears-ui-immediately");
        feed.Invalidate();
        Expect(states == 5, "no-event-storm-while-unavailable");
        Expect(YardCraneHudText.Number(float.NaN) == "—" && YardCraneHudText.Number(float.PositiveInfinity) == "—", "non-finite-numbers-never-display-as-zero");
        Expect(YardCraneHudText.Number(-0.001f) == "0.00", "negative-zero-suppressed");
        Expect(YardCraneHudText.Grab(Status(YardCraneHudSignals.Ready | YardCraneHudSignals.Locked | YardCraneHudSignals.Unlocking)).Text == "解锁中", "unlocking-wins-over-locked");
        Expect(YardCraneHudText.Grab(Status(YardCraneHudSignals.Locked)).Text == "未知", "uninitialized-lock-not-healthy");
        var spreaderContactStatus = Status(YardCraneHudSignals.Ready | YardCraneHudSignals.SpreaderContact);
        Expect(spreaderContactStatus.Has(YardCraneHudSignals.SpreaderContact) &&
               !spreaderContactStatus.Has(YardCraneHudSignals.Landed) &&
               !spreaderContactStatus.Has(YardCraneHudSignals.BrakeApplied),
            "spreader-contact-is-independent-from-container-landing-and-brake");
        Expect(YardCraneHudText.Clamp(WheelClampState.Unknown).Tone == HudTone.Caution, "unknown-clamp-not-released");
        Expect(YardCraneHudText.Anchor(YardCraneAnchorState.Transitioning).Text == "动作中", "transition-not-completion");
        Expect(YardCraneHudText.Banner(Status(YardCraneHudSignals.Ready | YardCraneHudSignals.SafetyKnown), false).Text == "急停已按下", "emergency-priority-over-power-and-ready");
        Expect(YardCraneHudText.Banner(Status(), true).Tone == HudTone.Danger, "stale-banner-never-healthy");
        Expect(YardCraneHudText.Banner(Status(YardCraneHudSignals.Ready, YardCraneOperationPhase.SafeHold), false).Tone == HudTone.Danger, "safe-hold-not-auto-recovered-by-ui");
        Expect(YardCraneHudText.Banner(Status(YardCraneHudSignals.Ready | YardCraneHudSignals.TrolleyRestricted), false).Text == "小车方向受限", "directional-interlock-visible");

        var reachableTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.GantryPosition,
            YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
            -3d,
            0.1d,
            "大车到达 -3 m");
        var unreachableTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.GantryPosition,
            YardCraneLocalTargetReferenceFrame.GantryHomeRelativeMeters,
            50d,
            0.1d,
            "大车到达 50 m");
        var reachability = new YardCraneLocalTrainingReachability(
            new YardCraneLocalReachableRange(-7.9d, 7.9d),
            new YardCraneLocalReachableRange(-4.47d, 4.47d),
            new YardCraneLocalReachableRange(-0.5d, 5.2d),
            new YardCraneLocalReachableRange(-3.7d, 2d),
            new YardCraneLocalReachableRange(-3d, 3d));
        Expect(YardCraneLocalTrainingTargetValidator.TryValidate(
                   reachableTarget, reachability, out var reachableError) && reachableError.Length == 0,
            "guided-scene-space-target-within-profile-range-is-accepted");
        Expect(!YardCraneLocalTrainingTargetValidator.TryValidate(
                   unreachableTarget, reachability, out var unreachableError) &&
               unreachableError.Contains("未做比例换算", StringComparison.Ordinal),
            "guided-target-outside-greybox-range-is-rejected-without-scaling");
        var relativeHeightTarget = new YardCraneLocalTrainingTarget(
            YardCraneLocalTrainingMetric.SpreaderHeight,
            YardCraneLocalTargetReferenceFrame.SpreaderStartRelativeMeters,
            1d,
            0.05d,
            "吊具上升 1 m");
        Expect(YardCraneLocalTrainingTargetValidator.TryValidate(
                   relativeHeightTarget, reachability, out _),
            "explicit-signed-start-relative-height-target-is-supported");

        var axisGoal = new YardCraneLearningGoalTracker(YardCraneLearningMetric.GantryPosition, 5f, 0.1f, 0.3);
        axisGoal.Update(Sample(2, Status(), 0, 0, 0, 0));
        var firstTargetSample = axisGoal.Update(Sample(2.1, Status(), 5, 0, 0, 0));
        Expect(firstTargetSample.State == YardCraneLearningGoalState.Active,
            "learning-target-requires-stable-dwell");
        var stableTarget = axisGoal.Update(Sample(2.41, Status(), 5.05f, 0, 0, 0));
        Expect(stableTarget.State == YardCraneLearningGoalState.Achieved && stableTarget.Progress01 == 1f,
            "learning-target-achieves-after-fresh-stable-samples");

        var staleDwellGoal = new YardCraneLearningGoalTracker(
            YardCraneLearningMetric.GantryPosition, 2f, 0.1f, 0.3);
        staleDwellGoal.Update(Sample(10, Status(), 0, 0, 0, 0));
        staleDwellGoal.Update(Sample(10.1, Status(), 2, 0, 0, 0));
        var afterSampleGap = staleDwellGoal.Update(Sample(10.7, Status(), 2, 0, 0, 0));
        Expect(afterSampleGap.State != YardCraneLearningGoalState.Achieved,
            "learning-dwell-resets-after-stale-sample-gap");
        var stableAfterRecovery = staleDwellGoal.Update(Sample(11.01, Status(), 2, 0, 0, 0));
        Expect(stableAfterRecovery.State == YardCraneLearningGoalState.Achieved,
            "learning-dwell-achieves-after-continuous-recovery-samples");

        var alreadyAtTarget = new YardCraneLearningGoalTracker(
            YardCraneLearningMetric.GantryPosition, 5f, 0.1f, 0.2);
        alreadyAtTarget.Update(Sample(2.5, Status(), 5, 0, 0, 0));
        var unchangedTarget = alreadyAtTarget.Update(Sample(2.8, Status(), 5, 0, 0, 0));
        Expect(unchangedTarget.State != YardCraneLearningGoalState.Achieved &&
               !unchangedTarget.ObservedRequiredAction,
            "learning-target-does-not-complete-without-an-observed-axis-action");

        var zeroGoal = new YardCraneLearningGoalTracker(YardCraneLearningMetric.SpreaderZeroing, 0f, 0.05f, 0.2);
        zeroGoal.Update(Sample(3.3, Status(), 0, 0, 0, 0));
        var initialZero = zeroGoal.Update(Sample(3.6, Status(), 0, 0, 0, 0));
        Expect(initialZero.State != YardCraneLearningGoalState.Achieved,
            "zeroing-does-not-complete-from-initial-zero");
        zeroGoal.Update(Sample(3.7, Status(), 0, 0, 0, 1f));
        zeroGoal.Update(Sample(3.8, Status(YardCraneHudSignals.Ready | YardCraneHudSignals.ReturningToZero), 0, 0, 0, 0.5f));
        zeroGoal.Update(Sample(3.9, Status(), 0, 0, 0, 0.02f));
        var returnedZero = zeroGoal.Update(Sample(4.11, Status(), 0, 0, 0, 0.01f));
        Expect(returnedZero.State == YardCraneLearningGoalState.Achieved && returnedZero.ObservedRequiredAction,
            "zeroing-requires-excursion-return-feedback-and-stable-zero");

        var heldZeroGoal = new YardCraneLearningGoalTracker(
            YardCraneLearningMetric.SpreaderZeroing, 0f, 0.05f, 1.5);
        heldZeroGoal.Update(Sample(4.2, Status(), 0, 0, 0, 0));
        heldZeroGoal.Update(Sample(4.3, Status(), 0, 0, 0, 1f));
        heldZeroGoal.Update(Sample(4.4,
            Status(YardCraneHudSignals.Ready | YardCraneHudSignals.ReturningToZero), 0, 0, 0, 0.5f));
        heldZeroGoal.Update(Sample(4.5, Status(), 0, 0, 0, 0.01f));
        var tooEarlyForHold = heldZeroGoal.Update(Sample(5.9, Status(), 0, 0, 0, 0.01f));
        Expect(tooEarlyForHold.State != YardCraneLearningGoalState.Achieved,
            "return-and-hold-zeroing-does-not-complete-before-required-hold");
        heldZeroGoal.Update(Sample(6.3, Status(), 0, 0, 0, 0.01f));
        heldZeroGoal.Update(Sample(6.7, Status(), 0, 0, 0, 0.01f));
        heldZeroGoal.Update(Sample(7.1, Status(), 0, 0, 0, 0.01f));
        var heldLongEnough = heldZeroGoal.Update(Sample(7.41, Status(), 0, 0, 0, 0.01f));
        Expect(heldLongEnough.State == YardCraneLearningGoalState.Achieved,
            "return-and-hold-zeroing-completes-after-required-hold");

        var assessment = new YardCraneLocalAssessmentTracker();
        Expect(assessment.Update(Sample(5, Status(), 0, 0, 2, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingLock,
            "assessment-starts-only-from-ready-unlocked-state");
        var lockedStatus = Status(YardCraneHudSignals.Ready | YardCraneHudSignals.Locked);
        Expect(assessment.Update(Sample(5.1, lockedStatus, 0, 0, 2, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingLoadedLift,
            "assessment-observes-completed-lock");
        var liftedStatus = Status(YardCraneHudSignals.Ready | YardCraneHudSignals.Locked,
            YardCraneOperationPhase.HoistingLoaded);
        Expect(assessment.Update(Sample(5.15,
                   Status(YardCraneHudSignals.Ready | YardCraneHudSignals.Locked,
                       YardCraneOperationPhase.ReadyLoaded), 0, 0, 2, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingLoadedLift,
            "assessment-ready-loaded-phase-alone-does-not-fake-lift");
        Expect(assessment.Update(Sample(5.2, liftedStatus, 0, 0, 2.3f, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingLoadedTravel,
            "assessment-observes-loaded-lift");
        Expect(assessment.Update(Sample(5.3, lockedStatus, 0, 0.6f, 2.3f, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingLanding,
            "assessment-observes-loaded-travel");
        var landedStatus = Status(YardCraneHudSignals.Ready | YardCraneHudSignals.Locked |
                                  YardCraneHudSignals.Landed, YardCraneOperationPhase.LandingConfirmed);
        Expect(assessment.Update(Sample(5.4, landedStatus, 0, 0.6f, 2, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingRelease,
            "assessment-observes-landing-before-release");
        var legalUnlockingStatus = Status(YardCraneHudSignals.Ready | YardCraneHudSignals.Locked |
                                          YardCraneHudSignals.Landed | YardCraneHudSignals.Unlocking,
            YardCraneOperationPhase.UnlockPending);
        Expect(assessment.Update(Sample(5.45, legalUnlockingStatus, 0, 0.6f, 2, 0)).Stage ==
               YardCraneLocalAssessmentStage.AwaitingRelease,
            "assessment-observes-unlock-start-while-still-landed");
        Expect(assessment.Update(Sample(5.5, Status(), 0, 0.6f, 2, 0)).Stage ==
               YardCraneLocalAssessmentStage.Passed,
            "assessment-passes-ordered-lock-lift-travel-land-release");

        var airborneRelease = new YardCraneLocalAssessmentTracker();
        airborneRelease.Update(Sample(20, Status(), 0, 0, 2, 0));
        airborneRelease.Update(Sample(20.1, lockedStatus, 0, 0, 2, 0));
        airborneRelease.Update(Sample(20.2, liftedStatus, 0, 0, 2.3f, 0));
        airborneRelease.Update(Sample(20.3, lockedStatus, 0, 0.6f, 2.3f, 0));
        airborneRelease.Update(Sample(20.4, landedStatus, 0, 0.6f, 2, 0));
        var landingLost = airborneRelease.Update(Sample(20.5, lockedStatus, 0, 0.6f, 2.3f, 0));
        Expect(landingLost.Stage == YardCraneLocalAssessmentStage.AwaitingLanding,
            "assessment-requires-new-landing-if-load-rises-before-unlock");
        var illegalAirborneRelease = airborneRelease.Update(Sample(20.6, Status(), 0, 0.6f, 2.3f, 0));
        Expect(illegalAirborneRelease.Stage == YardCraneLocalAssessmentStage.Failed,
            "assessment-never-passes-an-airborne-release");

        var timedOutAssessment = new YardCraneLocalAssessmentTracker();
        Expect(timedOutAssessment.Timeout().Stage == YardCraneLocalAssessmentStage.Failed &&
               timedOutAssessment.Progress.FailureReason.Length > 0,
            "assessment-timeout-is-local-failure-not-score");
        Console.WriteLine($"[YC_UI_HARNESS_PASS] assertions={checks}, unityPlay=false");
    }
}
