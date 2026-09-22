using System;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using Deadlands.Player;
using UnityEngine;

namespace Deadlands.Combat
{
    /// <summary>
    /// Unlockable combat ability: a close-range shove that knocks every zombie in front of the player off balance.
    /// Costs stamina, has a cooldown. Locked until granted by an upgrade.
    /// </summary>
    public class ShoveAbility : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] PlayerAnimationController animationController;
        [SerializeField] Health ownerHealth;
        [SerializeField] bool unlocked;
        [SerializeField] float staminaCost = 25f;
        [SerializeField] float cooldown = 3f;
        [SerializeField] float range = 1.8f;
        [SerializeField] float arc = 120f;
        [SerializeField] float staggerDuration = 1.2f;
        [SerializeField] float pushDistance = 1.5f;
        [SerializeField] LayerMask targetMask = ~0;

        readonly Collider[] hits = new Collider[16];
        float readyAt;

        public bool IsUnlocked => unlocked;
        public float CooldownRemaining => Mathf.Max(0f, readyAt - Time.time);

        /// <summary>(targetsHit)</summary>
        public event Action<int> Shoved;

        void OnEnable() => input.ShovePressed += TryShove;
        void OnDisable() => input.ShovePressed -= TryShove;

        public void Unlock() => unlocked = true;

        void TryShove()
        {
            if (!unlocked || Time.time < readyAt || motor.IsDodging || motor.IsMovementLocked) return;
            if (ownerHealth && ownerHealth.IsDead) return;
            if (!stamina.TrySpend(staminaCost)) return;

            readyAt = Time.time + cooldown;
            animationController.PlayMelee(0);

            int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, range, hits, targetMask, QueryTriggerInteraction.Ignore);
            int affected = 0;
            for (int i = 0; i < count; i++)
            {
                var target = hits[i].GetComponentInParent<IStaggerable>();
                if (target == null) continue;
                Vector3 to = hits[i].transform.position - transform.position;
                to.y = 0f;
                if (Vector3.Angle(transform.forward, to) > arc * 0.5f) continue;
                target.ForceStagger(staggerDuration, to.normalized * pushDistance);
                affected++;
            }
            Shoved?.Invoke(affected);
        }
    }
}
