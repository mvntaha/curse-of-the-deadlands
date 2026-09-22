using Deadlands.Core.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace Deadlands.Enemy
{
    /// <summary>One behaviour of the zombie state machine. Subclass to add new behaviours (e.g. a runner's lunge).</summary>
    public abstract class ZombieState
    {
        protected readonly ZombieAI ai;
        protected ZombieState(ZombieAI ai) => this.ai = ai;

        public virtual void Enter() { }
        public abstract void Tick(float dt);
        public virtual void Exit() { }
    }

    /// <summary>Roams around its spawn point until it notices the player.</summary>
    public class WanderState : ZombieState
    {
        float pauseTimer;

        public WanderState(ZombieAI ai) : base(ai) { }

        public override void Enter()
        {
            ai.Agent.isStopped = false;
            ai.Agent.speed = ai.Data.wanderSpeed;
            pauseTimer = Random.Range(0f, ai.Data.wanderPause.y);
            ai.Agent.ResetPath();
        }

        public override void Tick(float dt)
        {
            if (ai.CanDetectTarget())
            {
                ai.ChangeState(ai.Pursue);
                return;
            }

            if (ai.Agent.pathPending || ai.Agent.remainingDistance > ai.Agent.stoppingDistance + 0.1f) return;

            pauseTimer -= dt;
            if (pauseTimer > 0f) return;

            if (TryPickPoint(out Vector3 point)) ai.Agent.SetDestination(point);
            pauseTimer = Random.Range(ai.Data.wanderPause.x, ai.Data.wanderPause.y);
        }

        bool TryPickPoint(out Vector3 point)
        {
            for (int i = 0; i < 6; i++)
            {
                Vector2 r = Random.insideUnitCircle * ai.Data.wanderRadius;
                Vector3 candidate = ai.HomePosition + new Vector3(r.x, 0f, r.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 1.5f, NavMesh.AllAreas))
                {
                    point = hit.position;
                    return true;
                }
            }
            point = ai.transform.position;
            return false;
        }
    }

    /// <summary>Chases the target until in attack range, or gives up if it gets away.</summary>
    public class PursueState : ZombieState
    {
        const float RepathInterval = 0.2f;
        float repathTimer;
        float lostTimer;

        public PursueState(ZombieAI ai) : base(ai) { }

        public override void Enter()
        {
            ai.Agent.isStopped = false;
            ai.Agent.speed = ai.Data.chaseSpeed;
            repathTimer = 0f;
            lostTimer = 0f;
        }

        public override void Tick(float dt)
        {
            if (!ai.HasLiveTarget)
            {
                ai.ChangeState(ai.Wander);
                return;
            }

            float distance = ai.DistanceToTarget();
            if (distance <= ai.Data.attackRange)
            {
                ai.ChangeState(ai.Attack);
                return;
            }

            lostTimer = distance > ai.Data.loseInterestRange ? lostTimer + dt : 0f;
            if (lostTimer >= ai.Data.loseInterestTime)
            {
                ai.ChangeState(ai.Wander);
                return;
            }

            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                ai.Agent.SetDestination(ai.Target.position);
                repathTimer = RepathInterval;
            }
        }
    }

    /// <summary>Stops, turns to the target, swings, and applies damage at the hit frame if the target is still there.</summary>
    public class AttackState : ZombieState
    {
        float timer;
        bool hitApplied;
        float cooldownUntil;

        public AttackState(ZombieAI ai) : base(ai) { }

        public override void Enter()
        {
            ai.Agent.isStopped = true;
            ai.Agent.velocity = Vector3.zero;
            timer = 0f;
            hitApplied = false;
            if (Time.time >= cooldownUntil) ai.PlayAttack();
            else timer = -(cooldownUntil - Time.time); // wait out the cooldown before swinging
        }

        public override void Tick(float dt)
        {
            if (!ai.HasLiveTarget)
            {
                ai.ChangeState(ai.Wander);
                return;
            }

            ai.FaceTarget(dt);

            bool wasWaiting = timer < 0f;
            timer += dt;
            if (wasWaiting && timer >= 0f) ai.PlayAttack();
            if (timer < 0f) return;

            if (!hitApplied && timer >= ai.Data.attackHitTime)
            {
                hitApplied = true;
                ai.TryHitTarget();
            }

            if (timer >= ai.Data.attackDuration)
            {
                cooldownUntil = Time.time + ai.Data.attackCooldown;
                if (ai.DistanceToTarget() <= ai.Data.attackRange) Enter(); // swing again
                else ai.ChangeState(ai.Pursue);
            }
        }

        public override void Exit() => ai.Agent.isStopped = false;
    }

    /// <summary>Ragdolls, lies there, sinks, then returns to the pool.</summary>
    public class DeadState : ZombieState
    {
        float timer;
        DamageInfo killingBlow;

        public DeadState(ZombieAI ai) : base(ai) { }

        public void SetKillingBlow(in DamageInfo info) => killingBlow = info;

        public override void Enter()
        {
            timer = 0f;
            ai.OnEnterDead(killingBlow);
        }

        public override void Tick(float dt)
        {
            timer += dt;
            float sinkStart = ai.Data.corpseLifetime;
            if (timer >= sinkStart) ai.SinkCorpse((timer - sinkStart) / ai.Data.corpseSinkTime);
            if (timer >= sinkStart + ai.Data.corpseSinkTime) ai.ReturnToPool();
        }
    }
}
