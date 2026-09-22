using UnityEngine;

namespace Deadlands.Core.Combat
{
    /// <summary>Everything a damage receiver needs to react: how much, where, which way, and who.</summary>
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Point;
        /// <summary>Direction the hit travels (attacker → victim), normalized.</summary>
        public readonly Vector3 Direction;
        /// <summary>Physics push applied to a ragdoll if this hit kills.</summary>
        public readonly float Force;
        public readonly GameObject Source;

        public DamageInfo(float amount, Vector3 point, Vector3 direction, float force, GameObject source)
        {
            Amount = amount;
            Point = point;
            Direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector3.forward;
            Force = force;
            Source = source;
        }
    }

    public interface IDamageable
    {
        /// <summary>Applies damage. Returns true if the damage was actually taken (not blocked/ignored).</summary>
        bool TakeDamage(in DamageInfo info);
    }
}
