using System;
using Deadlands.Core.Input;
using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Camera-relative third-person locomotion: walk, sprint, aim-strafe and dodge-roll.
    /// Owns only movement state; animation, camera and (later) combat read from it instead of the reverse.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour
    {
        [SerializeField] PlayerMovementConfig config;
        [SerializeField] PlayerInputReader input;
        [Tooltip("Transform whose facing defines 'forward' for movement input (the gameplay camera).")]
        [SerializeField] Transform viewTransform;
        [Tooltip("Optional. When set, sprinting drains it and dodging costs it.")]
        [SerializeField] PlayerStamina stamina;

        CharacterController controller;
        Vector3 horizontalVelocity;
        float verticalVelocity;
        float dodgeTimer;
        float dodgeCooldownTimer;
        float dodgeBaseSpeed;
        Vector3 dodgeDirection;
        int movementLocks;

        /// <summary>True while something (grab, melee swing, cutscene) holds the player in place.</summary>
        public bool IsMovementLocked => movementLocks > 0;
        public bool IsAiming { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsDodging { get; private set; }
        public bool IsGrounded { get; private set; }
        /// <summary>True during the dodge's invulnerability window. Damage systems should check this.</summary>
        public bool IsInvulnerable { get; private set; }
        public Vector3 HorizontalVelocity => horizontalVelocity;
        public float Speed => horizontalVelocity.magnitude;
        public float DodgeNormalizedTime => IsDodging ? Mathf.Clamp01(dodgeTimer / config.dodgeDuration) : 0f;
        public PlayerMovementConfig Config => config;

        public event Action DodgeStarted;
        public event Action DodgeEnded;

        void Awake()
        {
            controller = GetComponent<CharacterController>();
            dodgeBaseSpeed = config.dodgeDistance / (config.dodgeDuration * AverageCurveValue(config.dodgeSpeedCurve));
        }

        void OnEnable() => input.DodgePressed += TryStartDodge;
        void OnDisable() => input.DodgePressed -= TryStartDodge;

        void Update()
        {
            float dt = Time.deltaTime;
            dodgeCooldownTimer -= dt;

            if (IsDodging) UpdateDodge(dt);
            else UpdateLocomotion(dt);

            if (controller.isGrounded && verticalVelocity < 0f) verticalVelocity = config.groundedStickForce;
            verticalVelocity += config.gravity * dt;

            controller.Move((horizontalVelocity + Vector3.up * verticalVelocity) * dt);
            IsGrounded = controller.isGrounded;

            // Feed back what actually happened so walls stop the run animation instead of it running in place.
            if (!IsDodging && dt > 0f)
            {
                Vector3 actual = controller.velocity;
                actual.y = 0f;
                if (actual.sqrMagnitude < horizontalVelocity.sqrMagnitude) horizontalVelocity = actual;
            }
        }

        /// <summary>Stack-style lock: every PushMovementLock needs a matching PopMovementLock.</summary>
        public void PushMovementLock() => movementLocks++;
        public void PopMovementLock() => movementLocks = Mathf.Max(0, movementLocks - 1);

        /// <summary>Snaps the character to face a flat direction (used by grabs and melee targeting).</summary>
        public void FaceDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        void UpdateLocomotion(float dt)
        {
            Vector3 wish = IsMovementLocked ? Vector3.zero : WishDirection();
            IsAiming = input.AimHeld && !IsMovementLocked;
            IsSprinting = input.SprintHeld && !IsAiming && wish.sqrMagnitude > 0.01f;
            if (IsSprinting && stamina && !stamina.Drain(config.sprintStaminaPerSecond, dt)) IsSprinting = false;

            float maxSpeed = IsAiming ? config.aimMoveSpeed : IsSprinting ? config.runSpeed : config.walkSpeed;
            Vector3 targetVelocity = wish * maxSpeed;
            float rate = targetVelocity.sqrMagnitude >= horizontalVelocity.sqrMagnitude ? config.acceleration : config.deceleration;
            horizontalVelocity = Vector3.Lerp(horizontalVelocity, targetVelocity, 1f - Mathf.Exp(-rate * dt));

            Vector3 facing = IsAiming ? ViewForward() : wish;
            if (!IsMovementLocked && facing.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(facing, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, config.turnSpeed * dt);
            }
        }

        void TryStartDodge()
        {
            if (!enabled || IsDodging || IsMovementLocked || dodgeCooldownTimer > 0f || !controller.isGrounded) return;
            if (stamina && !stamina.TrySpend(config.dodgeStaminaCost)) return;

            Vector3 wish = WishDirection();
            dodgeDirection = wish.sqrMagnitude > 0.01f ? wish.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(dodgeDirection, Vector3.up);

            IsDodging = true;
            IsAiming = false;
            IsSprinting = false;
            dodgeTimer = 0f;
            DodgeStarted?.Invoke();
        }

        void UpdateDodge(float dt)
        {
            dodgeTimer += dt;
            float t = Mathf.Clamp01(dodgeTimer / config.dodgeDuration);
            horizontalVelocity = dodgeDirection * (dodgeBaseSpeed * config.dodgeSpeedCurve.Evaluate(t));
            IsInvulnerable = t >= config.dodgeInvulnerableWindow.x && t <= config.dodgeInvulnerableWindow.y;

            if (t >= 1f)
            {
                IsDodging = false;
                IsInvulnerable = false;
                dodgeCooldownTimer = config.dodgeCooldown;
                horizontalVelocity *= 0.3f;
                DodgeEnded?.Invoke();
            }
        }

        Vector3 WishDirection()
        {
            Vector2 move = input.Move;
            Vector3 forward = ViewForward();
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
        }

        Vector3 ViewForward()
        {
            Vector3 f = viewTransform ? viewTransform.forward : transform.forward;
            f.y = 0f;
            return f.sqrMagnitude > 0.0001f ? f.normalized : transform.forward;
        }

        static float AverageCurveValue(AnimationCurve curve)
        {
            const int samples = 32;
            float sum = 0f;
            for (int i = 0; i < samples; i++) sum += curve.Evaluate((i + 0.5f) / samples);
            return Mathf.Max(0.01f, sum / samples);
        }
    }
}
