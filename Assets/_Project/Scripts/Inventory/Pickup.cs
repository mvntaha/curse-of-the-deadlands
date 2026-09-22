using System;
using Deadlands.Core.Interaction;
using UnityEngine;

namespace Deadlands.Inventory
{
    /// <summary>
    /// World item. Either collected automatically on contact (ammo, health) or via the Interact button
    /// (weapons, key items). Stays in the world if the player can't carry any more.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Pickup : MonoBehaviour, IInteractable
    {
        [SerializeField] ItemData item;
        [SerializeField] int amount = 1;
        [Tooltip("Collected by walking into it instead of pressing Interact.")]
        [SerializeField] bool collectOnContact;
        [Tooltip("Optional visual that gently bobs/spins so items read as collectible in the dark.")]
        [SerializeField] Transform visual;
        [SerializeField] float spinSpeed = 45f;
        [SerializeField] float bobHeight = 0.05f;

        Vector3 visualBase;

        public ItemData Item => item;
        public int Amount => amount;
        public string Prompt => $"Pick up {item.displayName}{(amount > 1 && item.kind != ItemKind.Weapon ? $" x{amount}" : "")}";

        /// <summary>Raised when fully collected (before the pickup deactivates).</summary>
        public event Action<Pickup> Collected;

        void Awake()
        {
            GetComponent<Collider>().isTrigger = true;
            if (visual) visualBase = visual.localPosition;
        }

        void Update()
        {
            if (!visual) return;
            visual.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
            visual.localPosition = visualBase + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobHeight);
        }

        public bool CanInteract(GameObject interactor) =>
            !collectOnContact && isActiveAndEnabled && interactor.GetComponentInParent<PlayerInventory>();

        public void Interact(GameObject interactor) => TryCollect(interactor.GetComponentInParent<PlayerInventory>());

        void OnTriggerEnter(Collider other)
        {
            if (!collectOnContact) return;
            var inventory = other.GetComponentInParent<PlayerInventory>();
            if (inventory) TryCollect(inventory);
        }

        void TryCollect(PlayerInventory inventory)
        {
            if (!inventory || !item) return;
            int taken = inventory.Add(item, amount);
            if (taken <= 0) return;
            amount -= taken;
            if (amount > 0 && item.kind != ItemKind.Weapon) return; // partially taken; the rest stays
            Collected?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
