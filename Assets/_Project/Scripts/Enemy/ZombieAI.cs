using System;
using Deadlands.Core.Combat;
using Deadlands.Core.Pooling;
using UnityEngine;
using UnityEngine.AI;

namespace Deadlands.Enemy
{
    /// <summary>
    /// NavMesh zombie driven by a small state machine (Wander → Pursue → Attack, plus Stagger, Grab, Dead).
    /// Stats come from <see cref="ZombieData"/>; new zombie types override <see cref="CreateStates"/> or add states
    /// rather than changing this class.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Health))]
    public class ZombieAI : MonoBehaviour, IPoolable, IGrabber, IHitZoneProvider
    {
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int LocomotionSpeedHash = Animator.StringToHash("LocomotionSpeed");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int GrabbingHash = Animator.StringToHash("Grabbing");

        [SerializeField] ZombieData data;
        [SerializeField] Animator animator;
        [SerializeField] ZombieRagdoll ragdoll;
        [SerializeField] Collider bodyCollider;
        [Tooltip("Where the zombie 'sees' from.")]
        [SerializeField] Transform eyes;
        [Tooltip("Layers that block line of sight.")]
        [SerializeField] LayerMask sightBlockers = 1; // Default

        static Transform cachedPlayer;

        bool initialized;
        Health targetHealth;
        IGrabbable targetGrabbable;
        IGrabbable grabbedVictim;
        Vector3 sinkStart;
        bool sinking;
        float staggerReadyAt;

        public ZombieData Data => data;
        public NavMeshAgent Agent { get; private set; }
        public Health Health { get; private set; }
        public Transform Target { get; private set; }
        public Vector3 HomePosition { get; private set; }
        public ZombieState CurrentState { get; private set; }

        public ZombieState Wander { get; protected set; }
        public ZombieState Pursue { get; protected set; }
        public ZombieState Attack { get; protected set; }
        public StaggerState Stagger { get; protected set; }
        public ZombieState Grab { get; protected set; }
        public DeadState Dead { get; protected set; }

        public bool HasLiveTarget => Target && (!targetHealth || !targetHealth.IsDead);

        /// <summary>Raised when the zombie finishes dying and goes back to the pool.</summary>
        public event Action<ZombieAI> Despawned;
        public event Action<ZombieAI> StateChanged;

        void Awake()
        {
            Agent = GetComponent<NavMeshAgent>();
            Health = GetComponent<Health>();
            CreateStates();
        }

        /// <summary>Override in a subclass to swap in different behaviours for a new zombie type.</summary>
        protected virtual void CreateStates()
        {
            Wander = new WanderState(this);
            Pursue = new PursueState(this);
            Attack = new AttackState(this);
            Stagger = new StaggerState(this);
            Grab = new GrabState(this);
            Dead = new DeadState(this);
        }

        void OnEnable()
        {
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        void OnDisable()
        {
            Health.Damaged -= OnDamaged;
            Health.Died -= OnDied;
        }

        void Start()
        {
            // Zombies placed directly in a scene (not spawned from a pool) still need setting up.
            if (!initialized) Setup();
        }

        public void OnSpawned() => Setup();

        public void OnDespawned()
        {
            CurrentState = null;
            initialized = false;
        }

        void Setup()
        {
            initialized = true;
            sinking = false;
            staggerReadyAt = 0f;
            grabbedVictim = null;
            Health.ResetHealth(data.maxHealth);
            ragdoll.ResetPose();
            bodyCollider.enabled = true;

            Agent.enabled = true;
            Agent.Warp(transform.position);
            Agent.angularSpeed = data.turnSpeed;
            Agent.stoppingDistance = Mathf.Min(0.5f, data.attackRange * 0.5f);
            HomePosition = transform.position;

            ResolveTarget();
            CurrentState = null;
            ChangeState(Wander);
        }

        void ResolveTarget()
        {
            if (!cachedPlayer)
            {
                var player = GameObject.FindWithTag("Player");
                cachedPlayer = player ? player.transform : null;
            }
            Target = cachedPlayer;
            targetHealth = Target ? Target.GetComponent<Health>() : null;
            targetGrabbable = Target ? Target.GetComponent<IGrabbable>() : null;
        }

        public void ChangeState(ZombieState next)
        {
            if (next == CurrentState) return;
            CurrentState?.Exit();
            CurrentState = next;
            CurrentState?.Enter();
            StateChanged?.Invoke(this);
        }

        void Update()
        {
            if (CurrentState == null) return;
            float dt = Time.deltaTime;
            CurrentState.Tick(dt);
            UpdateAnimator(dt);
        }

        void UpdateAnimator(float dt)
        {
            if (!animator.enabled) return;
            float speed = Agent.enabled ? Agent.velocity.magnitude : 0f;
            animator.SetFloat(SpeedHash, speed, 0.1f, dt);
            animator.SetFloat(LocomotionSpeedHash, speed > 0.05f ? Mathf.Max(speed, 0.3f) / data.locomotionClipSpeed : 1f);
        }

        // ---------- Queries & actions used by states ----------

        public float DistanceToTarget()
        {
            if (!Target) return float.MaxValue;
            Vector3 d = Target.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        public bool CanDetectTarget()
        {
            if (!HasLiveTarget) return false;
            float distance = DistanceToTarget();
            if (distance <= data.proximityRadius) return true;
            if (distance > data.sightRange) return false;

            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            if (Vector3.Angle(transform.forward, to) > data.sightAngle * 0.5f) return false;

            Vector3 eye = eyes ? eyes.position : transform.position + Vector3.up * 1.4f;
            return !Physics.Linecast(eye, Target.position + Vector3.up * 1.3f, sightBlockers, QueryTriggerInteraction.Ignore);
        }

        public void FaceTarget(float dt)
        {
            if (!Target) return;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), data.turnSpeed * dt);
        }

        public void PlayAttack() => animator.SetTrigger(AttackHash);
        public void PlayHitReaction() => animator.SetTrigger(HitHash);
        public void SetGrabbing(bool grabbing) => animator.SetBool(GrabbingHash, grabbing);

        public void TryHitTarget()
        {
            if (!HasLiveTarget || !targetHealth) return;
            Vector3 to = Target.position - transform.position;
            to.y = 0f;
            if (to.magnitude > data.attackRange + 0.4f) return;
            if (Vector3.Angle(transform.forward, to) > data.attackArc * 0.5f) return;

            if (targetGrabbable != null && targetGrabbable.CanBeGrabbed && UnityEngine.Random.value < data.grabChance)
            {
                grabbedVictim = targetGrabbable;
                ChangeState(Grab);
                grabbedVictim.BeginGrab(this, new GrabSettings(data.grabPressesToEscape, data.grabDuration,
                    data.grabDamagePerSecond, data.grabFailDamage));
                return;
            }

            var info = new DamageInfo(data.attackDamage, Target.position + Vector3.up * 1.2f, to, 0f, gameObject);
            targetHealth.TakeDamage(info);
        }

        // ---------- IGrabber ----------

        public void OnGrabEnded(bool victimEscaped)
        {
            grabbedVictim = null;
            if (Health.IsDead) return;
            if (victimEscaped)
            {
                // Shoved off: stumble back and reel for a moment.
                Vector3 back = -transform.forward * data.escapePushDistance;
                if (Agent.enabled) Agent.Move(back);
                BeginStagger(data.escapeStaggerDuration, force: true);
            }
            else
            {
                ChangeState(HasLiveTarget ? Pursue : Wander);
            }
        }

        // ---------- IHitZoneProvider ----------

        public float GetDamageMultiplier(Vector3 worldPoint, out bool isCritical)
        {
            isCritical = worldPoint.y - transform.position.y >= data.headHeight;
            return isCritical ? data.headshotMultiplier : 1f;
        }

        // ---------- Health reactions ----------

        void OnDamaged(Health _, DamageInfo info)
        {
            if (Health.IsDead) return;
            if (CurrentState == Grab) return; // committed to the grab
            if (Time.time >= staggerReadyAt) BeginStagger(data.staggerDuration);
            else if (CurrentState == Wander && HasLiveTarget) ChangeState(Pursue); // getting hit pulls it toward the player
        }

        void BeginStagger(float duration, bool force = false)
        {
            if (!force && Time.time < staggerReadyAt) return;
            staggerReadyAt = Time.time + data.staggerCooldown;
            Stagger.SetDuration(duration);
            if (CurrentState == Stagger) Stagger.Enter(); // re-flinch
            else ChangeState(Stagger);
        }

        void OnDied(Health _, DamageInfo info)
        {
            if (grabbedVictim != null)
            {
                grabbedVictim.ReleaseFrom(this);
                grabbedVictim = null;
            }
            Dead.SetKillingBlow(info);
            ChangeState(Dead);
        }

        public void OnEnterDead(in DamageInfo killingBlow)
        {
            if (Agent.enabled) Agent.isStopped = true;
            Agent.enabled = false;
            bodyCollider.enabled = false;
            ragdoll.Activate(killingBlow.Direction * killingBlow.Force, killingBlow.Point);
        }

        public void SinkCorpse(float t)
        {
            if (!sinking)
            {
                sinking = true;
                ragdoll.Freeze();
                sinkStart = transform.position;
            }
            transform.position = sinkStart + Vector3.down * Mathf.Clamp01(t) * 1.2f;
        }

        public void ReturnToPool()
        {
            var pooled = GetComponent<PooledObject>();
            Despawned?.Invoke(this);
            if (pooled) pooled.Release();
            else gameObject.SetActive(false);
        }
    }
}
