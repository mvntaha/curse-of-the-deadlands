using Deadlands.Core.Combat;
using Deadlands.Core.Pooling;
using UnityEngine;

namespace Deadlands.Core
{
    /// <summary>
    /// Development-only readout (player HP, pool stats) for test scenes. Not the game HUD — that's Phase 8.
    /// </summary>
    public class DebugOverlay : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] GameObjectPool[] pools;

        GUIStyle style;

        void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
            float y = 10f;
            if (playerHealth)
                GUI.Label(new Rect(10, y, 400, 24), $"Player HP: {playerHealth.Current:0}/{playerHealth.Max:0}{(playerHealth.IsDead ? "  (DEAD)" : "")}", style);
            foreach (var pool in pools)
            {
                if (!pool) continue;
                y += 22f;
                GUI.Label(new Rect(10, y, 500, 24), $"Pool {pool.Prefab.name}: active {pool.CountActive}, idle {pool.CountInactive}, created {pool.TotalCreated}", style);
            }
        }
    }
}
