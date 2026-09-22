using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadlands.Core.Input
{
    /// <summary>
    /// Single entry point between the Input System and gameplay. Gameplay scripts read intent from here
    /// (never from devices directly), so every binding stays remappable through the InputActionAsset.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] ControlSettings controlSettings;
        [SerializeField] string actionMapName = "Player";

        InputActionMap map;
        InputAction move, look, sprint, aim, dodge, swapShoulder, attack;

        public InputActionAsset Actions => actions;
        public ControlSettings Settings => controlSettings;

        /// <summary>Movement intent, magnitude 0-1.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Camera rotation requested this frame, in degrees (x = yaw, y = pitch). Sensitivity applied.</summary>
        public Vector2 LookDegrees { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AimHeld { get; private set; }

        public event Action DodgePressed;
        public event Action SwapShoulderPressed;
        public event Action AttackPressed;

        void Awake()
        {
            InputBindingStore.Load(actions);
            map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            aim = map.FindAction("Aim", true);
            dodge = map.FindAction("Dodge", true);
            swapShoulder = map.FindAction("SwapShoulder", true);
            attack = map.FindAction("Attack", true);
        }

        void OnEnable()
        {
            dodge.performed += OnDodge;
            swapShoulder.performed += OnSwapShoulder;
            attack.performed += OnAttack;
            map.Enable();
        }

        void OnDisable()
        {
            dodge.performed -= OnDodge;
            swapShoulder.performed -= OnSwapShoulder;
            attack.performed -= OnAttack;
            map.Disable();
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            SprintHeld = sprint.IsPressed();
            AimHeld = aim.IsPressed();
            LookDegrees = ReadLook();
        }

        Vector2 ReadLook()
        {
            Vector2 raw = look.ReadValue<Vector2>();
            if (raw == Vector2.zero) return Vector2.zero;

            // Mouse delta is already per-frame; stick values are rates and need deltaTime.
            bool fromGamepad = look.activeControl != null && look.activeControl.device is Gamepad;
            Vector2 degrees = fromGamepad
                ? raw * (controlSettings.gamepadSensitivity * Time.unscaledDeltaTime)
                : raw * controlSettings.mouseSensitivity;

            if (AimHeld) degrees *= controlSettings.aimSensitivityMultiplier;
            if (controlSettings.invertY) degrees.y = -degrees.y;
            return degrees;
        }

        void OnDodge(InputAction.CallbackContext _) => DodgePressed?.Invoke();
        void OnSwapShoulder(InputAction.CallbackContext _) => SwapShoulderPressed?.Invoke();
        void OnAttack(InputAction.CallbackContext _) => AttackPressed?.Invoke();
    }
}
