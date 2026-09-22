using System;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Connects the player's Health to the rest of the player: dodge i-frames block damage, hits play a
    /// reaction, death stops control. Mission-fail/retry flow hooks into <see cref="PlayerDied"/> (Phase 5).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerDamageHandler : MonoBehaviour
    {
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAnimationController animationController;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerStruggle struggle;

        Health health;

        public Health Health => health;
        public event Action PlayerDied;

        void Awake()
        {
            health = GetComponent<Health>();
            health.IsInvulnerable = () => motor.IsInvulnerable;
        }

        void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }

        void OnDamaged(Health _, DamageInfo info)
        {
            // While grabbed, the struggle animation already sells the bites.
            if (!health.IsDead && !(struggle && struggle.IsGrabbed)) animationController.PlayHit();
        }

        void OnDied(Health _, DamageInfo info)
        {
            animationController.SetDead(true);
            input.enabled = false;
            motor.enabled = false;
            PlayerDied?.Invoke();
        }

        /// <summary>Brings the player back (used by retry/checkpoint later, and by tests).</summary>
        public void Revive()
        {
            health.ResetHealth();
            animationController.SetDead(false);
            input.enabled = true;
            motor.enabled = true;
        }
    }
}
