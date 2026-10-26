using System;
using System.Collections;
using System.Collections.Generic;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Experiments.RopePhysics;
using PortTraining.YardCraneSimulator.Operations;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Editor
{
    public static class YardCraneModelHierarchyBuilder
    {
        public const string RootName = "YC_RuntimeRoot";
        public const string OperationalScenePath = "Assets/YardCraneSimulator/Scenes/YardCrane.unity";
        private const string GrayboxMaterialFolder = "Assets/YardCraneSimulator/Art/Materials/Graybox";

        private static readonly string[] RequiredPaths =
        {
            "00_ScenePlacement/ImportedModel_Source",
            "10_GantryMotion/Visual",
            "10_GantryMotion/Visual/Graybox_Placeholder",
            "10_GantryMotion/Physics/Colliders",
            "10_GantryMotion/Sensors/Gantry_ForwardLimit",
            "10_GantryMotion/Sensors/Gantry_BackwardLimit",
            "20_TrolleyMotion/Visual",
            "20_TrolleyMotion/Visual/Graybox_Placeholder",
            "20_TrolleyMotion/Physics/Colliders",
            "20_TrolleyMotion/Hoist/Drum_A/Visual",
            "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop01_Exit",
            "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop02_Exit",
            "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop03_Exit",
            "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop04_Exit",
            "20_TrolleyMotion/Hoist/Drum_B/Visual",
            "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop05_Exit",
            "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop06_Exit",
            "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop07_Exit",
            "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop08_Exit",
            // Legacy v1 top-anchor candidates remain stable scene slots. The v2 straight-rope
            // profile selects only four of these via topAnchorPath; none creates a second leg.
            "20_TrolleyMotion/RopeDeadEnds/Loop01_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop02_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop03_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop04_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop05_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop06_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop07_DeadEnd",
            "20_TrolleyMotion/RopeDeadEnds/Loop08_DeadEnd",
            "20_TrolleyMotion/MicroMotion/TrolleyActuators",
            "20_TrolleyMotion/Sensors/Trolley_ForwardLimit",
            "20_TrolleyMotion/Sensors/Trolley_BackwardLimit",
            "30_SpreaderPhysics/Visual",
            "30_SpreaderPhysics/Visual/Graybox_Placeholder",
            "30_SpreaderPhysics/Physics/RigidbodyRoot",
            "30_SpreaderPhysics/Physics/Colliders",
            "30_SpreaderPhysics/Physics/Colliders/Graybox_HeadblockSpreaderCollider",
            "30_SpreaderPhysics/Headblock/Visual",
            "30_SpreaderPhysics/RopeSheaves/Loop01_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop02_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop03_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop04_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop05_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop06_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop07_Sheave",
            "30_SpreaderPhysics/RopeSheaves/Loop08_Sheave",
            "30_SpreaderPhysics/MicroMotion/HeadblockActuators",
            "30_SpreaderPhysics/35_Spreader/Visual",
            "30_SpreaderPhysics/35_Spreader/HeadblockLocks",
            "30_SpreaderPhysics/ContainerAttachment/Twistlock_FL",
            "30_SpreaderPhysics/ContainerAttachment/Twistlock_FR",
            "30_SpreaderPhysics/ContainerAttachment/Twistlock_RL",
            "30_SpreaderPhysics/ContainerAttachment/Twistlock_RR",
            "30_SpreaderPhysics/Sensors/Spreader_Landing",
            "30_SpreaderPhysics/Sensors/Spreader_Contact",
            "40_RopeRig/EightRopeLoops/Loop01_X_FL",
            "40_RopeRig/EightRopeLoops/Loop02_X_FR",
            "40_RopeRig/EightRopeLoops/Loop03_X_RL",
            "40_RopeRig/EightRopeLoops/Loop04_X_RR",
            "40_RopeRig/EightRopeLoops/Loop05_Z_FL",
            "40_RopeRig/EightRopeLoops/Loop06_Z_FR",
            "40_RopeRig/EightRopeLoops/Loop07_Z_RL",
            "40_RopeRig/EightRopeLoops/Loop08_Z_RR",
            "40_RopeRig/_ObiSceneComponents",
            "50_ContainerWorkspace/ActiveContainer",
            "50_ContainerWorkspace/ActiveContainer/Graybox_Placeholder",
            "50_ContainerWorkspace/ContainerFreeBody",
            "50_ContainerWorkspace/AttachmentProxy",
            "60_Sensors/CollisionSensors",
            "60_Sensors/AlignmentSensors",
            "60_Sensors/SafetyZones",
            "70_Cameras/LocalCabinCameraMount",
            "70_Cameras/ROCS_MainCameraMount",
            "70_Cameras/ROCS_SpreaderCameraMount",
            "70_Cameras/ROCS_LaneCameraMount",
            "80_Audio/Motors",
            "80_Audio/Alarms",
            "80_Audio/Environment",
            "90_Diagnostics/DebugVisuals",
            "90_Diagnostics/RuntimeEvidence"
        };

        [MenuItem("Tools/Yard Crane/Model Hierarchy/Create or Repair YC Runtime Root")]
        public static void CreateOrRepairInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("No loaded scene is available for the yard-crane hierarchy.");
            }

            var root = FindRoot(scene);
            if (root == null)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, $"Create {RootName}");
            }

            foreach (var path in RequiredPaths)
            {
                EnsurePath(root.transform, path);
            }

            var prototypeRig = root.GetComponent<YardCraneEightRopeMaxPrototypeRig>();
            var addedPrototypeRig = prototypeRig == null;
            if (addedPrototypeRig)
            {
                prototypeRig = Undo.AddComponent<YardCraneEightRopeMaxPrototypeRig>(root);
                ApplyDefaultMaxPrototypePose(root.transform);
            }

            var legacyFourRopeRig = root.GetComponent<YardCraneObiRopePrototypeRig>();
            if (legacyFourRopeRig != null)
            {
                Undo.DestroyObjectImmediate(legacyFourRopeRig);
            }

            CreateOrRepairGraybox(root.transform);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = root;
            Debug.Log($"[YC_HIERARCHY_READY] {RootName} created/repaired with {RequiredPaths.Length} required slots.");
        }

        // 2026-09-11: ApplyDefaultMaxPrototypePose only runs automatically the first time the
        // YardCraneEightRopeMaxPrototypeRig component is added to a scene (see
        // CreateOrRepairInActiveScene above), so an existing scene's trolley/spreader/rope-node
        // local positions do not pick up new pose defaults on their own. This menu item is the
        // explicit, opt-in way to push current defaults (e.g. the 2026-09-11 24m/13.5m rescale)
        // onto an already-populated scene without silently altering CreateOrRepairInActiveScene's
        // existing first-creation-only behavior.
        [MenuItem("Tools/Yard Crane/Model Hierarchy/Reapply Default MAX Prototype Pose")]
        public static void ReapplyDefaultPoseInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("No loaded scene is available for the yard-crane hierarchy.");
            }

            var root = FindRoot(scene);
            if (root == null)
            {
                throw new InvalidOperationException($"{RootName} does not exist in the active scene; run \"Create or Repair YC Runtime Root\" first.");
            }

            Undo.RegisterFullObjectHierarchyUndo(root, "Reapply Default MAX Prototype Pose");
            ApplyDefaultMaxPrototypePose(root.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[YC_HIERARCHY_POSE_REAPPLIED] {RootName} trolley/spreader/rope-node local positions reset to current ApplyDefaultMaxPrototypePose defaults.");
        }

        public static void CreateAndValidateOperationalSceneHierarchy()
        {
            EditorSceneManager.OpenScene(OperationalScenePath, OpenSceneMode.Single);
            CreateOrRepairInActiveScene();
            ValidateInActiveScene();
        }

        // Retained for existing automation callers while the scene name migrates.
        [Obsolete("Use CreateAndValidateOperationalSceneHierarchy instead.")]
        public static void CreateAndValidateTestSceneHierarchy()
        {
            CreateAndValidateOperationalSceneHierarchy();
        }

        [MenuItem("Tools/Yard Crane/Model Hierarchy/Validate YC Runtime Root")]
        public static void ValidateInActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            var root = FindRoot(scene);
            if (root == null)
            {
                throw new InvalidOperationException($"Missing scene root: {RootName}");
            }

            var missing = new List<string>();
            foreach (var path in RequiredPaths)
            {
                if (root.transform.Find(path) == null)
                {
                    missing.Add(path);
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException("Yard-crane hierarchy is incomplete:\n" + string.Join("\n", missing));
            }

            Debug.Log($"[YC_HIERARCHY_PASS] {RootName} contains all {RequiredPaths.Length} required slots.");
        }

        [MenuItem("Tools/Yard Crane/Tests/Run Spreader Rotation Play Smoke Test")]
        public static void RunSpreaderRotationPlaySmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogError("[YC_SPREADER_ROTATION_PLAY_SMOKE_FAIL] reason=editor-not-playing");
                return;
            }

            var rig = UnityEngine.Object.FindFirstObjectByType<YardCraneEightRopeMaxPrototypeRig>();
            if (rig == null || !rig.LoopsReady)
            {
                Debug.LogError(
                    $"[YC_SPREADER_ROTATION_PLAY_SMOKE_FAIL] reason=rig-not-ready, rigFound={rig != null}");
                return;
            }

            var standaloneHost = UnityEngine.Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>();
            rig.StartCoroutine(RunSpreaderRotationPlaySmokeTest(rig, standaloneHost));
        }

        [MenuItem("Tools/Yard Crane/Tests/Run Spreader Rotation Play Smoke Test", true)]
        private static bool CanRunSpreaderRotationPlaySmokeTest() => EditorApplication.isPlaying;

        private static IEnumerator RunSpreaderRotationPlaySmokeTest(
            YardCraneEightRopeMaxPrototypeRig rig,
            YardCraneStandaloneOperationHost standaloneHost)
        {
            var actuator = (IYardCraneOperationActuator)rig;
            var hostWasEnabled = standaloneHost != null && standaloneHost.enabled;
            var pumpWasOn = rig.SpreaderPumpOn;
            var failure = string.Empty;
            var peakLogicalAngle = 0f;
            var peakPhysicalAngle = 0f;

            if (standaloneHost != null)
            {
                standaloneHost.enabled = false;
            }

            try
            {
                if (!actuator.TryQueueSpreaderPump(true, out var pumpError))
                {
                    failure = $"pump-request-rejected:{pumpError}";
                }
                else
                {
                    var driveUntil = Time.realtimeSinceStartup + 2.25f;
                    while (Time.realtimeSinceStartup < driveUntil)
                    {
                        actuator.SetSpreaderRotationInput(1f);
                        yield return new WaitForFixedUpdate();
                        peakLogicalAngle = Mathf.Max(peakLogicalAngle, rig.SpreaderRotationAngleDegrees);
                        peakPhysicalAngle = Mathf.Max(peakPhysicalAngle, rig.SpreaderPhysicalRotationAngleDegrees);
                    }

                    actuator.SetSpreaderRotationInput(0f);
                    yield return new WaitForFixedUpdate();

                    if (peakLogicalAngle < 2.9f || peakPhysicalAngle < 2.9f ||
                        Mathf.Abs(peakLogicalAngle - peakPhysicalAngle) > 0.02f)
                    {
                        failure =
                            $"positive-limit-not-reached:logical={peakLogicalAngle:F2},physical={peakPhysicalAngle:F2}";
                    }

                    if (!actuator.TryQueueSpreaderRotationReturnToZero(out var returnError))
                    {
                        failure = string.IsNullOrEmpty(failure)
                            ? $"return-request-rejected:{returnError}"
                            : $"{failure};return-request-rejected:{returnError}";
                    }
                    else
                    {
                        var returnDeadline = Time.realtimeSinceStartup + 4f;
                        while (rig.SpreaderRotationReturningToZero &&
                               Time.realtimeSinceStartup < returnDeadline)
                        {
                            actuator.SetSpreaderRotationInput(0f);
                            yield return new WaitForFixedUpdate();
                        }

                        var returnedToZero =
                            !rig.SpreaderRotationReturningToZero &&
                            Mathf.Abs(rig.SpreaderRotationAngleDegrees) <= 0.01f &&
                            Mathf.Abs(rig.SpreaderPhysicalRotationAngleDegrees) <= 0.01f;
                        if (!returnedToZero)
                        {
                            failure = string.IsNullOrEmpty(failure)
                                ? $"return-timeout:logical={rig.SpreaderRotationAngleDegrees:F2},physical={rig.SpreaderPhysicalRotationAngleDegrees:F2}"
                                : $"{failure};return-timeout:logical={rig.SpreaderRotationAngleDegrees:F2},physical={rig.SpreaderPhysicalRotationAngleDegrees:F2}";
                        }
                    }
                }

                if (string.IsNullOrEmpty(failure))
                {
                    Debug.Log(
                        $"[YC_SPREADER_ROTATION_PLAY_SMOKE_PASS] physicalBound={rig.SpreaderPhysicalRotationBound}, " +
                        $"peakLogical={peakLogicalAngle:F2}deg, peakPhysical={peakPhysicalAngle:F2}deg, " +
                        $"returnedLogical={rig.SpreaderRotationAngleDegrees:F2}deg, " +
                        $"returnedPhysical={rig.SpreaderPhysicalRotationAngleDegrees:F2}deg",
                        rig);
                }
                else
                {
                    Debug.LogError(
                        $"[YC_SPREADER_ROTATION_PLAY_SMOKE_FAIL] reason={failure}, " +
                        $"physicalBound={rig.SpreaderPhysicalRotationBound}",
                        rig);
                }
            }
            finally
            {
                actuator.SetSpreaderRotationInput(0f);
                if (Mathf.Abs(rig.SpreaderPhysicalRotationAngleDegrees) <= 0.01f &&
                    rig.SpreaderPumpOn != pumpWasOn)
                {
                    actuator.TryQueueSpreaderPump(pumpWasOn, out _);
                }

                if (standaloneHost != null)
                {
                    standaloneHost.enabled = hostWasEnabled;
                }
            }
        }

        private static GameObject FindRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == RootName)
                {
                    return root;
                }
            }

            return null;
        }

        private static Transform EnsurePath(Transform root, string path)
        {
            var current = root;
            foreach (var segment in path.Split('/'))
            {
                var child = current.Find(segment);
                if (child == null)
                {
                    var childObject = new GameObject(segment);
                    Undo.RegisterCreatedObjectUndo(childObject, $"Create {segment}");
                    childObject.transform.SetParent(current, false);
                    child = childObject.transform;
                }

                current = child;
            }

            return current;
        }

        private static void ApplyDefaultMaxPrototypePose(Transform root)
        {
            // 2026-09-11 real-scale recalibration: trolley Y tracks the user-specified 24m
            // structure height directly (this is the "trolleyWorldY" reference the hoist-range
            // math in YardCraneEightRopeMaxProfile.GeometryAndSafetySection is derived from -
            // keep these two in sync if either changes). Spreader startup Y is placed at the
            // same proportion of the new reachable range [0m, 21m] that the old startup pose
            // (3.2) occupied in the old range [-0.5m, 5.2m] (~65% of the way up) - a reasoned
            // carry-over, not a requirement; the physics settles it on its own after startup
            // pre-tensioning regardless of the exact starting value. Container rest Y is
            // unchanged - it is half the container's own physical height above ground, and
            // container size is not part of this round's rescale.
            SetLocalPosition(root, "20_TrolleyMotion", new Vector3(0f, 24f, 0f));
            SetLocalPosition(root, "30_SpreaderPhysics", new Vector3(0f, 13.5f, 0f));
            SetLocalPosition(root, "50_ContainerWorkspace/ActiveContainer", new Vector3(0f, 1.35f, 0f));

            // Drum A legacy top-anchor candidates. The v2 straight-rope profile selects a
            // left/right symmetric subset from Exit and DeadEnd slots; these points no longer
            // form four two-leg triangles.
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop01_Exit", new Vector3(-2.2f, -0.35f, 0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop02_Exit", new Vector3(0.1f, -0.35f, 0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop03_Exit", new Vector3(-2.2f, -0.35f, -0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_A/RopeExits/Loop04_Exit", new Vector3(0.1f, -0.35f, -0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop01_DeadEnd", new Vector3(-0.1f, -0.35f, 0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop02_DeadEnd", new Vector3(2.2f, -0.35f, 0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop03_DeadEnd", new Vector3(-0.1f, -0.35f, -0.85f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop04_DeadEnd", new Vector3(2.2f, -0.35f, -0.85f));

            // Drum B legacy top-anchor candidates. The v2 profile selects a front/rear
            // symmetric subset so the eight independent ropes do not introduce a net side load.
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop05_Exit", new Vector3(-0.95f, -0.35f, 1.9f));
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop06_Exit", new Vector3(0.95f, -0.35f, 1.9f));
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop07_Exit", new Vector3(-0.95f, -0.35f, 0f));
            SetLocalPosition(root, "20_TrolleyMotion/Hoist/Drum_B/RopeExits/Loop08_Exit", new Vector3(0.95f, -0.35f, 0f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop05_DeadEnd", new Vector3(-0.95f, -0.35f, 0f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop06_DeadEnd", new Vector3(0.95f, -0.35f, 0f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop07_DeadEnd", new Vector3(-0.95f, -0.35f, -1.9f));
            SetLocalPosition(root, "20_TrolleyMotion/RopeDeadEnds/Loop08_DeadEnd", new Vector3(0.95f, -0.35f, -1.9f));

            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop01_Sheave", new Vector3(-1.15f, 0.2f, 0.85f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop02_Sheave", new Vector3(1.15f, 0.2f, 0.85f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop03_Sheave", new Vector3(-1.15f, 0.2f, -0.85f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop04_Sheave", new Vector3(1.15f, 0.2f, -0.85f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop05_Sheave", new Vector3(-0.95f, 0.2f, 0.95f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop06_Sheave", new Vector3(0.95f, 0.2f, 0.95f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop07_Sheave", new Vector3(-0.95f, 0.2f, -0.95f));
            SetLocalPosition(root, "30_SpreaderPhysics/RopeSheaves/Loop08_Sheave", new Vector3(0.95f, 0.2f, -0.95f));
        }

        private static void CreateOrRepairGraybox(Transform root)
        {
            var gantry = root.Find("10_GantryMotion/Visual/Graybox_Placeholder");
            var trolley = root.Find("20_TrolleyMotion/Visual/Graybox_Placeholder");
            var spreader = root.Find("30_SpreaderPhysics/Visual/Graybox_Placeholder");
            var container = root.Find("50_ContainerWorkspace/ActiveContainer/Graybox_Placeholder");

            // 2026-09-11 real-scale recalibration: X spread (~29m between leg centers, 30m
            // beam) matches the user's 30m span; leg height 24f matches the user's 24m structure
            // height (kept equal to "20_TrolleyMotion" Y above, and to the hoist-range math in
            // YardCraneEightRopeMaxProfile - all three must move together). Z leg spread (2.8f,
            // the gantry's own travel-direction wheelbase footprint) and the leg/beam cross-
            // section thickness are left unchanged - they are not driven by any of the three
            // user-specified numbers (span/height/rail length), just kept as a reasonable
            // greybox proportion; this is a placeholder box, not a structural design.
            CreateCube(gantry, "Gantry_Leg_FL", new Vector3(-14.5f, 12f, 2.8f), new Vector3(0.45f, 24f, 0.45f), "GantryBlue", new Color(0.10f, 0.38f, 0.82f));
            CreateCube(gantry, "Gantry_Leg_FR", new Vector3(14.5f, 12f, 2.8f), new Vector3(0.45f, 24f, 0.45f), "GantryBlue", new Color(0.10f, 0.38f, 0.82f));
            CreateCube(gantry, "Gantry_Leg_RL", new Vector3(-14.5f, 12f, -2.8f), new Vector3(0.45f, 24f, 0.45f), "GantryBlue", new Color(0.10f, 0.38f, 0.82f));
            CreateCube(gantry, "Gantry_Leg_RR", new Vector3(14.5f, 12f, -2.8f), new Vector3(0.45f, 24f, 0.45f), "GantryBlue", new Color(0.10f, 0.38f, 0.82f));
            CreateCube(gantry, "Gantry_TopBeam_Front", new Vector3(0f, 24f, 2.8f), new Vector3(30f, 0.5f, 0.55f), "GantryLightBlue", new Color(0.12f, 0.52f, 0.95f));
            CreateCube(gantry, "Gantry_TopBeam_Rear", new Vector3(0f, 24f, -2.8f), new Vector3(30f, 0.5f, 0.55f), "GantryLightBlue", new Color(0.12f, 0.52f, 0.95f));

            CreateCube(trolley, "Trolley_Body", Vector3.zero, new Vector3(3f, 0.55f, 2.5f), "TrolleyAmber", new Color(1f, 0.68f, 0.08f));
            CreateCube(spreader, "Spreader_Body", Vector3.zero, new Vector3(4f, 0.5f, 2.2f), "SpreaderCyan", new Color(0.10f, 0.82f, 0.68f));
            CreateCube(container, "Container_Body", Vector3.zero, new Vector3(4.8f, 2.4f, 2.4f), "ContainerOrange", new Color(0.95f, 0.34f, 0.12f));
        }

        private static void CreateCube(Transform parent, string objectName, Vector3 localPosition, Vector3 localScale, string materialName, Color color)
        {
            if (parent == null)
            {
                return;
            }

            var existing = parent.Find(objectName);
            var cube = existing != null ? existing.gameObject : GameObject.CreatePrimitive(PrimitiveType.Cube);
            if (existing == null)
            {
                Undo.RegisterCreatedObjectUndo(cube, $"Create {objectName}");
                cube.name = objectName;
                cube.transform.SetParent(parent, false);
            }

            cube.transform.localPosition = localPosition;
            cube.transform.localRotation = Quaternion.identity;
            cube.transform.localScale = localScale;

            var collider = cube.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            var renderer = cube.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = GetOrCreateGrayboxMaterial(materialName, color);
            }
        }

        private static Material GetOrCreateGrayboxMaterial(string materialName, Color color)
        {
            EnsureAssetFolder(GrayboxMaterialFolder);
            var assetPath = $"{GrayboxMaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader == null)
                {
                    throw new InvalidOperationException("No supported shader is available for yard-crane graybox materials.");
                }

                material = new Material(shader) { name = materialName };
                AssetDatabase.CreateAsset(material, assetPath);
                Debug.Log($"[YC_GRAYBOX_MATERIAL_CREATED] {assetPath}");
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var index = 1; index < segments.Length; index++)
            {
                var next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

        private static void SetLocalPosition(Transform root, string path, Vector3 position)
        {
            var target = root.Find(path);
            if (target != null)
            {
                target.localPosition = position;
            }
        }
    }
}
