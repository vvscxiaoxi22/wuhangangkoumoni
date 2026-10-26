using System;
using System.Net;
using System.Net.Sockets;
using game4automation;
using PortTraining.YardCraneSimulator.SharedNet;
using UnityEngine;

namespace PortTraining.YardCraneSimulator.S7Integration
{
    /// <summary>Pairs one of the 12 momentary discrete signals with the PLCOutputBool that
    /// carries it. An array (not 12 named fields) so the Inspector list can be validated for
    /// "exactly one binding per YardCraneS7MomentarySignal value" instead of relying on the
    /// developer never mixing up 12 near-identical fields.</summary>
    [Serializable]
    public struct YardCraneS7MomentaryBinding
    {
        public YardCraneS7MomentarySignal Signal;
        public PLCOutputBool Point;
    }

    /// <summary>
    /// Owns the guarded S7 connection for the dedicated integration scene and exposes
    /// PLC-issued gantry/trolley servo commands as normalized values in [-1, 1], plus the 12
    /// momentary discrete buttons (edge-detected, accumulated) and the maintained emergency-stop
    /// level (read fresh every frame, never toggled) confirmed with the field team 2026-09-16.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class YardCraneS7IntegrationBridge : MonoBehaviour
    {
        public const string ImplementationVersion = "2026-09-16-yc-s7-integration-v3-discrete-signals";
        private const string PlaceholderAddress = "0.0.0.0";

        [Header("S7 connection (disabled by default)")]
        [SerializeField] private S7Interface s7Interface = null;
        [SerializeField] private bool connectOnPlay = false;

        [Header("PLC -> Unity command signals")]
        [SerializeField] private PLCOutputInt gantryCommandSignal = null;
        [SerializeField] private PLCOutputInt trolleyCommandSignal = null;

        [Header("PLC -> Unity discrete signals")]
        [Tooltip("Exactly one binding per YardCraneS7MomentarySignal value (12 total) - all momentary/edge-triggered per the field team's 2026-09-16 confirmation.")]
        [SerializeField] private YardCraneS7MomentaryBinding[] momentarySignals = Array.Empty<YardCraneS7MomentaryBinding>();
        [Tooltip("Maintained level, not a button press - read directly every frame, never toggled. Fails toward 'engaged' whenever unset/disconnected.")]
        [SerializeField] private PLCOutputBool emergencyStopSignal = null;

        [Header("Servo scaling")]
        [SerializeField, Min(1)] private int commandFullScale = 3000;
        [SerializeField, Min(0)] private int commandDeadZoneRaw;
        [SerializeField, Min(0.05f)] private float communicationTimeoutSeconds = 0.5f;

        [Header("Runtime diagnostics (read only)")]
        [SerializeField] private string runtimeStatus = "未连接（安全归零）";
        [SerializeField] private int gantryCommandRaw;
        [SerializeField] private int trolleyCommandRaw;
        [SerializeField] private float gantryCommandNormalized;
        [SerializeField] private float trolleyCommandNormalized;
        [SerializeField] private bool commandFrameValid;
        [SerializeField] private bool awaitingFreshCycle = true;
        [SerializeField] private int pendingMomentaryMask;
        // Fails safe: until the first real read happens, treat e-stop as engaged, never released.
        [SerializeField] private bool emergencyStopEngaged = true;

        private ulong lastThreadCycle;
        private float lastThreadCycleTime;
        private bool applicationIsQuitting;
        private bool[] previousMomentaryValues = Array.Empty<bool>();

        public int GantryCommandRaw => gantryCommandRaw;
        public int TrolleyCommandRaw => trolleyCommandRaw;
        public float GantryCommandNormalized => gantryCommandNormalized;
        public float TrolleyCommandNormalized => trolleyCommandNormalized;
        public bool CommandFrameValid => commandFrameValid;
        public string RuntimeStatus => runtimeStatus;
        public bool EmergencyStopEngaged => emergencyStopEngaged;

        /// <summary>Returns every momentary signal newly pressed since the last call, then
        /// clears the accumulator - "consume", not "peek". Intended to be called once per
        /// broadcast tick by YardCraneS7CommandWebSocketServer; calling it from more than one
        /// place would silently split presses between callers.</summary>
        public int ConsumeMomentaryPressedMask()
        {
            var mask = pendingMomentaryMask;
            pendingMomentaryMask = 0;
            return mask;
        }

        private void Awake()
        {
            // The vendor component opens the socket immediately from OnEnable. The scene
            // therefore serializes it disabled; this guard also protects accidental edits.
            if (s7Interface != null && s7Interface.enabled)
            {
                try
                {
                    s7Interface.enabled = false;
                    Debug.LogWarning(
                        "[YC_S7_SAFE_START] S7Interface was disabled during Awake; the bridge will only re-enable it after configuration validation.",
                        this);
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[YC_S7_SAFE_START_FAILED] " + exception.GetType().Name + ": " + exception.Message,
                        this);
                }
            }

            ArmFreshCycleGate();
            ClearCommandSignalRuntimeState();
            ResetCommands("未连接（安全归零）");
            Debug.Log(
                "[YC_S7_INTEGRATION_READY] version=" + ImplementationVersion +
                ", connectOnPlay=" + connectOnPlay +
                ", interfaceEnabled=" + (s7Interface != null && s7Interface.enabled),
                this);
        }

        private void Start()
        {
            if (connectOnPlay)
            {
                TryConnect();
            }
        }

        private void Update()
        {
            if (!HasLiveConnection())
            {
                ArmFreshCycleGate();
                ClearCommandSignalRuntimeState();
                ResetCommands("PLC未连接或信号无效（安全归零）");
                ReadDiscreteSignals(linkHealthy: false);
                return;
            }

            ulong currentThreadCycle = s7Interface.ThreadCycleNum;
            if (currentThreadCycle < lastThreadCycle)
            {
                ArmFreshCycleGate();
                ClearCommandSignalRuntimeState();
                ResetCommands("PLC通信已重启，等待首个新周期（安全归零）");
                ReadDiscreteSignals(linkHealthy: false);
                return;
            }

            if (awaitingFreshCycle)
            {
                if (currentThreadCycle <= lastThreadCycle)
                {
                    ResetCommands("等待PLC首个新通信周期（安全归零）");
                    ReadDiscreteSignals(linkHealthy: false);
                    return;
                }

                lastThreadCycle = currentThreadCycle;
                lastThreadCycleTime = Time.unscaledTime;
                awaitingFreshCycle = false;
            }
            else if (currentThreadCycle > lastThreadCycle)
            {
                lastThreadCycle = currentThreadCycle;
                lastThreadCycleTime = Time.unscaledTime;
            }

            if (Time.unscaledTime - lastThreadCycleTime > communicationTimeoutSeconds)
            {
                ArmFreshCycleGate();
                ClearCommandSignalRuntimeState();
                ResetCommands("PLC通信超时（安全归零）");
                ReadDiscreteSignals(linkHealthy: false);
                return;
            }

            gantryCommandRaw = ClampRaw(gantryCommandSignal.Value);
            trolleyCommandRaw = ClampRaw(trolleyCommandSignal.Value);
            gantryCommandNormalized = NormalizeRaw(gantryCommandRaw);
            trolleyCommandNormalized = NormalizeRaw(trolleyCommandRaw);
            commandFrameValid = true;
            runtimeStatus = "PLC命令有效";
            ReadDiscreteSignals(linkHealthy: true);
        }

        /// <summary>
        /// Momentary buttons: edge-detect only while the servo command link itself is healthy
        /// (see linkHealthy) - a reconnect must never manufacture a phantom press out of
        /// whatever undefined signal state existed before the link came up, so the "previous
        /// value" bookkeeping is cleared instead of compared while unhealthy.
        /// Emergency stop: read directly every single frame regardless of linkHealthy, and fail
        /// toward "engaged" the instant the signal itself is missing/disconnected - this is a
        /// maintained safety level, not a button, and must never depend on the servo path being
        /// in any particular state to report the truth.
        /// </summary>
        private void ReadDiscreteSignals(bool linkHealthy)
        {
            if (previousMomentaryValues.Length != momentarySignals.Length)
            {
                previousMomentaryValues = new bool[momentarySignals.Length];
            }

            if (!linkHealthy)
            {
                Array.Clear(previousMomentaryValues, 0, previousMomentaryValues.Length);
            }
            else
            {
                for (var i = 0; i < momentarySignals.Length; i++)
                {
                    var point = momentarySignals[i].Point;
                    if (point == null || !point.Status.Connected)
                    {
                        previousMomentaryValues[i] = false;
                        continue;
                    }

                    var current = point.Value;
                    if (current && !previousMomentaryValues[i])
                    {
                        pendingMomentaryMask |= 1 << (int)momentarySignals[i].Signal;
                    }

                    previousMomentaryValues[i] = current;
                }
            }

            emergencyStopEngaged = emergencyStopSignal == null ||
                                   !emergencyStopSignal.Status.Connected ||
                                   emergencyStopSignal.Value;
        }

        public bool TryConnect()
        {
            NormalizeSignalAddresses();

            if (!ValidateConfiguration(out string reason))
            {
                ResetCommands("配置无效：" + reason);
                Debug.LogError("[YC_S7_CONFIGURATION_INVALID] " + reason, this);
                return false;
            }

            if (HasLiveConnection() && !awaitingFreshCycle)
            {
                runtimeStatus = "PLC已连接";
                return true;
            }

            ArmFreshCycleGate();
            ClearCommandSignalRuntimeState();

            if (!s7Interface.enabled)
            {
                // S7Interface.OnEnable performs ImportSignals, opens the PLC socket and
                // starts its communication thread.
                try
                {
                    s7Interface.enabled = true;
                }
                catch (Exception exception)
                {
                    ResetCommands("S7连接启动失败（安全归零）");
                    Debug.LogError(
                        "[YC_S7_CONNECT_FAILED] " + exception.GetType().Name + ": " + exception.Message,
                        this);

                    if (s7Interface.enabled)
                    {
                        try
                        {
                            s7Interface.enabled = false;
                        }
                        catch (Exception closeException)
                        {
                            Debug.LogError(
                                "[YC_S7_CONNECT_CLEANUP_FAILED] " + closeException.GetType().Name + ": " + closeException.Message,
                                this);
                        }
                    }

                    return false;
                }
            }

            runtimeStatus = "正在连接PLC";
            Debug.Log("[YC_S7_CONNECT_REQUESTED] address=" + s7Interface.Adress, this);
            return true;
        }

        [ContextMenu("S7/连接（先校验配置）")]
        private void ConnectFromContextMenu()
        {
            TryConnect();
        }

        [ContextMenu("S7/断开并归零")]
        public void Disconnect()
        {
            ArmFreshCycleGate();

            if (s7Interface != null && s7Interface.enabled)
            {
                try
                {
                    s7Interface.enabled = false;
                }
                catch (Exception exception)
                {
                    Debug.LogError(
                        "[YC_S7_DISCONNECT_FAILED] " + exception.GetType().Name + ": " + exception.Message,
                        this);
                }
            }

            ClearCommandSignalRuntimeState();
            ResetCommands("已断开（安全归零）");
            // Fail safe immediately rather than waiting for the next Update() tick to notice
            // the interface is down - a caller reading EmergencyStopEngaged in this same frame
            // must never see a stale "released" value survive past an explicit disconnect.
            emergencyStopEngaged = true;
            Array.Clear(previousMomentaryValues, 0, previousMomentaryValues.Length);
        }

        public bool ValidateConfiguration(out string reason)
        {
            if (s7Interface == null)
            {
                reason = "缺少S7Interface引用。";
                return false;
            }

            if (!s7Interface.gameObject.activeInHierarchy)
            {
                reason = "S7Interface对象未处于激活层级。";
                return false;
            }

            string address = (s7Interface.Adress ?? string.Empty).Trim();
            if (address.Length == 0 || string.Equals(address, PlaceholderAddress, StringComparison.Ordinal))
            {
                reason = "PLC IP仍是占位值0.0.0.0。";
                return false;
            }

            if (!IPAddress.TryParse(address, out IPAddress parsedAddress) ||
                parsedAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                reason = "PLC地址必须是有效的IPv4地址。";
                return false;
            }

            if (s7Interface.Rack < 0 || s7Interface.Slot < 0)
            {
                reason = "Rack和Slot不能为负数。";
                return false;
            }

            if (s7Interface.MinCommCycleMs < 10)
            {
                reason = "MinCommCycleMs必须至少为10，禁止通信线程忙循环。";
                return false;
            }

            if (s7Interface.NoThreading)
            {
                reason = "当前S7联调必须启用通信线程。";
                return false;
            }

            if (s7Interface.AreaReadWriteMode)
            {
                reason = "首阶段禁止AreaReadWriteMode整块读写。";
                return false;
            }

            if (!s7Interface.ReverseHighLowBytes)
            {
                reason = "当前INT点位约定要求ReverseHighLowBytes保持启用。";
                return false;
            }

            if (gantryCommandSignal == null || trolleyCommandSignal == null)
            {
                reason = "大小车PLCOutputInt信号引用不完整。";
                return false;
            }

            if (!IsSignedIntOutputAddress(gantryCommandSignal.Name))
            {
                reason = "大车指令地址不是可识别的PLC输出INT绝对地址。";
                return false;
            }

            if (!IsSignedIntOutputAddress(trolleyCommandSignal.Name))
            {
                reason = "小车指令地址不是可识别的PLC输出INT绝对地址。";
                return false;
            }

            if (!IsSignedInt(gantryCommandSignal.OriginDataType) ||
                !IsSignedInt(trolleyCommandSignal.OriginDataType))
            {
                reason = "大小车指令的OriginDataType必须明确填写INT。";
                return false;
            }

            if (string.Equals(gantryCommandSignal.Name, trolleyCommandSignal.Name, StringComparison.Ordinal))
            {
                reason = "大小车指令不能使用同一个PLC地址。";
                return false;
            }

            if (!gantryCommandSignal.transform.IsChildOf(s7Interface.transform) ||
                !trolleyCommandSignal.transform.IsChildOf(s7Interface.transform))
            {
                reason = "所有PLC点位必须位于S7Interface的子层级。";
                return false;
            }

            if (!gantryCommandSignal.gameObject.activeInHierarchy ||
                !trolleyCommandSignal.gameObject.activeInHierarchy ||
                !gantryCommandSignal.Settings.Active ||
                !trolleyCommandSignal.Settings.Active)
            {
                reason = "大小车指令点位必须处于激活状态。";
                return false;
            }

            if (gantryCommandSignal.Settings.Override || trolleyCommandSignal.Settings.Override)
            {
                reason = "大小车PLC指令点位禁止启用Override强制值。";
                return false;
            }

            if (commandDeadZoneRaw >= commandFullScale)
            {
                reason = "指令死区必须小于满量程。";
                return false;
            }

            if (!ValidateMomentarySignals(out reason))
            {
                return false;
            }

            if (!ValidateEmergencyStopSignal(out reason))
            {
                return false;
            }

            reason = string.Empty;
            return true;
        }

        private bool ValidateMomentarySignals(out string reason)
        {
            const int expectedCount = 12; // one per YardCraneS7MomentarySignal value
            if (momentarySignals.Length != expectedCount)
            {
                reason = $"离散按钮点位应恰好{expectedCount}个（每个YardCraneS7MomentarySignal一个），当前{momentarySignals.Length}个。";
                return false;
            }

            var seen = 0;
            foreach (var binding in momentarySignals)
            {
                var bit = 1 << (int)binding.Signal;
                if ((seen & bit) != 0)
                {
                    reason = $"离散按钮 {binding.Signal} 被重复绑定。";
                    return false;
                }

                seen |= bit;

                var point = binding.Point;
                if (point == null)
                {
                    reason = $"离散按钮 {binding.Signal} 缺少PLCOutputBool引用。";
                    return false;
                }

                if (!IsBoolOutputAddress(point.Name))
                {
                    reason = $"离散按钮 {binding.Signal} 地址不是可识别的PLC输出BOOL绝对地址。";
                    return false;
                }

                if (!IsBool(point.OriginDataType))
                {
                    reason = $"离散按钮 {binding.Signal} 的OriginDataType必须明确填写BOOL。";
                    return false;
                }

                if (!point.transform.IsChildOf(s7Interface.transform))
                {
                    reason = $"离散按钮 {binding.Signal} 必须位于S7Interface的子层级。";
                    return false;
                }

                if (!point.gameObject.activeInHierarchy || !point.Settings.Active)
                {
                    reason = $"离散按钮 {binding.Signal} 必须处于激活状态。";
                    return false;
                }

                if (point.Settings.Override)
                {
                    reason = $"离散按钮 {binding.Signal} 禁止启用Override强制值。";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private bool ValidateEmergencyStopSignal(out string reason)
        {
            if (emergencyStopSignal == null)
            {
                reason = "缺少急停PLCOutputBool引用。";
                return false;
            }

            if (!IsBoolOutputAddress(emergencyStopSignal.Name))
            {
                reason = "急停地址不是可识别的PLC输出BOOL绝对地址。";
                return false;
            }

            if (!IsBool(emergencyStopSignal.OriginDataType))
            {
                reason = "急停的OriginDataType必须明确填写BOOL。";
                return false;
            }

            if (!emergencyStopSignal.transform.IsChildOf(s7Interface.transform))
            {
                reason = "急停点位必须位于S7Interface的子层级。";
                return false;
            }

            if (!emergencyStopSignal.gameObject.activeInHierarchy || !emergencyStopSignal.Settings.Active)
            {
                reason = "急停点位必须处于激活状态。";
                return false;
            }

            if (emergencyStopSignal.Settings.Override)
            {
                reason = "急停点位禁止启用Override强制值——这是安全信号，不允许软件伪造。";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public float NormalizeRaw(int raw)
        {
            int limited = ClampRaw(raw);
            if (Mathf.Abs(limited) <= commandDeadZoneRaw)
            {
                return 0f;
            }

            return limited / (float)commandFullScale;
        }

        private bool HasLiveConnection()
        {
            return s7Interface != null &&
                   s7Interface.enabled &&
                   s7Interface.IsConnected &&
                   gantryCommandSignal != null &&
                   trolleyCommandSignal != null &&
                   gantryCommandSignal.Settings.Active &&
                   trolleyCommandSignal.Settings.Active &&
                   !gantryCommandSignal.Settings.Override &&
                   !trolleyCommandSignal.Settings.Override &&
                   gantryCommandSignal.Status.Connected &&
                   trolleyCommandSignal.Status.Connected;
        }

        private int ClampRaw(int raw)
        {
            return Mathf.Clamp(raw, -commandFullScale, commandFullScale);
        }

        private static bool IsSignedInt(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), "INT", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSignedIntOutputAddress(string value)
        {
            string address = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (address.Length == 0 || address.IndexOf('%') >= 0)
            {
                return false;
            }

            if (StartsWithWordAddress(address, "QW") ||
                StartsWithWordAddress(address, "AW") ||
                StartsWithWordAddress(address, "MW"))
            {
                return true;
            }

            if (!address.StartsWith("DB", StringComparison.Ordinal))
            {
                return false;
            }

            int separator = address.IndexOf(".DBW", StringComparison.Ordinal);
            return separator > 2 &&
                   IsDigitsOnly(address, 2, separator) &&
                   IsDigitsOnly(address, separator + 4, address.Length);
        }

        private void NormalizeSignalAddresses()
        {
            if (s7Interface != null)
            {
                s7Interface.Adress = (s7Interface.Adress ?? string.Empty).Trim();
            }

            if (gantryCommandSignal != null)
            {
                gantryCommandSignal.Name = (gantryCommandSignal.Name ?? string.Empty).Trim().ToUpperInvariant();
            }

            if (trolleyCommandSignal != null)
            {
                trolleyCommandSignal.Name = (trolleyCommandSignal.Name ?? string.Empty).Trim().ToUpperInvariant();
            }

            foreach (var binding in momentarySignals)
            {
                if (binding.Point != null)
                {
                    binding.Point.Name = (binding.Point.Name ?? string.Empty).Trim().ToUpperInvariant();
                }
            }

            if (emergencyStopSignal != null)
            {
                emergencyStopSignal.Name = (emergencyStopSignal.Name ?? string.Empty).Trim().ToUpperInvariant();
            }
        }

        private static bool StartsWithWordAddress(string address, string prefix)
        {
            return address.StartsWith(prefix, StringComparison.Ordinal) &&
                   IsDigitsOnly(address, prefix.Length, address.Length);
        }

        private static bool IsBool(string value)
        {
            return string.Equals((value ?? string.Empty).Trim(), "BOOL", StringComparison.OrdinalIgnoreCase);
        }

        // Bit-addressed BOOL points use a different S7 syntax than the word-addressed INT
        // points above: Q/I/M followed by <byte>.<bit>, or DB<n>.DBX<byte>.<bit>.
        private static bool IsBoolOutputAddress(string value)
        {
            string address = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (address.Length == 0 || address.IndexOf('%') >= 0)
            {
                return false;
            }

            if (StartsWithBitAddress(address, "Q") ||
                StartsWithBitAddress(address, "I") ||
                StartsWithBitAddress(address, "M"))
            {
                return true;
            }

            if (!address.StartsWith("DB", StringComparison.Ordinal))
            {
                return false;
            }

            int dbxSeparator = address.IndexOf(".DBX", StringComparison.Ordinal);
            if (dbxSeparator <= 2 || !IsDigitsOnly(address, 2, dbxSeparator))
            {
                return false;
            }

            int byteStart = dbxSeparator + 4;
            int bitSeparator = address.LastIndexOf('.');
            return bitSeparator > byteStart &&
                   IsDigitsOnly(address, byteStart, bitSeparator) &&
                   IsDigitsOnly(address, bitSeparator + 1, address.Length);
        }

        private static bool StartsWithBitAddress(string address, string prefix)
        {
            if (!address.StartsWith(prefix, StringComparison.Ordinal))
            {
                return false;
            }

            int dot = address.LastIndexOf('.');
            return dot > prefix.Length &&
                   IsDigitsOnly(address, prefix.Length, dot) &&
                   IsDigitsOnly(address, dot + 1, address.Length);
        }

        private static bool IsDigitsOnly(string value, int start, int end)
        {
            if (start >= end)
            {
                return false;
            }

            for (int i = start; i < end; i++)
            {
                if (!char.IsDigit(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void ResetCommands(string status)
        {
            gantryCommandRaw = 0;
            trolleyCommandRaw = 0;
            gantryCommandNormalized = 0f;
            trolleyCommandNormalized = 0f;
            commandFrameValid = false;
            runtimeStatus = status;
        }

        private void ArmFreshCycleGate()
        {
            awaitingFreshCycle = true;
            lastThreadCycle = s7Interface != null ? s7Interface.ThreadCycleNum : 0;
            lastThreadCycleTime = Time.unscaledTime;
        }

        private void ClearCommandSignalRuntimeState()
        {
            ClearSignalRuntimeState(gantryCommandSignal);
            ClearSignalRuntimeState(trolleyCommandSignal);
        }

        private static void ClearSignalRuntimeState(PLCOutputInt signal)
        {
            if (signal == null)
            {
                return;
            }

            StatusInt status = signal.Status;
            status.Connected = false;
            status.Value = 0;
            status.OldValue = 0;
            signal.Status = status;
        }

        private void OnApplicationQuit()
        {
            applicationIsQuitting = true;
            Disconnect();
        }

        private void OnDisable()
        {
            if (!applicationIsQuitting)
            {
                Disconnect();
            }
        }

        private void OnValidate()
        {
            commandFullScale = Mathf.Max(1, commandFullScale);
            commandDeadZoneRaw = Mathf.Clamp(commandDeadZoneRaw, 0, commandFullScale - 1);
            communicationTimeoutSeconds = Mathf.Max(0.05f, communicationTimeoutSeconds);
        }
    }
}
