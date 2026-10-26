using System;
using System.IO;
using PortTraining.YardCraneSimulator.Experiments.RopePhysics;
using PortTraining.YardCraneSimulator.Operations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Editor
{
    /// <summary>
    /// Runs the two Play-mode gates that protect the current yard-crane physics baseline.
    /// A request file under Temp allows the same verification to be started without
    /// relying on editor focus or synthetic input; it is never included in a build.
    /// </summary>
    [InitializeOnLoad]
    public static class YardCranePlayVerificationRunner
    {
        private const string RequestFileName = "YC_FullPlayVerification.request";
        private const string ResultFileName = "YC_FullPlayVerification.result.json";
        private const string OperationalScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        private const double OverallTimeoutSeconds = 300d;
        private const double RigReadyHoldSeconds = 0.75d;
        private const double EditorStableHoldSeconds = 1.5d;

        private enum VerificationPhase
        {
            Idle,
            WaitingForPlayMode,
            WaitingForRig,
            WaitingForSpreaderRotation,
            ResettingForLoadedRegression,
            WaitingForReset,
            WaitingForLoadedRegression,
            ResettingForLoadedRegression41,
            WaitingForReset41,
            WaitingForLoadedRegression41,
            Finishing
        }

        [Serializable]
        private sealed class VerificationResult
        {
            public string status;
            public string completedAtUtc;
            public string detail;
        }

        private static VerificationPhase phase;
        private static YardCraneEightRopeMaxPrototypeRig rig;
        private static double startedAt;
        private static double readyAt;
        private static double editorStableSince = -1d;
        private static bool stopPlayModeAfterRun;
        private static bool straightRopeTopologyValidated;

        static YardCranePlayVerificationRunner()
        {
            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        [MenuItem("Tools/Yard Crane/Tests/Run Full Play Verification")]
        public static void RunFullPlayVerification()
        {
            WriteRequest(stopAfterRun: !EditorApplication.isPlaying);
            TryStartPendingRequest();
        }

        // Command-line entry point for an isolated Unity validation workspace.
        // Do not pass -quit: this asynchronous runner exits Unity after the result is written.
        public static void RunFullPlayVerificationBatch()
        {
            if (!Application.isBatchMode)
            {
                throw new InvalidOperationException("Batch verification requires Unity -batchmode.");
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("Batch verification must start in Edit mode.");
            }

            EditorSceneManager.OpenScene(OperationalScenePath, OpenSceneMode.Single);
            WriteRequest(stopAfterRun: true);
            TryStartPendingRequest();
        }

        [MenuItem("Tools/Yard Crane/Tests/Run Full Play Verification", true)]
        private static bool CanRunFullPlayVerification() => phase == VerificationPhase.Idle;

        private static string ProjectTempPath => Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
            "Temp");

        private static string RequestPath => Path.Combine(ProjectTempPath, RequestFileName);
        private static string ResultPath => Path.Combine(
            Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath,
            "Logs",
            ResultFileName);

        private static void WriteRequest(bool stopAfterRun)
        {
            Directory.CreateDirectory(ProjectTempPath);
            File.WriteAllText(
                RequestPath,
                (stopAfterRun ? "stop-after-run" : "keep-playing") + "|pending-refresh");
            if (File.Exists(ResultPath))
            {
                File.Delete(ResultPath);
            }
        }

        private static void TryStartPendingRequest()
        {
            if (phase != VerificationPhase.Idle || !File.Exists(RequestPath))
            {
                return;
            }

            var request = File.ReadAllText(RequestPath).Trim();
            if (!EditorApplication.isPlaying && request.EndsWith("|pending-refresh", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(RequestPath, request.Replace("|pending-refresh", "|refreshed"));
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                editorStableSince = -1d;
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    editorStableSince = -1d;
                    return;
                }
                if (editorStableSince < 0d)
                {
                    editorStableSince = EditorApplication.timeSinceStartup;
                    return;
                }
                if (EditorApplication.timeSinceStartup - editorStableSince < EditorStableHoldSeconds)
                {
                    return;
                }

                var activeScene = SceneManager.GetActiveScene();
                if (!string.Equals(activeScene.path, OperationalScenePath, StringComparison.Ordinal))
                {
                    if (activeScene.isDirty)
                    {
                        WriteImmediateFailure(
                            $"active-scene-dirty:{activeScene.path}; YardCrane was not opened so the user's unsaved scene is preserved");
                        return;
                    }

                    EditorSceneManager.OpenScene(OperationalScenePath, OpenSceneMode.Single);
                    editorStableSince = -1d;
                    return;
                }
            }
            else if (!string.Equals(SceneManager.GetActiveScene().path, OperationalScenePath, StringComparison.Ordinal))
            {
                WriteImmediateFailure($"wrong-play-scene:{SceneManager.GetActiveScene().path}");
                return;
            }

            stopPlayModeAfterRun = string.Equals(
                request.Split('|')[0],
                "stop-after-run",
                StringComparison.OrdinalIgnoreCase);
            startedAt = EditorApplication.timeSinceStartup;
            rig = null;
            straightRopeTopologyValidated = false;
            editorStableSince = -1d;
            phase = EditorApplication.isPlaying
                ? VerificationPhase.WaitingForRig
                : VerificationPhase.WaitingForPlayMode;

            Debug.Log(
                $"[YC_FULL_PLAY_VERIFICATION_STARTED] stopAfterRun={stopPlayModeAfterRun}, " +
                $"scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path}");

            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = true;
            }
        }

        private static void OnEditorUpdate()
        {
            if (phase == VerificationPhase.Idle)
            {
                TryStartPendingRequest();
                return;
            }

            if (!File.Exists(RequestPath))
            {
                Finish(false, "request-file-removed");
                return;
            }

            if (EditorApplication.timeSinceStartup - startedAt > OverallTimeoutSeconds)
            {
                Finish(false, $"overall-timeout:{phase}");
                return;
            }

            switch (phase)
            {
                case VerificationPhase.WaitingForPlayMode:
                    if (EditorApplication.isPlaying)
                    {
                        phase = VerificationPhase.WaitingForRig;
                    }
                    else if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.isPlaying = true;
                    }
                    break;

                case VerificationPhase.WaitingForRig:
                    if (!EditorApplication.isPlaying)
                    {
                        break;
                    }

                    rig = UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>();
                    if (rig == null || !rig.LoopsReady)
                    {
                        break;
                    }

                    if (!straightRopeTopologyValidated)
                    {
                        if (!TryValidateStraightRopeActors(rig, out var topologyError))
                        {
                            Finish(false, topologyError);
                            return;
                        }

                        straightRopeTopologyValidated = true;
                        Debug.Log("[YC_EIGHT_STRAIGHT_ROPE_TOPOLOGY_PASS] visualRopes=8, legacyDeadEndLegs=0");

                        foreach (var yardContainer in UnityEngine.Object.FindObjectsByType<YardCraneContainer>(
                                     FindObjectsInactive.Include,
                                     FindObjectsSortMode.None))
                        {
                            if (yardContainer.gameObject.scene != rig.gameObject.scene)
                            {
                                continue;
                            }

                            if (!yardContainer.TryValidateLockHoleColliders(out var lockHoleError))
                            {
                                Finish(false,
                                    $"serialized-lock-hole-authoring-invalid:{yardContainer.name}:{lockHoleError}");
                                return;
                            }
                        }

                        Debug.Log("[YC_SERIALIZED_LOCK_HOLE_AUTHORING_PASS] containers=3, lockHoles=12");
                    }

                    if (readyAt <= 0d)
                    {
                        readyAt = EditorApplication.timeSinceStartup + RigReadyHoldSeconds;
                        break;
                    }

                    if (EditorApplication.timeSinceStartup < readyAt)
                    {
                        break;
                    }

                    phase = VerificationPhase.WaitingForSpreaderRotation;
                    YardCraneModelHierarchyBuilder.RunSpreaderRotationPlaySmokeTest();
                    break;

                case VerificationPhase.ResettingForLoadedRegression:
                    ResetForLoadedRegression();
                    break;

                case VerificationPhase.WaitingForReset:
                    StartLoadedRegressionWhenReady(30000f, VerificationPhase.WaitingForLoadedRegression);
                    break;

                case VerificationPhase.ResettingForLoadedRegression41:
                    ResetForLoadedRegression(VerificationPhase.WaitingForReset41);
                    break;

                case VerificationPhase.WaitingForReset41:
                    StartLoadedRegressionWhenReady(41000f, VerificationPhase.WaitingForLoadedRegression41);
                    break;
            }
        }

        private static bool TryValidateStraightRopeActors(
            YardCraneEightRopeMaxPrototypeRig targetRig,
            out string error)
        {
            error = string.Empty;
            var slots = targetRig.transform.Find("40_RopeRig/EightRopeLoops");
            if (slots == null)
            {
                error = "straight-rope-slots-missing";
                return false;
            }

            var visualRopeCount = 0;
            var legacyDeadEndLegCount = 0;
            foreach (var candidate in slots.GetComponentsInChildren<Transform>(true))
            {
                if (!candidate.name.StartsWith("ObiRope_", StringComparison.Ordinal))
                {
                    continue;
                }

                visualRopeCount++;
                if (candidate.name.IndexOf("DeadEndLeg", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    legacyDeadEndLegCount++;
                }
            }

            if (visualRopeCount != 8 || legacyDeadEndLegCount != 0)
            {
                error = $"straight-rope-topology-invalid:visualRopes={visualRopeCount},legacyDeadEndLegs={legacyDeadEndLegCount}";
                return false;
            }

            return true;
        }

        private static void ResetForLoadedRegression(
            VerificationPhase waitingPhase = VerificationPhase.WaitingForReset)
        {
            if (rig == null)
            {
                Finish(false, "rig-lost-before-reset");
                return;
            }

            var actuator = (IYardCraneOperationActuator)rig;
            if (!actuator.TryReset(out var rejectionCode))
            {
                Finish(false, $"reset-rejected:{rejectionCode}");
                return;
            }

            readyAt = EditorApplication.timeSinceStartup + RigReadyHoldSeconds;
            phase = waitingPhase;
        }

        private static void StartLoadedRegressionWhenReady(
            float grossMassKg,
            VerificationPhase runningPhase)
        {
            if (rig == null)
            {
                Finish(false, "rig-lost-after-reset");
                return;
            }

            var actuator = (IYardCraneOperationActuator)rig;
            if (EditorApplication.timeSinceStartup < readyAt || actuator.IsPreTensioning ||
                actuator.IsLoadTransferActive || actuator.IsMechanismBusy)
            {
                return;
            }

            if (!rig.TrySetContainerGrossMass(grossMassKg))
            {
                Finish(false, $"{grossMassKg / 1000f:F0}t-container-mass-rejected");
                return;
            }

            phase = runningPhase;
            rig.BeginLoadedOperationRegression();
            if (!rig.LoadedOperationRegressionActive)
            {
                Finish(false, "loaded-regression-not-started");
            }
        }

        private static void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (phase == VerificationPhase.WaitingForSpreaderRotation)
            {
                if (condition.Contains("[YC_SPREADER_ROTATION_PLAY_SMOKE_PASS]"))
                {
                    phase = VerificationPhase.ResettingForLoadedRegression;
                }
                else if (condition.Contains("[YC_SPREADER_ROTATION_PLAY_SMOKE_FAIL]"))
                {
                    Finish(false, condition);
                }
                return;
            }

            if (phase != VerificationPhase.WaitingForLoadedRegression &&
                phase != VerificationPhase.WaitingForLoadedRegression41)
            {
                return;
            }

            if (condition.Contains("[YC_LOADED_OPERATION_REGRESSION_PASS]"))
            {
                if (phase == VerificationPhase.WaitingForLoadedRegression)
                {
                    Debug.Log("[YC_LOADED_OPERATION_30T_PASS]");
                    phase = VerificationPhase.ResettingForLoadedRegression41;
                }
                else
                {
                    Debug.Log("[YC_LOADED_OPERATION_41T_PASS]");
                    Finish(true, condition);
                }
            }
            else if (condition.Contains("[YC_LOADED_OPERATION_REGRESSION_FAIL]") ||
                     condition.Contains("[YC_LOADED_OPERATION_REGRESSION_REJECTED]"))
            {
                Finish(false, condition);
            }
        }

        private static void Finish(bool passed, string detail)
        {
            if (phase == VerificationPhase.Finishing || phase == VerificationPhase.Idle)
            {
                return;
            }

            phase = VerificationPhase.Finishing;
            var result = new VerificationResult
            {
                status = passed ? "PASS" : "FAIL",
                completedAtUtc = DateTime.UtcNow.ToString("O"),
                detail = detail
            };

            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectTempPath);
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }

            var message =
                $"[YC_FULL_PLAY_VERIFICATION_{(passed ? "PASS" : "FAIL")}] " +
                $"duration={EditorApplication.timeSinceStartup - startedAt:F2}s, detail={detail}";
            if (passed)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }

            rig = null;
            readyAt = 0d;
            phase = VerificationPhase.Idle;

            if (stopPlayModeAfterRun && EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
            }

            if (Application.isBatchMode)
            {
                EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private static void WriteImmediateFailure(string detail)
        {
            var result = new VerificationResult
            {
                status = "FAIL",
                completedAtUtc = DateTime.UtcNow.ToString("O"),
                detail = detail
            };
            Directory.CreateDirectory(Path.GetDirectoryName(ResultPath) ?? ProjectTempPath);
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
            if (File.Exists(RequestPath))
            {
                File.Delete(RequestPath);
            }
            editorStableSince = -1d;
            Debug.LogError($"[YC_FULL_PLAY_VERIFICATION_FAIL] detail={detail}");
        }
    }
}
