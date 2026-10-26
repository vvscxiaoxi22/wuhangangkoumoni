using System;
using PortTraining.YardCraneSimulator.Controls;

// .NET-only test substitutes for Unity lifecycle/time and physical device sampling.
// Host, router, commands, controller and mechanisms are compiled from production source.
namespace UnityEngine
{
    // DefaultExecutionOrderAttribute is deliberately NOT stubbed here: with the real
    // Unity.InputSystem/UnityEngine.CoreModule DLLs now also referenced (see the .csproj), a
    // locally-defined duplicate with a constructor taking the same (int) argument is a genuine
    // ambiguity error (CS1614) for attribute-application syntax specifically, not just a
    // tolerated duplicate-type warning - the real one from CoreModule is used instead.
    public sealed class DisallowMultipleComponent : Attribute { }
    public class MonoBehaviour
    {
        protected static void Destroy(object value) { }
    }
    public static class Time
    {
        public static double realtimeSinceStartupAsDouble;
        // YardCraneS7InputSource stamps its own received-frame timestamps off this float
        // clock (matching the S7 adapter's own staleness-timeout convention), separate from
        // the double clock the host and router use for input sequencing.
        public static float realtimeSinceStartup;
    }
    public static class Application
    {
        public static bool isFocused = true;
    }
    public static class Debug
    {
        public static void Log(object message, object context) { }
        public static void LogWarning(object message, object context) { }
    }
}

namespace PortTraining.YardCraneSimulator.Controls
{
    public sealed class YardCraneInputProfile
    {
        public static YardCraneInputProfile CreateRuntimeDefault() => new();
    }
    public interface IYardCraneInputSource
    {
        YardCraneInputSnapshot Sample(YardCraneInputProfile profile);
    }
    public sealed class YardCraneKeyboardInputSource : IYardCraneInputSource
    {
        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile) => DeviceProbe.Keyboard;
    }
    public sealed class YardCraneXboxInputSource : IYardCraneInputSource
    {
        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile) =>
            new("XBOX_GAMEPAD", false, 0f, 0f, 0f, false);
    }
}

internal static class DeviceProbe
{
    internal static YardCraneInputSnapshot Keyboard = new("KEYBOARD", true, 0f, 0f, 0f, false);
}
