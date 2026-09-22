using System;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Being grabbed by a zombie: movement and actions lock, the zombie bites over time, and the player
    /// mashes the Struggle button to break free before the grab ends in a heavy bite.
    /// </summary>
    public class PlayerStruggle : MonoBehaviour, IGrabbable
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAnimationController animationController;
        [SerializeField] Health health;
        [Tooltip("Seconds after a grab ends during which the player can't be grabbed again.")]
        [SerializeField] float regrabImmunity = 2f;
        [SerializeField] float biteTickInterval = 0.5f;

        IGrabber grabber;
        GrabSettings settings;
        float elapsed;
        float tickTimer;
        float immuneUntil;
        int presses;

        public bool IsGrabbed => grabber != null;
        public float Progress => IsGrabbed ? Mathf.Clamp01(presses / (float)Mathf.Max(1, settings.PressesToEscape)) : 0f;
        public float TimeRemaining => IsGrabbed ? Mathf.Max(0f, settings.Duration - elapsed) : 0f;
        public int Presses => presses;

        public bool CanBeGrabbed => enabled && !IsGrabbed && !motor.IsDodging && !health.IsDead && Time.time >= immuneUntil;

        public event Action GrabStarted;
        /// <summary>True = escaped, false = bitten.</summary>
        public event Action<bool> GrabEnded;

        void OnEnable() => input.StrugglePressed += OnStrugglePressed;
        void OnDisable() => input.StrugglePressed -= OnStrugglePressed;

        public void BeginGrab(IGrabber by, in GrabSettings grabSettings)
        {
            if (!CanBeGrabbed) return;
            grabber = by;
            settings = grabSettings;
            elapsed = 0f;
            tickTimer = 0f;
            presses = 0;
            motor.PushMovementLock();
            motor.FaceDirection(by.transform.position - transform.position);
            animationController.SetGrabbed(true);
            GrabStarted?.Invoke();
        }

        public void ReleaseFrom(IGrabber by)
        {
            if (grabber == by) End(escaped: true, notifyGrabber: false);
        }

        void OnStrugglePressed()
        {
            if (!IsGrabbed) return;
            presses++;
            if (presses >= settings.PressesToEscape) End(escaped: true, notifyGrabber: true);
        }

        void Update()
        {
            if (!IsGrabbed) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            tickTimer += dt;

            if (tickTimer >= biteTickInterval)
            {
                tickTimer -= biteTickInterval;
                Bite(settings.DamagePerSecond * biteTickInterval);
            }

            if (health.IsDead) { End(escaped: false, notifyGrabber: true); return; }

            if (elapsed >= settings.Duration)
            {
                Bite(settings.FailDamage);
                End(escaped: false, notifyGrabber: true);
            }
        }

        void Bite(float amount)
        {
            if (amount <= 0f || grabber == null) return;
            Vector3 dir = transform.position - grabber.transform.position;
            health.TakeDamage(new DamageInfo(amount, transform.position + Vector3.up * 1.5f, dir, 0f, grabber.transform.gameObject));
        }

        void End(bool escaped, bool notifyGrabber)
        {
            if (grabber == null) return;
            var by = grabber;
            grabber = null;
            motor.PopMovementLock();
            animationController.SetGrabbed(false);
            immuneUntil = Time.time + regrabImmunity;
            if (notifyGrabber) by.OnGrabEnded(escaped);
            GrabEnded?.Invoke(escaped);
        }
    }
}
