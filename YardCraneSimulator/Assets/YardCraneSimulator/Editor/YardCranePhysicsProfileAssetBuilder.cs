using System.IO;
using PortTraining.YardCraneSimulator.Configuration;
using UnityEditor;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.Editor
{
    public static class YardCranePhysicsProfileAssetBuilder
    {
        // Asset generation is explicit after the one-time v15 migration.
        private const string AssetFolder = "Assets/YardCraneSimulator/Resources/Configuration";
        private const string AssetPath = AssetFolder + "/YC-EightRopeMAX-Greybox-v1.asset";

        [MenuItem("Tools/Yard Crane/Configuration/Create or Update Default Physics Profile")]
        public static void CreateOrUpdateDefaultProfile()
        {
            EnsureFolder(AssetFolder);

            var profile = AssetDatabase.LoadAssetAtPath<YardCraneEightRopeMaxProfile>(AssetPath);
            var defaults = YardCraneEightRopeMaxProfile.CreateRuntimeDefaults();
            if (profile == null)
            {
                defaults.hideFlags = HideFlags.None;
                defaults.name = "YC-EightRopeMAX-Greybox-v1";
                profile = defaults;
                AssetDatabase.CreateAsset(profile, AssetPath);
            }
            else
            {
                EditorUtility.CopySerialized(defaults, profile);
                profile.hideFlags = HideFlags.None;
                profile.name = "YC-EightRopeMAX-Greybox-v1";
                Object.DestroyImmediate(defaults);
                EditorUtility.SetDirty(profile);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!profile.TryValidate(out var error))
            {
                Debug.LogError($"[YC_PHYSICS_PROFILE_FAIL] path={AssetPath}, reason={error}", profile);
                return;
            }

            Debug.Log(
                $"[YC_PHYSICS_PROFILE_ASSET_READY] path={AssetPath}, " +
                $"schema={profile.identity.schemaVersion}, profile={profile.identity.profileId}, " +
                $"topology={profile.identity.topologyProfileId}, loops={profile.loopBindings.Length}",
                profile);
            Selection.activeObject = profile;
        }

        [MenuItem("Tools/Yard Crane/Configuration/Validate Default Physics Profile")]
        public static void ValidateDefaultProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<YardCraneEightRopeMaxProfile>(AssetPath);
            if (profile == null)
            {
                Debug.LogError($"[YC_PHYSICS_PROFILE_FAIL] path={AssetPath}, reason=asset-not-found");
                return;
            }

            if (!profile.TryValidate(out var error))
            {
                Debug.LogError($"[YC_PHYSICS_PROFILE_FAIL] path={AssetPath}, reason={error}", profile);
                return;
            }

            Debug.Log(
                $"[YC_PHYSICS_PROFILE_PASS] path={AssetPath}, schema={profile.identity.schemaVersion}, " +
                $"profile={profile.identity.profileId}, topology={profile.identity.topologyProfileId}, " +
                $"loops={profile.loopBindings.Length}",
                profile);
        }

        [MenuItem("Tools/Yard Crane/Configuration/Run Physics Profile Contract Self-Test")]
        public static void RunProfileContractSelfTest()
        {
            var valid = YardCraneEightRopeMaxProfile.CreateRuntimeDefaults();
            var validAccepted = valid.TryValidate(out _);
            Object.DestroyImmediate(valid);

            var badSchema = YardCraneEightRopeMaxProfile.CreateRuntimeDefaults();
            badSchema.identity.schemaVersion = "unsupported";
            var badSchemaRejected = !badSchema.TryValidate(out _);
            Object.DestroyImmediate(badSchema);

            var duplicateLoop = YardCraneEightRopeMaxProfile.CreateRuntimeDefaults();
            duplicateLoop.loopBindings[1].id = duplicateLoop.loopBindings[0].id;
            var duplicateLoopRejected = !duplicateLoop.TryValidate(out _);
            Object.DestroyImmediate(duplicateLoop);

            if (!validAccepted || !badSchemaRejected || !duplicateLoopRejected)
            {
                Debug.LogError(
                    $"[YC_PHYSICS_PROFILE_CONTRACT_FAIL] validAccepted={validAccepted}, " +
                    $"badSchemaRejected={badSchemaRejected}, duplicateLoopRejected={duplicateLoopRejected}");
                return;
            }

            Debug.Log(
                $"[YC_PHYSICS_PROFILE_CONTRACT_PASS] validAccepted={validAccepted}, " +
                $"badSchemaRejected={badSchemaRejected}, duplicateLoopRejected={duplicateLoopRejected}");
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            var name = Path.GetFileName(folder);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
            {
                throw new InvalidDataException($"Invalid Unity asset folder: {folder}");
            }

            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
