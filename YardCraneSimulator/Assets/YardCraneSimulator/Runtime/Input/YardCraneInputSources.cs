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

        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return new YardCraneInputSnapshot(Id, false, 0f, 0f, 0f, false, false, false, false);
            }

            var gantry = Axis(keyboard.jKey.isPressed, keyboard.lKey.isPressed);
            var trolley = Axis(
                keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed,
                keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed);
            var hoist = Axis(
                keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed,
                keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed);
            var spreaderRotation = Axis(keyboard.leftBracketKey.isPressed, keyboard.rightBracketKey.isPressed);

            return new YardCraneInputSnapshot(
                Id,
                true,
                gantry,
                trolley,
                hoist,
                keyboard.spaceKey.isPressed,
                keyboard.eKey.wasPressedThisFrame,
                keyboard.qKey.wasPressedThisFrame,
                keyboard.escapeKey.wasPressedThisFrame,
                keyboard.cKey.wasPressedThisFrame,
                keyboard.f8Key.wasPressedThisFrame,
                keyboard.f9Key.wasPressedThisFrame,
                keyboard.rKey.wasPressedThisFrame,
                keyboard.xKey.wasPressedThisFrame,
                keyboard.zKey.wasPressedThisFrame,
                keyboard.pKey.wasPressedThisFrame,
                keyboard.digit1Key.wasPressedThisFrame,
                keyboard.digit2Key.wasPressedThisFrame,
                spreaderRotation,
                keyboard.digit0Key.wasPressedThisFrame);
        }

        private static float Axis(bool negative, bool positive)
        {
            return (positive ? 1f : 0f) - (negative ? 1f : 0f);
        }
    }

    internal sealed class YardCraneXboxInputSource : IYardCraneInputSource
    {
        public const string Id = "XBOX_GAMEPAD";

        public YardCraneInputSnapshot Sample(YardCraneInputProfile profile)
        {
            var gamepad = Gamepad.current;
            if (gamepad == null || !gamepad.added)
            {
                return new YardCraneInputSnapshot(Id, false, 0f, 0f, 0f, false, false, false, false);
            }

            var leftStick = gamepad.leftStick.ReadValue();
            var rightStick = gamepad.rightStick.ReadValue();
            return new YardCraneInputSnapshot(
                Id,
                true,
                profile.ProcessGamepadAxis(leftStick.x),
                profile.ProcessGamepadAxis(rightStick.x),
                profile.ProcessGamepadAxis(-leftStick.y),
                gamepad.leftShoulder.isPressed,
                gamepad.buttonWest.wasPressedThisFrame,
                gamepad.buttonEast.wasPressedThisFrame,
                gamepad.startButton.wasPressedThisFrame,
                gamepad.buttonNorth.wasPressedThisFrame,
                gamepad.dpad.up.wasPressedThisFrame,
                gamepad.selectButton.wasPressedThisFrame,
                gamepad.buttonSouth.wasPressedThisFrame,
                gamepad.dpad.down.wasPressedThisFrame,
                gamepad.rightShoulder.wasPressedThisFrame,
                gamepad.leftTrigger.wasPressedThisFrame,
                gamepad.dpad.left.wasPressedThisFrame,
                gamepad.dpad.right.wasPressedThisFrame,
                profile.ProcessGamepadAxis(rightStick.y),
                gamepad.rightStickButton.wasPressedThisFrame);
        }
    }
}
