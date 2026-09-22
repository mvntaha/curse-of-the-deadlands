using UnityEngine;

namespace Deadlands.Core.Input
{
    /// <summary>Player-adjustable control preferences (sensitivity, inversion). Edited by the settings menu later.</summary>
    [CreateAssetMenu(fileName = "ControlSettings", menuName = "Deadlands/Input/Control Settings")]
    public class ControlSettings : ScriptableObject
    {
        [Tooltip("Degrees of camera rotation per pixel of mouse movement.")]
        [Range(0.01f, 1f)] public float mouseSensitivity = 0.12f;
        [Tooltip("Degrees per second of camera rotation at full stick deflection.")]
        [Range(30f, 600f)] public float gamepadSensitivity = 200f;
        [Tooltip("Multiplier applied to look sensitivity while aiming.")]
        [Range(0.1f, 1f)] public float aimSensitivityMultiplier = 0.6f;
        public bool invertY;
    }
}
