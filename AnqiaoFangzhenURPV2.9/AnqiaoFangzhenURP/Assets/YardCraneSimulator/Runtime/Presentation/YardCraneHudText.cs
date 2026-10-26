using System.Globalization;
using PortTraining.YardCraneSimulator.Crane;
using PortTraining.YardCraneSimulator.Operations;

namespace PortTraining.YardCraneSimulator.Presentation
{
    public enum HudTone { Neutral, Good, Caution, Danger }
    public readonly struct HudReadout
    {
        public HudReadout(string text, HudTone tone = HudTone.Neutral) { Text = text; Tone = tone; }
        public string Text { get; }
        public HudTone Tone { get; }
    }

    public static class YardCraneHudText
    {
        public static HudReadout Unknown => new HudReadout("未知", HudTone.Caution);
        public static string Number(float value, string format = "F2") =>
            float.IsNaN(value) || float.IsInfinity(value) ? "—" :
            (System.Math.Abs(value) < 0.005f ? 0f : value).ToString(format, CultureInfo.InvariantCulture);

        // mm:ss, or hh:mm:ss past 59:59. Negative/NaN/Infinity means "no running clock" (stale or
        // not yet started), not zero — matches the Number() convention of never faking a reading.
        public static string Elapsed(double seconds)
        {
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) return "--:--";
            var total = (long)System.Math.Floor(seconds);
            var h = total / 3600;
            var m = total % 3600 / 60;
            var s = total % 60;
            return h > 0 ? $"{h:D2}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
        }

        public static HudReadout Grab(YardCraneHudStatus s)
        {
            if (!s.Has(YardCraneHudSignals.Ready)) return Unknown;
            // Unlock request wins: the physical lock stays closed during load transfer.
            if (s.Has(YardCraneHudSignals.Unlocking)) return new HudReadout("解锁中", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.Locking)) return new HudReadout("锁箱中", HudTone.Caution);
            return s.Has(YardCraneHudSignals.Locked)
                ? new HudReadout("已抓箱", HudTone.Good) : new HudReadout("未抓箱");
        }

        public static HudReadout Clamp(WheelClampState state) => state switch
        {
            WheelClampState.Released => new HudReadout("已松开", HudTone.Good),
            WheelClampState.Clamped => new HudReadout("已夹紧"),
            WheelClampState.Transitioning => new HudReadout("动作中", HudTone.Caution),
            WheelClampState.Fault => new HudReadout("故障", HudTone.Danger),
            _ => Unknown
        };

        public static HudReadout Anchor(YardCraneAnchorState state) => state switch
        {
            YardCraneAnchorState.Released => new HudReadout("已释放", HudTone.Good),
            YardCraneAnchorState.Anchored => new HudReadout("已锚定"),
            YardCraneAnchorState.Transitioning => new HudReadout("动作中", HudTone.Caution),
            YardCraneAnchorState.Fault => new HudReadout("故障", HudTone.Danger),
            _ => Unknown
        };

        public static HudReadout Size(YardCraneHudStatus s)
        {
            if (s.SizeState == YardCraneSpreaderSizeMechanismState.Transitioning)
                return new HudReadout("变幅中", HudTone.Caution);
            if (s.SizeState == YardCraneSpreaderSizeMechanismState.Fault)
                return new HudReadout("尺寸故障", HudTone.Danger);
            return s.Size switch
            {
                YardCraneSpreaderSize.Ft20 => new HudReadout("20 尺"),
                YardCraneSpreaderSize.Ft40 => new HudReadout("40 尺"),
                _ => Unknown
            };
        }

        // Always-on "what is currently happening" text for the task capsule - the phase tail of
        // Banner() below, but never blanked out by a danger/caution condition. Pair with Alert().
        public static HudReadout Operation(YardCraneHudStatus s) => new HudReadout(s.Phase switch
        {
            YardCraneOperationPhase.PreTensioning => "钢索预张紧",
            YardCraneOperationPhase.Initializing => "机构准备中",
            YardCraneOperationPhase.WaitingForRun => "等待运行授权",
            YardCraneOperationPhase.AlignedEmpty => "已对位 · 等待锁箱",
            YardCraneOperationPhase.LockPending => "锁箱中",
            YardCraneOperationPhase.UnlockPending => "解锁中",
            YardCraneOperationPhase.LoadTransfer => "载荷交接中",
            YardCraneOperationPhase.LandingConfirmed => "已落稳 · 可请求解锁",
            YardCraneOperationPhase.HoistingLoaded => "带箱起升",
            YardCraneOperationPhase.TrolleyTravelLoaded => "带箱转运",
            YardCraneOperationPhase.MovingEmpty => "空载移动",
            YardCraneOperationPhase.ReadyLoaded => "带箱就绪",
            YardCraneOperationPhase.WheelClampTransition => "夹轮器动作中",
            YardCraneOperationPhase.AnchorTransition => "锚定动作中",
            YardCraneOperationPhase.SpreaderSizeTransition => "吊具尺寸切换中",
            _ => "空载就绪"
        });

        // Priority-ranked hazard/interlock text for the separate alert strip. Returns null when
        // nothing urgent applies - the task capsule (Operation() above) already covers the calm
        // case, so this must never fall back to a "everything is fine" message of its own.
        public static HudReadout? Alert(YardCraneHudStatus s, bool stale)
        {
            if (stale) return new HudReadout("状态数据中断 · 等待恢复", HudTone.Danger);
            if (s.Has(YardCraneHudSignals.SafetyKnown) && !s.Has(YardCraneHudSignals.EmergencyReleased))
                return new HudReadout("急停已按下", HudTone.Danger);
            if (s.Has(YardCraneHudSignals.SafetyKnown) && !s.Has(YardCraneHudSignals.PowerOn))
                return new HudReadout("控制电源已断", HudTone.Danger);
            if (s.Phase == YardCraneOperationPhase.Faulted) return new HudReadout("机构故障 · 请检查", HudTone.Danger);
            if (s.Has(YardCraneHudSignals.Paused)) return new HudReadout("训练已暂停", HudTone.Caution);
            if (s.Phase == YardCraneOperationPhase.SafeHold || s.Has(YardCraneHudSignals.AwaitingReset))
                return new HudReadout("安全保持 · 回中后显式复位", HudTone.Danger);
            if (!s.Has(YardCraneHudSignals.Ready)) return new HudReadout("机构准备中", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.TrolleyRestricted | YardCraneHudSignals.GantryRestricted))
                return new HudReadout("大车 / 小车方向受限", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.TrolleyRestricted)) return new HudReadout("小车方向受限", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.GantryRestricted)) return new HudReadout("大车方向受限", HudTone.Caution);
            return null;
        }

        public static HudReadout Banner(YardCraneHudStatus s, bool stale)
        {
            if (stale) return new HudReadout("状态数据中断 · 等待恢复", HudTone.Danger);
            if (s.Has(YardCraneHudSignals.SafetyKnown) && !s.Has(YardCraneHudSignals.EmergencyReleased))
                return new HudReadout("急停已按下", HudTone.Danger);
            if (s.Has(YardCraneHudSignals.SafetyKnown) && !s.Has(YardCraneHudSignals.PowerOn))
                return new HudReadout("控制电源已断", HudTone.Danger);
            if (s.Phase == YardCraneOperationPhase.Faulted) return new HudReadout("机构故障 · 请检查", HudTone.Danger);
            if (s.Has(YardCraneHudSignals.Paused)) return new HudReadout("训练已暂停", HudTone.Caution);
            if (s.Phase == YardCraneOperationPhase.SafeHold || s.Has(YardCraneHudSignals.AwaitingReset))
                return new HudReadout("安全保持 · 回中后显式复位", HudTone.Danger);
            if (!s.Has(YardCraneHudSignals.Ready)) return new HudReadout("机构准备中", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.TrolleyRestricted | YardCraneHudSignals.GantryRestricted))
                return new HudReadout("大车 / 小车方向受限", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.TrolleyRestricted)) return new HudReadout("小车方向受限", HudTone.Caution);
            if (s.Has(YardCraneHudSignals.GantryRestricted)) return new HudReadout("大车方向受限", HudTone.Caution);
            return new HudReadout(s.Phase switch
            {
                YardCraneOperationPhase.PreTensioning => "钢索预张紧",
                YardCraneOperationPhase.Initializing => "机构准备中",
                YardCraneOperationPhase.WaitingForRun => "等待运行授权",
                YardCraneOperationPhase.AlignedEmpty => "已对位 · 等待锁箱",
                YardCraneOperationPhase.LockPending => "锁箱中",
                YardCraneOperationPhase.UnlockPending => "解锁中",
                YardCraneOperationPhase.LoadTransfer => "载荷交接中",
                YardCraneOperationPhase.LandingConfirmed => "已落稳 · 可请求解锁",
                YardCraneOperationPhase.HoistingLoaded => "带箱起升",
                YardCraneOperationPhase.TrolleyTravelLoaded => "带箱转运",
                YardCraneOperationPhase.MovingEmpty => "空载移动",
                YardCraneOperationPhase.ReadyLoaded => "带箱就绪",
                YardCraneOperationPhase.WheelClampTransition => "夹轮器动作中",
                YardCraneOperationPhase.AnchorTransition => "锚定动作中",
                YardCraneOperationPhase.SpreaderSizeTransition => "吊具尺寸切换中",
                _ => "空载就绪"
            });
        }
    }
}
