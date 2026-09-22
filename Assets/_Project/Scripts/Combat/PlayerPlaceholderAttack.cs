using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using Deadlands.Player;
using UnityEngine;

namespace Deadlands.Combat
{
    /// <summary>
    /// PHASE 2 PLACEHOLDER: a simple punch so damage can flow from player to zombies before real weapons exist.
    /// Replaced by the weapon system in Phase 3.
    /// </summary>
    public class PlayerPlaceholderAttack : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAnimationController animationController;
        [SerializeField] Health ownerHealth;

        [SerializeField] float damage = 25f;
        [SerializeField] float knockbackForce = 6f;
        [SerializeField] float hitDelay = 0.3f;
        [SerializeField] float cooldown = 0.7f;
        [SerializeField] float reach = 0.9f;
        [SerializeField] float radius = 0.6f;
        [SerializeField] float height = 1.1f;
        [SerializeField] LayerMask targetLayers;

        readonly Collider[] hits = new Collider[8];
        float cooldownUntil;
        float pendingHitAt = -1f;

        public int LastHitCount { get; private set; }

        void OnEnable() => input.AttackPressed += OnAttackPressed;
        void OnDisable() => input.AttackPressed -= OnAttackPressed;

        void OnAttackPressed()
        {
            if (Time.time < cooldownUntil || motor.IsDodging || (ownerHealth && ownerHealth.IsDead)) return;
            cooldownUntil = Time.time + cooldown;
            pendingHitAt = Time.time + hitDelay;
            animationController.PlayAttack();
        }

        void Update()
        {
            if (pendingHitAt < 0f || Time.time < pendingHitAt) return;
            pendingHitAt = -1f;
            ApplyHit();
        }

        void ApplyHit()
        {
            Vector3 center = transform.position + transform.forward * reach + Vector3.up * height;
            int count = Physics.OverlapSphereNonAlloc(center, radius, hits, targetLayers, QueryTriggerInteraction.Ignore);
            LastHitCount = 0;
            for (int i = 0; i < count; i++)
            {
                var target = hits[i].GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, ownerHealth)) continue;
                var info = new DamageInfo(damage, hits[i].ClosestPoint(center), transform.forward, knockbackForce, gameObject);
                if (target.TakeDamage(info)) LastHitCount++;
            }
        }
    }
}
