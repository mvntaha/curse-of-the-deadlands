using UnityEngine;

namespace Deadlands.Combat
{
    public enum WeaponKind { Ranged, Melee }
    public enum FireMode { SemiAuto, FullAuto }
    public enum AmmoType { None, PistolRounds, ShotgunShells, SmgRounds }

    /// <summary>All tuning for one weapon. New weapons are new assets, not new code.</summary>
    [CreateAssetMenu(fileName = "WeaponData", menuName = "Deadlands/Combat/Weapon Data")]
    public class WeaponData : ScriptableObject
    {
        public string displayName = "Weapon";
        public WeaponKind kind = WeaponKind.Ranged;
        [Tooltip("Name of the weapon mesh already parented to the character's hand (ZAK rig). Empty = no model (fists).")]
        public string handModelName;
        [Tooltip("Uniform scale applied to the hand model (the kit's weapons are oversized).")]
        public float handModelScale = 1f;

        [Header("Damage")]
        public float damage = 20f;
        [Tooltip("Physics push applied to a ragdoll on a killing blow.")]
        public float knockbackForce = 6f;

        [Header("Ranged")]
        public FireMode fireMode = FireMode.SemiAuto;
        public AmmoType ammoType = AmmoType.PistolRounds;
        public int magazineSize = 12;
        [Tooltip("Seconds between shots.")]
        public float fireInterval = 0.25f;
        public float reloadTime = 1.4f;
        public float range = 40f;
        [Tooltip("Rays per shot (shotgun pellets).")]
        public int pellets = 1;
        [Tooltip("Cone half-angle in degrees.")]
        public float spread = 1f;
        [Tooltip("Degrees of procedural upper-body kick per shot.")]
        public float recoilKick = 4f;

        [Header("Melee")]
        public float swingDuration = 0.8f;
        [Tooltip("Seconds into the swing when the hit is checked.")]
        public float swingHitTime = 0.35f;
        public float reach = 1.1f;
        public float hitRadius = 0.7f;
        [Tooltip("0 = never breaks (fists).")]
        public int maxDurability = 20;
        [Tooltip("Durability lost per swing that connects.")]
        public int durabilityLossPerHit = 1;

        public bool IsRanged => kind == WeaponKind.Ranged;
        public bool Breakable => kind == WeaponKind.Melee && maxDurability > 0;
    }
}
