using Obi;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    [DisallowMultipleComponent]
    public sealed class ObiRopeHoistTestRig : MonoBehaviour
    {
        private const float TestDurationSeconds = 9f;
        private const float MinRopeLengthMeters = 3.2f;
        private const float MaxRopeLengthMeters = 7.2f;
        private const float HoistSpeedMetersPerSecond = 0.65f;
        private const float TrolleySpeedMetersPerSecond = 1.8f;

        [SerializeField] private bool runAutomatedSequence = true;

        private ObiSolver solver;
        private Obi.ObiRope rope;
        private ObiRopeCursor cursor;
        private ObiRopeBlueprint blueprint;
        private Transform topAnchor;
        private Rigidbody loadBody;
        private ObiParticleAttachment topAttachment;
        private ObiParticleAttachment loadAttachment;

        private Material ropeMaterial;
        private Material structureMaterial;
        private Material loadMaterial;
        private Material groundMaterial;

        private Vector3 topAnchorHome;
        private Vector3 loadHome;
        private float initialRopeLength;
        private float commandedRopeLength;
        private float minimumObservedRopeLength = float.PositiveInfinity;
        private float maximumObservedSway;
        private float sequenceStartedAt;
        private bool impulseApplied;
        private bool ropeReady;
        private bool resultLogged;
        private bool testPassed;
        private string testStatus = "Initializing";

        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle passStyle;

        public bool TestPassed => resultLogged && testPassed;
        public string TestStatus => testStatus;

        private void Awake()
        {
            BuildTestRig();
            testStatus = "Waiting for Obi rope blueprint to load";
        }

        private void Update()
        {
            if (rope == null || loadBody == null || topAnchor == null)
            {
                return;
            }

            if (!ropeReady)
            {
                TryInitializeLoadedRope();
                return;
            }

            HandleManualControls();

            if (runAutomatedSequence && !resultLogged)
            {
                RunAutomatedSequence(Time.time - sequenceStartedAt);
            }

            ObserveMetrics();
        }

        private void BuildTestRig()
        {
            CreateMaterials();
            CreateEnvironment();

            var collisionWorldObject = new GameObject("ObiBurstCollisionWorld");
            collisionWorldObject.transform.SetParent(transform, false);
            collisionWorldObject.AddComponent<BurstColliderWorld>();

            var solverObject = new GameObject("ObiSolver_Burst_SynchronousFixed");
            solverObject.transform.SetParent(transform, false);
            solver = solverObject.AddComponent<ObiSolver>();
            solver.backendType = ObiSolver.BackendType.Burst;
            solver.synchronization = ObiSolver.Synchronization.SynchronousFixed;
            solver.substeps = 8;
            solver.maxStepsPerFrame = 1;
            solver.distanceConstraintParameters.iterations = 8;
            solver.collisionConstraintParameters.enabled = false;
            solver.particleCollisionConstraintParameters.enabled = false;

            CreateTrolleyAndAnchor();
            CreateLoad();
            CreateRope();
        }

        private void CreateMaterials()
        {
            // A shader found only by name can be stripped from a standalone build.
            // Clone the material used by Unity's built-in primitive instead: it is
            // guaranteed to be present on the active render pipeline.
            var materialProbe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            materialProbe.name = "DefaultMaterialProbe";
            var materialTemplate = materialProbe.GetComponent<MeshRenderer>().sharedMaterial;

            ropeMaterial = CreateMaterial(materialTemplate, new Color(0.10f, 0.12f, 0.14f), 0.65f, 0.35f);
            structureMaterial = CreateMaterial(materialTemplate, new Color(0.07f, 0.30f, 0.58f), 0.35f, 0.25f);
            loadMaterial = CreateMaterial(materialTemplate, new Color(0.96f, 0.50f, 0.08f), 0.15f, 0.25f);
            groundMaterial = CreateMaterial(materialTemplate, new Color(0.15f, 0.18f, 0.22f), 0.05f, 0.15f);

            Destroy(materialProbe);
        }

        private static Material CreateMaterial(Material template, Color color, float metallic, float smoothness)
        {
            var material = new Material(template)
            {
                color = color
            };

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }

            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }

            return material;
        }

        private void CreateEnvironment()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.SetParent(transform, false);
            ground.transform.position = new Vector3(0f, -0.25f, 0f);
            ground.transform.localScale = new Vector3(18f, 0.5f, 12f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            var frameLeft = CreatePrimitive("Frame_Left", PrimitiveType.Cube, new Vector3(-4.2f, 3.7f, 0f), new Vector3(0.35f, 7.4f, 0.6f), structureMaterial);
            var frameRight = CreatePrimitive("Frame_Right", PrimitiveType.Cube, new Vector3(4.2f, 3.7f, 0f), new Vector3(0.35f, 7.4f, 0.6f), structureMaterial);
            var beam = CreatePrimitive("Frame_TopBeam", PrimitiveType.Cube, new Vector3(0f, 7.35f, 0f), new Vector3(8.8f, 0.45f, 0.7f), structureMaterial);
            frameLeft.transform.SetParent(transform, true);
            frameRight.transform.SetParent(transform, true);
            beam.transform.SetParent(transform, true);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            cameraObject.transform.position = new Vector3(10.5f, 5.2f, -12.5f);
            cameraObject.transform.LookAt(new Vector3(0f, 3.6f, 0f));
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 48f;
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
        }

        private void CreateTrolleyAndAnchor()
        {
            var trolley = CreatePrimitive("Trolley", PrimitiveType.Cube, new Vector3(0f, 6.95f, 0f), new Vector3(1.4f, 0.35f, 1.1f), loadMaterial);
            trolley.transform.SetParent(transform, true);

            var anchorObject = new GameObject("RopeTopAnchor");
            anchorObject.transform.SetParent(trolley.transform, false);
            anchorObject.transform.localPosition = new Vector3(0f, -0.25f, 0f);
            topAnchor = anchorObject.transform;
            topAnchorHome = topAnchor.position;

            var marker = CreatePrimitive("TopAnchorMarker", PrimitiveType.Sphere, topAnchor.position, Vector3.one * 0.18f, ropeMaterial);
            marker.transform.SetParent(topAnchor, true);
        }

        private void CreateLoad()
        {
            var load = CreatePrimitive("SuspendedLoad", PrimitiveType.Cube, new Vector3(0f, 1.8f, 0f), new Vector3(1.8f, 0.65f, 1.05f), loadMaterial);
            load.transform.SetParent(transform, true);
            loadHome = load.transform.position;

            loadBody = load.AddComponent<Rigidbody>();
            // Obi provides the visual secondary motion in this experiment. Real
            // container tonnage remains the deterministic crane model's authority.
            loadBody.mass = 2f;
            loadBody.linearDamping = 0.08f;
            loadBody.angularDamping = 0.25f;
            loadBody.interpolation = RigidbodyInterpolation.Interpolate;
            loadBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            load.AddComponent<ObiRigidbody>();
            load.AddComponent<ObiCollider>();
        }

        private void CreateRope()
        {
            var ropeObject = new GameObject("ObiHoistRope");

            var loadAnchorWorld = loadBody.transform.TransformPoint(new Vector3(0f, 0.325f, 0f));
            var start = solver.transform.InverseTransformPoint(topAnchor.position);
            var end = solver.transform.InverseTransformPoint(loadAnchorWorld);
            var tangent = (end - start).normalized * 0.5f;
            var filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);

            blueprint = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
            blueprint.name = "Runtime_ObiHoistRope_Blueprint";
            blueprint.thickness = 0.045f;
            blueprint.resolution = 0.75f;
            blueprint.pooledParticles = 80;
            blueprint.path.Clear();
            blueprint.path.AddControlPoint(start, -tangent, tangent, Vector3.forward, 0.15f, 0.1f, 1f, filter, Color.white, "Top");
            blueprint.path.AddControlPoint(end, -tangent, tangent, Vector3.forward, 0.15f, 0.1f, 1f, filter, Color.white, "Load");
            blueprint.path.FlushEvents();
            blueprint.GenerateImmediate();

            rope = ropeObject.AddComponent<Obi.ObiRope>();
            rope.maxBending = 0.02f;
            rope.stretchCompliance = 0f;

            var renderer = ropeObject.AddComponent<ObiRopeExtrudedRenderer>();
            renderer.section = Resources.Load<ObiRopeSection>("DefaultRopeSection");
            renderer.material = ropeMaterial;
            renderer.thicknessScale = 0.9f;
            renderer.uvScale = new Vector2(1f, 4f);

            cursor = ropeObject.AddComponent<ObiRopeCursor>();
            cursor.cursorMu = 0f;
            cursor.direction = true;

            topAttachment = ropeObject.AddComponent<ObiParticleAttachment>();
            loadAttachment = ropeObject.AddComponent<ObiParticleAttachment>();

            rope.ropeBlueprint = blueprint;

            topAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
            topAttachment.target = topAnchor;
            topAttachment.particleGroup = blueprint.groups[0];

            loadAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Dynamic;
            loadAttachment.target = loadBody.transform;
            loadAttachment.particleGroup = blueprint.groups[1];
            loadAttachment.projection = true;

            // Match Obi's runtime sample order: assign the blueprint/attachments first,
            // then parent the actor under the solver so it loads exactly once.
            ropeObject.transform.SetParent(solver.transform, false);

            initialRopeLength = blueprint.restLength;
            commandedRopeLength = initialRopeLength;
            loadBody.useGravity = false;
        }

        private void TryInitializeLoadedRope()
        {
            if (!rope.isLoaded || rope.elements.Count == 0)
            {
                return;
            }

            rope.RecalculateRestLength();
            if (rope.restLength > 0.01f)
            {
                initialRopeLength = rope.restLength;
            }

            commandedRopeLength = initialRopeLength;
            loadBody.useGravity = true;
            ropeReady = true;
            ResetSequence();
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType type, Vector3 position, Vector3 scale, Material material)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = objectName;
            instance.transform.position = position;
            instance.transform.localScale = scale;
            instance.GetComponent<MeshRenderer>().sharedMaterial = material;
            return instance;
        }

        private void HandleManualControls()
        {
            var trolleyInput = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) trolleyInput -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) trolleyInput += 1f;

            if (!runAutomatedSequence || resultLogged)
            {
                MoveTopAnchor(trolleyInput * TrolleySpeedMetersPerSecond * Time.deltaTime);
            }

            var hoistInput = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) hoistInput -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) hoistInput += 1f;

            if (!Mathf.Approximately(hoistInput, 0f))
            {
                ChangeRopeLength(hoistInput * HoistSpeedMetersPerSecond * Time.deltaTime);
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                loadBody.AddForce(Vector3.right * 4f, ForceMode.Impulse);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetSequence();
            }
        }

        private void RunAutomatedSequence(float elapsed)
        {
            if (elapsed < 1f)
            {
                testStatus = "Settling rope and dynamic attachment";
                return;
            }

            if (elapsed < 3f)
            {
                testStatus = "Moving trolley anchor to excite sway";
                var phase = (elapsed - 1f) / 2f;
                var targetX = topAnchorHome.x + Mathf.Sin(phase * Mathf.PI * 2f) * 1.15f;
                var next = topAnchor.position;
                next.x = targetX;
                topAnchor.position = next;
                return;
            }

            MoveTopAnchorTowardsHome();

            if (elapsed < 4.6f)
            {
                testStatus = "Hoisting: shortening rope";
                ChangeRopeLength(-HoistSpeedMetersPerSecond * Time.deltaTime);
                return;
            }

            if (elapsed < 6.2f)
            {
                testStatus = "Lowering: extending rope";
                ChangeRopeLength(HoistSpeedMetersPerSecond * Time.deltaTime);
                return;
            }

            if (!impulseApplied)
            {
                testStatus = "Applying lateral impulse to suspended load";
                loadBody.AddForce(Vector3.forward * 3.5f, ForceMode.Impulse);
                impulseApplied = true;
            }

            if (elapsed >= TestDurationSeconds)
            {
                CompleteTest();
            }
            else
            {
                testStatus = "Observing stable pendulum response";
            }
        }

        private void MoveTopAnchor(float deltaX)
        {
            var next = topAnchor.position;
            next.x = Mathf.Clamp(next.x + deltaX, topAnchorHome.x - 2.5f, topAnchorHome.x + 2.5f);
            topAnchor.position = next;
        }

        private void MoveTopAnchorTowardsHome()
        {
            var next = topAnchor.position;
            next.x = Mathf.MoveTowards(next.x, topAnchorHome.x, TrolleySpeedMetersPerSecond * Time.deltaTime);
            topAnchor.position = next;
        }

        private void ChangeRopeLength(float requestedDelta)
        {
            var predictedLength = commandedRopeLength + requestedDelta;
            var clampedLength = Mathf.Clamp(predictedLength, MinRopeLengthMeters, MaxRopeLengthMeters);
            var appliedDelta = clampedLength - commandedRopeLength;
            if (!Mathf.Approximately(appliedDelta, 0f))
            {
                cursor.ChangeLength(appliedDelta);
                commandedRopeLength = clampedLength;
            }
        }

        private void ObserveMetrics()
        {
            minimumObservedRopeLength = Mathf.Min(minimumObservedRopeLength, commandedRopeLength);
            var horizontalOffset = loadBody.position - topAnchor.position;
            horizontalOffset.y = 0f;
            maximumObservedSway = Mathf.Max(maximumObservedSway, horizontalOffset.magnitude);
        }

        private void CompleteTest()
        {
            if (resultLogged)
            {
                return;
            }

            var finitePosition = IsFinite(loadBody.position) && IsFinite(loadBody.linearVelocity);
            var ropeShortened = minimumObservedRopeLength < initialRopeLength - 0.2f;
            var swayObserved = maximumObservedSway > 0.15f;
            var loadWithinBounds = loadBody.position.y > 0.75f && loadBody.position.magnitude < 50f;
            var attachmentsBound = topAttachment.isBound && loadAttachment.isBound;
            var physicalLength = rope.CalculateLength();
            var stretchRatio = physicalLength / Mathf.Max(commandedRopeLength, 0.01f);

            testPassed = rope.isLoaded &&
                          rope.activeParticleCount >= 2 &&
                          rope.restLength > 0.01f &&
                          physicalLength > 0.01f &&
                          stretchRatio < 1.15f &&
                         finitePosition &&
                         ropeShortened &&
                         swayObserved &&
                         loadWithinBounds &&
                         attachmentsBound;

            testStatus = testPassed ? "PASS - rope physics is stable" : "FAIL - inspect Console diagnostics";
            resultLogged = true;

            var metrics = $"loaded={rope.isLoaded}, particles={rope.activeParticleCount}, " +
                          $"initialLength={initialRopeLength:F3}m, commandedLength={commandedRopeLength:F3}m, " +
                          $"restLength={rope.restLength:F3}m, physicalLength={physicalLength:F3}m, " +
                          $"stretchRatio={stretchRatio:F3}, " +
                          $"minLength={minimumObservedRopeLength:F3}m, " +
                          $"maxSway={maximumObservedSway:F3}m, loadY={loadBody.position.y:F3}m, " +
                          $"topBound={topAttachment.isBound}, loadBound={loadAttachment.isBound}";

            if (testPassed)
            {
                Debug.Log($"[OBI_ROPE_TEST_PASS] {metrics}");
            }
            else
            {
                Debug.LogError($"[OBI_ROPE_TEST_FAIL] {metrics}");
            }

#if !UNITY_EDITOR
            Application.Quit(testPassed ? 0 : 21);
#endif
        }

        private void ResetSequence()
        {
            sequenceStartedAt = Time.time;
            impulseApplied = false;
            resultLogged = false;
            testPassed = false;
            testStatus = "Resetting";
            maximumObservedSway = 0f;
            minimumObservedRopeLength = float.PositiveInfinity;

            topAnchor.position = topAnchorHome;
            loadBody.position = loadHome;
            loadBody.rotation = Quaternion.identity;
            loadBody.linearVelocity = Vector3.zero;
            loadBody.angularVelocity = Vector3.zero;

            var lengthDelta = initialRopeLength - commandedRopeLength;
            if (!Mathf.Approximately(lengthDelta, 0f))
            {
                cursor.ChangeLength(lengthDelta);
            }
            commandedRopeLength = initialRopeLength;
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private void OnGUI()
        {
            EnsureGuiStyles();

            GUILayout.BeginArea(new Rect(24f, 24f, 470f, 250f), GUI.skin.box);
            GUILayout.Label("OBI ROPE 7.1 · 场桥起升绳物理验证", titleStyle);
            GUILayout.Space(8f);
            GUILayout.Label($"状态：{testStatus}", resultLogged && testPassed ? passStyle : bodyStyle);
            GUILayout.Label($"求解：Burst CPU / SynchronousFixed / {solver?.substeps ?? 0} substeps", bodyStyle);
            GUILayout.Label($"绳长：{commandedRopeLength:F3} m    粒子：{rope?.activeParticleCount ?? 0}", bodyStyle);
            GUILayout.Label($"吊载位置：{FormatVector(loadBody != null ? loadBody.position : Vector3.zero)}", bodyStyle);
            GUILayout.Label($"最大水平摆幅：{maximumObservedSway:F3} m", bodyStyle);
            GUILayout.Space(8f);
            GUILayout.Label("操作：A/D移动小车 · W/S收放绳 · Space施加侧向力 · R重置", bodyStyle);
            GUILayout.EndArea();
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 0.80f, 1f) }
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                normal = { textColor = Color.white }
            };

            passStyle = new GUIStyle(bodyStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.35f, 1f, 0.60f) }
            };
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }

        private void OnDestroy()
        {
            if (blueprint != null) Destroy(blueprint);
            if (ropeMaterial != null) Destroy(ropeMaterial);
            if (structureMaterial != null) Destroy(structureMaterial);
            if (loadMaterial != null) Destroy(loadMaterial);
            if (groundMaterial != null) Destroy(groundMaterial);
        }
    }
}
