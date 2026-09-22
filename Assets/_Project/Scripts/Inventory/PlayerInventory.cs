using System;
using System.Collections.Generic;
using Deadlands.Combat;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using UnityEngine;

namespace Deadlands.Inventory
{
    /// <summary>
    /// The player's inventory: consumables and key items (stacks), plus the ammo reserve (<see cref="AmmoStore"/>)
    /// and weapon slots (<see cref="PlayerWeaponController"/>) it fronts for. Quick-use: heal button.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] AmmoStore ammo;
        [SerializeField] PlayerWeaponController weapons;
        [SerializeField] Health health;
        [Tooltip("The consumable used by the quick-heal button.")]
        [SerializeField] ItemData quickHealItem;
        [SerializeField] float useCooldown = 0.8f;

        [Serializable]
        struct StartingItem
        {
            public ItemData item;
            public int count;
        }

        [SerializeField] StartingItem[] startingItems;

        readonly Dictionary<ItemData, int> stacks = new Dictionary<ItemData, int>();
        float nextUseTime;

        public AmmoStore Ammo => ammo;
        public PlayerWeaponController Weapons => weapons;
        public ItemData QuickHealItem => quickHealItem;
        public IReadOnlyDictionary<ItemData, int> Items => stacks;

        /// <summary>(item, newCount)</summary>
        public event Action<ItemData, int> ItemChanged;
        /// <summary>(item, amountAdded) — for pickup feedback.</summary>
        public event Action<ItemData, int> ItemAcquired;
        public event Action<ItemData> ItemUsed;

        void Awake()
        {
            foreach (var s in startingItems)
                if (s.item) Add(s.item, s.count, silent: true);
        }

        void OnEnable() => input.UseHealPressed += UseQuickHeal;
        void OnDisable() => input.UseHealPressed -= UseQuickHeal;

        public int Count(ItemData item) => item && stacks.TryGetValue(item, out int n) ? n : 0;

        public bool HasKey(string itemId)
        {
            foreach (var kv in stacks)
                if (kv.Value > 0 && kv.Key.id == itemId) return true;
            return false;
        }

        /// <summary>
        /// Adds an item of any kind, routing ammo and weapons to their systems. Returns how much was actually taken
        /// (0 if full), so pickups can stay in the world when the player can't carry more.
        /// </summary>
        public int Add(ItemData item, int amount, bool silent = false)
        {
            if (!item || amount <= 0) return 0;
            int added;
            switch (item.kind)
            {
                case ItemKind.Ammo:
                    added = ammo.Add(item.ammoType, amount);
                    break;
                case ItemKind.Weapon:
                    added = AddWeapon(item, amount) ? 1 : 0;
                    break;
                default:
                    int current = Count(item);
                    added = Mathf.Min(amount, Mathf.Max(0, item.maxStack - current));
                    if (added > 0)
                    {
                        stacks[item] = current + added;
                        ItemChanged?.Invoke(item, stacks[item]);
                    }
                    break;
            }
            if (added > 0 && !silent) ItemAcquired?.Invoke(item, added);
            return added;
        }

        bool AddWeapon(ItemData item, int bonusAmmo)
        {
            if (!item.weapon) return false;
            bool alreadyCarried = false;
            foreach (var w in weapons.Loadout)
                if (w.data == item.weapon) { alreadyCarried = true; break; }

            if (!alreadyCarried)
            {
                weapons.AddWeapon(item.weapon);
                return true;
            }
            // Duplicate weapon pickup: melee = repair/replace, firearm = its ammo.
            if (item.weapon.Breakable)
            {
                weapons.RestoreDurability(item.weapon);
                return true;
            }
            return ammo.Add(item.weapon.ammoType, Mathf.Max(bonusAmmo, item.weapon.magazineSize)) > 0;
        }

        public bool Remove(ItemData item, int amount = 1)
        {
            int current = Count(item);
            if (current < amount) return false;
            stacks[item] = current - amount;
            ItemChanged?.Invoke(item, stacks[item]);
            return true;
        }

        public bool CanUse(ItemData item) =>
            item && item.kind == ItemKind.Consumable && Count(item) > 0 && Time.time >= nextUseTime
            && health && !health.IsDead && health.Current < health.Max;

        public bool Use(ItemData item)
        {
            if (!CanUse(item)) return false;
            Remove(item);
            health.Heal(item.healAmount);
            nextUseTime = Time.time + useCooldown;
            ItemUsed?.Invoke(item);
            return true;
        }

        void UseQuickHeal() => Use(quickHealItem);
    }
}
