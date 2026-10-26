using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PortTraining.YardCraneSimulator.Controls
{
    internal interface IYardCraneInputSource
    {
        YardCraneInputSnapshot Sample(YardCraneInputProfile profile);
    }

    internal sealed class YardCraneKeyboardInputSource : IYardCraneInputSource
    {
        public const string Id = "KEYBOARD";

        private readonly List<YardCraneOperatorIntent> intents = new(13);

        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return new YardCraneInputSnapshot(Id, false, 0f, 0f, 0f, false);
            }

            var gantry = Axis(keyboard.jKey.isPressed, keyboard.lKey.isPressed);
            var trolley = Axis(
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            var hoist = Axis(
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            var spreaderRotation = Axis(keyboard.leftBracketKey.isPressed, keyboard.rightBracketKey.isPressed);

            intents.Clear();
            if (keyboard.eKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.ContainerLock);
            if (keyboard.qKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.ContainerUnlock);
            if (keyboard.escapeKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.Pause);
            if (keyboard.cKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.WheelClampToggle);
            if (keyboard.f8Key.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.ControlPowerToggle);
            if (keyboard.f9Key.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.EmergencyStopToggle);
            if (keyboard.rKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.FaultReset);
            if (keyboard.xKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.TrolleyAnchorToggle);
            if (keyboard.zKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.GantryAnchorToggle);
            if (keyboard.pKey.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderPumpToggle);
            if (keyboard.digit1Key.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderSizeFt20);
            if (keyboard.digit2Key.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderSizeFt40);
            if (keyboard.digit0Key.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderReturnToZero);

            return new YardCraneInputSnapshot(
                Id,
                true,
                gantry,
                trolley,
                hoist,
                keyboard.spaceKey.isPressed,
                spreaderRotation,
                intents);
        }

        private static float Axis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }

    internal sealed class YardCraneXboxInputSource : IYardCraneInputSource
    {
        public const string Id = "XBOX_GAMEPAD";

        private readonly List<YardCraneOperatorIntent> intents = new(13);

        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile)
        {
            var gamepad = Gamepad.current;
            if (gamepad == null || !gamepad.added)
            {
                return new YardCraneInputSnapshot(Id, false, 0f, 0f, 0f, false);
            }

            var leftStick = gamepad.leftStick.ReadValue();
            var rightStick = gamepad.rightStick.ReadValue();

            intents.Clear();
            if (gamepad.buttonWest.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.ContainerLock);
            if (gamepad.buttonEast.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.ContainerUnlock);
            if (gamepad.startButton.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.Pause);
            if (gamepad.buttonNorth.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.WheelClampToggle);
            if (gamepad.dpad.up.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.ControlPowerToggle);
            if (gamepad.selectButton.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.EmergencyStopToggle);
            if (gamepad.buttonSouth.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.FaultReset);
            if (gamepad.dpad.down.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.TrolleyAnchorToggle);
            if (gamepad.rightShoulder.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.GantryAnchorToggle);
            if (gamepad.leftTrigger.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderPumpToggle);
            if (gamepad.dpad.left.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderSizeFt20);
            if (gamepad.dpad.right.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderSizeFt40);
            if (gamepad.rightStickButton.wasPressedThisFrame) intents.Add(YardCraneOperatorIntent.SpreaderReturnToZero);

            return new YardCraneInputSnapshot(
                Id,
                true,
                profile.ProcessGamepadAxis(leftStick.x),
                profile.ProcessGamepadAxis(rightStick.x),
                profile.ProcessGamepadAxis(-leftStick.y),
                gamepad.leftShoulder.isPressed,
                profile.ProcessGamepadAxis(rightStick.y),
                intents);
        }
    }
}
