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
        InputAction move, look, sprint, aim, attack;

        public InputActionAsset Actions => actions;
        public ControlSettings Settings => controlSettings;

        /// <summary>Movement intent, magnitude 0-1.</summary>
        public Vector2 Move { get; private set; }
        /// <summary>Camera rotation requested this frame, in degrees (x = yaw, y = pitch). Sensitivity applied.</summary>
        public Vector2 LookDegrees { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool AimHeld { get; private set; }
        /// <summary>Attack/fire button held (automatic weapons).</summary>
        public bool AttackHeld { get; private set; }

        public event Action DodgePressed;
        public event Action SwapShoulderPressed;
        public event Action AttackPressed;
        public event Action ReloadPressed;
        public event Action NextWeaponPressed;
        public event Action PrevWeaponPressed;
        /// <summary>Weapon slot chosen directly (0-based).</summary>
        public event Action<int> SlotPressed;
        public event Action StrugglePressed;
        public event Action InteractPressed;
        public event Action UseHealPressed;
        public event Action InventoryPressed;
        public event Action ShovePressed;

        void Awake()
        {
            InputBindingStore.Load(actions);
            map = actions.FindActionMap(actionMapName, throwIfNotFound: true);
            move = map.FindAction("Move", true);
            look = map.FindAction("Look", true);
            sprint = map.FindAction("Sprint", true);
            aim = map.FindAction("Aim", true);
            attack = map.FindAction("Attack", true);
        }

        void OnEnable()
        {
            Bind("Dodge", () => DodgePressed?.Invoke());
            Bind("SwapShoulder", () => SwapShoulderPressed?.Invoke());
            Bind("Attack", () => AttackPressed?.Invoke());
            Bind("Reload", () => ReloadPressed?.Invoke());
            Bind("NextWeapon", () => NextWeaponPressed?.Invoke());
            Bind("PrevWeapon", () => PrevWeaponPressed?.Invoke());
            Bind("Struggle", () => StrugglePressed?.Invoke());
            Bind("Interact", () => InteractPressed?.Invoke());
            Bind("UseHeal", () => UseHealPressed?.Invoke());
            Bind("Inventory", () => InventoryPressed?.Invoke());
            Bind("Shove", () => ShovePressed?.Invoke());
            for (int i = 0; i < 4; i++)
            {
                int slot = i;
                Bind("Slot" + (i + 1), () => SlotPressed?.Invoke(slot));
            }
            map.Enable();
        }

        void OnDisable()
        {
            foreach (var (action, handler) in bound) action.performed -= handler;
            bound.Clear();
            map.Disable();
            Move = Vector2.zero;
            LookDegrees = Vector2.zero;
            SprintHeld = AimHeld = AttackHeld = false;
        }

        readonly System.Collections.Generic.List<(InputAction, Action<InputAction.CallbackContext>)> bound =
            new System.Collections.Generic.List<(InputAction, Action<InputAction.CallbackContext>)>();

        void Bind(string actionName, Action callback)
        {
            InputAction action = map.FindAction(actionName, true);
            Action<InputAction.CallbackContext> handler = _ => callback();
            action.performed += handler;
            bound.Add((action, handler));
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            SprintHeld = sprint.IsPressed();
            AimHeld = aim.IsPressed();
            AttackHeld = attack.IsPressed();
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
    }
}
