using Deadlands.Combat;
using UnityEngine;

namespace Deadlands.Inventory
{
    public enum ItemKind
    {
        /// <summary>Stackable, usable (health kits).</summary>
        Consumable,
        /// <summary>Mission items and keys. Not usable directly; checked by doors/objectives.</summary>
        KeyItem,
        /// <summary>Goes straight into the ammo reserve.</summary>
        Ammo,
        /// <summary>Adds a weapon to the loadout (or refills its ammo / repairs it if already carried).</summary>
        Weapon,
    }

    /// <summary>Definition of one kind of item. Pickups and rewards reference these assets.</summary>
    [CreateAssetMenu(fileName = "ItemData", menuName = "Deadlands/Inventory/Item Data")]
    public class ItemData : ScriptableObject
    {
        [Tooltip("Stable id used by saves and mission objectives.")]
        public string id = "item";
        public string displayName = "Item";
        [TextArea] public string description;
        public ItemKind kind = ItemKind.Consumable;
        public int maxStack = 5;

        [Header("Consumable")]
        public float healAmount = 40f;

        [Header("Ammo")]
        public AmmoType ammoType = AmmoType.None;

        [Header("Weapon")]
        public WeaponData weapon;
    }
}
