using Obi;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Experiments.RopePhysics
{
    internal static class YardCraneSceneSimulationContract
    {
        public const string RopeRigPath = "40_RopeRig";
        public const string ObiSceneComponentsPath = "40_RopeRig/_ObiSceneComponents";
        public const string HeadblockPath = "30_SpreaderPhysics";
        public const string HeadblockColliderPath =
            "30_SpreaderPhysics/Physics/Colliders/Graybox_HeadblockSpreaderCollider";
        public const string ActiveContainerPath = "50_ContainerWorkspace/ActiveContainer";
        public const string ContainerFreeBodyPath = "50_ContainerWorkspace/ContainerFreeBody";
        public const string AuthoringMenuPath =
            "Tools/Yard Crane/Model Hierarchy/Bake Scene-Authored Ropes and Physics";

        public static string GetRopeActorName(string ropeId) => $"ObiRope_{ropeId}_Straight";
    }

    // Pure bookkeeping types for the eight independent straight ropes. Each runtime record owns
    // one trolley-side top anchor, one headblock-side lower anchor and exactly one Obi visual rope.
    // The profile still serializes the historical dead-end path for old scenes, but that legacy
    // compatibility value is deliberately absent from the runtime topology.

    internal readonly struct LoopDefinition
    {
        public LoopDefinition(string id, string drum, string antiSwayAxis, string topAnchorPath, string sheavePath, string slotPath)
        {
            Id = id;
            Drum = drum;
            AntiSwayAxis = antiSwayAxis;
            TopAnchorPath = topAnchorPath;
            SheavePath = sheavePath;
            SlotPath = slotPath;
        }

        public string Id { get; }
        public string Drum { get; }
        public string AntiSwayAxis { get; }
        public string TopAnchorPath { get; }
        public string SheavePath { get; }
        public string SlotPath { get; }
    }

    internal sealed class LoopRuntime
    {
        public LoopRuntime(
            LoopDefinition definition,
            Transform topAnchor,
            Transform sheave,
            Vector3 localSheaveAnchor,
            VisualLegRuntime visualRope,
            float initialRopeLength)
        {
            Definition = definition;
            TopAnchor = topAnchor;
            Sheave = sheave;
            LocalSheaveAnchor = localSheaveAnchor;
            VisualRope = visualRope;
            InitialRopeLength = initialRopeLength;
            CommandedRopeLength = initialRopeLength;
            LastCommandedRopeLength = initialRopeLength;
            LastTopAnchorPosition = topAnchor.position;
        }

        public LoopDefinition Definition { get; }
        public Transform TopAnchor { get; }
        public Transform Sheave { get; }
        public Vector3 LocalSheaveAnchor { get; }
        public VisualLegRuntime VisualRope { get; }
        public float InitialRopeLength { get; set; }
        public float CommandedRopeLength { get; set; }
        public float LastCommandedRopeLength { get; set; }
        public Vector3 LastTopAnchorPosition { get; set; }
        public Vector3 TopAnchorVelocity { get; set; }
        public float RopeLengthRate { get; set; }
    }

    internal sealed class VisualLegRuntime
    {
        public VisualLegRuntime(
            GameObject actorObject,
            Obi.ObiRope rope,
            ObiRopeCursor cursor,
            ObiRopeBlueprint blueprint,
            ObiParticleAttachment fixedAttachment,
            ObiParticleAttachment sheaveAttachment,
            float commandedVisualLength)
        {
            ActorObject = actorObject;
            Rope = rope;
            Cursor = cursor;
            Blueprint = blueprint;
            FixedAttachment = fixedAttachment;
            SheaveAttachment = sheaveAttachment;
            CommandedVisualLength = commandedVisualLength;
        }

        public GameObject ActorObject { get; }
        public Obi.ObiRope Rope { get; }
        public ObiRopeCursor Cursor { get; }
        public ObiRopeBlueprint Blueprint { get; }
        public ObiParticleAttachment FixedAttachment { get; }
        public ObiParticleAttachment SheaveAttachment { get; }
        public float CommandedVisualLength { get; set; }
        public bool IsLoaded => Rope.isLoaded && Rope.elements.Count > 0;
        public bool AttachmentsBound => FixedAttachment.isBound && SheaveAttachment.isBound;
    }
}
