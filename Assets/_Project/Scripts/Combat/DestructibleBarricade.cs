using System;
using Deadlands.Core.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Deadlands.Combat
{
    /// <summary>
    /// Wooden barricade that blocks a path until it takes enough damage, then bursts into physics planks.
    /// Blocks navigation too (carving obstacle) until broken.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DestructibleBarricade : MonoBehaviour
    {
        [SerializeField] Collider blockingCollider;
        [SerializeField] NavMeshObstacle navObstacle;
        [Tooltip("Plank pieces: kinematic and collider-off while intact.")]
        [SerializeField] Rigidbody[] pieces;
        [SerializeField] float breakImpulse = 3f;
        [SerializeField] float scatterImpulse = 1.5f;
        [Tooltip("Seconds of physics before the debris is frozen in place (keeps the cost bounded).")]
        [SerializeField] float debrisSimulationTime = 6f;

        Health health;
        float freezeAt = -1f;

        public bool IsBroken { get; private set; }
        public event Action<DestructibleBarricade> Broken;

        void Awake()
        {
            health = GetComponent<Health>();
            foreach (var p in pieces)
            {
                p.isKinematic = true;
                var c = p.GetComponent<Collider>();
                if (c) c.enabled = false;
            }
        }

        void OnEnable() => health.Died += OnDied;
        void OnDisable() => health.Died -= OnDied;

        void OnDied(Health _, DamageInfo info)
        {
            IsBroken = true;
            if (blockingCollider) blockingCollider.enabled = false;
            if (navObstacle) navObstacle.enabled = false;

            foreach (var p in pieces)
            {
                var c = p.GetComponent<Collider>();
                if (c) c.enabled = true;
                p.isKinematic = false;
                Vector3 push = info.Direction * breakImpulse + UnityEngine.Random.insideUnitSphere * scatterImpulse + Vector3.up;
                p.AddForceAtPosition(push, info.Point, ForceMode.Impulse);
                p.AddTorque(UnityEngine.Random.insideUnitSphere * scatterImpulse, ForceMode.Impulse);
            }
            freezeAt = Time.time + debrisSimulationTime;
            Broken?.Invoke(this);
        }

        void Update()
        {
            if (freezeAt < 0f || Time.time < freezeAt) return;
            freezeAt = -1f;
            foreach (var p in pieces)
            {
                p.isKinematic = true;
                var c = p.GetComponent<Collider>();
                if (c) c.enabled = false;
            }
        }
    }
}
