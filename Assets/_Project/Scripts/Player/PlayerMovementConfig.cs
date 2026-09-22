using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>Tunable movement values for the player. Data-driven so balance changes never touch code.</summary>
    [CreateAssetMenu(fileName = "PlayerMovementConfig", menuName = "Deadlands/Player/Movement Config")]
    public class PlayerMovementConfig : ScriptableObject
    {
        [Header("Speeds (m/s)")]
        public float walkSpeed = 1.9f;
        public float runSpeed = 3.6f;
        public float aimMoveSpeed = 1.6f;

        [Header("Responsiveness")]
        [Tooltip("How fast horizontal velocity reaches the target speed.")]
        public float acceleration = 14f;
        [Tooltip("How fast horizontal velocity drops to zero when input is released.")]
        public float deceleration = 18f;
        [Tooltip("Degrees per second the character turns toward its move/aim direction.")]
        public float turnSpeed = 900f;

        [Header("Gravity")]
        public float gravity = -20f;
        public float groundedStickForce = -2f;

        [Header("Dodge (roll)")]
        public float dodgeDistance = 3.2f;
        public float dodgeDuration = 0.6f;
        public float dodgeCooldown = 0.35f;
        [Tooltip("Normalized window (0-1 of the dodge) during which the player can't be damaged.")]
        public Vector2 dodgeInvulnerableWindow = new Vector2(0.05f, 0.7f);
        [Tooltip("Speed profile over the dodge (x = normalized time, y = relative speed).")]
        public AnimationCurve dodgeSpeedCurve = new AnimationCurve(
            new Keyframe(0f, 1.4f), new Keyframe(0.6f, 1.1f), new Keyframe(1f, 0.2f));
    }
}
