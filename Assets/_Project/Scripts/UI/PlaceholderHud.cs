using Deadlands.Combat;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using Deadlands.Core.Pooling;
using Deadlands.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Deadlands.UI
{
    /// <summary>
    /// PLACEHOLDER HUD (IMGUI) so combat is readable while testing: HP, weapon/ammo, melee durability,
    /// crosshair while aiming, struggle prompt, and dev pool stats. Replaced by the real horror HUD in Phase 8.
    /// </summary>
    public class PlaceholderHud : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] PlayerWeaponController weapons;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerStruggle struggle;
        [SerializeField] PlayerInputReader input;
        [SerializeField] bool showPoolStats = true;
        [SerializeField] GameObjectPool[] pools;

        GUIStyle label, big;
        Texture2D white;

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
                big = new GUIStyle(label) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
                white = Texture2D.whiteTexture;
            }

            float y = 10f;
            if (playerHealth)
            {
                Line(ref y, $"HP {playerHealth.Current:0}/{playerHealth.Max:0}{(playerHealth.IsDead ? "  (DEAD)" : "")}");
                Bar(10f, y, 200f, 8f, playerHealth.Normalized, new Color(0.6f, 0.05f, 0.05f));
                y += 14f;
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

                string slots = "";
                for (int i = 0; i < weapons.Loadout.Count; i++)
                {
                    var s = weapons.Loadout[i];
                    slots += $"[{i + 1}] {s.data.displayName}{(s.IsBroken ? " (broken)" : "")}   ";
                }
                Line(ref y, slots);

                if (motor && motor.IsAiming && w.data.IsRanged) DrawCrosshair();
            }

            if (struggle && struggle.IsGrabbed)
            {
                string key = input ? input.Actions.FindAction("Struggle").GetBindingDisplayString() : "E";
                var r = new Rect(Screen.width / 2f - 250f, Screen.height * 0.65f, 500f, 40f);
                GUI.Label(r, $"GRABBED!  MASH [{key}]", big);
                Bar(Screen.width / 2f - 150f, r.yMax + 6f, 300f, 12f, struggle.Progress, new Color(0.9f, 0.8f, 0.3f));
            }

            if (showPoolStats && pools != null)
                foreach (var pool in pools)
                    if (pool) Line(ref y, $"[dev] pool {pool.Prefab.name}: active {pool.CountActive}, idle {pool.CountInactive}, created {pool.TotalCreated}");
        }

        void Line(ref float y, string text)
        {
            GUI.Label(new Rect(10f, y, 900f, 24f), text, label);
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
