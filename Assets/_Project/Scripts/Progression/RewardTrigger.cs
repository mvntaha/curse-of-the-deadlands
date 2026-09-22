using System;
using UnityEngine;

namespace Deadlands.Progression
{
    /// <summary>
    /// Grants upgrades when the player enters it. Used as a debug/test trigger now; Phase 5's mission
    /// completion grants rewards through the same <see cref="PlayerProgression.Grant"/> call.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class RewardTrigger : MonoBehaviour
    {
        [SerializeField] UpgradeData[] rewards;
        [SerializeField] bool oneShot = true;
        [SerializeField] GameObject visualToHideWhenUsed;

        bool used;

        public event Action<RewardTrigger> Triggered;

        void Awake() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            if (used && oneShot) return;
            var progression = other.GetComponentInParent<PlayerProgression>();
            if (!progression) return;

            foreach (var r in rewards) progression.Grant(r);
            used = true;
            if (oneShot && visualToHideWhenUsed) visualToHideWhenUsed.SetActive(false);
            Triggered?.Invoke(this);
        }
    }
}
