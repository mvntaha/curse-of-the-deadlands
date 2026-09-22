using System;
using UnityEngine;

namespace Deadlands.Core.Combat
{
    /// <summary>
    /// Generic hit points shared by the player and every enemy. Other systems react through events
    /// (animation, AI aggro, ragdoll, HUD) instead of Health knowing about them.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float maxHealth = 100f;

        public float Max => maxHealth;
        public float Current { get; private set; }
        public float Normalized => maxHealth > 0f ? Current / maxHealth : 0f;
        public bool IsDead { get; private set; }

        /// <summary>Optional gate (e.g. dodge i-frames). Damage is ignored while it returns true.</summary>
        public Func<bool> IsInvulnerable;

        public event Action<Health, DamageInfo> Damaged;
        public event Action<Health, DamageInfo> Died;
        public event Action<Health> Healed;

        void Awake() => ResetHealth();

        /// <summary>Restores to full. Pass a value to change the max (e.g. from ScriptableObject data or upgrades).</summary>
        public void ResetHealth(float newMax = -1f)
        {
            if (newMax > 0f) maxHealth = newMax;
            Current = maxHealth;
            IsDead = false;
        }

        public bool TakeDamage(in DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f) return false;
            if (IsInvulnerable != null && IsInvulnerable()) return false;

            Current = Mathf.Max(0f, Current - info.Amount);
            Damaged?.Invoke(this, info);

            if (Current <= 0f)
            {
                IsDead = true;
                Died?.Invoke(this, info);
            }
            return true;
        }

        /// <summary>Raises/lowers max HP (upgrades). Current HP moves by the same delta so an upgrade also heals.</summary>
        public void SetMax(float newMax)
        {
            float delta = newMax - maxHealth;
            maxHealth = Mathf.Max(1f, newMax);
            if (!IsDead) Current = Mathf.Clamp(Current + Mathf.Max(0f, delta), 0f, maxHealth);
            Healed?.Invoke(this);
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            Healed?.Invoke(this);
        }
    }
}
