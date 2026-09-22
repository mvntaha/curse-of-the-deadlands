using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Drives the Animator from PlayerMotor state and layers procedural tweaks the source clips lack:
    /// lower-body yaw for aim-strafing, reversed walk for backpedalling and upper-body aim pitch.
    /// </summary>
    public class PlayerAnimationController : MonoBehaviour
    {
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int IsAimingHash = Animator.StringToHash("IsAiming");
        static readonly int DodgeHash = Animator.StringToHash("Dodge");
        static readonly int AimPlaybackHash = Animator.StringToHash("AimPlayback");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly int HitHash = Animator.StringToHash("Hit");
        static readonly int DeadHash = Animator.StringToHash("Dead");

        [SerializeField] PlayerMotor motor;
        [SerializeField] Animator animator;
        [SerializeField] ThirdPersonCamera cameraRig;
        [SerializeField] float speedDampTime = 0.08f;

        [Header("Procedural bones")]
        [Tooltip("Bone that parents the whole skeleton including IK feet (ZAK rig: 'Root').")]
        [SerializeField] Transform lowerBodyBone;
        [Tooltip("Upper-body bone kept facing the aim direction (ZAK rig: 'Torso').")]
        [SerializeField] Transform upperBodyBone;
        [SerializeField] float maxLegYaw = 65f;
        [SerializeField] float legYawSharpness = 12f;
        [SerializeField, Range(0f, 1f)] float aimPitchWeight = 0.7f;
        [SerializeField] float maxAimPitch = 45f;
        [SerializeField] float aimPitchSharpness = 15f;

        float legYaw;
        float aimPitch;

        void OnEnable() => motor.DodgeStarted += OnDodgeStarted;
        void OnDisable() => motor.DodgeStarted -= OnDodgeStarted;

        void OnDodgeStarted() => animator.SetTrigger(DodgeHash);

        public void PlayAttack() => animator.SetTrigger(AttackHash);
        public void PlayHit() => animator.SetTrigger(HitHash);
        public void SetDead(bool dead) => animator.SetBool(DeadHash, dead);

        void Update()
        {
            float dt = Time.deltaTime;
            animator.SetFloat(SpeedHash, motor.Speed, speedDampTime, dt);
            animator.SetBool(IsAimingHash, motor.IsAiming);

            Vector3 local = transform.InverseTransformDirection(motor.HorizontalVelocity);
            bool backpedal = motor.IsAiming && local.z < -0.1f;
            animator.SetFloat(AimPlaybackHash, backpedal ? -1f : 1f);
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            // Lower body turns toward the strafe direction while the upper body keeps facing the aim.
            float targetYaw = 0f;
            if (motor.IsAiming && motor.Speed > 0.2f)
            {
                Vector3 local = transform.InverseTransformDirection(motor.HorizontalVelocity);
                if (local.z < -0.1f) local = -local; // backpedal plays the walk in reverse
                targetYaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -maxLegYaw, maxLegYaw);
            }
            legYaw = Mathf.Lerp(legYaw, targetYaw, 1f - Mathf.Exp(-legYawSharpness * dt));

            float targetPitch = motor.IsAiming && cameraRig
                ? Mathf.Clamp(cameraRig.Pitch, -maxAimPitch, maxAimPitch) * aimPitchWeight
                : 0f;
            aimPitch = Mathf.Lerp(aimPitch, targetPitch, 1f - Mathf.Exp(-aimPitchSharpness * dt));

            if (motor.IsDodging) return; // the roll clip owns the skeleton

            if (lowerBodyBone && Mathf.Abs(legYaw) > 0.01f)
            {
                lowerBodyBone.rotation = Quaternion.AngleAxis(legYaw, Vector3.up) * lowerBodyBone.rotation;
                if (upperBodyBone)
                    upperBodyBone.rotation = Quaternion.AngleAxis(-legYaw, Vector3.up) * upperBodyBone.rotation;
            }

            if (upperBodyBone && Mathf.Abs(aimPitch) > 0.01f)
                upperBodyBone.rotation = Quaternion.AngleAxis(aimPitch, transform.right) * upperBodyBone.rotation;
        }
    }
}
