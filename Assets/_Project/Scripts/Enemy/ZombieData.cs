using UnityEngine;

namespace Deadlands.Enemy
{
    /// <summary>
    /// Everything that makes one zombie type different from another. New types (runner, tank, spitter)
    /// are new assets of this — plus, if needed, extra states — never edits to the base AI.
    /// </summary>
    [CreateAssetMenu(fileName = "ZombieData", menuName = "Deadlands/Enemy/Zombie Data")]
    public class ZombieData : ScriptableObject
    {
        public string displayName = "Shambler";

        [Header("Vitals")]
        public float maxHealth = 60f;

        [Header("Movement (m/s)")]
        public float wanderSpeed = 0.55f;
        public float chaseSpeed = 1.1f;
        public float turnSpeed = 240f;
        [Tooltip("Speed the locomotion clip was authored at; used to keep feet from sliding.")]
        public float locomotionClipSpeed = 1.1f;

        [Header("Wander")]
        public float wanderRadius = 6f;
        public Vector2 wanderPause = new Vector2(1.5f, 4f);

        [Header("Perception")]
        public float sightRange = 12f;
        [Range(10f, 360f)] public float sightAngle = 120f;
        [Tooltip("Within this radius the zombie notices the player regardless of facing (hearing/smell).")]
        public float proximityRadius = 3f;
        [Tooltip("Stops chasing after the target has been farther than this for loseInterestTime.")]
        public float loseInterestRange = 20f;
        public float loseInterestTime = 4f;

        [Header("Attack")]
        public float attackRange = 1.3f;
        public float attackDamage = 12f;
        [Tooltip("Seconds from the start of the attack until the hit lands.")]
        public float attackHitTime = 0.4f;
        public float attackDuration = 1.0f;
        public float attackCooldown = 0.6f;
        [Tooltip("Max angle (deg) between the zombie's facing and the target for the hit to connect.")]
        public float attackArc = 70f;

        [Header("Hit reactions")]
        [Tooltip("Height above the feet where hits count as headshots.")]
        public float headHeight = 1.2f;
        public float headshotMultiplier = 2.5f;
        [Tooltip("How long a hit staggers (interrupts) the zombie.")]
        public float staggerDuration = 0.55f;
        [Tooltip("Minimum seconds between staggers so rapid fire can't stun-lock forever.")]
        public float staggerCooldown = 1.2f;

        [Header("Grab")]
        [Tooltip("Chance that an attack becomes a grab (0 = never).")]
        [Range(0f, 1f)] public float grabChance = 0.3f;
        public int grabPressesToEscape = 8;
        public float grabDuration = 3.5f;
        public float grabDamagePerSecond = 4f;
        public float grabFailDamage = 20f;
        [Tooltip("Stagger length when the player breaks free.")]
        public float escapeStaggerDuration = 1.4f;
        public float escapePushDistance = 1.2f;

        [Header("Death")]
        [Tooltip("Seconds the ragdoll lies there before sinking and returning to the pool.")]
        public float corpseLifetime = 8f;
        public float corpseSinkTime = 1.5f;
    }
}
