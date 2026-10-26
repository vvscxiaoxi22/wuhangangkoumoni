using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using PortTraining.YardCraneSimulator.Shell;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

// Copy this file into Assets/Editor of an isolated Unity 6000.0.42f1 project,
// together with the real shell, shell editor lifecycle hooks and their resources.
// Run with -batchmode -executeMethod LifecycleVerification.Run (without -quit).
// The fixture creates only the isolated project's Assets/StudentEntry.unity.
[InitializeOnLoad]
public static class LifecycleVerification
{
    private const string Prefix = "YC_SHELL_LIFECYCLE_";
    private const string ScenePath = "Assets/StudentEntry.unity";
    private const string UpperResource = "YardCraneShell/Display1UI/StudentShellUpperPanel";
    private const string LowerResource = "YardCraneShell/Display2UI/StudentShellLowerPanel";
    private const string UpperTreeResource = "YardCraneShell/Display1UI/StudentShellUpper";
    private const string LowerTreeResource = "YardCraneShell/Display2UI/StudentShellLower";
    private static string ResultPath => Path.GetFullPath("Logs/StudentShellLifecycle.result.txt");
    private static bool Running => SessionState.GetBool(Prefix + "running", false);
    private static int Stage => SessionState.GetInt(Prefix + "stage", 0);
    private static int Cycle => SessionState.GetInt(Prefix + "cycle", 0);
    private static YardCraneStudentShell Shell => UnityEngine.Object.FindFirstObjectByType<YardCraneStudentShell>();
    private static UIDocument Upper => GameObject.Find("LifecycleUpper").GetComponent<UIDocument>();
    private static UIDocument Lower => GameObject.Find("LifecycleLower").GetComponent<UIDocument>();

    static LifecycleVerification()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    public static void Run()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ResultPath));
        File.WriteAllText(ResultPath, "Unity shell lifecycle verification\n");
        SessionState.SetBool(Prefix + "running", true);
        SessionState.SetBool(Prefix + "finishing", false);
        SessionState.SetBool(Prefix + "expectLowerError", false);
        SessionState.SetInt(Prefix + "errors", 0);
        SessionState.SetInt(Prefix + "expectedErrors", 0);
        SessionState.SetInt(Prefix + "loginLogs", 0);
        SessionState.SetInt(Prefix + "assertions", 0);
        SessionState.SetInt(Prefix + "cycle", 0);
        SessionState.SetString(Prefix + "firstError", string.Empty);
        SessionState.SetBool(Prefix + "originalOptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
        SessionState.SetInt(Prefix + "originalOptions", (int)EditorSettings.enterPlayModeOptions);
        try
        {
            Require(Application.isBatchMode, "isolated batch process");
            Require(Application.unityVersion == "6000.0.42f1", "exact Unity version", Application.unityVersion);
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "starts in edit mode");
            Record("VERSION " + YardCraneStudentShell.Version);
            RememberAsset("upper", Resources.Load<PanelSettings>(UpperResource));
            RememberAsset("lower", Resources.Load<PanelSettings>(LowerResource));
            RememberAsset("upperTree", Resources.Load<VisualTreeAsset>(UpperTreeResource));
            RememberAsset("lowerTree", Resources.Load<VisualTreeAsset>(LowerTreeResource));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "temporary StudentEntry scene created");
            var upperObject = new GameObject("LifecycleUpper");
            upperObject.SetActive(false);
            var upper = upperObject.AddComponent<UIDocument>();
            upper.visualTreeAsset = Resources.Load<VisualTreeAsset>(UpperTreeResource);
            upper.panelSettings = Resources.Load<PanelSettings>(UpperResource);
            var lowerObject = new GameObject("LifecycleLower");
            lowerObject.SetActive(false);
            var lower = lowerObject.AddComponent<UIDocument>();
            lower.visualTreeAsset = Resources.Load<VisualTreeAsset>(LowerTreeResource);
            lower.panelSettings = Resources.Load<PanelSettings>(LowerResource);
            var shell = upperObject.AddComponent<YardCraneStudentShell>();
            SetLowerDocument(shell, lower);
            var camera = new GameObject("LifecycleBackgroundCamera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.cullingMask = 0;
            camera.targetDisplay = 0;
            lowerObject.SetActive(true);
            upperObject.SetActive(true);
            Next(10);
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (!Running) return;
        if (message.Contains("[YC_ENTRY_LOCAL_LOGIN]"))
            SessionState.SetInt(Prefix + "loginLogs", SessionState.GetInt(Prefix + "loginLogs", 0) + 1);
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        var expected = SessionState.GetBool(Prefix + "expectLowerError", false) &&
                       message.Contains("YC_STUDENT_SHELL_") && message.Contains("Lower");
        var counter = expected ? "expectedErrors" : "errors";
        SessionState.SetInt(Prefix + counter, SessionState.GetInt(Prefix + counter, 0) + 1);
        Record((expected ? "EXPECTED_ERROR " : "UNEXPECTED_ERROR ") + message + "\n" + stackTrace);
        if (!expected && string.IsNullOrEmpty(SessionState.GetString(Prefix + "firstError", "")))
            SessionState.SetString(Prefix + "firstError", message);
    }

    private static void Tick()
    {
        if (!Running) return;
        if (SessionState.GetBool(Prefix + "finishing", false))
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) CompleteExit();
            else if (DateTime.UtcNow.Ticks > long.Parse(SessionState.GetString(Prefix + "finishDeadline", "0"), CultureInfo.InvariantCulture))
            {
                SessionState.SetBool(Prefix + "passed", false);
                Record("FAIL Timed out restoring Edit mode during teardown.");
                CompleteExit();
            }
            return;
        }
        try
        {
            var deadline = long.Parse(SessionState.GetString(Prefix + "deadline", "0"), CultureInfo.InvariantCulture);
            if (DateTime.UtcNow.Ticks > deadline) throw new TimeoutException("stage=" + Stage + ", cycle=" + Cycle);
            if (SessionState.GetInt(Prefix + "errors", 0) > 0)
                throw new InvalidOperationException("Unexpected Unity error: " + SessionState.GetString(Prefix + "firstError", ""));
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.QueuePlayerLoopUpdate();
            var notBefore = long.Parse(SessionState.GetString(Prefix + "notBefore", "0"), CultureInfo.InvariantCulture);
            if (DateTime.UtcNow.Ticks < notBefore) return;
            var shell = Shell;
            switch (Stage)
            {
                case 10:
                    if (shell == null || !shell.IsReady) return;
                    VerifyReady("initial edit preview");
                    shell.EditorPreview(YardCraneShellRole.Teacher, YardCraneStudentShellPage.Home);
                    Require(shell.Role == YardCraneShellRole.Teacher && shell.Page == YardCraneStudentShellPage.Login,
                        "legacy Home preview normalizes to teacher login");
                    shell.EditorPreview(YardCraneShellRole.Student, YardCraneStudentShellPage.Home);
                    Require(shell.Role == YardCraneShellRole.Student && shell.Page == YardCraneStudentShellPage.Login,
                        "legacy Home preview normalizes to student login");
                    shell.enabled = false;
                    Next(11);
                    break;
                case 11:
                    VerifyRestored("edit disable");
                    shell.enabled = true;
                    Next(12);
                    break;
                case 12:
                    if (!shell.IsReady) return;
                    VerifyReady("edit re-enable");
                    shell.EditorSuspendInterface();
                    VerifyRestored("explicit editor suspend");
                    shell.EditorResumeInterface();
                    Next(13);
                    break;
                case 13:
                    if (!shell.IsReady) return;
                    VerifyReady("editor resume");
                    shell.EditorPreview(YardCraneShellRole.Student, YardCraneStudentShellPage.Login);
                    SaveAndCheckScene("initial save");
                    Next(14);
                    break;
                case 14:
                    if (!shell.IsReady) return;
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    Next(15);
                    break;
                case 15:
                    if (shell == null || !shell.IsReady) return;
                    VerifyReady("saved scene re-opened");
                    StartCycle(1);
                    break;
                case 21: // EnteredPlayMode callback advances this across domain reload.
                case 51: // EnteredEditMode callback advances this across domain reload.
                    break;
                case 30:
                    if (shell == null || !shell.IsReady) return;
                    Require(Application.isPlaying, "real Play mode", "cycle=" + Cycle);
                    VerifyReady("Play entry cycle " + Cycle);
                    Require(shell.Page == YardCraneStudentShellPage.Login, "Play starts at login", "cycle=" + Cycle);
                    SetRenderTarget(shell);
                    SessionState.SetInt(Prefix + "cycleLoginStart", SessionState.GetInt(Prefix + "loginLogs", 0));
                    Next(31, 0.8);
                    break;
                case 31:
                    Click("role-teacher");
                    Next(32);
                    break;
                case 32:
                    Require(shell.Role == YardCraneShellRole.Teacher, "pointer selects teacher", "cycle=" + Cycle);
                    Click("role-student");
                    Next(33);
                    break;
                case 33:
                    Require(shell.Role == YardCraneShellRole.Student, "pointer selects student", "cycle=" + Cycle);
                    shell.VisualRoot.Q<TextField>("student-number").value = "X";
                    shell.VisualRoot.Q<TextField>("display-name").value = "Lifecycle Test";
                    Click("login-button");
                    Next(34);
                    break;
                case 34:
                    Require(shell.Page == YardCraneStudentShellPage.Login,
                        "invalid pointer login remains on entry", "cycle=" + Cycle);
                    Require(shell.VisualRoot.Q<Label>("login-error").resolvedStyle.display == DisplayStyle.Flex,
                        "invalid pointer login exposes validation feedback", "cycle=" + Cycle);
                    Require(SessionState.GetInt(Prefix + "loginLogs", 0) - SessionState.GetInt(Prefix + "cycleLoginStart", 0) == 0,
                        "invalid pointer login emits no success receipt", "cycle=" + Cycle);
                    DestroyRenderTarget();
                    shell.enabled = false;
                    Next(36);
                    break;
                case 36:
                    VerifyRestored("Play disable cycle " + Cycle);
                    shell.enabled = true;
                    Next(37);
                    break;
                case 37:
                    if (!shell.IsReady) return;
                    VerifyReady("Play re-enable cycle " + Cycle);
                    SetRenderTarget(shell);
                    Next(38, 0.6);
                    break;
                case 38:
                    Click("role-teacher");
                    Next(39);
                    break;
                case 39:
                    Require(shell.Role == YardCraneShellRole.Teacher, "role event survives runtime rebuild", "cycle=" + Cycle);
                    DestroyRenderTarget();
                    Next(51);
                    EditorApplication.ExitPlaymode();
                    break;
                case 60:
                    Require(!Application.isPlaying, "returned to real edit mode", "cycle=" + Cycle);
                    if (shell == null || !shell.IsReady) return;
                    shell.EditorSuspendInterface();
                    VerifyRestored("after Exit Play cycle " + Cycle);
                    shell.EditorResumeInterface();
                    VerifyAssetHashes("after Exit Play cycle " + Cycle);
                    SaveAndCheckScene("after Exit Play cycle " + Cycle);
                    Next(61);
                    break;
                case 61:
                    if (!shell.IsReady) return;
                    if (Cycle < 3) StartCycle(Cycle + 1);
                    else
                    {
                        shell.enabled = false;
                        SetLowerDocument(shell, Upper);
                        SessionState.SetBool(Prefix + "expectLowerError", true);
                        shell.enabled = true;
                        Next(70, 1);
                    }
                    break;
                case 70:
                    Require(!shell.IsReady, "wrong same-document binding never reports ready");
                    shell.EditorRefreshPreviewLayout();
                    shell.EditorRefreshPreviewLayout();
                    Next(71, 0.6);
                    break;
                case 71:
                    Require(SessionState.GetInt(Prefix + "expectedErrors", 0) == 1,
                        "invalid binding reports one actionable error without repeated spam",
                        "count=" + SessionState.GetInt(Prefix + "expectedErrors", 0));
                    shell.enabled = false;
                    SetLowerDocument(shell, Lower);
                    SessionState.SetBool(Prefix + "expectLowerError", false);
                    shell.enabled = true;
                    Next(72);
                    break;
                case 72:
                    if (!shell.IsReady) return;
                    VerifyReady("recovery after correcting lower reference");
                    shell.enabled = false;
                    VerifyRestored("final disabled shell leaves documents usable");
                    AssetDatabase.SaveAssets();
                    VerifyAssetHashes("final assets");
                    Require(SessionState.GetInt(Prefix + "errors", 0) == 0, "no unexpected Unity errors");
                    Finish(true, "Two default reload cycles and one reload-disabled cycle, pointer login/logout, save/reopen and negative-reference recovery passed.");
                    break;
            }
        }
        catch (Exception error) { Finish(false, error.ToString()); }
    }

    private static void StartCycle(int cycle)
    {
        SessionState.SetInt(Prefix + "cycle", cycle);
        EditorSettings.enterPlayModeOptionsEnabled = cycle == 3;
        EditorSettings.enterPlayModeOptions = cycle == 3
            ? EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload
            : EnterPlayModeOptions.None;
        Record("ENTER_PLAY cycle=" + cycle + ", optionsEnabled=" + EditorSettings.enterPlayModeOptionsEnabled +
               ", options=" + EditorSettings.enterPlayModeOptions);
        Next(21);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (!Running) return;
        Record("PLAY_CALLBACK " + state + ", stage=" + Stage + ", cycle=" + Cycle);
        if (SessionState.GetBool(Prefix + "finishing", false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode && Stage == 21) Next(30);
        if (state == PlayModeStateChange.EnteredEditMode && Stage == 51) Next(60);
    }

    private static void VerifyReady(string label)
    {
        var shell = Shell;
        Require(shell != null && shell.IsReady, label + ": shell ready");
        Require(shell.Document == Upper && shell.DocumentLower == Lower, label + ": same two scene components");
        Require(Upper.enabled && Lower.enabled, label + ": documents enabled");
        Require(Upper.panelSettings != null && Lower.panelSettings != null, label + ": both panels alive");
        Require(Upper.panelSettings != Lower.panelSettings, label + ": independent upper/lower panels");
        Require(shell.VisualRoot != null && shell.VisualRoot == Upper.rootVisualElement &&
                shell.VisualRootLower != null && shell.VisualRootLower == Lower.rootVisualElement,
            label + ": live roots match bound roots");
        Require(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Upper.visualTreeAsset)) == Get("upperTreeGuid") &&
                AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(Lower.visualTreeAsset)) == Get("lowerTreeGuid"),
            label + ": original UXML GUIDs preserved");
        Require(shell.VisualRoot.Q<Button>("role-teacher") != null && shell.VisualRoot.Q<Button>("login-button") != null &&
                shell.VisualRootLower.Q<Image>("lower-poster") != null, label + ": real upper/lower UXML loaded");
    }

    private static void VerifyRestored(string label)
    {
        Require(Upper.enabled && Lower.enabled, label + ": shell does not disable owned-by-scene UIDocuments");
        Require(Upper.panelSettings == Resources.Load<PanelSettings>(UpperResource), label + ": upper original asset restored");
        Require(Lower.panelSettings == Resources.Load<PanelSettings>(LowerResource), label + ": lower original asset restored");
        Require(EditorUtility.IsPersistent(Upper.panelSettings) && EditorUtility.IsPersistent(Lower.panelSettings),
            label + ": restored references are persistent assets");
    }

    private static void SetLowerDocument(YardCraneStudentShell shell, UIDocument lower)
    {
        var serialized = new SerializedObject(shell);
        var property = serialized.FindProperty("lowerDocument");
        Require(property != null, "serialized scene lower-document field exists");
        property.objectReferenceValue = lower;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRenderTarget(YardCraneStudentShell shell)
    {
        // Only the shell's transient runtime panel may be changed by this fixture.
        Require(!EditorUtility.IsPersistent(shell.Document.panelSettings), "runtime panel is an isolated copy");
        var texture = new RenderTexture(1920, 1080, 0)
        { name = "LifecyclePointerTarget", hideFlags = HideFlags.HideAndDontSave };
        texture.Create();
        shell.Document.panelSettings.targetTexture = texture;
    }

    private static void DestroyRenderTarget()
    {
        var panel = Upper.panelSettings;
        if (panel == null || EditorUtility.IsPersistent(panel)) return;
        var texture = panel.targetTexture;
        if (texture == null || texture.name != "LifecyclePointerTarget") return;
        panel.targetTexture = null;
        UnityEngine.Object.Destroy(texture);
    }

    private static void Click(string name)
    {
        var shell = Shell;
        var button = shell.VisualRoot.Q<Button>(name);
        Require(button != null && button.enabledInHierarchy && button.panel != null, "pointer target available: " + name);
        var center = button.worldBound.center;
        Require(button.worldBound.width > 1 && button.worldBound.height > 1 && !float.IsNaN(center.x),
            "pointer target has computed layout: " + name, button.worldBound.ToString());
        var hit = button.panel.Pick(center);
        Require(hit == button || (hit != null && button.Contains(hit)), "panel hit-test reaches " + name,
            "hit=" + (hit == null ? "null" : hit.name));
        using (var down = PointerDownEvent.GetPooled(new Event
               { type = EventType.MouseDown, button = 0, mousePosition = center }))
        { down.target = button; button.SendEvent(down); }
        using (var up = PointerUpEvent.GetPooled(new Event
               { type = EventType.MouseUp, button = 0, mousePosition = center }))
        { up.target = button; button.SendEvent(up); }
        Record("POINTER_DOWN_UP " + name + ", cycle=" + Cycle);
    }

    private static void RememberAsset(string name, UnityEngine.Object asset)
    {
        Require(asset != null && EditorUtility.IsPersistent(asset), name + ": real resource asset exists");
        var path = AssetDatabase.GetAssetPath(asset);
        SessionState.SetString(Prefix + name + "Path", path);
        SessionState.SetString(Prefix + name + "Guid", AssetDatabase.AssetPathToGUID(path));
        SessionState.SetString(Prefix + name + "Hash", Hash(path));
        Record("SOURCE " + name + " guid=" + Get(name + "Guid") + " sha256=" + Get(name + "Hash") + " path=" + path);
    }

    private static void VerifyAssetHashes(string label)
    {
        foreach (var name in new[] { "upper", "lower", "upperTree", "lowerTree" })
        {
            var path = Get(name + "Path");
            Require(File.Exists(path) && Hash(path) == Get(name + "Hash"), label + ": unchanged bytes for " + name);
            Require(AssetDatabase.AssetPathToGUID(path) == Get(name + "Guid"), label + ": unchanged GUID for " + name);
        }
    }

    private static void SaveAndCheckScene(string label)
    {
        Require(EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath), label + ": scene saved");
        var yaml = File.ReadAllText(ScenePath);
        var panelLines = yaml.Split('\n').Where(line => line.TrimStart().StartsWith("m_PanelSettings:", StringComparison.Ordinal)).ToArray();
        Require(panelLines.Length == 2, label + ": two serialized UIDocument panel fields", string.Join(" | ", panelLines));
        Require(panelLines.Any(line => line.Contains(Get("upperGuid"))) && panelLines.Any(line => line.Contains(Get("lowerGuid"))),
            label + ": scene serializes both original panel GUIDs instead of transient clones");
    }

    private static string Hash(string path)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "");
    }

    private static string Get(string key) => SessionState.GetString(Prefix + key, string.Empty);

    private static void Next(int stage, double delaySeconds = 0.15)
    {
        SessionState.SetInt(Prefix + "stage", stage);
        SessionState.SetString(Prefix + "deadline", DateTime.UtcNow.AddSeconds(120).Ticks.ToString(CultureInfo.InvariantCulture));
        SessionState.SetString(Prefix + "notBefore", DateTime.UtcNow.AddSeconds(delaySeconds).Ticks.ToString(CultureInfo.InvariantCulture));
    }

    private static void Require(bool condition, string name, string detail = "")
    {
        if (!condition) throw new InvalidOperationException("ASSERT FAILED: " + name + " " + detail);
        SessionState.SetInt(Prefix + "assertions", SessionState.GetInt(Prefix + "assertions", 0) + 1);
        Record("PASS " + name + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail));
    }

    private static void Record(string text) => File.AppendAllText(ResultPath, DateTime.UtcNow.ToString("O") + " " + text + "\n");

    private static void Finish(bool passed, string detail)
    {
        if (SessionState.GetBool(Prefix + "finishing", false)) return;
        SessionState.SetBool(Prefix + "finishing", true);
        SessionState.SetBool(Prefix + "passed", passed);
        SessionState.SetString(Prefix + "finishDeadline", DateTime.UtcNow.AddSeconds(30).Ticks.ToString(CultureInfo.InvariantCulture));
        Record((passed ? "PASS " : "FAIL ") + detail);
        EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Prefix + "originalOptionsEnabled", false);
        EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Prefix + "originalOptions", 0);
        if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode();
        else CompleteExit();
    }

    private static void CompleteExit()
    {
        var passed = SessionState.GetBool(Prefix + "passed", false);
        SessionState.SetBool(Prefix + "running", false);
        Record("FINAL " + (passed ? "PASS" : "FAIL") + " assertions=" + SessionState.GetInt(Prefix + "assertions", 0) +
               " unexpectedErrors=" + SessionState.GetInt(Prefix + "errors", 0) +
               " expectedErrors=" + SessionState.GetInt(Prefix + "expectedErrors", 0) +
               " completedPlayCycles=" + Cycle + " unity=" + Application.unityVersion);
        EditorApplication.Exit(passed ? 0 : 1);
    }
}
