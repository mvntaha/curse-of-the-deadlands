using System.Collections.Generic;
using Deadlands.Combat;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using Deadlands.Core.Pooling;
using Deadlands.Inventory;
using Deadlands.Player;
using Deadlands.Progression;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadlands.UI
{
    /// <summary>
    /// PLACEHOLDER HUD (IMGUI) so gameplay is readable while testing: HP/stamina, weapon/ammo, melee durability,
    /// crosshair, struggle + interaction prompts, quick-slot bar, pickup/upgrade toasts and a Tab inventory panel.
    /// Replaced by the real horror HUD/inventory UI in Phase 8.
    /// </summary>
    public class PlaceholderHud : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerStamina stamina;
        [SerializeField] PlayerWeaponController weapons;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerStruggle struggle;
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerInventory inventory;
        [SerializeField] PlayerProgression progression;
        [SerializeField] PlayerInteractor interactor;
        [SerializeField] ShoveAbility shove;
        [SerializeField] bool showPoolStats = true;
        [SerializeField] GameObjectPool[] pools;
        [SerializeField] float toastDuration = 2.5f;

        GUIStyle label, big, small, panelTitle;
        Texture2D white;
        bool inventoryOpen;
        readonly List<(string text, float until)> toasts = new List<(string, float)>();

        public bool InventoryOpen => inventoryOpen;

        void OnEnable()
        {
            if (input) input.InventoryPressed += ToggleInventory;
            if (inventory) inventory.ItemAcquired += OnItemAcquired;
            if (inventory) inventory.ItemUsed += OnItemUsed;
            if (progression) progression.UpgradeGranted += OnUpgrade;
            if (weapons) weapons.MeleeBroke += OnMeleeBroke;
        }

        void OnDisable()
        {
            if (input) input.InventoryPressed -= ToggleInventory;
            if (inventory) inventory.ItemAcquired -= OnItemAcquired;
            if (inventory) inventory.ItemUsed -= OnItemUsed;
            if (progression) progression.UpgradeGranted -= OnUpgrade;
            if (weapons) weapons.MeleeBroke -= OnMeleeBroke;
        }

        void ToggleInventory() => inventoryOpen = !inventoryOpen;
        void OnItemAcquired(ItemData item, int amount) => Toast($"+ {item.displayName}{(amount > 1 ? $" x{amount}" : "")}");
        void OnItemUsed(ItemData item) => Toast($"Used {item.displayName}");
        void OnUpgrade(UpgradeData u) => Toast($"UPGRADE: {u.displayName}");
        void OnMeleeBroke(WeaponInstance w) => Toast($"{w.data.displayName} BROKE!");

        void Toast(string text) => toasts.Add((text, Time.time + toastDuration));

        string Key(string action)
        {
            if (!input) return "?";
            var a = input.Actions.FindAction(action);
            return a != null ? a.GetBindingDisplayString(InputBinding.MaskByGroup("Keyboard&Mouse")) : "?";
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
                small = new GUIStyle(label) { fontSize = 13 };
                big = new GUIStyle(label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                panelTitle = new GUIStyle(label) { fontSize = 20, fontStyle = FontStyle.Bold };
                white = Texture2D.whiteTexture;
            }

            float y = 10f;
            if (playerHealth)
            {
                Line(ref y, $"HP {playerHealth.Current:0}/{playerHealth.Max:0}{(playerHealth.IsDead ? "  (DEAD)" : "")}");
                Bar(10f, y, 200f, 8f, playerHealth.Normalized, new Color(0.6f, 0.05f, 0.05f));
                y += 12f;
            }
            if (stamina)
            {
                Bar(10f, y, 200f * (stamina.Max / 100f), 5f, stamina.Normalized, stamina.IsExhausted ? new Color(0.5f, 0.3f, 0.1f) : new Color(0.75f, 0.7f, 0.3f));
                y += 12f;
            }

            if (weapons)
            {
                var w = weapons.Current;
                if (w.data.IsRanged)
                    Line(ref y, $"{w.data.displayName}  {w.ammoInMagazine}/{w.data.magazineSize}  | reserve {weapons.Ammo.Get(w.data.ammoType)}{(weapons.IsReloading ? "  RELOADING..." : "")}");
                else if (w.data.Breakable)
                {
                    float d = w.durability / (float)w.data.maxDurability;
                    Line(ref y, $"{w.data.displayName}  durability {w.durability}/{w.data.maxDurability}");
                    Bar(10f, y, 200f, 8f, d, Color.Lerp(new Color(0.8f, 0.2f, 0.1f), new Color(0.8f, 0.7f, 0.3f), d));
                    y += 14f;
                }
                else Line(ref y, w.data.displayName);

                if (motor && motor.IsAiming && w.data.IsRanged && !inventoryOpen) DrawCrosshair();
            }
            if (shove && shove.IsUnlocked)
                Line(ref y, shove.CooldownRemaining > 0f ? $"Shove [{Key("Shove")}] {shove.CooldownRemaining:0.0}s" : $"Shove [{Key("Shove")}] ready");

            DrawQuickSlots();

            if (interactor && interactor.Current != null && !(struggle && struggle.IsGrabbed))
                GUI.Label(new Rect(Screen.width / 2f - 250f, Screen.height * 0.62f, 500f, 30f), $"[{Key("Interact")}] {interactor.Current.Prompt}", new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 });

            if (struggle && struggle.IsGrabbed)
            {
                var r = new Rect(Screen.width / 2f - 250f, Screen.height * 0.65f, 500f, 40f);
                GUI.Label(r, $"GRABBED!  MASH [{Key("Struggle")}]", big);
                Bar(Screen.width / 2f - 150f, r.yMax + 6f, 300f, 12f, struggle.Progress, new Color(0.9f, 0.8f, 0.3f));
            }

            DrawToasts();
            if (inventoryOpen) DrawInventoryPanel();

            if (showPoolStats && pools != null)
                foreach (var pool in pools)
                    if (pool) Line(ref y, $"[dev] pool {pool.Prefab.name}: active {pool.CountActive}, idle {pool.CountInactive}, created {pool.TotalCreated}", small);
        }

        void DrawQuickSlots()
        {
            if (!weapons) return;
            const float slotW = 110f, slotH = 46f, gap = 6f;
            int slots = weapons.Loadout.Count + (inventory ? 1 : 0);
            float x = Screen.width / 2f - (slots * (slotW + gap)) / 2f;
            float y = Screen.height - slotH - 12f;
            for (int i = 0; i < weapons.Loadout.Count; i++)
            {
                var w = weapons.Loadout[i];
                bool selected = weapons.Current == w;
                string detail = w.data.IsRanged ? $"{w.ammoInMagazine}/{weapons.Ammo.Get(w.data.ammoType)}"
                              : w.data.Breakable ? (w.IsBroken ? "BROKEN" : $"{w.durability}/{w.data.maxDurability}") : "";
                Slot(x, y, slotW, slotH, $"[{i + 1}] {w.data.displayName}", detail, selected, w.IsBroken);
                x += slotW + gap;
            }
            if (inventory && inventory.QuickHealItem)
                Slot(x, y, slotW, slotH, $"[{Key("UseHeal")}] {inventory.QuickHealItem.displayName}", $"x{inventory.Count(inventory.QuickHealItem)}", false, inventory.Count(inventory.QuickHealItem) == 0);
        }

        void Slot(float x, float y, float w, float h, string title, string detail, bool selected, bool dim)
        {
            GUI.color = selected ? new Color(0.55f, 0.1f, 0.1f, 0.85f) : new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = dim ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
            GUI.Label(new Rect(x + 6f, y + 2f, w - 8f, 20f), title, small);
            GUI.Label(new Rect(x + 6f, y + 22f, w - 8f, 20f), detail, label);
            GUI.color = Color.white;
        }

        void DrawToasts()
        {
            toasts.RemoveAll(t => Time.time > t.until);
            float y = Screen.height * 0.3f;
            foreach (var t in toasts)
            {
                GUI.Label(new Rect(Screen.width - 420f, y, 400f, 26f), t.text, new GUIStyle(label) { alignment = TextAnchor.MiddleRight, fontSize = 18 });
                y += 26f;
            }
        }

        void DrawInventoryPanel()
        {
            // Two columns so everything fits above the quick-slot bar at 720p.
            var r = new Rect(Screen.width / 2f - 420f, 70f, 840f, Screen.height - 150f);
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.DrawTexture(r, white);
            GUI.color = Color.white;
            float y = r.y + 12f, x = r.x + 18f;
            GUI.Label(new Rect(x, y, 800f, 28f), $"INVENTORY   ([{Key("Inventory")}] to close)", panelTitle); y += 38f;
            float columnTop = y;

            GUI.Label(new Rect(x, y, 560f, 22f), "Weapons", panelTitle); y += 26f;
            foreach (var w in weapons.Loadout)
            {
                string detail = w.data.IsRanged ? $"mag {w.ammoInMagazine}/{w.data.magazineSize}" : w.data.Breakable ? $"durability {w.durability}/{w.data.maxDurability}{(w.IsBroken ? " (broken)" : "")}" : "";
                GUI.Label(new Rect(x + 10f, y, 540f, 22f), $"{w.data.displayName}   {detail}", label); y += 22f;
            }
            y += 8f;
            GUI.Label(new Rect(x, y, 560f, 22f), "Ammo", panelTitle); y += 26f;
            foreach (AmmoType t in System.Enum.GetValues(typeof(AmmoType)))
            {
                if (t == AmmoType.None) continue;
                GUI.Label(new Rect(x + 10f, y, 540f, 22f), $"{t}: {inventory.Ammo.Get(t)} / {inventory.Ammo.Cap(t)}", label); y += 22f;
            }
            // right column
            x = r.x + r.width / 2f + 10f;
            y = columnTop;
            GUI.Label(new Rect(x, y, 560f, 22f), "Items", panelTitle); y += 26f;
            bool any = false;
            foreach (var kv in inventory.Items)
            {
                if (kv.Value <= 0) continue;
                any = true;
                GUI.Label(new Rect(x + 10f, y, 540f, 22f), $"{kv.Key.displayName} x{kv.Value}{(kv.Key.kind == ItemKind.KeyItem ? "  (key item)" : "")}", label); y += 22f;
            }
            if (!any) { GUI.Label(new Rect(x + 10f, y, 540f, 22f), "(none)", label); y += 22f; }
            y += 8f;
            if (progression)
            {
                GUI.Label(new Rect(x, y, 560f, 22f), "Upgrades", panelTitle); y += 26f;
                if (progression.Granted.Count == 0) { GUI.Label(new Rect(x + 10f, y, 540f, 22f), "(none)", label); y += 22f; }
                foreach (var u in progression.Granted) { GUI.Label(new Rect(x + 10f, y, 400f, 22f), $"{u.displayName} — {u.description}", small); y += 20f; }
            }
        }

        void Line(ref float y, string text, GUIStyle style = null)
        {
            GUI.Label(new Rect(10f, y, 900f, 24f), text, style ?? label);
            y += 22f;
        }

        void Bar(float x, float y, float w, float h, float fill, Color color)
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(x, y, w, h), white);
            GUI.color = color;
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(fill), h), white);
            GUI.color = Color.white;
        }

        void DrawCrosshair()
        {
            float cx = Screen.width / 2f, cy = Screen.height / 2f;
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.DrawTexture(new Rect(cx - 1f, cy - 9f, 2f, 6f), white);
            GUI.DrawTexture(new Rect(cx - 1f, cy + 3f, 2f, 6f), white);
            GUI.DrawTexture(new Rect(cx - 9f, cy - 1f, 6f, 2f), white);
            GUI.DrawTexture(new Rect(cx + 3f, cy - 1f, 6f, 2f), white);
            GUI.color = Color.white;
        }
    }
}
