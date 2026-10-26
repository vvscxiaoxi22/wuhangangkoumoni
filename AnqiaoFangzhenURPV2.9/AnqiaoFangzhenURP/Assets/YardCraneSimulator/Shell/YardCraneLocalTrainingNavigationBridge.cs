using System.Collections;
using PortTraining.YardCraneSimulator.Bootstrap;
using PortTraining.YardCraneSimulator.Controls;
using PortTraining.YardCraneSimulator.Presentation;
using PortTraining.YardCraneSimulator.Training;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PortTraining.YardCraneSimulator.Shell
{
    /// <summary>
    /// Owns navigation for a local student training run without introducing a Runtime -> Shell
    /// assembly dependency. The bridge survives both the direct StudentHome -> operational
    /// scene switch and the legacy Bootstrap path, then releases its persistent state on return.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(14000)]
    public sealed class YardCraneLocalTrainingNavigationBridge : MonoBehaviour
    {
        private static YardCraneLocalTrainingNavigationBridge instance;

        private YardCraneLocalStudentProfile profile;
        private System.Guid sessionId;
        private YardCraneLocalTrainingReturnPage returnPage = YardCraneLocalTrainingReturnPage.StudentHome;
        private YardCraneOperatorHud hud;
        private YardCraneBootstrap bootstrap;
        private bool returning;
        private string pendingReturnNotice = string.Empty;

        public static YardCraneLocalTrainingNavigationBridge Active => instance;
        public bool IsBound => hud != null;

        public static YardCraneLocalTrainingNavigationBridge Begin(YardCraneLocalStudentProfile student)
        {
            var context = YardCraneFallbackSession.CurrentContext;
            if (context == null)
                throw new System.InvalidOperationException("A frozen local training context must exist before creating navigation.");
            return Begin(student, context.SessionId);
        }

        public static YardCraneLocalTrainingNavigationBridge Begin(
            YardCraneLocalStudentProfile student,
            System.Guid expectedSessionId)
        {
            if (student == null) throw new System.ArgumentNullException(nameof(student));
            var context = YardCraneFallbackSession.CurrentContext;
            if (context == null || expectedSessionId == System.Guid.Empty ||
                context.SessionId != expectedSessionId)
            {
                throw new System.InvalidOperationException(
                    "Navigation can only bind to the currently active frozen local training context.");
            }

            if (instance != null && instance.sessionId != System.Guid.Empty &&
                instance.sessionId != expectedSessionId)
            {
                // The registry proves expectedSessionId is the only active context, therefore
                // an instance carrying another id is a deferred-destroy/stale bridge from an
                // earlier run. Retire it before creating the new owner; otherwise one stale
                // object can permanently block every later launch.
                Retire(instance);
            }

            if (instance == null)
            {
                var host = new GameObject("YardCraneLocalTrainingNavigation");
                instance = host.AddComponent<YardCraneLocalTrainingNavigationBridge>();
            }

            instance.profile = student;
            instance.sessionId = expectedSessionId;
            instance.returnPage = context.ReturnPage;
            instance.returning = false;
            instance.pendingReturnNotice = string.Empty;
            instance.TryBindHud();
            return instance;
        }

        /// <summary>
        /// Rolls back a bridge created for a StudentHome launch that failed before Unity
        /// accepted the Bootstrap scene-load request. Never touches a different/newer run.
        /// </summary>
        public static bool TryAbortLaunch(System.Guid expectedSessionId)
        {
            if (instance == null || expectedSessionId == System.Guid.Empty ||
                instance.sessionId != expectedSessionId)
            {
                return false;
            }

            Retire(instance);
            return true;
        }

        /// <summary>
        /// Releases a bridge after StudentHome has been loaded by either this bridge or an
        /// external operational scene. Session and identity cleanup remains owned by the
        /// StudentHome shell so foreign scenes only need to load that scene normally.
        /// </summary>
        public static bool TryReleaseAtStudentHome(System.Guid expectedSessionId)
        {
            if (instance == null || expectedSessionId == System.Guid.Empty ||
                instance.sessionId != expectedSessionId)
            {
                return false;
            }

            Retire(instance);
            return true;
        }

        private static void Retire(YardCraneLocalTrainingNavigationBridge bridge)
        {
            if (bridge == null) return;
            if (instance == bridge) instance = null;
            bridge.returning = true;
            bridge.UnbindHud();
            bridge.enabled = false;
            if (Application.isPlaying) Destroy(bridge.gameObject);
            else DestroyImmediate(bridge.gameObject);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                enabled = false;
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryBindHud();
        }

        private void Update()
        {
            if (returning) return;
            if (TryRecoverFailedBootstrap()) return;
            if (!IsBound) TryBindHud();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (returning) return;
            if (TryRecoverFailedBootstrap()) return;
            TryBindHud();
        }

        private void TryBindHud()
        {
            if (profile == null || returning) return;

            var candidate = Object.FindFirstObjectByType<YardCraneOperatorHud>();
            if (candidate == null || !candidate.IsReady) return;
            // The formal YardCrane scene owns the HUD. Bootstrap is optional: StudentHome now
            // loads the operational scene directly, while older/standalone launch paths may
            // still keep a Bootstrap alive to supply lifecycle authority.
            var owner = YardCraneBootstrap.Active;

            if (hud != candidate)
            {
                UnbindHud();
                hud = candidate;
                bootstrap = owner;
                hud.BindNavigationExecutor(this, HandleBackRequested, HandleExitRequested);
                Debug.Log(
                    $"[YC_LOCAL_TRAINING_NAV_BOUND] sessionId={sessionId:D}, " +
                    $"scene={candidate.gameObject.scene.name}, bootstrap={(owner != null)}",
                    this);
            }

            // The local mode coordinator owns the business-mode label and the task card.
            // Do not fall back to BindLocalFreePractice here: that would overwrite guided
            // learning or assessment presentation just because all three share this bridge.
        }

        private void HandleBackRequested()
        {
            if (returning) return;
            if (!OwnsActiveLocalSession(out var error))
            {
                Debug.LogError($"[YC_LOCAL_TRAINING_BACK_REJECTED] {error}", this);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(YardCraneStudentSideShell.SceneName))
            {
                Debug.LogError(
                    $"[YC_LOCAL_TRAINING_BACK_SCENE_MISSING] '{YardCraneStudentSideShell.SceneName}' is not in Build Settings.",
                    this);
                return;
            }

            returning = true;
            pendingReturnNotice = string.Empty;
            try
            {
                StartCoroutine(ReturnToStudentHome());
            }
            catch (System.Exception exception)
            {
                returning = false;
                Debug.LogError($"[YC_LOCAL_TRAINING_BACK_FAILED] {exception}", this);
            }
        }

        private void HandleExitRequested()
        {
            if (returning) return;
            if (!OwnsActiveLocalSession(out var error))
            {
                Debug.LogError($"[YC_LOCAL_TRAINING_EXIT_REJECTED] {error}", this);
                return;
            }

            returning = true;
            HoldBusinessInput();
            MarkRunTerminated();
            UnbindHud();
            Debug.Log("[YC_LOCAL_TRAINING_EXIT] Local run terminated by the operator.", this);

            // Keep the student/session identity alive through scene teardown so the
            // operational rig can persist its final local record from OnDestroy.
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit(0);
#endif
        }

        private IEnumerator ReturnToStudentHome()
        {
            AsyncOperation loadOperation;
            try
            {
                // Acquire a valid, non-activating scene request before committing handoff or
                // terminal lifecycle state. If this preflight fails, the current HUD,
                // Bootstrap, session and input authority remain intact and Back can be retried.
                loadOperation = SceneManager.LoadSceneAsync(
                    YardCraneStudentSideShell.SceneName, LoadSceneMode.Single);
                if (loadOperation == null)
                    throw new System.InvalidOperationException("Unity returned no StudentHome scene-load operation.");
                loadOperation.allowSceneActivation = false;
            }
            catch (System.Exception exception)
            {
                returning = false;
                Debug.LogError(
                    $"[YC_LOCAL_TRAINING_BACK_FAILED] Could not prepare '{YardCraneStudentSideShell.SceneName}': {exception}",
                    this);
                yield break;
            }

            YardCraneStudentHomeHandoff.Begin(profile, returnPage, pendingReturnNotice);
            HoldBusinessInput();
            MarkRunTerminated();
            var owner = bootstrap;
            UnbindHud();
            if (owner != null)
            {
                owner.gameObject.SetActive(false);
                Destroy(owner.gameObject);
            }

            // Let Unity destroy an optional legacy Bootstrap before activating the home screen.
            // The scene-owned HUD is unloaded by the Single-mode scene switch.
            yield return null;

            loadOperation.allowSceneActivation = true;
            yield return loadOperation;

            // Single-mode unload has completed, so the old rig has already had the chance
            // to persist its result with the still-live identity. The new StudentHome has
            // consumed its separate one-shot profile handoff in Awake.
            var sessionCleared = YardCraneFallbackSession.TryClear(sessionId);
            if (YardCraneTrainingSessionIdentity.SessionId == sessionId)
                YardCraneTrainingSessionIdentity.Clear();
            Debug.Log(
                $"[YC_LOCAL_TRAINING_RETURNED_HOME] studentNumber={profile.AccountId}, " +
                $"scene={YardCraneStudentSideShell.SceneName}, returnPage={returnPage}, " +
                $"sessionCleared={sessionCleared}", this);
            Destroy(gameObject);
        }

        private bool TryRecoverFailedBootstrap()
        {
            var owner = bootstrap != null ? bootstrap : YardCraneBootstrap.Active;
            if (owner == null || !owner.HasStartupFailure ||
                owner.StateMachine.Current != YardCraneSceneState.Failed)
                return false;

            bootstrap = owner;
            if (!OwnsActiveLocalSession(out var error))
            {
                Debug.LogError($"[YC_LOCAL_TRAINING_STARTUP_RECOVERY_REJECTED] {error}", this);
                return false;
            }
            if (!Application.CanStreamedLevelBeLoaded(YardCraneStudentSideShell.SceneName))
            {
                Debug.LogError(
                    $"[YC_LOCAL_TRAINING_STARTUP_RECOVERY_SCENE_MISSING] '{YardCraneStudentSideShell.SceneName}' is not in Build Settings.",
                    this);
                return false;
            }

            returning = true;
            pendingReturnNotice = "训练场景启动失败，请检查场景配置后重试。";
            Debug.LogWarning(
                $"[YC_LOCAL_TRAINING_STARTUP_RECOVERY] sessionId={sessionId:D}, returnPage={returnPage}",
                this);
            StartCoroutine(ReturnToStudentHome());
            return true;
        }

        private void HoldBusinessInput()
        {
            var host = Object.FindFirstObjectByType<YardCraneStandaloneOperationHost>(
                FindObjectsInactive.Include);
            if (host == null) return;
            try { host.SetTrainingInputEnabled(false); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }

        private bool OwnsActiveLocalSession(out string error)
        {
            if (profile == null)
            {
                error = "The bridge has no student profile.";
                return false;
            }

            var context = YardCraneFallbackSession.CurrentContext;
            if (!YardCraneFallbackSession.IsActive || YardCraneFallbackSession.Student == null ||
                context == null)
            {
                error = "No local fallback training session is active.";
                return false;
            }

            if (!ReferenceEquals(YardCraneFallbackSession.Student, profile))
            {
                error = "The active local session belongs to a different student.";
                return false;
            }

            if (sessionId == System.Guid.Empty || context.SessionId != sessionId ||
                !string.Equals(context.Participant.StudentNumber, profile.StudentNumber,
                    System.StringComparison.Ordinal) ||
                !string.Equals(context.Participant.DisplayName, profile.DisplayName,
                    System.StringComparison.Ordinal))
            {
                error = "The active local session id or participant no longer matches this bridge.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private void MarkRunTerminated()
        {
            if (bootstrap == null)
            {
                YardCraneTrainingSessionIdentity.TryMarkTerminated(sessionId);
                return;
            }
            var state = bootstrap.StateMachine.Current;
            if (state == YardCraneSceneState.ExitAllowed)
            {
                YardCraneTrainingSessionIdentity.TryMarkCompleted(sessionId);
                return;
            }
            if (state == YardCraneSceneState.Terminated)
            {
                YardCraneTrainingSessionIdentity.TryMarkTerminated(sessionId);
                return;
            }
            if (state == YardCraneSceneState.Failed)
            {
                YardCraneTrainingSessionIdentity.TryMarkFailed(sessionId);
                return;
            }
            if (!bootstrap.StateMachine.TryTransition(YardCraneSceneState.Terminated, out var error))
            {
                YardCraneTrainingSessionIdentity.TryMarkTerminated(sessionId);
                Debug.LogWarning($"[YC_LOCAL_TRAINING_TERMINATION_STATE_REJECTED] {error}", this);
            }
        }

        private void UnbindHud()
        {
            if (hud != null)
            {
                hud.UnbindNavigationExecutor(this);
            }
            hud = null;
            bootstrap = null;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            UnbindHud();
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }
}
