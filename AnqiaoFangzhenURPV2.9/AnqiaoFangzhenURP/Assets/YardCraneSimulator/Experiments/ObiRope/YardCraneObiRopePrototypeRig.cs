using System;
using System.Collections.Generic;
using Obi;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// Runtime greybox for the yard-crane mechanism hierarchy. The coloured cubes
    /// are replaceable visual placeholders; motion, rigidbody and rope bindings use
    /// the stable mechanism nodes under YC_RuntimeRoot.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class YardCraneObiRopePrototypeRig : MonoBehaviour
    {
        private const float HoistSpeedMetersPerSecond = 0.65f;
        private const float TrolleySpeedMetersPerSecond = 1.8f;
        private const float TrolleyAccelerationMetersPerSecondSquared = 0.9f;
        private const float TrolleyBrakingMetersPerSecondSquared = 1.2f;
        private const float MinimumRopeLengthMeters = 2.8f;
        private const float MaximumRopeLengthMeters = 8.5f;
        private const float RatedSuspendedMassKg = 40500f;
        private const float ReferenceRopeSystemLengthMeters = 40f;
        private const float ReferenceRopeSystemStiffnessNPerMeter = 4.02e7f;
        private const float SimulationProxyMassKg = 2f;
        private const float MaximumPermittedNumericalAxialStretchMeters = 0.008f;
        private const float DeterministicConstraintToleranceMeters = 0.001f;
        private const int DeterministicConstraintIterations = 16;
        private const float StartupPreTensionDurationSeconds = 0.4f;
        private const string ImplementationVersion = "2026-08-30-rigid-cable-v6";

        private static readonly RopeDefinition[] RopeDefinitions =
        {
            new("FL", "20_TrolleyMotion/RopeTopAnchors/Rope_FL_Top", "30_SpreaderPhysics/RopeBottomAnchors/Rope_FL_Bottom", "40_RopeRig/Rope_FL"),
            new("FR", "20_TrolleyMotion/RopeTopAnchors/Rope_FR_Top", "30_SpreaderPhysics/RopeBottomAnchors/Rope_FR_Bottom", "40_RopeRig/Rope_FR"),
            new("RL", "20_TrolleyMotion/RopeTopAnchors/Rope_RL_Top", "30_SpreaderPhysics/RopeBottomAnchors/Rope_RL_Bottom", "40_RopeRig/Rope_RL"),
            new("RR", "20_TrolleyMotion/RopeTopAnchors/Rope_RR_Top", "30_SpreaderPhysics/RopeBottomAnchors/Rope_RR_Bottom", "40_RopeRig/Rope_RR")
        };

        [Header("Prototype")]
        [SerializeField] private bool showGraybox = true;
        [SerializeField] private bool runAutomatedDemo = true;
        [SerializeField] private bool showRuntimePanel = true;

        [Header("Obi Solver")]
        [SerializeField, Range(1, 12)] private int solverSubsteps = 10;
        [SerializeField, Range(1, 16)] private int distanceIterations = 2;

        private readonly List<RopeRuntime> ropes = new();
        private readonly List<Material> runtimeMaterials = new();

        private Transform trolleyMotion;
        private Transform spreaderPhysics;
        private Rigidbody spreaderBody;
        private ObiSolver solver;
        private Material ropeMaterial;

        private Vector3 trolleyHome;
        private Vector3 spreaderHome;
        private Quaternion spreaderHomeRotation;
        private float demoStartedAt;
        private float maximumObservedSway;
        private float maximumObservedAxialStretch;
        private float maximumStretchObservedAt;
        private float maximumRawConstraintDrift;
        private float maximumRawDriftObservedAt;
        private float trolleyInput;
        private float hoistInput;
        private float currentTrolleyVelocity;
        private string maximumStretchRopeId = "-";
        private string maximumRawDriftRopeId = "-";
        private float startupReleaseAt;
        private bool startupPreTensioning;
        private bool ropesReady;
        private bool demoCompleted;
        private bool setupFailed;
        private string runtimeStatus = "Initializing yard-crane prototype";

        public bool ShowGraybox => showGraybox;
        public bool RopesReady => ropesReady;
        public string RuntimeStatus => runtimeStatus;

        private void Awake()
        {
            try
            {
                ResolveHierarchy();
                ApplyGrayboxVisibility();
                CreateRuntimeMaterials();
                ConfigureSpreaderPhysics();
                CreateSolver();
                CreateFourRopes();
                EnsurePrototypeView();

                trolleyHome = trolleyMotion.localPosition;
                spreaderHome = spreaderPhysics.position;
                spreaderHomeRotation = spreaderPhysics.rotation;
                runtimeStatus = "Waiting for four Obi rope blueprints";
            }
            catch (Exception exception)
            {
                setupFailed = true;
                runtimeStatus = exception.Message;
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (setupFailed)
            {
                return;
            }

            if (!ropesReady)
            {
                TryFinishRopeInitialization();
                return;
            }

            HandleKeyboardControls();
        }

        private void FixedUpdate()
        {
            if (!ropesReady || setupFailed)
            {
                return;
            }

            if (startupPreTensioning)
            {
                RunStartupPreTension();
                return;
            }

            // PhysX provides forces, torque and sway. The deterministic projection
            // closes the small residual left by four parallel cable constraints.
            EnforceDeterministicRopeLimits();
            ObserveRuntimeState();

            if (runAutomatedDemo && !demoCompleted)
            {
                RunAutomatedDemo(Time.fixedTime - demoStartedAt);
            }
            else
            {
                UpdateManualTrolleyMotion();
                ChangeAllRopeLengths(hoistInput * HoistSpeedMetersPerSecond * Time.fixedDeltaTime);
            }

            foreach (var ropeRuntime in ropes)
            {
                // connectedAnchor is world-space because the trolley does not use a Rigidbody.
                ropeRuntime.SuspensionJoint.connectedAnchor = ropeRuntime.TopAnchor.position;
                SetJointRopeLength(ropeRuntime.SuspensionJoint, ropeRuntime.CommandedLength);
                CaptureAppliedBoundaryMotion(ropeRuntime);
            }
        }

        private void ResolveHierarchy()
        {
            trolleyMotion = RequireTransform("20_TrolleyMotion");
            spreaderPhysics = RequireTransform("30_SpreaderPhysics");

            foreach (var definition in RopeDefinitions)
            {
                RequireTransform(definition.TopAnchorPath);
                RequireTransform(definition.BottomAnchorPath);
                RequireTransform(definition.RopeSlotPath);
            }
        }

        private Transform RequireTransform(string path)
        {
            var target = transform.Find(path);
            if (target == null)
            {
                throw new InvalidOperationException($"YC_RuntimeRoot is missing required node: {path}");
            }

            return target;
        }

        private void ConfigureSpreaderPhysics()
        {
            spreaderBody = spreaderPhysics.GetComponent<Rigidbody>();
            if (spreaderBody == null)
            {
                spreaderBody = spreaderPhysics.gameObject.AddComponent<Rigidbody>();
            }

            spreaderBody.mass = SimulationProxyMassKg;
            spreaderBody.linearDamping = 0.04f;
            spreaderBody.angularDamping = 1.25f;
            spreaderBody.interpolation = RigidbodyInterpolation.Interpolate;
            spreaderBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            spreaderBody.solverIterations = 32;
            spreaderBody.solverVelocityIterations = 16;
            spreaderBody.useGravity = false;

            if (spreaderPhysics.GetComponent<ObiRigidbody>() == null)
            {
                spreaderPhysics.gameObject.AddComponent<ObiRigidbody>();
            }

            var colliderRoot = RequireTransform("30_SpreaderPhysics/Physics/Colliders");
            var colliderTransform = colliderRoot.Find("Graybox_SpreaderCollider");
            if (colliderTransform == null)
            {
                var colliderObject = new GameObject("Graybox_SpreaderCollider");
                colliderObject.transform.SetParent(colliderRoot, false);
                colliderTransform = colliderObject.transform;
            }

            colliderTransform.localPosition = Vector3.zero;
            colliderTransform.localRotation = Quaternion.identity;
            colliderTransform.localScale = Vector3.one;

            var boxCollider = colliderTransform.GetComponent<BoxCollider>();
            if (boxCollider == null)
            {
                boxCollider = colliderTransform.gameObject.AddComponent<BoxCollider>();
            }

            boxCollider.size = new Vector3(4f, 0.5f, 2.2f);
            if (colliderTransform.GetComponent<ObiCollider>() == null)
            {
                colliderTransform.gameObject.AddComponent<ObiCollider>();
            }

            // The collider remains independent from the replaceable visual model.
        }

        private void CreateSolver()
        {
            var ropeRig = RequireTransform("40_RopeRig");
            var runtimeRoot = ropeRig.Find("_ObiRuntime");
            if (runtimeRoot == null)
            {
                var runtimeObject = new GameObject("_ObiRuntime");
                runtimeObject.transform.SetParent(ropeRig, false);
                runtimeRoot = runtimeObject.transform;
            }

            var collisionWorldObject = new GameObject("ObiBurstCollisionWorld");
            collisionWorldObject.transform.SetParent(runtimeRoot, false);
            collisionWorldObject.AddComponent<BurstColliderWorld>();

            solver = ropeRig.GetComponent<ObiSolver>();
            if (solver == null)
            {
                solver = ropeRig.gameObject.AddComponent<ObiSolver>();
            }
            solver.backendType = ObiSolver.BackendType.Burst;
            solver.synchronization = ObiSolver.Synchronization.SynchronousFixed;
            solver.substeps = Mathf.Max(10, solverSubsteps);
            solver.maxStepsPerFrame = 1;
            // Obi is a visual follower in this rig. Applying a second gravity field to
            // particles that do not carry the spreader load creates visible startup sag
            // and recoil, so the visual cable uses zero gravity and high damping.
            solver.gravity = Vector3.zero;
            solver.gravitySpace = Space.World;
            // For a stiff rope, Obi recommends more substeps instead of stacking
            // distance iterations. Zero compliance is retained below.
            solver.distanceConstraintParameters.iterations = Mathf.Clamp(distanceIterations, 1, 2);
            solver.collisionConstraintParameters.enabled = false;
            solver.particleCollisionConstraintParameters.enabled = false;
            var parameters = solver.parameters;
            parameters.damping = 0.28f;
            solver.parameters = parameters;
        }

        private void CreateFourRopes()
        {
            foreach (var definition in RopeDefinitions)
            {
                var topAnchor = RequireTransform(definition.TopAnchorPath);
                var bottomAnchor = RequireTransform(definition.BottomAnchorPath);
                var ropeSlot = RequireTransform(definition.RopeSlotPath);
                var actorObject = new GameObject($"ObiRope_{definition.Id}_RuntimeActor");

                var start = solver.transform.InverseTransformPoint(topAnchor.position);
                var end = solver.transform.InverseTransformPoint(bottomAnchor.position);
                var initialGeometricLength = Vector3.Distance(topAnchor.position, bottomAnchor.position);
                var tangent = (end - start).normalized * 0.5f;
                var filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);

                var blueprint = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
                blueprint.name = $"Runtime_YC_Rope_{definition.Id}_Blueprint";
                blueprint.thickness = 0.035f;
                blueprint.resolution = 0.7f;
                blueprint.pooledParticles = 80;
                blueprint.path.Clear();
                blueprint.path.AddControlPoint(start, -tangent, tangent, Vector3.forward, 0.15f, 0.1f, 1f, filter, Color.white, "Top");
                blueprint.path.AddControlPoint(end, -tangent, tangent, Vector3.forward, 0.15f, 0.1f, 1f, filter, Color.white, "Spreader");
                blueprint.path.FlushEvents();
                blueprint.GenerateImmediate();

                var rope = actorObject.AddComponent<Obi.ObiRope>();
                rope.maxBending = 0.02f;
                rope.stretchCompliance = 0f;
                // A wire rope transmits tension and goes slack under compression.
                rope.maxCompression = 1f;
                rope.ropeBlueprint = blueprint;

                var renderer = actorObject.AddComponent<ObiRopeExtrudedRenderer>();
                renderer.section = Resources.Load<ObiRopeSection>("DefaultRopeSection");
                renderer.material = ropeMaterial;
                renderer.thicknessScale = 0.8f;
                renderer.uvScale = new Vector2(1f, 4f);

                var cursor = actorObject.AddComponent<ObiRopeCursor>();
                cursor.cursorMu = 0f;
                cursor.direction = true;

                var topAttachment = actorObject.AddComponent<ObiParticleAttachment>();
                topAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
                topAttachment.target = topAnchor;
                topAttachment.particleGroup = blueprint.groups[0];

                var bottomAttachment = actorObject.AddComponent<ObiParticleAttachment>();
                // Obi is now the visual rope layer. It follows the engineering
                // suspension model and no longer drives the spreader Rigidbody.
                bottomAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
                bottomAttachment.target = bottomAnchor;
                bottomAttachment.particleGroup = blueprint.groups[1];
                bottomAttachment.projection = true;

                // Blueprint restLength is not authoritative here: before the actor is
                // loaded it may still be zero. Starting a joint from that value would
                // clamp it to 0.1 m and violently pull a 4+ m suspension at frame zero.
                var suspensionJoint = CreateTensionOnlySuspensionJoint(
                    topAnchor,
                    bottomAnchor,
                    initialGeometricLength);

                // Each actor remains visible below its stable Rope_FL/FR/RL/RR slot,
                // while all four slots are descendants of the shared Obi solver.
                actorObject.transform.SetParent(ropeSlot, false);
                ropes.Add(new RopeRuntime(
                    definition.Id,
                    ropeSlot,
                    actorObject,
                    rope,
                    cursor,
                    blueprint,
                    topAttachment,
                    bottomAttachment,
                    topAnchor,
                    bottomAnchor,
                    suspensionJoint,
                    initialGeometricLength));
            }
        }

        private ConfigurableJoint CreateTensionOnlySuspensionJoint(Transform topAnchor, Transform bottomAnchor, float ropeLength)
        {
            var currentLength = Mathf.Max(ropeLength, 0.1f);

            var joint = spreaderPhysics.gameObject.AddComponent<ConfigurableJoint>();
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedBody = null;
            joint.anchor = spreaderPhysics.InverseTransformPoint(bottomAnchor.position);
            joint.connectedAnchor = topAnchor.position;
            joint.xMotion = ConfigurableJointMotion.Limited;
            joint.yMotion = ConfigurableJointMotion.Limited;
            joint.zMotion = ConfigurableJointMotion.Limited;
            joint.angularXMotion = ConfigurableJointMotion.Free;
            joint.angularYMotion = ConfigurableJointMotion.Free;
            joint.angularZMotion = ConfigurableJointMotion.Free;
            // ConfigurableJoint's non-zero limit spring permits travel beyond the
            // limit and produced a visible rubber-band response. Crane wire rope is
            // effectively inextensible at this prototype scale, so PhysX owns only
            // the unilateral hard length limit. Engineering stretch is monitored
            // separately by CalculateEngineeringProfile().
            joint.linearLimitSpring = new SoftJointLimitSpring { spring = 0f, damper = 0f };
            joint.linearLimit = new SoftJointLimit
            {
                limit = currentLength,
                contactDistance = 0.005f
            };
            // Four parallel suspension constraints can retain a small residual after
            // the iterative solve while the trolley/spreader is moving. Projection is
            // only a millimetre-scale safety correction here, never the primary motion
            // model, and prevents the residual from looking like elastic cable stretch.
            joint.projectionMode = JointProjectionMode.PositionAndRotation;
            joint.projectionDistance = 0.001f;
            joint.projectionAngle = 0.25f;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;
            return joint;
        }

        private static void SetJointRopeLength(ConfigurableJoint joint, float ropeLength)
        {
            var limit = joint.linearLimit;
            limit.limit = ropeLength;
            joint.linearLimit = limit;
        }

        private void TryFinishRopeInitialization()
        {
            foreach (var ropeRuntime in ropes)
            {
                if (!ropeRuntime.Rope.isLoaded || ropeRuntime.Rope.elements.Count == 0)
                {
                    return;
                }
            }

            foreach (var ropeRuntime in ropes)
            {
                ropeRuntime.Rope.RecalculateRestLength();
                var physicalBottomAnchor = GetPhysicalBottomAnchor(
                    ropeRuntime,
                    spreaderBody.position,
                    spreaderBody.rotation);
                var physicalLength = Mathf.Max(
                    Vector3.Distance(ropeRuntime.TopAnchor.position, physicalBottomAnchor),
                    0.1f);
                var visualLengthCorrection = physicalLength - ropeRuntime.Rope.restLength;
                if (!Mathf.Approximately(visualLengthCorrection, 0f))
                {
                    ropeRuntime.Cursor.ChangeLength(visualLengthCorrection);
                }

                ropeRuntime.InitialLength = physicalLength;
                ropeRuntime.CommandedLength = physicalLength;
                ropeRuntime.LastAppliedLength = physicalLength;
                ropeRuntime.LastAppliedTopPosition = ropeRuntime.TopAnchor.position;
                ropeRuntime.AppliedLengthRate = 0f;
                ropeRuntime.AppliedTopVelocity = Vector3.zero;
                SetJointRopeLength(ropeRuntime.SuspensionJoint, physicalLength);
            }

            spreaderBody.useGravity = false;
            spreaderBody.linearVelocity = Vector3.zero;
            spreaderBody.angularVelocity = Vector3.zero;
            ropesReady = true;
            startupPreTensioning = true;
            startupReleaseAt = Time.fixedTime + StartupPreTensionDurationSeconds;
            demoStartedAt = Time.fixedTime;
            runtimeStatus = "Pre-tensioning four rigid cables";
            var profile = CalculateEngineeringProfile();
            Debug.Log($"[YC_STEEL_ROPE_READY] model={ImplementationVersion}, four tension-only suspensions, " +
                      $"physicalStaticStretch={profile.StaticStretchMeters * 1000f:F2}mm, " +
                      $"systemStiffness={profile.SystemStiffnessNPerMeter:E2}N/m, " +
                      $"ropeLength={profile.RopeLengthMeters:F3}m", this);
        }

        private void HandleKeyboardControls()
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                SetGrayboxVisible(!showGraybox);
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetPrototype();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                spreaderBody.AddForce(Vector3.forward * 3.5f, ForceMode.Impulse);
            }

            if (runAutomatedDemo && !demoCompleted)
            {
                trolleyInput = 0f;
                hoistInput = 0f;
                return;
            }

            trolleyInput = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) trolleyInput -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) trolleyInput += 1f;

            hoistInput = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) hoistInput -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) hoistInput += 1f;
        }

        private void RunAutomatedDemo(float elapsed)
        {
            currentTrolleyVelocity = 0f;

            if (elapsed < 1f)
            {
                runtimeStatus = "Settling four rope attachments";
                return;
            }

            if (elapsed < 3f)
            {
                runtimeStatus = "Accelerating trolley with an S-curve";
                var phase = (elapsed - 1f) / 2f;
                var targetX = trolleyHome.x + SmootherStep01(phase) * 0.8f;
                var position = trolleyMotion.localPosition;
                position.x = targetX;
                trolleyMotion.localPosition = position;
                return;
            }

            if (elapsed < 5f)
            {
                runtimeStatus = "Decelerating trolley with an S-curve";
                var phase = (elapsed - 3f) / 2f;
                var position = trolleyMotion.localPosition;
                position.x = trolleyHome.x + (1f - SmootherStep01(phase)) * 0.8f;
                trolleyMotion.localPosition = position;
                return;
            }

            if (elapsed < 7f)
            {
                runtimeStatus = "Hoisting with smooth acceleration and deceleration";
                var phase = Mathf.Clamp01((elapsed - 5f) / 2f);
                var speed = -HoistSpeedMetersPerSecond * Mathf.Sin(Mathf.PI * phase);
                ChangeAllRopeLengths(speed * Time.fixedDeltaTime);
                return;
            }

            if (elapsed < 9f)
            {
                runtimeStatus = "Lowering with smooth acceleration and deceleration";
                var phase = Mathf.Clamp01((elapsed - 7f) / 2f);
                var speed = HoistSpeedMetersPerSecond * Mathf.Sin(Mathf.PI * phase);
                ChangeAllRopeLengths(speed * Time.fixedDeltaTime);
                return;
            }

            if (elapsed < 12f)
            {
                runtimeStatus = "Observing stable four-rope suspension";
                return;
            }

            demoCompleted = true;
            runAutomatedDemo = false;
            var attachmentsBound = ropes.TrueForAll(item => item.TopAttachment.isBound && item.BottomAttachment.isBound &&
                                                             item.SuspensionJoint != null);
            var finiteState = IsFinite(spreaderBody.position) && IsFinite(spreaderBody.linearVelocity);
            var profile = CalculateEngineeringProfile();
            var permittedStretch = Mathf.Max(
                MaximumPermittedNumericalAxialStretchMeters,
                profile.StaticStretchMeters * 3f);
            var steelLikeStretch = maximumObservedAxialStretch <= permittedStretch;
            if (attachmentsBound && finiteState && spreaderBody.position.y > 0.5f && steelLikeStretch)
            {
                runtimeStatus = "PASS - manual controls enabled";
                Debug.Log($"[YC_STEEL_ROPE_PASS] model={ImplementationVersion}, ropes=4, maxSway={maximumObservedSway:F3}m, " +
                          $"maxAxialStretch={maximumObservedAxialStretch * 1000f:F2}mm, " +
                          $"allowed={permittedStretch * 1000f:F2}mm, peakRope={maximumStretchRopeId}, " +
                          $"peakTime={maximumStretchObservedAt:F2}s, rawDrift={maximumRawConstraintDrift * 1000f:F2}mm, " +
                          $"rawRope={maximumRawDriftRopeId}, rawTime={maximumRawDriftObservedAt:F2}s, " +
                          $"spreaderY={spreaderBody.position.y:F3}m", this);
            }
            else
            {
                runtimeStatus = "FAIL - inspect four-rope bindings";
                Debug.LogError($"[YC_STEEL_ROPE_FAIL] model={ImplementationVersion}, attachmentsBound={attachmentsBound}, finiteState={finiteState}, " +
                               $"maxAxialStretch={maximumObservedAxialStretch * 1000f:F2}mm, " +
                               $"allowed={permittedStretch * 1000f:F2}mm, peakRope={maximumStretchRopeId}, " +
                               $"peakTime={maximumStretchObservedAt:F2}s, rawDrift={maximumRawConstraintDrift * 1000f:F2}mm, " +
                               $"rawRope={maximumRawDriftRopeId}, rawTime={maximumRawDriftObservedAt:F2}s, " +
                               $"spreaderY={spreaderBody.position.y:F3}m", this);
            }
        }

        private void MoveTrolley(float deltaX)
        {
            if (Mathf.Approximately(deltaX, 0f))
            {
                return;
            }

            var position = trolleyMotion.localPosition;
            position.x = Mathf.Clamp(position.x + deltaX, trolleyHome.x - 4.5f, trolleyHome.x + 4.5f);
            trolleyMotion.localPosition = position;
        }

        private void UpdateManualTrolleyMotion()
        {
            var targetVelocity = trolleyInput * TrolleySpeedMetersPerSecond;
            var isBraking = Mathf.Approximately(targetVelocity, 0f) ||
                            Mathf.Sign(targetVelocity) != Mathf.Sign(currentTrolleyVelocity) ||
                            Mathf.Abs(targetVelocity) < Mathf.Abs(currentTrolleyVelocity);
            var acceleration = isBraking
                ? TrolleyBrakingMetersPerSecondSquared
                : TrolleyAccelerationMetersPerSecondSquared;
            currentTrolleyVelocity = Mathf.MoveTowards(
                currentTrolleyVelocity,
                targetVelocity,
                acceleration * Time.fixedDeltaTime);
            MoveTrolley(currentTrolleyVelocity * Time.fixedDeltaTime);
        }

        private void MoveTrolleyTowardsHome()
        {
            var position = trolleyMotion.localPosition;
            position.x = Mathf.MoveTowards(position.x, trolleyHome.x, TrolleySpeedMetersPerSecond * Time.deltaTime);
            trolleyMotion.localPosition = position;
        }

        private void ChangeAllRopeLengths(float requestedDelta)
        {
            if (Mathf.Approximately(requestedDelta, 0f))
            {
                return;
            }

            foreach (var ropeRuntime in ropes)
            {
                var next = Mathf.Clamp(ropeRuntime.CommandedLength + requestedDelta, MinimumRopeLengthMeters, MaximumRopeLengthMeters);
                var appliedDelta = next - ropeRuntime.CommandedLength;
                if (!Mathf.Approximately(appliedDelta, 0f))
                {
                    ropeRuntime.Cursor.ChangeLength(appliedDelta);
                    ropeRuntime.CommandedLength = next;
                    SetJointRopeLength(ropeRuntime.SuspensionJoint, next);
                }
            }
        }

        private void EnforceDeterministicRopeLimits()
        {
            var correctedPosition = spreaderBody.position;
            var bodyRotation = spreaderBody.rotation;

            // Preserve the uncorrected PhysX residual for diagnostics. The accepted
            // stretch below is measured only after the real correction is applied.
            foreach (var ropeRuntime in ropes)
            {
                var bottomAnchor = GetPhysicalBottomAnchor(ropeRuntime, correctedPosition, bodyRotation);
                var rawDrift = Mathf.Max(
                    0f,
                    Vector3.Distance(ropeRuntime.TopAnchor.position, bottomAnchor) - ropeRuntime.CommandedLength);
                if (rawDrift > maximumRawConstraintDrift)
                {
                    maximumRawConstraintDrift = rawDrift;
                    maximumRawDriftRopeId = ropeRuntime.Id;
                    maximumRawDriftObservedAt = Mathf.Max(0f, Time.fixedTime - demoStartedAt);
                }
            }

            // Alternating projection onto the four cable-length balls converges to
            // their common feasible region while preserving tangential sway.
            for (var iteration = 0; iteration < DeterministicConstraintIterations; iteration++)
            {
                var correctionRequired = false;
                foreach (var ropeRuntime in ropes)
                {
                    var topAnchor = ropeRuntime.TopAnchor.position;
                    var bottomAnchor = GetPhysicalBottomAnchor(ropeRuntime, correctedPosition, bodyRotation);
                    var topToBottom = bottomAnchor - topAnchor;
                    var distance = topToBottom.magnitude;
                    var excess = distance - ropeRuntime.CommandedLength - DeterministicConstraintToleranceMeters;
                    if (excess <= 0f || distance <= Mathf.Epsilon)
                    {
                        continue;
                    }

                    correctedPosition -= topToBottom / distance * excess;
                    correctionRequired = true;
                }

                if (!correctionRequired)
                {
                    break;
                }
            }

            spreaderBody.position = correctedPosition;
            RemoveOutwardConstraintVelocity(correctedPosition, bodyRotation);
        }

        private void RunStartupPreTension()
        {
            EnforceDeterministicRopeLimits();
            spreaderBody.linearVelocity = Vector3.zero;
            spreaderBody.angularVelocity = Vector3.zero;
            currentTrolleyVelocity = 0f;

            foreach (var ropeRuntime in ropes)
            {
                ropeRuntime.SuspensionJoint.connectedAnchor = ropeRuntime.TopAnchor.position;
                SetJointRopeLength(ropeRuntime.SuspensionJoint, ropeRuntime.CommandedLength);
                CaptureAppliedBoundaryMotion(ropeRuntime);
            }

            if (Time.fixedTime < startupReleaseAt)
            {
                runtimeStatus = "Pre-tensioning four rigid cables";
                return;
            }

            ClearObservedMetrics();
            demoStartedAt = Time.fixedTime;
            startupPreTensioning = false;
            spreaderBody.useGravity = true;
            spreaderBody.linearVelocity = Vector3.zero;
            spreaderBody.angularVelocity = Vector3.zero;
            runtimeStatus = runAutomatedDemo ? "Automated four-rope demo started" : "Ready for manual operation";
            Debug.Log($"[YC_STEEL_ROPE_PRETENSIONED] model={ImplementationVersion}, hold={StartupPreTensionDurationSeconds:F2}s, " +
                      $"visualGravity=0, spreaderY={spreaderBody.position.y:F3}m", this);
        }

        private void RemoveOutwardConstraintVelocity(Vector3 bodyPosition, Quaternion bodyRotation)
        {
            var correctedVelocity = spreaderBody.linearVelocity;
            foreach (var ropeRuntime in ropes)
            {
                var topAnchor = ropeRuntime.TopAnchor.position;
                var bottomAnchor = GetPhysicalBottomAnchor(ropeRuntime, bodyPosition, bodyRotation);
                var topToBottom = bottomAnchor - topAnchor;
                var distance = topToBottom.magnitude;
                if (distance < ropeRuntime.CommandedLength - DeterministicConstraintToleranceMeters ||
                    distance <= Mathf.Epsilon)
                {
                    continue;
                }

                var outwardDirection = topToBottom / distance;
                var anchorOffset = bodyRotation * ropeRuntime.SuspensionJoint.anchor;
                var pointVelocity = correctedVelocity + Vector3.Cross(spreaderBody.angularVelocity, anchorOffset);
                var permittedOutwardSpeed = Vector3.Dot(ropeRuntime.AppliedTopVelocity, outwardDirection) +
                                            ropeRuntime.AppliedLengthRate;
                var excessOutwardSpeed = Vector3.Dot(pointVelocity, outwardDirection) - permittedOutwardSpeed;
                if (excessOutwardSpeed > 0f)
                {
                    correctedVelocity -= outwardDirection * excessOutwardSpeed;
                }
            }

            spreaderBody.linearVelocity = correctedVelocity;
        }

        private static Vector3 GetPhysicalBottomAnchor(
            RopeRuntime ropeRuntime,
            Vector3 bodyPosition,
            Quaternion bodyRotation)
        {
            return bodyPosition + bodyRotation * ropeRuntime.SuspensionJoint.anchor;
        }

        private void CaptureAppliedBoundaryMotion(RopeRuntime ropeRuntime)
        {
            var fixedDeltaTime = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            var topPosition = ropeRuntime.TopAnchor.position;
            ropeRuntime.AppliedTopVelocity = (topPosition - ropeRuntime.LastAppliedTopPosition) / fixedDeltaTime;
            ropeRuntime.AppliedLengthRate = (ropeRuntime.CommandedLength - ropeRuntime.LastAppliedLength) / fixedDeltaTime;
            ropeRuntime.LastAppliedTopPosition = topPosition;
            ropeRuntime.LastAppliedLength = ropeRuntime.CommandedLength;
        }

        private void ClearObservedMetrics()
        {
            maximumObservedSway = 0f;
            maximumObservedAxialStretch = 0f;
            maximumStretchObservedAt = 0f;
            maximumStretchRopeId = "-";
            maximumRawConstraintDrift = 0f;
            maximumRawDriftObservedAt = 0f;
            maximumRawDriftRopeId = "-";
        }

        private void ObserveRuntimeState()
        {
            var topCenter = Vector3.zero;
            foreach (var definition in RopeDefinitions)
            {
                topCenter += RequireTransform(definition.TopAnchorPath).position;
            }

            topCenter /= RopeDefinitions.Length;
            var offset = spreaderBody.position - topCenter;
            offset.y = 0f;
            maximumObservedSway = Mathf.Max(maximumObservedSway, offset.magnitude);

            foreach (var ropeRuntime in ropes)
            {
                // BottomAnchor.Transform is an interpolated render pose because the
                // spreader Rigidbody uses Interpolate. Measuring it in FixedUpdate can
                // therefore report roughly one physics step of motion as false cable
                // extension. Reconstruct the joint anchor from the authoritative
                // Rigidbody pose instead.
                var physicalBottomAnchor = GetPhysicalBottomAnchor(
                    ropeRuntime,
                    spreaderBody.position,
                    spreaderBody.rotation);
                var actualLength = Vector3.Distance(ropeRuntime.TopAnchor.position, physicalBottomAnchor);
                var axialStretch = Mathf.Max(0f, actualLength - ropeRuntime.CommandedLength);
                if (axialStretch > maximumObservedAxialStretch)
                {
                    maximumObservedAxialStretch = axialStretch;
                    maximumStretchRopeId = ropeRuntime.Id;
                    maximumStretchObservedAt = Mathf.Max(0f, Time.fixedTime - demoStartedAt);
                }
            }
        }

        private EngineeringProfile CalculateEngineeringProfile()
        {
            var ropeLength = ropes.Count > 0 ? ropes[0].CommandedLength : 5f;
            var systemStiffness = ReferenceRopeSystemStiffnessNPerMeter *
                                  ReferenceRopeSystemLengthMeters / Mathf.Max(ropeLength, 0.1f);
            var staticStretch = RatedSuspendedMassKg * Physics.gravity.magnitude / systemStiffness;
            return new EngineeringProfile(ropeLength, systemStiffness, staticStretch);
        }

        [ContextMenu("Toggle Graybox Visuals")]
        public void ToggleGrayboxVisuals()
        {
            SetGrayboxVisible(!showGraybox);
        }

        public void SetGrayboxVisible(bool visible)
        {
            showGraybox = visible;
            ApplyGrayboxVisibility();
        }

        private void ApplyGrayboxVisibility()
        {
            var allTransforms = GetComponentsInChildren<Transform>(true);
            foreach (var child in allTransforms)
            {
                if (child != transform && child.name == "Graybox_Placeholder")
                {
                    child.gameObject.SetActive(showGraybox);
                }
            }
        }

        [ContextMenu("Reset Prototype")]
        public void ResetPrototype()
        {
            if (!ropesReady)
            {
                return;
            }

            trolleyMotion.localPosition = trolleyHome;
            spreaderPhysics.SetPositionAndRotation(spreaderHome, spreaderHomeRotation);
            spreaderBody.linearVelocity = Vector3.zero;
            spreaderBody.angularVelocity = Vector3.zero;

            foreach (var ropeRuntime in ropes)
            {
                var delta = ropeRuntime.InitialLength - ropeRuntime.CommandedLength;
                if (!Mathf.Approximately(delta, 0f))
                {
                    ropeRuntime.Cursor.ChangeLength(delta);
                    ropeRuntime.CommandedLength = ropeRuntime.InitialLength;
                }
                SetJointRopeLength(ropeRuntime.SuspensionJoint, ropeRuntime.InitialLength);
            }

            demoStartedAt = Time.fixedTime;
            demoCompleted = false;
            ClearObservedMetrics();
            foreach (var ropeRuntime in ropes)
            {
                ropeRuntime.LastAppliedTopPosition = ropeRuntime.TopAnchor.position;
                ropeRuntime.LastAppliedLength = ropeRuntime.CommandedLength;
                ropeRuntime.AppliedTopVelocity = Vector3.zero;
                ropeRuntime.AppliedLengthRate = 0f;
            }
            spreaderBody.useGravity = false;
            startupPreTensioning = true;
            startupReleaseAt = Time.fixedTime + StartupPreTensionDurationSeconds;
            runtimeStatus = "Prototype reset · pre-tensioning rigid cables";
        }

        private void EnsurePrototypeView()
        {
            if (Camera.main == null)
            {
                var cameraMount = RequireTransform("70_Cameras/LocalCabinCameraMount");
                var cameraObject = new GameObject("Prototype Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(cameraMount, false);
                cameraObject.transform.position = transform.TransformPoint(new Vector3(12f, 7f, -15f));
                cameraObject.transform.LookAt(transform.TransformPoint(new Vector3(0f, 3.8f, 0f)));
                var camera = cameraObject.AddComponent<Camera>();
                camera.fieldOfView = 48f;
                cameraObject.AddComponent<AudioListener>();
            }

            if (FindFirstObjectByType<Light>() == null)
            {
                var lightObject = new GameObject("Prototype Directional Light");
                lightObject.transform.SetParent(transform, false);
                lightObject.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.15f;
            }
        }

        private void CreateRuntimeMaterials()
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            probe.name = "YC_RuntimeMaterialProbe";
            var template = probe.GetComponent<MeshRenderer>().sharedMaterial;
            ropeMaterial = new Material(template) { color = new Color(0.08f, 0.10f, 0.14f) };
            runtimeMaterials.Add(ropeMaterial);
            Destroy(probe);
        }

        private void OnGUI()
        {
            if (!showRuntimePanel)
            {
                return;
            }

            GUILayout.BeginArea(new Rect(24f, 24f, 510f, 225f), GUI.skin.box);
            GUILayout.Label("YC RUNTIME ROOT · 四绳场桥灰盒");
            GUILayout.Space(6f);
            GUILayout.Label($"状态：{runtimeStatus}");
            GUILayout.Label($"钢绳：张力悬挂 + Obi视觉 / {solver?.substeps ?? 0} substeps");
            GUILayout.Label($"绳索：{(ropesReady ? "4/4 Ready" : $"{ropes.Count}/4 Loading")}");
            GUILayout.Label($"吊具位置：{FormatVector(spreaderBody != null ? spreaderBody.position : Vector3.zero)} · " +
                            $"小车速度：{currentTrolleyVelocity:F2} m/s");
            GUILayout.Label($"灰盒：{(showGraybox ? "显示" : "隐藏")} · 摆幅：{maximumObservedSway:F3} m · " +
                            $"伸长：{maximumObservedAxialStretch * 1000f:F1} mm · 原始残差：{maximumRawConstraintDrift * 1000f:F1} mm");
            GUILayout.Space(6f);
            GUILayout.Label("A/D 小车 · W/S 收放绳 · Space 扰动 · R 重置 · G 隐藏/显示灰盒");
            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            foreach (var ropeRuntime in ropes)
            {
                if (ropeRuntime.Blueprint != null)
                {
                    Destroy(ropeRuntime.Blueprint);
                }
            }

            foreach (var material in runtimeMaterials)
            {
                if (material != null)
                {
                    Destroy(material);
                }
            }
        }

        private static bool IsFinite(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        }

        private static string FormatVector(Vector3 value)
        {
            return $"({value.x:F2}, {value.y:F2}, {value.z:F2})";
        }

        private static float SmootherStep01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * value * (value * (value * 6f - 15f) + 10f);
        }

        private readonly struct EngineeringProfile
        {
            public EngineeringProfile(float ropeLengthMeters, float systemStiffnessNPerMeter, float staticStretchMeters)
            {
                RopeLengthMeters = ropeLengthMeters;
                SystemStiffnessNPerMeter = systemStiffnessNPerMeter;
                StaticStretchMeters = staticStretchMeters;
            }

            public float RopeLengthMeters { get; }
            public float SystemStiffnessNPerMeter { get; }
            public float StaticStretchMeters { get; }
        }

        private readonly struct RopeDefinition
        {
            public RopeDefinition(string id, string topAnchorPath, string bottomAnchorPath, string ropeSlotPath)
            {
                Id = id;
                TopAnchorPath = topAnchorPath;
                BottomAnchorPath = bottomAnchorPath;
                RopeSlotPath = ropeSlotPath;
            }

            public string Id { get; }
            public string TopAnchorPath { get; }
            public string BottomAnchorPath { get; }
            public string RopeSlotPath { get; }
        }

        private sealed class RopeRuntime
        {
            public RopeRuntime(
                string id,
                Transform slot,
                GameObject actorObject,
                Obi.ObiRope rope,
                ObiRopeCursor cursor,
                ObiRopeBlueprint blueprint,
                ObiParticleAttachment topAttachment,
                ObiParticleAttachment bottomAttachment,
                Transform topAnchor,
                Transform bottomAnchor,
                ConfigurableJoint suspensionJoint,
                float initialLength)
            {
                Id = id;
                Slot = slot;
                ActorObject = actorObject;
                Rope = rope;
                Cursor = cursor;
                Blueprint = blueprint;
                TopAttachment = topAttachment;
                BottomAttachment = bottomAttachment;
                TopAnchor = topAnchor;
                BottomAnchor = bottomAnchor;
                SuspensionJoint = suspensionJoint;
                InitialLength = initialLength;
                CommandedLength = initialLength;
                LastAppliedTopPosition = topAnchor.position;
                LastAppliedLength = initialLength;
            }

            public string Id { get; }
            public Transform Slot { get; }
            public GameObject ActorObject { get; }
            public Obi.ObiRope Rope { get; }
            public ObiRopeCursor Cursor { get; }
            public ObiRopeBlueprint Blueprint { get; }
            public ObiParticleAttachment TopAttachment { get; }
            public ObiParticleAttachment BottomAttachment { get; }
            public Transform TopAnchor { get; }
            public Transform BottomAnchor { get; }
            public ConfigurableJoint SuspensionJoint { get; }
            public float InitialLength { get; set; }
            public float CommandedLength { get; set; }
            public Vector3 LastAppliedTopPosition { get; set; }
            public float LastAppliedLength { get; set; }
            public Vector3 AppliedTopVelocity { get; set; }
            public float AppliedLengthRate { get; set; }
        }
    }
}
