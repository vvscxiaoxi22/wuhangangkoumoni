using UnityEngine;

namespace PortTraining.YardCraneSimulator.Controls
{
    [CreateAssetMenu(
        fileName = "YC-StandaloneInputProfile",
        menuName = "PortTraining/Yard Crane/Standalone Input Profile")]
    public sealed class YardCraneInputProfile : ScriptableObject
    {
        [Header("Xbox axis processing")]
        [SerializeField, Range(0f, 0.5f)] private float gamepadDeadzone = 0.16f;
        [SerializeField, Range(0.25f, 3f)] private float responseExponent = 1.2f;

        public float ProcessGamepadAxis(float rawValue)
        {
            var magnitude = Mathf.Abs(rawValue);
            if (magnitude <= gamepadDeadzone)
            {
                return 0f;
            }

            var normalized = Mathf.InverseLerp(gamepadDeadzone, 1f, magnitude);
            var curved = Mathf.Pow(normalized, responseExponent);
            return Mathf.Sign(rawValue) * curved;
        }

        public static YardCraneInputProfile CreateRuntimeDefault()
        {
            var profile = CreateInstance<YardCraneInputProfile>();
            profile.hideFlags = HideFlags.HideAndDontSave;
            return profile;
        }
    }
}
