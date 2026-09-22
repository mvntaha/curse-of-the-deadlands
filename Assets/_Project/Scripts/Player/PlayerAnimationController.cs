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
        static readonly int ArmedHash = Animator.StringToHash("Armed");
        static readonly int MeleeStyleHash = Animator.StringToHash("MeleeStyle");
        static readonly int ReloadHash = Animator.StringToHash("Reload");
        static readonly int GrabbedHash = Animator.StringToHash("Grabbed");
        static readonly int ReloadSpeedHash = Animator.StringToHash("ReloadSpeed");
        static readonly int ReloadCancelHash = Animator.StringToHash("ReloadCancel");

        [Header("Recoil")]
        [SerializeField] float recoilRecovery = 12f;

        bool armed;
        float recoil;

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

        /// <summary>Melee swing. Style 0 = punch (fists), 1 = overhead slash (axe/bat).</summary>
        public void PlayMelee(int style)
        {
            animator.SetInteger(MeleeStyleHash, style);
            animator.SetTrigger(AttackHash);
        }

        public void PlayHit() => animator.SetTrigger(HitHash);
        public void SetDead(bool dead) => animator.SetBool(DeadHash, dead);
        /// <summary>Plays the (1 s authored) reload clip stretched to <paramref name="duration"/> seconds.</summary>
        public void PlayReload(float duration)
        {
            animator.SetFloat(ReloadSpeedHash, 1f / Mathf.Max(0.1f, duration));
            animator.ResetTrigger(ReloadCancelHash);
            animator.SetTrigger(ReloadHash);
        }

        public void CancelReload()
        {
            animator.ResetTrigger(ReloadHash);
            animator.SetTrigger(ReloadCancelHash);
        }
        public void SetGrabbed(bool grabbed) => animator.SetBool(GrabbedHash, grabbed);

        /// <summary>True while a firearm is equipped (gun-holding locomotion + aim pose).</summary>
        public void SetArmed(bool value)
        {
            armed = value;
            animator.SetBool(ArmedHash, value);
        }

        public void AddRecoil(float degrees) => recoil += degrees;

        void Update()
        {
            float dt = Time.deltaTime;
            animator.SetFloat(SpeedHash, motor.Speed, speedDampTime, dt);
            // The gun aim pose only makes sense with a firearm; melee aiming just orients the character.
            animator.SetBool(IsAimingHash, motor.IsAiming && armed);

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

            recoil = Mathf.Lerp(recoil, 0f, 1f - Mathf.Exp(-recoilRecovery * dt));
            float pitch = aimPitch - recoil; // recoil kicks the upper body up/back
            if (upperBodyBone && Mathf.Abs(pitch) > 0.01f)
                upperBodyBone.rotation = Quaternion.AngleAxis(pitch, transform.right) * upperBodyBone.rotation;
        }
    }
}
