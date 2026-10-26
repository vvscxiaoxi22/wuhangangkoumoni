using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HikvisionFourDisplay.Runtime.Bootstrap;
using HikvisionFourDisplay.Runtime.Configuration;
using HikvisionFourDisplay.Runtime.Decoding;
using HikvisionFourDisplay.Runtime.Feeds;
using HikvisionFourDisplay.Runtime.Presentation;
using HikvisionFourDisplay.Runtime.Security;
using HikvisionFourDisplay.Runtime.UI;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HikvisionFourDisplay.Editor
{
    /// <summary>
    /// Focused, offline verification. Uses real configuration storage and display logic,
    /// fake credentials/decoders, and an unsaved temporary scene. Never contacts a camera.
    /// </summary>
    public static class FeedConfigurationVerification
    {
        private const string MainScenePath = "Assets/HikvisionFourDisplay/Scenes/Main.unity";
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private static int assertions;

        public static void Verify()
        {
            assertions = 0;
            var feedsBackup = new FileBackup(FeedConfigurationLoader.ExternalConfigPath);
            var schemesBackup = new FileBackup(FeedSchemeStore.ExternalConfigPath);
            var originalScene = SceneManager.GetActiveScene();
            var originalFallback = GetStatic<Texture2D>("fallbackTexture");
            var originalFallbackAttempted = GetStatic<bool>("fallbackLoadAttempted");
            var originalFallbackUsers = GetStatic<int>("fallbackTextureUsers");
            var temporaryScene = default(Scene);
            var presenters = new List<FeedSurfacePresenter>();
            var controllers = new List<CameraFeedController>();
            var materials = new List<Material>();
            FakeDecoderFactory factory = null;
            try
            {
                VerifyCatalog();
                var schemes = VerifySchemes();
                temporaryScene = EditorSceneManager.NewPreviewScene();
                factory = new FakeDecoderFactory();
                VerifyRuntime(temporaryScene, schemes, factory, presenters, controllers, materials);
                Debug.Log($"[HFD_CONFIGURATION_VERIFY] status=passed assertions={assertions} network=none");
            }
            catch (Exception)
            {
                // Assertions contain only fixed labels. Never log config contents, request URLs, or credentials.
                Debug.LogError("[HFD_CONFIGURATION_VERIFY] status=failed; inspect the fixed verification assertion.");
                throw;
            }
            finally
            {
                // Keep cleanup independent of validation outcome, including a partially constructed scene.
                try
                {
                    foreach (var controller in controllers)
                        if (controller != null) controller.Shutdown();
                    foreach (var presenter in presenters)
                    {
                        if (presenter == null) continue;
                        SetPrivate(presenter, "instanceMaterial", null);
                        SetPrivate(presenter, "fallbackTextureAcquired", false);
                    }
                    foreach (var material in materials)
                        if (material != null) UnityEngine.Object.DestroyImmediate(material);
                    var acquiredFallback = GetStatic<Texture2D>("fallbackTexture");
                    SetStatic("fallbackTexture", originalFallback);
                    SetStatic("fallbackLoadAttempted", originalFallbackAttempted);
                    SetStatic("fallbackTextureUsers", originalFallbackUsers);
                    if (acquiredFallback != null && acquiredFallback != originalFallback)
                        UnityEngine.Object.DestroyImmediate(acquiredFallback);
                    factory?.ReleaseTextures();
                    if (temporaryScene.IsValid() && temporaryScene.isLoaded)
                        EditorSceneManager.ClosePreviewScene(temporaryScene);
                    if (originalScene.IsValid() && originalScene.isLoaded)
                        SceneManager.SetActiveScene(originalScene);
                }
                finally
                {
                    try { feedsBackup.Restore(); }
                    finally { schemesBackup.Restore(); }
                }
            }
        }

        /// <summary>Builds the already-saved Main scene; does not create or save scene objects.</summary>
        public static void BuildWindows()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath) == null)
                throw new InvalidOperationException("The saved Main scene is missing.");
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("An open scene has unsaved changes; build did not save or discard it.");
            var buildPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", "2026-10-06",
                "Hikvision Four Display.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(buildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build did not succeed.");
            Debug.Log($"[HFD_WINDOWS_BUILD] status=passed sceneCount=1 output={buildPath}");
        }

        private static void VerifyCatalog()
        {
            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "192.168.199.200", "视频监控主机" }, { "192.168.199.202", "机房 1" },
                { "192.168.199.203", "机房 2" }, { "192.168.199.204", "港口实训中心" },
                { "192.168.199.201", "沙盘监控" }, { "192.168.199.211", "场桥 1-1" },
                { "192.168.199.212", "场桥 1-2" }, { "192.168.199.213", "场桥 2-1" },
                { "192.168.199.214", "场桥 2-2" }, { "192.168.199.215", "岸桥 1" },
                { "192.168.199.216", "岸桥 2" }, { "192.168.199.100", "岸桥监控" }
            };
            var catalog = CameraCatalogLoader.Load(out _, out var source);
            Require(catalog.cameras.Length == expected.Count, "catalog has exactly twelve cameras");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var camera in catalog.cameras)
            {
                Require(expected.TryGetValue(camera.host, out var expectedName), "catalog contains only requested addresses");
                Require(camera.name == expectedName && seen.Add(camera.host), "catalog names and addresses match uniquely");
                Require(camera.rtspPort == 554 && camera.channel == "1" && camera.streamType == "Main",
                    "catalog uses expected stream defaults");
                Require(!string.IsNullOrWhiteSpace(camera.credentialKey), "catalog stores a credential reference");
            }
            RequireNoSecretFields(File.ReadAllText(source));
            var bundledPath = Path.Combine(Application.streamingAssetsPath, "HikvisionFourDisplay", "cameras.json");
            var bundled = JsonUtility.FromJson<CameraCatalogDocument>(File.ReadAllText(bundledPath));
            Require(bundled != null && bundled.cameras.Length == expected.Count, "bundled catalog has twelve cameras");
            var bundledHosts = new HashSet<string>(StringComparer.Ordinal);
            foreach (var camera in bundled.cameras)
                Require(expected.TryGetValue(camera.host, out var expectedName) && camera.name == expectedName &&
                        bundledHosts.Add(camera.host), "bundled catalog matches requested cameras");
            RequireNoSecretFields(File.ReadAllText(bundledPath));
        }

        private static FeedSchemeDocument VerifySchemes()
        {
            Directory.CreateDirectory(FeedConfigurationLoader.ExternalConfigDirectory);
            File.WriteAllText(FeedSchemeStore.ExternalConfigPath, "{\"configVersion\":1,\"schemes\":[]}");
            var baseline = CreateConfiguration();
            FeedConfigurationLoader.Save(baseline);
            var active = FeedConfigurationLoader.Load(out _, out _);
            Require(active.feeds.Length == 4 && active.feeds[0].host == baseline.feeds[0].host,
                "active configuration round trips");
            var savedA = FeedSchemeStore.SaveOrUpdate("Verification A", baseline);
            var baselineHost = baseline.feeds[0].host;
            baseline.feeds[0].host = "verification-input-mutated";
            savedA.configuration.feeds[0].host = "verification-return-mutated";
            var loaded = FeedSchemeStore.Load(out _);
            Require(loaded.schemes.Length == 1 && loaded.schemes[0].configuration.feeds[0].host == baselineHost,
                "save copies caller and returned configurations");

            var configurationB = CreateConfiguration();
            configurationB.feeds[1].host = "192.168.199.203";
            configurationB.canvasDisplayIndex = 2;
            configurationB.feeds[2].rotationDegrees = 90;
            var savedB = FeedSchemeStore.SaveOrUpdate("Verification B", configurationB);
            var replacementA = CreateConfiguration();
            replacementA.feeds[0].rotationDegrees = 180;
            var overwritten = FeedSchemeStore.SaveOrUpdate("  verification a  ", replacementA);
            Require(overwritten.id == savedA.id, "same name overwrite preserves scheme identity");
            loaded = FeedSchemeStore.Load(out _);
            Require(loaded.schemes.Length == 2, "same name overwrite does not add a third scheme");
            Require(FeedSchemeStore.TryGet(loaded, savedA.id, out var copyOne) && copyOne.feeds[0].rotationDegrees == 180,
                "scheme overwrite survives reload");
            Require(FeedSchemeStore.TryGet(loaded, savedB.id, out var copyB) && copyB.canvasDisplayIndex == 2 &&
                    copyB.feeds[2].rotationDegrees == 90, "second scheme retains canvas route and rotation");
            Require(FeedSchemeStore.TryGet(loaded, savedA.id, out var copyTwo), "scheme retrieval succeeds twice");
            copyOne.feeds[0].host = "verification-copy-mutated";
            Require(copyTwo.feeds[0].host == baselineHost && loaded.schemes[0].configuration.feeds[0].host == baselineHost,
                "retrieved schemes are deep copies");
            var cloned = FeedSchemeStore.CloneConfiguration(copyTwo);
            cloned.feeds[0].rotationDegrees = 270;
            Require(copyTwo.feeds[0].rotationDegrees == 180, "explicit clone separates camera entries");
            RequireNoSecretFields(File.ReadAllText(FeedConfigurationLoader.ExternalConfigPath));
            RequireNoSecretFields(File.ReadAllText(FeedSchemeStore.ExternalConfigPath));

            var validBytes = File.ReadAllBytes(FeedSchemeStore.ExternalConfigPath);
            foreach (var corrupted in new[]
                     { "{", "{\"configVersion\":1,\"schemes\":[{\"id\":\"invalid\",\"name\":\"invalid\",\"configuration\":null}]}",
                       "{\"configVersion\":1,\"schemes\":[{\"id\":\"incomplete\",\"name\":\"incomplete\",\"configuration\":{\"feeds\":[]}}]}" })
            {
                File.WriteAllText(FeedSchemeStore.ExternalConfigPath, corrupted);
                ExpectInvalidData(() => FeedSchemeStore.Load(out _), "corrupt scheme cannot be loaded");
                ExpectInvalidData(() => FeedSchemeStore.SaveOrUpdate("new entry", replacementA),
                    "corrupt scheme cannot be replaced by save or update");
                ExpectInvalidData(() => FeedSchemeStore.Save(loaded), "corrupt scheme cannot be overwritten by an old list");
                ExpectInvalidData(() => FeedSchemeStore.Delete(savedA.id), "delete preserves a corrupt scheme file");
                Require(File.ReadAllText(FeedSchemeStore.ExternalConfigPath) == corrupted,
                    "rejected scheme operations preserve corrupt file bytes");
                File.WriteAllBytes(FeedSchemeStore.ExternalConfigPath, validBytes);
            }
            Require(FeedSchemeStore.Delete(savedA.id), "deleting an existing scheme succeeds");
            var afterDelete = FeedSchemeStore.Load(out _);
            Require(afterDelete.schemes.Length == 1 && afterDelete.schemes[0].id == savedB.id &&
                    afterDelete.schemes[0].configuration.feeds[1].host == "192.168.199.203",
                "deletion survives reload and preserves the other scheme");
            var remainingBytes = File.ReadAllText(FeedSchemeStore.ExternalConfigPath);
            Require(!FeedSchemeStore.Delete(savedA.id) &&
                    File.ReadAllText(FeedSchemeStore.ExternalConfigPath) == remainingBytes,
                "deleting a missing scheme does not change the file");
            Require(FeedSchemeStore.Delete(savedB.id) && FeedSchemeStore.Load(out _).schemes.Length == 0,
                "the last scheme can be deleted to an empty valid list");
            File.WriteAllBytes(FeedSchemeStore.ExternalConfigPath, validBytes);
            return FeedSchemeStore.Load(out _);
        }

        private static void VerifyRuntime(Scene scene, FeedSchemeDocument schemes, FakeDecoderFactory factory,
            List<FeedSurfacePresenter> presenters, List<CameraFeedController> controllers, List<Material> materials)
        {
            var canvasObject = NewObject(scene, "Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            for (var i = 0; i < 4; i++)
            {
                var surfaceObject = NewObject(scene, $"VerificationSurface{i + 1}");
                var renderer = surfaceObject.AddComponent<MeshRenderer>();
                var presenter = surfaceObject.AddComponent<FeedSurfacePresenter>();
                presenters.Add(presenter);
                presenter.Configure(renderer, null, null, i + 1, Color.black);
                if (GetPrivate<Material>(presenter, "instanceMaterial") == null) InvokePrivate(presenter, "Awake");
                var material = GetPrivate<Material>(presenter, "instanceMaterial");
                materials.Add(material);
                Require(material != null, "presenter creates an independent material");
                Require(material.mainTexture != null, "presenter loads default fallback artwork");
                var controller = NewObject(scene, $"VerificationController{i + 1}").AddComponent<CameraFeedController>();
                controller.Configure($"feed{i + 1}", i, presenter);
                controllers.Add(controller);
            }
            var bootstrap = NewObject(scene, "VerificationBootstrap").AddComponent<FourDisplayBootstrap>();
            bootstrap.Configure(controllers.ToArray());
            SetPrivate(bootstrap, "credentialProvider", new FakeCredentials());
            SetPrivate(bootstrap, "decoderFactory", factory);
            SetPrivate(bootstrap, "routedCanvas", canvas);

            Require(FeedSchemeStore.TryGet(schemes, schemes.schemes[0].id, out var configurationA),
                "runtime first scheme is available");
            Require(bootstrap.ApplyConfiguration(configurationA), "bootstrap accepts first scheme");
            Require(factory.Decoders.Count == 4, "initial configuration creates exactly four decoders");
            var original = factory.Decoders.ToArray();
            for (var i = 0; i < 4; i++)
            {
                Require(original[i].ConnectCount == 1 && original[i].ConnectedHost == configurationA.feeds[i].host,
                    "controller connects its own scheme address once");
                Require(materials[i].mainTexture == original[i].CurrentTexture, "controller displays its own decoder texture");
                for (var j = 0; j < i; j++)
                    Require(materials[i] != materials[j] && materials[i].mainTexture != materials[j].mainTexture,
                        "feeds use separate materials and textures");
            }

            Require(FeedSchemeStore.TryGet(schemes, schemes.schemes[1].id, out var configurationB),
                "runtime second scheme is available");
            Require(bootstrap.ApplyConfiguration(configurationB, reconnectChangedOnly: true), "bootstrap accepts second scheme");
            Require(factory.Decoders.Count == 5 && original[1].Disposed, "switching schemes replaces only changed feed");
            Require(original[1].StateSubscribers == 0 && original[1].FaultSubscribers == 0,
                "replaced decoder has no stale controller event handlers");
            for (var i = 0; i < 4; i++)
            {
                if (i == 1) continue;
                Require(!original[i].Disposed && original[i].ConnectCount == 1 &&
                        GetPrivate<IVideoStreamDecoder>(controllers[i], "decoder") == original[i],
                    "unchanged scheme feed preserves decoder and connection");
            }
            var changed = factory.Decoders[4];
            Require(changed.ConnectCount == 1 && changed.ConnectedHost == configurationB.feeds[1].host &&
                    materials[1].mainTexture == changed.CurrentTexture, "changed feed presents new decoder texture");
            Require(canvas.enabled && canvas.gameObject.activeSelf && canvas.targetDisplay == 2,
                "scheme routes canvas to selected display");

            var rotationOnly = FeedSchemeStore.CloneConfiguration(configurationB);
            rotationOnly.feeds[0].rotationDegrees = 270;
            Require(bootstrap.ApplyConfiguration(rotationOnly, reconnectChangedOnly: true), "rotation-only scheme applies");
            Require(factory.Decoders.Count == 5 && original[0].ConnectCount == 1 && !original[0].Disposed,
                "rotation-only scheme never reconnects");
            Require(presenters[0].RotationDegrees == 270, "rotation-only scheme updates visible angle");
            Require(bootstrap.SetFeedRotation(0, 90) && presenters[0].RotationDegrees == 90,
                "bootstrap exposes immediate per-display rotation");
            Require(!bootstrap.SetFeedRotation(4, 90), "unknown display rotation is rejected");
            VerifyRotation(presenters[0], controllers[0], materials[0], original[0], factory);
            VerifyCanvas(scene, bootstrap, canvas);
            VerifyUI(scene, bootstrap, canvas, factory, presenters, controllers, materials);
        }

        private static void VerifyUI(Scene scene, FourDisplayBootstrap bootstrap, Canvas canvas,
            FakeDecoderFactory factory, List<FeedSurfacePresenter> presenters,
            List<CameraFeedController> controllers, List<Material> materials)
        {
            const string credentialKey = "site-cameras-20261006";
            var provider = new WindowsCredentialManagerProvider();
            Require(provider.TryGet(credentialKey, out var originalCredential) &&
                    !string.IsNullOrWhiteSpace(originalCredential.Username),
                "UI verification has a pre-existing site credential to read");
            var baseline = CreateConfiguration();
            foreach (var entry in baseline.feeds) entry.credentialKey = credentialKey;
            baseline.canvasDisplayIndex = 1;
            FeedConfigurationLoader.Save(baseline);
            Require(bootstrap.ApplyConfiguration(baseline), "UI verification establishes fake controller baseline");
            var ui = NewObject(scene, "VerificationConfigurationUI").AddComponent<FeedConfigurationPanelUI>();
            ui.Configure(bootstrap);
            InvokePrivate(ui, "LoadFormsFromCurrentConfig");

            var draftHosts = new[] { "192.168.199.215", "192.168.199.212", "192.168.199.214", "192.168.199.216" };
            var draftRotations = new[] { 270, 90, 180, 0 };
            for (var i = 0; i < 4; i++)
            {
                Require(GetForm<string>(ui, i, "username") == originalCredential.Username &&
                        GetForm<string>(ui, i, "password") == string.Empty &&
                        GetForm<string>(ui, i, "credentialKey") == credentialKey,
                    "UI forms reuse existing credentials without a password or username edit");
                SetForm(ui, i, "host", draftHosts[i]);
                SetForm(ui, i, "rotationDegrees", draftRotations[i]);
            }
            SetForm(ui, 0, "rtspPort", "8554");
            SetForm(ui, 0, "channel", "2");
            SetForm(ui, 0, "isSubStream", true);
            SetPrivate(ui, "schemeName", "Verification UI Draft A");
            var schemeCountBefore = FeedSchemeStore.Load(out _).schemes.Length;
            var decoderCountBefore = factory.Decoders.Count;
            InvokePrivate(ui, "SaveScheme");
            var buttonSchemes = GetPrivate<FeedSchemeDocument>(ui, "schemes");
            Require(buttonSchemes.schemes.Length == schemeCountBefore + 1,
                "UI save adds a scheme to the list used by selection buttons");
            var schemeA = Array.Find(buttonSchemes.schemes, candidate => candidate.name == "Verification UI Draft A");
            Require(schemeA != null && schemeA.configuration.feeds.Length == 4,
                "UI save stores a named four-display draft");
            Require(schemeA.configuration.canvasDisplayIndex == 1, "UI save includes the draft canvas selection");
            for (var i = 0; i < 4; i++)
            {
                var entry = schemeA.configuration.feeds[i];
                Require(entry.displayIndex == i && entry.host == draftHosts[i] &&
                        entry.rotationDegrees == draftRotations[i] && entry.credentialKey == credentialKey,
                    "UI save preserves each draft camera and angle");
            }
            Require(schemeA.configuration.feeds[0].rtspPort == 8554 &&
                    schemeA.configuration.feeds[0].channel == "2" &&
                    schemeA.configuration.feeds[0].streamType == "Sub", "UI save preserves draft stream settings");
            Require(factory.Decoders.Count == decoderCountBefore,
                "UI saving a draft never reconnects current cameras");
            Require(FeedConfigurationLoader.Load(out _, out _).feeds[0].host == baseline.feeds[0].host,
                "UI saving a draft does not replace active configuration");

            InvokePrivate(ui, "ApplyScheme", schemeA.id);
            var active = FeedConfigurationLoader.Load(out _, out _);
            Require(active.activeSchemeId == schemeA.id && GetPrivate<string>(ui, "activeSchemeId") == schemeA.id,
                "UI scheme selection persists active identity and updates selection state");
            Require(GetPrivate<string>(ui, "schemeName") == schemeA.name,
                "UI scheme selection restores the saved scheme name");
            for (var i = 0; i < 4; i++)
                Require(GetForm<string>(ui, i, "host") == draftHosts[i] &&
                        GetForm<int>(ui, i, "rotationDegrees") == draftRotations[i],
                    "UI scheme selection repopulates all four forms");
            Require(canvas.enabled && canvas.gameObject.activeSelf && canvas.targetDisplay == 1,
                "UI scheme selection applies its canvas route");

            SetForm(ui, 2, "host", "192.168.199.213");
            SetPrivate(ui, "schemeName", "Verification UI Draft B");
            decoderCountBefore = factory.Decoders.Count;
            InvokePrivate(ui, "SaveScheme");
            buttonSchemes = GetPrivate<FeedSchemeDocument>(ui, "schemes");
            var schemeB = Array.Find(buttonSchemes.schemes, candidate => candidate.name == "Verification UI Draft B");
            Require(schemeB != null && buttonSchemes.schemes.Length == schemeCountBefore + 2,
                "UI saves a second selectable four-display scheme");
            Require(factory.Decoders.Count == decoderCountBefore, "UI second draft save preserves current decoders");
            var liveDecoders = new FakeDecoder[4];
            for (var i = 0; i < 4; i++) liveDecoders[i] = GetPrivate<FakeDecoder>(controllers[i], "decoder");
            InvokePrivate(ui, "ApplyScheme", schemeB.id);
            active = FeedConfigurationLoader.Load(out _, out _);
            Require(factory.Decoders.Count == decoderCountBefore + 1 && liveDecoders[2].Disposed,
                "UI switching schemes reconnects only the changed camera");
            Require(active.activeSchemeId == schemeB.id && GetPrivate<string>(ui, "activeSchemeId") == schemeB.id &&
                    active.feeds[2].host == "192.168.199.213", "UI changed scheme identity and source persist");
            for (var i = 0; i < 4; i++)
            {
                if (i == 2) continue;
                Require(!liveDecoders[i].Disposed &&
                        GetPrivate<IVideoStreamDecoder>(controllers[i], "decoder") == liveDecoders[i] &&
                        materials[i].mainTexture == liveDecoders[i].CurrentTexture,
                    "UI scheme switch preserves each unchanged live feed");
            }
            var newThirdDecoder = GetPrivate<FakeDecoder>(controllers[2], "decoder");
            Require(newThirdDecoder != liveDecoders[2] && newThirdDecoder.ConnectedHost == "192.168.199.213" &&
                    materials[2].mainTexture == newThirdDecoder.CurrentTexture,
                "UI changed feed presents its new decoder texture");

            decoderCountBefore = factory.Decoders.Count;
            InvokePrivate(ui, "RotateFeed", 1, 90);
            active = FeedConfigurationLoader.Load(out _, out _);
            Require(active.feeds[1].rotationDegrees == 180 && presenters[1].RotationDegrees == 180 &&
                    GetForm<int>(ui, 1, "rotationDegrees") == 180, "UI right rotation persists and applies immediately");
            Require(active.activeSchemeId == string.Empty && GetPrivate<string>(ui, "activeSchemeId") == string.Empty,
                "UI rotation clears active saved scheme identity");
            Require(factory.Decoders.Count == decoderCountBefore &&
                    GetPrivate<IVideoStreamDecoder>(controllers[1], "decoder") == liveDecoders[1] &&
                    !liveDecoders[1].Disposed, "UI rotation never creates or replaces a decoder");
            for (var i = 0; i < 4; i++)
                if (i != 1) Require(active.feeds[i].rotationDegrees == draftRotations[i],
                    "UI rotation leaves other display angles unchanged");
            var renderer = GetPrivate<MeshRenderer>(presenters[1], "videoSurfaceRenderer");
            Require(Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0f, 0f, -180f)) < 0.01f,
                "UI rotation changes the corresponding visible transform");
            liveDecoders[1].EmitState(FeedConnectionState.Faulted);
            InvokePrivate(ui, "RotateFeed", 1, -90);
            active = FeedConfigurationLoader.Load(out _, out _);
            Require(active.feeds[1].rotationDegrees == 90 && presenters[1].RotationDegrees == 90,
                "UI left rotation persists during feed failure");
            Require(materials[1].mainTexture == GetStatic<Texture2D>("fallbackTexture") &&
                    Quaternion.Angle(renderer.transform.localRotation, Quaternion.identity) < 0.01f &&
                    materials[1].mainTextureScale == Vector2.one && materials[1].mainTextureOffset == Vector2.zero,
                "UI rotation leaves failure artwork upright with normal UVs");
            Require(factory.Decoders.Count == decoderCountBefore, "UI failure rotation still does not reconnect");
            liveDecoders[1].EmitState(FeedConnectionState.Playing);
            Require(materials[1].mainTexture == liveDecoders[1].CurrentTexture &&
                    Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0f, 0f, -90f)) < 0.01f,
                "UI requested angle appears when video recovers");

            InvokePrivate(ui, "ApplyScheme", schemeB.id);
            var currentBeforeDelete = FeedConfigurationLoader.Load(out _, out _);
            decoderCountBefore = factory.Decoders.Count;
            InvokePrivate(ui, "DeleteScheme", schemeA.id);
            Require(!FeedSchemeStore.TryGet(FeedSchemeStore.Load(out _), schemeA.id, out _) &&
                    FeedConfigurationLoader.Load(out _, out _).activeSchemeId == schemeB.id,
                "UI deletion removes only the requested inactive scheme");
            InvokePrivate(ui, "DeleteScheme", schemeB.id);
            var currentAfterDelete = FeedConfigurationLoader.Load(out _, out _);
            currentBeforeDelete.activeSchemeId = string.Empty;
            Require(currentAfterDelete.activeSchemeId == string.Empty &&
                    GetPrivate<string>(ui, "activeSchemeId") == string.Empty &&
                    GetPrivate<string>(ui, "schemeName") == string.Empty,
                "UI deletion clears active scheme markers and the deleted name");
            Require(JsonUtility.ToJson(currentAfterDelete) == JsonUtility.ToJson(currentBeforeDelete) &&
                    factory.Decoders.Count == decoderCountBefore && canvas.targetDisplay == 1,
                "deleting the active scheme preserves current playback configuration without reconnecting");
            var remainingSchemes = GetPrivate<FeedSchemeDocument>(ui, "schemes");
            Require(Array.Find(remainingSchemes.schemes, candidate => candidate.id == schemeA.id || candidate.id == schemeB.id) == null,
                "UI deletion removes the corresponding selection buttons");

            Require(provider.TryGet(credentialKey, out var finalCredential) &&
                    finalCredential.Username == originalCredential.Username &&
                    finalCredential.Password == originalCredential.Password,
                "UI verification preserves the existing site credential");
            Require(GetPrivate<List<Texture2D>>(ui, "generatedTextures").Count == 0,
                "UI verification does not allocate IMGUI styling textures in EditMode");
            RequireNoSecretFields(File.ReadAllText(FeedConfigurationLoader.ExternalConfigPath));
            RequireNoSecretFields(File.ReadAllText(FeedSchemeStore.ExternalConfigPath));
        }

        private static void VerifyRotation(FeedSurfacePresenter presenter, CameraFeedController controller,
            Material material, FakeDecoder decoder, FakeDecoderFactory factory)
        {
            var renderer = GetPrivate<MeshRenderer>(presenter, "videoSurfaceRenderer");
            var decoderCount = factory.Decoders.Count;
            var connectCount = decoder.ConnectCount;
            foreach (var frame in new[]
                     { new DecoderFrameInfo(1920, 1080), new DecoderFrameInfo(1080, 1920), new DecoderFrameInfo(1024, 1024) })
            {
                foreach (var degrees in new[] { 0, 90, 180, 270 })
                {
                    presenter.ApplyState(FeedConnectionState.Playing, decoder.CurrentTexture, frame);
                    controller.SetRotationDegrees(degrees);
                    Require(Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0f, 0f, -degrees)) < 0.01f,
                        "video rotates clockwise around z axis");
                    Require(material.mainTextureScale == new Vector2(1f, -1f) &&
                            material.mainTextureOffset == new Vector2(0f, 1f), "video retains vertical UV correction");
                    VerifyFit(renderer.transform, frame.Width, frame.Height, degrees);
                    Require(factory.Decoders.Count == decoderCount && decoder.ConnectCount == connectCount && !decoder.Disposed,
                        "orientation changes preserve decoder connection");
                }
            }
            decoder.EmitState(FeedConnectionState.Faulted);
            Require(material.mainTexture == GetStatic<Texture2D>("fallbackTexture"), "fault switches to default artwork");
            Require(Quaternion.Angle(renderer.transform.localRotation, Quaternion.identity) < 0.01f,
                "fault artwork resets geometry rotation");
            Require(material.mainTextureScale == Vector2.one && material.mainTextureOffset == Vector2.zero,
                "fault artwork uses normal UV orientation");
            VerifyFit(renderer.transform, material.mainTexture.width, material.mainTexture.height, 0);
            controller.SetRotationDegrees(90);
            Require(Quaternion.Angle(renderer.transform.localRotation, Quaternion.identity) < 0.01f,
                "changing angle while faulted leaves artwork upright");
            decoder.EmitState(FeedConnectionState.Playing);
            Require(Quaternion.Angle(renderer.transform.localRotation, Quaternion.Euler(0f, 0f, -90)) < 0.01f &&
                    material.mainTexture == decoder.CurrentTexture, "recovered video restores requested angle");
        }

        private static void VerifyFit(Transform transform, int width, int height, int degrees)
        {
            var right = transform.localRotation * Vector3.right * transform.localScale.x;
            var up = transform.localRotation * Vector3.up * transform.localScale.y;
            var fittedWidth = Mathf.Abs(right.x) + Mathf.Abs(up.x);
            var fittedHeight = Mathf.Abs(right.y) + Mathf.Abs(up.y);
            var expectedAspect = degrees == 90 || degrees == 270 ? (float)height / width : (float)width / height;
            Require(fittedWidth <= 16.001f && fittedHeight <= 9.001f, "rotated frame stays within screen bounds");
            Require(Mathf.Abs(fittedWidth / fittedHeight - expectedAspect) < 0.001f,
                "rotated frame retains original aspect ratio");
            Require(Mathf.Abs(fittedWidth - 16f) < 0.001f || Mathf.Abs(fittedHeight - 9f) < 0.001f,
                "aspect fit fills one screen dimension");
        }

        private static void VerifyCanvas(Scene scene, FourDisplayBootstrap bootstrap, Canvas canvas)
        {
            foreach (var index in new[] { 0, 1, 2, 3 })
            {
                Require(bootstrap.ApplyCanvasDisplay(index) && canvas.enabled && canvas.gameObject.activeSelf &&
                        canvas.targetDisplay == index, "canvas moves to exactly the selected display");
                var count = 0;
                foreach (var root in scene.GetRootGameObjects()) count += root.GetComponentsInChildren<Canvas>(true).Length;
                Require(count == 1, "canvas routing never duplicates canvas objects");
            }
            foreach (var index in new[] { -1, 4 })
                Require(bootstrap.ApplyCanvasDisplay(index) && !canvas.enabled && !canvas.gameObject.activeSelf,
                    "disabled or invalid canvas selection hides canvas");
        }

        private static FeedConfigurationDocument CreateConfiguration()
        {
            var hosts = new[] { "192.168.199.200", "192.168.199.202", "192.168.199.204", "192.168.199.201" };
            var entries = new FeedConfigEntry[4];
            for (var i = 0; i < entries.Length; i++)
                entries[i] = new FeedConfigEntry
                {
                    feedId = $"feed{i + 1}", displayIndex = i, host = hosts[i], rtspPort = 554,
                    channel = "1", streamType = "Main", credentialKey = "verification-reference", enabled = true
                };
            return new FeedConfigurationDocument { feeds = entries, canvasDisplayIndex = -1 };
        }

        private static GameObject NewObject(Scene scene, string name)
        {
            // The temporary scene is never saved. Ordinary scene objects also let the
            // Canvas count assert inspect the same hierarchy as production routing.
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            SceneManager.MoveGameObjectToScene(gameObject, scene);
            return gameObject;
        }

        private static void RequireNoSecretFields(string json)
        {
            foreach (var field in new[] { "password", "username", "token" })
                Require(json.IndexOf("\"" + field + "\"", StringComparison.OrdinalIgnoreCase) < 0,
                    "configuration contains no login credential fields");
        }

        private static void ExpectInvalidData(Action action, string label)
        {
            var rejected = false;
            try { action(); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, label);
        }

        private static void Require(bool condition, string label)
        {
            assertions++;
            if (!condition) throw new InvalidOperationException("HFD verification assertion: " + label);
        }

        private static T GetPrivate<T>(object instance, string field) =>
            (T)instance.GetType().GetField(field, PrivateInstance).GetValue(instance);
        private static void SetPrivate(object instance, string field, object value) =>
            instance.GetType().GetField(field, PrivateInstance).SetValue(instance, value);
        private static T GetStatic<T>(string field) =>
            (T)typeof(FeedSurfacePresenter).GetField(field, PrivateStatic).GetValue(null);
        private static void SetStatic(string field, object value) =>
            typeof(FeedSurfacePresenter).GetField(field, PrivateStatic).SetValue(null, value);
        private static void InvokePrivate(object instance, string method, params object[] arguments) =>
            instance.GetType().GetMethod(method, PrivateInstance).Invoke(instance, arguments);
        private static T GetForm<T>(FeedConfigurationPanelUI ui, int index, string field)
        {
            var form = GetPrivate<Array>(ui, "forms").GetValue(index);
            return (T)form.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).GetValue(form);
        }
        private static void SetForm(FeedConfigurationPanelUI ui, int index, string field, object value)
        {
            var form = GetPrivate<Array>(ui, "forms").GetValue(index);
            form.GetType().GetField(field, BindingFlags.Instance | BindingFlags.Public).SetValue(form, value);
        }

        private sealed class FileBackup
        {
            private readonly string path;
            private readonly byte[] bytes;
            private readonly bool existed;
            public FileBackup(string filePath)
            {
                path = filePath;
                existed = File.Exists(path);
                if (existed) bytes = File.ReadAllBytes(path);
            }
            public void Restore()
            {
                if (existed) File.WriteAllBytes(path, bytes);
                else if (File.Exists(path)) File.Delete(path);
            }
        }

        private sealed class FakeCredentials : ICredentialProvider
        {
            public bool TryGet(string credentialKey, out FeedCredential credential)
            {
                credential = new FeedCredential("verification-user", string.Empty);
                return true;
            }
        }

        private sealed class FakeDecoderFactory : IVideoStreamDecoderFactory
        {
            public readonly List<FakeDecoder> Decoders = new List<FakeDecoder>();
            public IVideoStreamDecoder Create(string feedId)
            {
                var texture = new Texture2D(16, 9) { name = "VerificationTexture", hideFlags = HideFlags.HideAndDontSave };
                var decoder = new FakeDecoder(texture);
                Decoders.Add(decoder);
                return decoder;
            }
            public void ReleaseTextures()
            {
                foreach (var decoder in Decoders)
                    if (decoder.CurrentTexture != null) UnityEngine.Object.DestroyImmediate(decoder.CurrentTexture);
            }
        }

        private sealed class FakeDecoder : IVideoStreamDecoder
        {
            public FeedConnectionState State { get; private set; } = FeedConnectionState.NotConfigured;
            public Texture CurrentTexture { get; }
            public DecoderFrameInfo FrameInfo => new DecoderFrameInfo(1920, 1080);
            public int ConnectCount { get; private set; }
            public string ConnectedHost { get; private set; }
            public bool Disposed { get; private set; }
            public int StateSubscribers => StateChanged?.GetInvocationList().Length ?? 0;
            public int FaultSubscribers => FaultReported?.GetInvocationList().Length ?? 0;
            public event Action<FeedConnectionState> StateChanged;
            public event Action<string> FaultReported;
            public FakeDecoder(Texture texture) => CurrentTexture = texture;
            public void Connect(DecoderConnectRequest request)
            {
                ConnectCount++;
                ConnectedHost = request.Host;
                EmitState(FeedConnectionState.Connecting);
                EmitState(FeedConnectionState.Playing);
            }
            public void EmitState(FeedConnectionState state)
            {
                State = state;
                StateChanged?.Invoke(state);
            }
            public void Disconnect() => EmitState(FeedConnectionState.NotConfigured);
            public void Tick() { }
            public void Dispose() => Disposed = true;
        }
    }
}
