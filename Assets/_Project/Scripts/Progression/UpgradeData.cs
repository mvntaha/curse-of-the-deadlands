using Deadlands.Combat;
using UnityEngine;

namespace Deadlands.Progression
{
    public enum UpgradeKind
    {
        MaxHealth,          // value = HP added
        MaxStamina,         // value = stamina added
        StaminaRegen,       // value = multiplier on regen (e.g. 1.25)
        ReloadSpeed,        // value = multiplier on reload time (e.g. 0.7 = 30% faster)
        MeleeDamage,        // value = multiplier on melee damage (e.g. 1.3)
        WeaponUnlock,       // weapon = unlocked weapon
        AbilityShove,       // unlocks the Shove combat ability
    }

    /// <summary>One reward/upgrade. Missions (Phase 5), achievements and pickups grant these by reference.</summary>
    [CreateAssetMenu(fileName = "UpgradeData", menuName = "Deadlands/Progression/Upgrade Data")]
    public class UpgradeData : ScriptableObject
    {
        [Tooltip("Stable id used by saves.")]
        public string id = "upgrade";
        public string displayName = "Upgrade";
        [TextArea] public string description;
        public UpgradeKind kind;
        public float value = 1f;
        public WeaponData weapon;
        [Tooltip("Can the same upgrade be granted more than once (stacking)?")]
        public bool stackable;
    }
}
