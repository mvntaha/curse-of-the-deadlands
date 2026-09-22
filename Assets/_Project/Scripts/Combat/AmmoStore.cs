using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadlands.Combat
{
    /// <summary>
    /// Reserve (not-yet-loaded) ammunition per type, with hard caps to keep ammo scarce.
    /// Phase 4's inventory uses this as the ammo part of the player's inventory.
    /// </summary>
    public class AmmoStore : MonoBehaviour
    {
        [Serializable]
        public struct AmmoEntry
        {
            public AmmoType type;
            public int startAmount;
            public int maxCarry;
        }

        [SerializeField] AmmoEntry[] ammo =
        {
            new AmmoEntry { type = AmmoType.PistolRounds, startAmount = 24, maxCarry = 60 },
            new AmmoEntry { type = AmmoType.ShotgunShells, startAmount = 6, maxCarry = 20 },
            new AmmoEntry { type = AmmoType.SmgRounds, startAmount = 40, maxCarry = 120 },
        };

        readonly Dictionary<AmmoType, int> reserve = new Dictionary<AmmoType, int>();
        readonly Dictionary<AmmoType, int> caps = new Dictionary<AmmoType, int>();

        public event Action<AmmoType, int> Changed;

        void Awake()
        {
            foreach (var e in ammo)
            {
                reserve[e.type] = Mathf.Min(e.startAmount, e.maxCarry);
                caps[e.type] = e.maxCarry;
            }
        }

        public int Get(AmmoType type) => reserve.TryGetValue(type, out int v) ? v : 0;
        public int Cap(AmmoType type) => caps.TryGetValue(type, out int v) ? v : 0;

        /// <summary>Adds ammo up to the carry cap. Returns how much was actually added.</summary>
        public int Add(AmmoType type, int amount)
        {
            if (type == AmmoType.None || amount <= 0) return 0;
            int current = Get(type);
            int added = Mathf.Min(amount, Mathf.Max(0, Cap(type) - current));
            if (added <= 0) return 0;
            reserve[type] = current + added;
            Changed?.Invoke(type, reserve[type]);
            return added;
        }

        /// <summary>Removes up to <paramref name="amount"/>. Returns how much was taken.</summary>
        public int Take(AmmoType type, int amount)
        {
            int current = Get(type);
            int taken = Mathf.Min(amount, current);
            if (taken <= 0) return 0;
            reserve[type] = current - taken;
            Changed?.Invoke(type, reserve[type]);
            return taken;
        }
    }
}
