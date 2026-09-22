using Deadlands.Core.Input;
using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Resident-Evil-style over-the-shoulder orbit camera with an aim zoom, shoulder swap and
    /// sphere-cast collision so it never ends up inside level geometry.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class ThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] Camera cam;

        [Header("Framing")]
        [SerializeField] float pivotHeight = 1.55f;
        [SerializeField] float verticalOffset = 0.1f;
        [SerializeField] float normalDistance = 2.8f;
        [SerializeField] float normalShoulder = 0.5f;
        [SerializeField] float normalFov = 60f;
        [SerializeField] float aimDistance = 2.1f;
        [SerializeField] float aimShoulder = 0.95f;
        [SerializeField] float aimFov = 45f;
        [SerializeField] Vector2 pitchLimits = new Vector2(-45f, 70f);

        [Header("Smoothing")]
        [SerializeField] float followSharpness = 20f;
        [SerializeField] float aimBlendSharpness = 12f;
        [SerializeField] float shoulderSwapSharpness = 10f;
        [Tooltip("How quickly the camera eases back out after a wall stops blocking it.")]
        [SerializeField] float pushOutSharpness = 6f;

        [Header("Collision")]
        [SerializeField] LayerMask collisionMask = ~0;
        [SerializeField] float collisionRadius = 0.2f;
        [SerializeField] float minDistance = 0.25f;

        float yaw, pitch;
        float aimBlend;
        float shoulderSide = 1f, currentSide = 1f;
        float currentDistance;
        Vector3 pivot;

        public float Yaw => yaw;
        public float Pitch => pitch;

        void Awake()
        {
            if (!cam) cam = GetComponent<Camera>();
            yaw = target.eulerAngles.y;
            pitch = 10f;
            pivot = target.position + Vector3.up * pivotHeight;
            currentDistance = normalDistance;
        }

        void OnEnable()
        {
            input.SwapShoulderPressed += SwapShoulder;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void OnDisable()
        {
            input.SwapShoulderPressed -= SwapShoulder;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void SwapShoulder() => shoulderSide = -shoulderSide;

        void LateUpdate()
        {
            float dt = Time.deltaTime;

            Vector2 look = input.LookDegrees;
            yaw += look.x;
            pitch = Mathf.Clamp(pitch - look.y, pitchLimits.x, pitchLimits.y);

            bool aiming = motor ? motor.IsAiming : input.AimHeld;
            aimBlend = Mathf.Lerp(aimBlend, aiming ? 1f : 0f, 1f - Mathf.Exp(-aimBlendSharpness * dt));
            currentSide = Mathf.Lerp(currentSide, shoulderSide, 1f - Mathf.Exp(-shoulderSwapSharpness * dt));

            Vector3 targetPivot = target.position + Vector3.up * pivotHeight;
            pivot = Vector3.Lerp(pivot, targetPivot, 1f - Mathf.Exp(-followSharpness * dt));

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            float shoulder = Mathf.Lerp(normalShoulder, aimShoulder, aimBlend) * currentSide;
            float desiredDistance = Mathf.Lerp(normalDistance, aimDistance, aimBlend);

            // 1) Keep the shoulder point itself out of walls (e.g. hugging a wall on the camera side).
            Vector3 shoulderPoint = pivot + rotation * new Vector3(shoulder, verticalOffset, 0f);
            Vector3 toShoulder = shoulderPoint - pivot;
            if (Physics.SphereCast(pivot, collisionRadius, toShoulder.normalized, out RaycastHit sideHit,
                    toShoulder.magnitude, collisionMask, QueryTriggerInteraction.Ignore))
                shoulderPoint = pivot + toShoulder.normalized * sideHit.distance;

            // 2) Pull the camera in when geometry is between the shoulder point and the desired position.
            Vector3 back = rotation * Vector3.back;
            float allowed = desiredDistance;
            if (Physics.SphereCast(shoulderPoint, collisionRadius, back, out RaycastHit hit,
                    desiredDistance, collisionMask, QueryTriggerInteraction.Ignore))
                allowed = Mathf.Max(minDistance, hit.distance);

            currentDistance = allowed < currentDistance
                ? allowed // snap in immediately so we never see through a wall
                : Mathf.Lerp(currentDistance, allowed, 1f - Mathf.Exp(-pushOutSharpness * dt));

            transform.SetPositionAndRotation(shoulderPoint + back * currentDistance, rotation);
            if (cam) cam.fieldOfView = Mathf.Lerp(normalFov, aimFov, aimBlend);
        }
    }
}
