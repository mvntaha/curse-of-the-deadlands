using System;
using System.Collections.Generic;
using Deadlands.Combat;
using Deadlands.Core.Combat;
using Deadlands.Player;
using UnityEngine;

namespace Deadlands.Progression
{
    /// <summary>
    /// Applies upgrades/unlocks to the player's systems and remembers what's been granted
    /// (so saves can restore it and non-stackable rewards aren't applied twice).
    /// </summary>
    public class PlayerProgression : MonoBehaviour
    {
        [SerializeField] Health health;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] PlayerWeaponController weapons;
        [SerializeField] ShoveAbility shove;

        readonly List<UpgradeData> granted = new List<UpgradeData>();

        public IReadOnlyList<UpgradeData> Granted => granted;
        public event Action<UpgradeData> UpgradeGranted;

        public bool Has(UpgradeData upgrade) => granted.Contains(upgrade);

        /// <summary>Grants and applies an upgrade. Returns false if it was already owned and isn't stackable.</summary>
        public bool Grant(UpgradeData upgrade)
        {
            if (!upgrade || (!upgrade.stackable && Has(upgrade))) return false;
            Apply(upgrade);
            granted.Add(upgrade);
            UpgradeGranted?.Invoke(upgrade);
            return true;
        }

        void Apply(UpgradeData u)
        {
            switch (u.kind)
            {
                case UpgradeKind.MaxHealth:
                    health.SetMax(health.Max + u.value);
                    break;
                case UpgradeKind.MaxStamina:
                    stamina.SetMax(stamina.Max + u.value);
                    break;
                case UpgradeKind.StaminaRegen:
                    stamina.SetRegen(stamina.RegenPerSecond * u.value);
                    break;
                case UpgradeKind.ReloadSpeed:
                    weapons.ReloadTimeMultiplier *= u.value;
                    break;
                case UpgradeKind.MeleeDamage:
                    weapons.MeleeDamageMultiplier *= u.value;
                    break;
                case UpgradeKind.WeaponUnlock:
                    if (u.weapon) weapons.AddWeapon(u.weapon);
                    break;
                case UpgradeKind.AbilityShove:
                    shove.Unlock();
                    break;
            }
        }
    }
}
