using UnityEngine;

namespace Deadlands.Core.Combat
{
    /// <summary>Something that can grab a victim (e.g. a zombie).</summary>
    public interface IGrabber
    {
        Transform transform { get; }
        /// <summary>Called by the victim when the grab ends.</summary>
        void OnGrabEnded(bool victimEscaped);
    }

    /// <summary>Something that can be grabbed and struggle free (the player).</summary>
    public interface IGrabbable
    {
        bool CanBeGrabbed { get; }
        void BeginGrab(IGrabber grabber, in GrabSettings settings);
        /// <summary>Ends a grab from the grabber's side (e.g. the zombie was killed mid-grab).</summary>
        void ReleaseFrom(IGrabber grabber);
    }

    public readonly struct GrabSettings
    {
        /// <summary>Button presses needed to break free.</summary>
        public readonly int PressesToEscape;
        /// <summary>Seconds before the grab ends in a bite if not escaped.</summary>
        public readonly float Duration;
        public readonly float DamagePerSecond;
        /// <summary>Damage dealt if the victim fails to escape in time.</summary>
        public readonly float FailDamage;

        public GrabSettings(int pressesToEscape, float duration, float damagePerSecond, float failDamage)
        {
            PressesToEscape = pressesToEscape;
            Duration = duration;
            DamagePerSecond = damagePerSecond;
            FailDamage = failDamage;
        }
    }

    /// <summary>Lets a target scale damage by where it was hit (e.g. headshots). Implemented by enemies.</summary>
    public interface IHitZoneProvider
    {
        float GetDamageMultiplier(Vector3 worldPoint, out bool isCritical);
    }
}
