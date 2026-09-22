using System;
using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Stamina for sprinting, dodging and abilities. Regenerates after a short pause. Max and regen are
    /// upgradable through <see cref="Deadlands.Progression.PlayerProgression"/>.
    /// </summary>
    public class PlayerStamina : MonoBehaviour
    {
        [SerializeField] float max = 100f;
        [SerializeField] float regenPerSecond = 22f;
        [Tooltip("Seconds after spending before regeneration starts.")]
        [SerializeField] float regenDelay = 0.8f;
        [Tooltip("When fully drained, sprinting stays locked until stamina recovers to this fraction.")]
        [SerializeField, Range(0f, 1f)] float exhaustedRecoverFraction = 0.3f;

        float lastSpendTime = -999f;

        public float Current { get; private set; }
        public float Max => max;
        public float Normalized => max > 0f ? Current / max : 0f;
        public float RegenPerSecond => regenPerSecond;
        /// <summary>True after running dry, until stamina recovers enough to sprint again.</summary>
        public bool IsExhausted { get; private set; }

        public event Action Changed;

        void Awake() => Current = max;

        void Update()
        {
            if (Current >= max || Time.time - lastSpendTime < regenDelay) return;
            Current = Mathf.Min(max, Current + regenPerSecond * Time.deltaTime);
            if (IsExhausted && Current >= max * exhaustedRecoverFraction) IsExhausted = false;
            Changed?.Invoke();
        }

        /// <summary>Spends a lump sum (dodge, shove). Fails without spending if there isn't enough.</summary>
        public bool TrySpend(float amount)
        {
            if (Current < amount) return false;
            Spend(amount);
            return true;
        }

        /// <summary>Continuous drain (sprinting). Returns false once empty.</summary>
        public bool Drain(float perSecond, float dt)
        {
            if (IsExhausted || Current <= 0f) return false;
            Spend(perSecond * dt);
            return true;
        }

        void Spend(float amount)
        {
            Current = Mathf.Max(0f, Current - amount);
            lastSpendTime = Time.time;
            if (Current <= 0f) IsExhausted = true;
            Changed?.Invoke();
        }

        public void SetMax(float newMax, bool refill = true)
        {
            max = Mathf.Max(1f, newMax);
            Current = refill ? max : Mathf.Min(Current, max);
            Changed?.Invoke();
        }

        public void SetRegen(float perSecond) => regenPerSecond = Mathf.Max(0f, perSecond);

        public void Refill()
        {
            Current = max;
            IsExhausted = false;
            Changed?.Invoke();
        }
    }
}
