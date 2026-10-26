#if UNITY_EDITOR
using System;
using Obi;
using PortTraining.YardCraneSimulator.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    /// <summary>
    /// One-time Edit Mode authoring for the objects that the MAX rig consumes at runtime.
    /// The runtime deliberately has no repair fallback: after this command has populated the
    /// scene, designers own the serialized components and assets through the Inspector.
    /// </summary>
    public static class YardCraneSceneSimulationAuthoring
    {
        private const string RootName = "YC_RuntimeRoot";
        private const string DefaultProfileAssetPath =
            "Assets/YardCraneSimulator/Resources/Configuration/YC-EightRopeMAX-Greybox-v1.asset";
        private const string RopeAssetFolder =
            "Assets/YardCraneSimulator/Art/Rope/SceneAuthored";
        private const string RopeMaterialAssetPath = RopeAssetFolder + "/YC_MAX_Rope.mat";

        [MenuItem(YardCraneSceneSimulationContract.AuthoringMenuPath)]
        public static void BakeActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException(
                    "Scene-authored ropes and physics can only be baked in Edit Mode.");
            }

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                throw new InvalidOperationException("No loaded scene is available for yard-crane authoring.");
            }

            var root = FindRoot(scene);
            if (root == null)
            {
                throw new InvalidOperationException($"Missing scene root: {RootName}");
            }

            var rig = root.GetComponent<YardCraneEightRopeMaxPrototypeRig>();
            if (rig == null)
            {
                throw new InvalidOperationException(
                    $"{RootName} requires {nameof(YardCraneEightRopeMaxPrototypeRig)} before authoring.");
            }

            var profile = GetOrAssignRigProfile(rig);
            var profileError = profile == null ? "asset-not-found" : string.Empty;
            if (profile == null || !profile.TryValidate(out profileError))
            {
                throw new InvalidOperationException(
                    $"Cannot author the scene because the default physics profile is invalid: " +
                    profileError);
            }

            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Bake Yard Crane Scene-Authored Ropes And Physics");
            Undo.RegisterFullObjectHierarchyUndo(root, "Bake Yard Crane Scene Simulation");

            var material = GetOrCreateRopeMaterial();
            var section = Resources.Load<ObiRopeSection>("DefaultRopeSection");
            if (section == null)
            {
                throw new InvalidOperationException(
                    "Obi DefaultRopeSection could not be loaded; the Obi package resources are incomplete.");
            }

            AuthorHeadblockPhysics(root.transform, profile);
            AuthorContainerPhysics(root.transform, rig, profile);
            AuthorSolver(root.transform, profile);
            AuthorEightRopes(root.transform, profile, material, section);
            ValidateOrThrow(root.transform, profile);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(undoGroup);
            Selection.activeGameObject = root;
            Debug.Log(
                "[YC_SCENE_AUTHORED_SIMULATION_READY] ropes=8, blueprints=8, " +
                "headblockPhysics=scene, containerFreeBody=scene, sceneSaved=false. " +
                "Inspect the hierarchy and press Ctrl+S when accepted.",
                root);
        }

        public static void ValidateOrThrow(Transform root, YardCraneEightRopeMaxProfile profile)
        {
            RequireComponent<Rigidbody>(root, YardCraneSceneSimulationContract.HeadblockPath);
            RequireComponent<ObiRigidbody>(root, YardCraneSceneSimulationContract.HeadblockPath);
            RequireComponent<BoxCollider>(root, YardCraneSceneSimulationContract.HeadblockColliderPath);

            var ropeRig = RequireTransform(root, YardCraneSceneSimulationContract.RopeRigPath);
            if (ropeRig.GetComponents<ObiSolver>().Length != 1)
            {
                throw new InvalidOperationException(
                    $"{YardCraneSceneSimulationContract.RopeRigPath} must contain exactly one ObiSolver.");
            }

            var infrastructure = RequireTransform(
                root, YardCraneSceneSimulationContract.ObiSceneComponentsPath);
            if (infrastructure.GetComponentInChildren<BurstColliderWorld>(true) == null)
            {
                throw new InvalidOperationException(
                    $"{YardCraneSceneSimulationContract.ObiSceneComponentsPath} requires BurstColliderWorld.");
            }

            var activeContainer = RequireTransform(root, YardCraneSceneSimulationContract.ActiveContainerPath);
            if (activeContainer.GetComponentInChildren<Rigidbody>(true) != null)
            {
                throw new InvalidOperationException(
                    "ActiveContainer and its visual subtree must not carry a Rigidbody while it becomes a " +
                    "headblock compound collider. " +
                    "Use the scene-authored ContainerFreeBody proxy instead.");
            }
            RequireComponent<BoxCollider>(root, YardCraneSceneSimulationContract.ActiveContainerPath);

            var freeBody = RequireTransform(root, YardCraneSceneSimulationContract.ContainerFreeBodyPath);
            RequireComponent<Rigidbody>(root, YardCraneSceneSimulationContract.ContainerFreeBodyPath);
            RequireComponent<YardCraneContainerImpactSensor>(
                root, YardCraneSceneSimulationContract.ContainerFreeBodyPath);
            if (freeBody.GetComponents<Collider>().Length != 0 || freeBody.childCount != 0)
            {
                throw new InvalidOperationException(
                    "ContainerFreeBody must be an empty Rigidbody proxy in the saved scene. " +
                    "Keep ActiveContainer as its sibling so runtime can transfer ownership explicitly.");
            }
            if (activeContainer.IsChildOf(freeBody))
            {
                throw new InvalidOperationException(
                    "ActiveContainer must remain a sibling of ContainerFreeBody in the saved scene. " +
                    "The runtime reparents it only while the released container is dynamic.");
            }

            if (profile.loopBindings == null || profile.loopBindings.Length != 8)
            {
                throw new InvalidOperationException("The active profile must define exactly eight rope bindings.");
            }

            foreach (var binding in profile.loopBindings)
            {
                var top = RequireTransform(root, binding.topAnchorPath);
                var sheave = RequireTransform(root, binding.sheavePath);
                var slot = RequireTransform(root, binding.slotPath);
                var actorName = YardCraneSceneSimulationContract.GetRopeActorName(binding.id);
                var actor = slot.Find(actorName);
                if (actor == null)
                {
                    throw new InvalidOperationException($"Missing scene-authored rope: {binding.slotPath}/{actorName}");
                }

                var rope = RequireComponent<ObiRope>(actor);
                var renderer = RequireComponent<ObiRopeExtrudedRenderer>(actor);
                RequireComponent<ObiRopeCursor>(actor);
                if (rope.ropeBlueprint == null || !EditorUtility.IsPersistent(rope.ropeBlueprint) ||
                    rope.ropeBlueprint.groups == null || rope.ropeBlueprint.groups.Count < 2)
                {
                    throw new InvalidOperationException(
                        $"{binding.id} requires its own persistent blueprint with two endpoint groups.");
                }
                if (renderer.material == null || !EditorUtility.IsPersistent(renderer.material) ||
                    renderer.section == null)
                {
                    throw new InvalidOperationException(
                        $"{binding.id} requires a persistent material and an Obi rope section.");
                }

                var attachments = actor.GetComponents<ObiParticleAttachment>();
                if (attachments.Length != 2)
                {
                    throw new InvalidOperationException(
                        $"{binding.id} requires exactly two ObiParticleAttachment components.");
                }

                var fixedBound = false;
                var sheaveBound = false;
                foreach (var attachment in attachments)
                {
                    fixedBound |= attachment.target == top &&
                                  attachment.particleGroup == rope.ropeBlueprint.groups[0];
                    sheaveBound |= attachment.target == sheave &&
                                   attachment.particleGroup == rope.ropeBlueprint.groups[1];
                }
                if (!fixedBound || !sheaveBound)
                {
                    throw new InvalidOperationException(
                        $"{binding.id} endpoint attachments do not match its profile anchors.");
                }
            }
        }

        private static void AuthorHeadblockPhysics(
            Transform root,
            YardCraneEightRopeMaxProfile profile)
        {
            var headblock = RequireTransform(root, YardCraneSceneSimulationContract.HeadblockPath);
            var body = EnsureComponent<Rigidbody>(headblock.gameObject, out var bodyAdded);
            if (bodyAdded)
            {
                body.mass = profile.load.simulationProxyMassKg;
                body.linearDamping = profile.ropePhysics.unloadedHeadblockLinearDamping;
                body.angularDamping = profile.ropePhysics.unloadedHeadblockAngularDamping;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                body.solverIterations = profile.ropePhysics.headblockSolverIterations;
                body.solverVelocityIterations = profile.ropePhysics.headblockSolverVelocityIterations;
                body.useGravity = false;
            }
            EnsureComponent<ObiRigidbody>(headblock.gameObject, out _);

            var colliderTransform = EnsurePath(
                root, YardCraneSceneSimulationContract.HeadblockColliderPath);
            var collider = EnsureComponent<BoxCollider>(colliderTransform.gameObject, out var colliderAdded);
            if (colliderAdded)
            {
                collider.center = Vector3.zero;
                collider.size = profile.geometryAndSafety.headblockColliderSizeMeters;
            }
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(collider);
        }

        private static void AuthorContainerPhysics(
            Transform root,
            YardCraneEightRopeMaxPrototypeRig rig,
            YardCraneEightRopeMaxProfile profile)
        {
            var activeContainer = RequireTransform(root, YardCraneSceneSimulationContract.ActiveContainerPath);
            var collider = EnsureComponent<BoxCollider>(activeContainer.gameObject, out var colliderAdded);
            if (colliderAdded)
            {
                collider.center = Vector3.zero;
                collider.size = profile.geometryAndSafety.containerColliderSizeMeters;
            }

            var freeBody = EnsurePath(root, YardCraneSceneSimulationContract.ContainerFreeBodyPath);
            if (freeBody.parent != activeContainer.parent)
            {
                freeBody.SetParent(activeContainer.parent, true);
            }
            if (freeBody.GetSiblingIndex() <= activeContainer.GetSiblingIndex())
            {
                freeBody.SetSiblingIndex(activeContainer.GetSiblingIndex() + 1);
            }
            freeBody.SetPositionAndRotation(activeContainer.position, activeContainer.rotation);
            freeBody.localScale = Vector3.one;

            var body = EnsureComponent<Rigidbody>(freeBody.gameObject, out var bodyAdded);
            if (bodyAdded || body.mass < profile.load.minimumContainerGrossMassKg ||
                body.mass > profile.load.maximumContainerGrossMassKg)
            {
                body.mass = Mathf.Clamp(
                    rig.containerGrossMassKg,
                    profile.load.minimumContainerGrossMassKg,
                    profile.load.maximumContainerGrossMassKg);
            }
            if (bodyAdded)
            {
                body.linearDamping = 0f;
                body.angularDamping = 0.05f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }
            body.useGravity = false;
            body.isKinematic = true;
            body.detectCollisions = true;

            EnsureComponent<YardCraneContainerImpactSensor>(freeBody.gameObject, out _);
            EditorUtility.SetDirty(body);
            EditorUtility.SetDirty(collider);
        }

        private static void AuthorSolver(Transform root, YardCraneEightRopeMaxProfile profile)
        {
            var infrastructure = EnsurePath(root, YardCraneSceneSimulationContract.ObiSceneComponentsPath);
            EnsureComponent<BurstColliderWorld>(infrastructure.gameObject, out _);

            var ropeRig = RequireTransform(root, YardCraneSceneSimulationContract.RopeRigPath);
            var solver = EnsureComponent<ObiSolver>(ropeRig.gameObject, out var solverAdded);
            if (solverAdded)
            {
                solver.backendType = ObiSolver.BackendType.Burst;
                solver.synchronization = ObiSolver.Synchronization.SynchronousFixed;
                solver.substeps = Mathf.Max(8, profile.ropePhysics.obiSolverSubsteps);
                solver.maxStepsPerFrame = 1;
                solver.gravity = Vector3.zero;
                solver.gravitySpace = Space.World;
                solver.distanceConstraintParameters.iterations =
                    Mathf.Clamp(profile.ropePhysics.obiDistanceIterations, 1, 2);
                solver.collisionConstraintParameters.enabled = false;
                solver.particleCollisionConstraintParameters.enabled = false;
                var parameters = solver.parameters;
                parameters.damping = 0.3f;
                solver.parameters = parameters;
            }
            EditorUtility.SetDirty(solver);
        }

        private static void AuthorEightRopes(
            Transform root,
            YardCraneEightRopeMaxProfile profile,
            Material material,
            ObiRopeSection section)
        {
            EnsureAssetFolder(RopeAssetFolder);
            foreach (var binding in profile.loopBindings)
            {
                var top = RequireTransform(root, binding.topAnchorPath);
                var sheave = RequireTransform(root, binding.sheavePath);
                var slot = RequireTransform(root, binding.slotPath);
                var actorName = YardCraneSceneSimulationContract.GetRopeActorName(binding.id);
                var actor = slot.Find(actorName);
                if (actor == null)
                {
                    var actorObject = new GameObject(actorName);
                    Undo.RegisterCreatedObjectUndo(actorObject, $"Create {actorName}");
                    actorObject.transform.SetParent(slot, false);
                    actor = actorObject.transform;
                }
                actor.localPosition = Vector3.zero;
                actor.localRotation = Quaternion.identity;
                actor.localScale = Vector3.one;

                var blueprint = GetOrCreateBlueprint(binding.id, actor, top, sheave);
                var rope = EnsureComponent<ObiRope>(actor.gameObject, out var ropeAdded);
                if (ropeAdded)
                {
                    rope.maxBending = 0.02f;
                    rope.stretchCompliance = 0f;
                    rope.maxCompression = 1f;
                }
                rope.ropeBlueprint = blueprint;

                var renderer = EnsureComponent<ObiRopeExtrudedRenderer>(
                    actor.gameObject, out var rendererAdded);
                if (rendererAdded)
                {
                    renderer.thicknessScale = 0.7f;
                    renderer.uvScale = new Vector2(1f, 4f);
                }
                if (renderer.section == null) renderer.section = section;
                if (renderer.material == null) renderer.material = material;

                var cursor = EnsureComponent<ObiRopeCursor>(actor.gameObject, out var cursorAdded);
                if (cursorAdded)
                {
                    cursor.cursorMu = 0f;
                    cursor.direction = true;
                }

                var attachments = actor.GetComponents<ObiParticleAttachment>();
                if (attachments.Length > 2)
                {
                    throw new InvalidOperationException(
                        $"{binding.id} already has {attachments.Length} attachments; remove the extra " +
                        "components manually before authoring to avoid destructive guesses.");
                }
                while (attachments.Length < 2)
                {
                    Undo.AddComponent<ObiParticleAttachment>(actor.gameObject);
                    attachments = actor.GetComponents<ObiParticleAttachment>();
                }

                var fixedAttachment = FindAttachment(attachments, top) ?? attachments[0];
                var sheaveAttachment = FindAttachment(attachments, sheave);
                if (sheaveAttachment == null || sheaveAttachment == fixedAttachment)
                {
                    sheaveAttachment = attachments[0] == fixedAttachment ? attachments[1] : attachments[0];
                }

                fixedAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
                fixedAttachment.target = top;
                fixedAttachment.particleGroup = blueprint.groups[0];
                fixedAttachment.projection = false;
                sheaveAttachment.attachmentType = ObiParticleAttachment.AttachmentType.Static;
                sheaveAttachment.target = sheave;
                sheaveAttachment.particleGroup = blueprint.groups[1];
                sheaveAttachment.projection = true;

                EditorUtility.SetDirty(rope);
                EditorUtility.SetDirty(renderer);
                EditorUtility.SetDirty(cursor);
                EditorUtility.SetDirty(fixedAttachment);
                EditorUtility.SetDirty(sheaveAttachment);
            }
        }

        private static ObiRopeBlueprint GetOrCreateBlueprint(
            string ropeId,
            Transform actor,
            Transform top,
            Transform sheave)
        {
            var assetPath = $"{RopeAssetFolder}/YC_MAX_{ropeId}_StraightRope.asset";
            var blueprint = AssetDatabase.LoadAssetAtPath<ObiRopeBlueprint>(assetPath);
            var created = blueprint == null;
            if (created)
            {
                blueprint = ScriptableObject.CreateInstance<ObiRopeBlueprint>();
                blueprint.name = $"YC_MAX_{ropeId}_StraightRope";
                AssetDatabase.CreateAsset(blueprint, assetPath);
            }

            if (created || blueprint.path.ControlPointCount < 2)
            {
                Undo.RecordObject(blueprint, $"Initialize {ropeId} rope blueprint");
                blueprint.thickness = 0.028f;
                blueprint.resolution = 0.55f;
                blueprint.pooledParticles = 64;
                blueprint.path.Clear();

                var start = actor.InverseTransformPoint(top.position);
                var end = actor.InverseTransformPoint(sheave.position);
                var tangent = (end - start).normalized * 0.5f;
                var filter = ObiUtils.MakeFilter(ObiUtils.CollideWithEverything, 0);
                blueprint.path.AddControlPoint(
                    start, -tangent, tangent, Vector3.forward, 0.12f, 0.1f, 1f,
                    filter, Color.white, "Fixed");
                blueprint.path.AddControlPoint(
                    end, -tangent, tangent, Vector3.forward, 0.12f, 0.1f, 1f,
                    filter, Color.white, "Sheave");
                blueprint.path.FlushEvents();
                blueprint.GenerateImmediate();
                EditorUtility.SetDirty(blueprint);
            }

            return blueprint;
        }

        private static Material GetOrCreateRopeMaterial()
        {
            EnsureAssetFolder(RopeAssetFolder);
            var material = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialAssetPath);
            if (material != null) return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException("No supported shader is available for the rope material.");
            }

            material = new Material(shader)
            {
                name = "YC_MAX_Rope",
                color = new Color(0.08f, 0.10f, 0.14f)
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", material.color);
            }
            AssetDatabase.CreateAsset(material, RopeMaterialAssetPath);
            return material;
        }

        private static YardCraneEightRopeMaxProfile GetOrAssignRigProfile(
            YardCraneEightRopeMaxPrototypeRig rig)
        {
            var serializedRig = new SerializedObject(rig);
            var profileProperty = serializedRig.FindProperty("profileAsset");
            if (profileProperty == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(YardCraneEightRopeMaxPrototypeRig)} no longer exposes the profileAsset field.");
            }

            var profile = profileProperty.objectReferenceValue as YardCraneEightRopeMaxProfile;
            if (profile != null)
            {
                return profile;
            }

            profile = AssetDatabase.LoadAssetAtPath<YardCraneEightRopeMaxProfile>(DefaultProfileAssetPath);
            if (profile == null)
            {
                return null;
            }

            Undo.RecordObject(rig, "Assign yard-crane physics profile");
            profileProperty.objectReferenceValue = profile;
            serializedRig.ApplyModifiedProperties();
            EditorUtility.SetDirty(rig);
            return profile;
        }

        private static T EnsureComponent<T>(GameObject target, out bool added) where T : Component
        {
            var component = target.GetComponent<T>();
            added = component == null;
            return component != null ? component : Undo.AddComponent<T>(target);
        }

        private static T RequireComponent<T>(Transform root, string path) where T : Component
        {
            return RequireComponent<T>(RequireTransform(root, path));
        }

        private static T RequireComponent<T>(Transform target) where T : Component
        {
            var component = target.GetComponent<T>();
            if (component == null)
            {
                throw new InvalidOperationException($"{target.name} requires {typeof(T).Name}.");
            }
            return component;
        }

        private static ObiParticleAttachment FindAttachment(
            ObiParticleAttachment[] attachments,
            Transform target)
        {
            foreach (var attachment in attachments)
            {
                if (attachment.target == target) return attachment;
            }
            return null;
        }

        private static GameObject FindRoot(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == RootName) return root;
            }
            return null;
        }

        private static Transform RequireTransform(Transform root, string path)
        {
            var target = root.Find(path);
            if (target == null) throw new InvalidOperationException($"Missing hierarchy path: {path}");
            return target;
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

        private static void EnsureAssetFolder(string folderPath)
        {
            var segments = folderPath.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }
    }
}
#endif
