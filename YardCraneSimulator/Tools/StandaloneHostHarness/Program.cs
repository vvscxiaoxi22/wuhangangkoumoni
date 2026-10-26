using System;
using System.Reflection;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Operations;
using PortTraining.YardCraneSimulator.Training;
using UnityEngine;

internal static class StandaloneHostFocusProbe
{
    private sealed class Fixture
    {
        internal readonly object Actuator;
        internal readonly YardCraneOperationController Controller;
        internal readonly YardCraneStandaloneOperationHost Host;

        internal Fixture()
        {
            // Reuse the existing harness fake. No production actuator contract is re-created.
            var fakeType = typeof(YardCraneOperationController).Assembly.GetType("Program", true)
                .GetNestedType("FakeActuator", BindingFlags.NonPublic);
            Actuator = Activator.CreateInstance(fakeType, true);
            Controller = new YardCraneOperationController((IYardCraneOperationActuator)Actuator);
            Time.realtimeSinceStartupAsDouble = 10d;
            Application.isFocused = true;
            Host = new YardCraneStandaloneOperationHost();
            Host.Configure(Controller, "YC-FOCUS-PROBE");
            Sample();
            Step(10.001d);
        }

        internal void Sample(YardCraneOperatorIntent? intent = null, float hoist = 0f)
        {
            DeviceProbe.Keyboard = new YardCraneInputSnapshot(
                "KEYBOARD", true, 0f, 0f, hoist, false,
                intents: intent.HasValue ? new[] { intent.Value } : null);
            Invoke("Update");
        }

        internal void LoseFocus(double time)
        {
            Time.realtimeSinceStartupAsDouble = time;
            Application.isFocused = false;
            Invoke("OnApplicationFocus", false);
        }

        internal void RegainFocus(double time)
        {
            Time.realtimeSinceStartupAsDouble = time;
            Application.isFocused = true;
            Invoke("OnApplicationFocus", true);
        }

        internal void Step(double time)
        {
            Time.realtimeSinceStartupAsDouble = time;
            Invoke("FixedUpdate");
        }

        internal T ReadActuator<T>(string field) => (T)Actuator.GetType().GetField(field).GetValue(Actuator);

        internal void SetTrainingInput(bool enabled) => Host.SetTrainingInputEnabled(enabled);

        private void Invoke(string method, params object[] args) =>
            typeof(YardCraneStandaloneOperationHost).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(Host, args);

        internal void TripAndRestorePower()
        {
            Sample(YardCraneOperatorIntent.ControlPowerToggle);
            Step(10.020d);
            if (Controller.Phase != YardCraneOperationPhase.SafeHold)
                throw new InvalidOperationException("Probe precondition: power off did not enter SafeHold.");
            Sample(YardCraneOperatorIntent.ControlPowerToggle);
            Step(10.040d);
        }
    }

    private static int Main()
    {
        var failures = 0;

        YardCraneLocalTrainingSession.Clear();
        var localContext = YardCraneLocalTrainingSessionContext.CreateFreePractice(
            new YardCraneLocalTrainingParticipant("YC-HOST", "主机验收"));
        YardCraneLocalTrainingSession.Begin(localContext);
        var correlatedRun = new Fixture();
        Check("student-session-id-correlates-operation-run",
            correlatedRun.Controller.Context.OperationRunId == localContext.SessionId,
            $"operation={correlatedRun.Controller.Context.OperationRunId:D}, session={localContext.SessionId:D}",
            ref failures);
        YardCraneLocalTrainingSession.Clear();

        var countdownGate = new Fixture();
        countdownGate.Sample(YardCraneOperatorIntent.ContainerLock, hoist: 0.8f);
        countdownGate.SetTrainingInput(false);
        countdownGate.Step(10.020d);
        Check("countdown-gate-clears-queued-business-action",
            countdownGate.ReadActuator<int>("LockCount") == 0,
            $"lockCount={countdownGate.ReadActuator<int>("LockCount")}", ref failures);
        Check("countdown-gate-submits-zero-motion-without-safehold",
            countdownGate.ReadActuator<float>("HoistInput") == 0f &&
            countdownGate.Controller.Phase != YardCraneOperationPhase.SafeHold &&
            !countdownGate.Host.IsTrainingInputEnabled,
            $"hoist={countdownGate.ReadActuator<float>("HoistInput")}, phase={countdownGate.Controller.Phase}",
            ref failures);
        countdownGate.Sample(YardCraneOperatorIntent.ContainerLock, hoist: 0.7f);
        countdownGate.Step(10.040d);
        Check("countdown-gate-rejects-new-business-input",
            countdownGate.ReadActuator<int>("LockCount") == 0 &&
            countdownGate.ReadActuator<float>("HoistInput") == 0f,
            $"lockCount={countdownGate.ReadActuator<int>("LockCount")}, hoist={countdownGate.ReadActuator<float>("HoistInput")}",
            ref failures);
        countdownGate.SetTrainingInput(true);
        countdownGate.Sample(hoist: 0.6f);
        countdownGate.Step(10.060d);
        Check("countdown-gate-reopens-after-explicit-enable",
            countdownGate.Host.IsTrainingInputEnabled &&
            Math.Abs(countdownGate.ReadActuator<float>("HoistInput") - 0.6f) < 0.0001f,
            $"hoist={countdownGate.ReadActuator<float>("HoistInput")}", ref failures);

        var gatedEmergencyStop = new Fixture();
        gatedEmergencyStop.SetTrainingInput(false);
        gatedEmergencyStop.Sample(YardCraneOperatorIntent.EmergencyStopToggle, hoist: 0.9f);
        gatedEmergencyStop.Step(10.020d);
        Check("countdown-gate-preserves-emergency-stop",
            !gatedEmergencyStop.Host.IsEmergencyStopReleased &&
            gatedEmergencyStop.Controller.Phase == YardCraneOperationPhase.SafeHold,
            $"released={gatedEmergencyStop.Host.IsEmergencyStopReleased}, phase={gatedEmergencyStop.Controller.Phase}",
            ref failures);

        var focusedLock = new Fixture();
        focusedLock.Sample(YardCraneOperatorIntent.ContainerLock);
        focusedLock.Step(10.020d);
        Check("focused-lock-positive-control", focusedLock.ReadActuator<int>("LockCount") == 1,
            $"lockCount={focusedLock.ReadActuator<int>("LockCount")}", ref failures);

        // A queued discrete action (lock/unlock/wheel-clamp/anchor/pump/size) is deliberately
        // NOT cancelled by a brief focus blip - it re-validates real physical state at dispatch
        // time (see YardCraneEightRopeMaxPrototypeRig's TryQueueContainerLock etc.), so a
        // momentary loss doesn't need to punish an action the operator genuinely requested. Only
        // FaultReset gets the stricter deferral below, since it alone trusts a raw axis reading
        // (HandlesCentered) instead of re-checking live physical state.
        var shortLoss = new Fixture();
        shortLoss.Sample(YardCraneOperatorIntent.ContainerLock, hoist: 0.6f);
        shortLoss.LoseFocus(10.010d);
        shortLoss.Step(10.020d);
        Check("focus-grace-queued-lock-still-executes", shortLoss.ReadActuator<int>("LockCount") == 1,
            $"lockCount={shortLoss.ReadActuator<int>("LockCount")}, phase={shortLoss.Controller.Phase}", ref failures);
        Check("focus-grace-motion-zero", shortLoss.ReadActuator<float>("HoistInput") == 0f,
            $"hoistInput={shortLoss.ReadActuator<float>("HoistInput")}", ref failures);

        var longLoss = new Fixture();
        longLoss.Sample(YardCraneOperatorIntent.ContainerLock);
        longLoss.LoseFocus(10.010d);
        longLoss.Step(10.420d);
        Check("long-focus-loss-holds-and-clears",
            longLoss.Controller.Phase == YardCraneOperationPhase.SafeHold && longLoss.ReadActuator<int>("LockCount") == 0,
            $"lockCount={longLoss.ReadActuator<int>("LockCount")}, phase={longLoss.Controller.Phase}", ref failures);

        var focusedReset = new Fixture();
        focusedReset.TripAndRestorePower();
        focusedReset.Sample(YardCraneOperatorIntent.FaultReset, hoist: 0.6f);
        focusedReset.Step(10.060d);
        Check("focused-deflected-reset-rejected", focusedReset.Controller.Phase == YardCraneOperationPhase.SafeHold,
            $"phase={focusedReset.Controller.Phase}", ref failures);

        var unfocusedReset = new Fixture();
        unfocusedReset.TripAndRestorePower();
        unfocusedReset.Sample(YardCraneOperatorIntent.FaultReset, hoist: 0.6f);
        unfocusedReset.LoseFocus(10.050d);
        unfocusedReset.Step(10.060d);
        Check("focus-grace-must-not-synthesize-centered-reset", unfocusedReset.Controller.Phase == YardCraneOperationPhase.SafeHold,
            $"phase={unfocusedReset.Controller.Phase}, syntheticCentered={unfocusedReset.Host.LatestCommand.HandlesCentered}", ref failures);

        // The FaultReset queued above is deferred, not dropped: continuation of the same
        // fixture proves it is re-evaluated (not silently lost) once a genuine post-focus
        // sample lands. Regain focus with the stick still deflected - the deferred request
        // must be fairly rejected against the real sample, not waved through, and (like any
        // rejected reset) is then consumed - a fresh R press is required next, same as the
        // always-focused case.
        unfocusedReset.RegainFocus(10.065d);
        unfocusedReset.Sample(hoist: 0.6f);
        unfocusedReset.Step(10.070d);
        Check("focus-return-deflected-reset-still-rejected", unfocusedReset.Controller.Phase == YardCraneOperationPhase.SafeHold,
            $"phase={unfocusedReset.Controller.Phase}", ref failures);

        // A fresh, genuinely centered FaultReset request now clears SafeHold normally - the
        // deferral above didn't leave the Host in a state that can no longer recover.
        unfocusedReset.Sample(YardCraneOperatorIntent.FaultReset, hoist: 0f);
        unfocusedReset.Step(10.080d);
        Check("focus-return-centered-reset-clears", unfocusedReset.Controller.Phase != YardCraneOperationPhase.SafeHold,
            $"phase={unfocusedReset.Controller.Phase}", ref failures);

        Console.WriteLine($"[YC_HOST_FOCUS_PROBE] failures={failures}, environment=dotnet-with-unity-shim, unityPlay=false");
        return failures == 0 ? 0 : 1;
    }

    private static void Check(string name, bool passed, string detail, ref int failures)
    {
        if (!passed) failures++;
        Console.WriteLine($"[{(passed ? "PASS" : "FAIL")}] {name}: {detail}");
    }
}
