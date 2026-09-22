using Deadlands.Core.Interaction;
using Deadlands.Core.Input;
using UnityEngine;

namespace Deadlands.Player
{
    /// <summary>
    /// Finds the best interactable in front of the player each frame and uses it on the Interact button.
    /// The HUD reads <see cref="Current"/> to show the prompt.
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] float radius = 1.6f;
        [Tooltip("Max angle from the player's facing for something to be selectable.")]
        [SerializeField] float maxAngle = 75f;
        [SerializeField] LayerMask mask = ~0;

        readonly Collider[] hits = new Collider[16];

        public IInteractable Current { get; private set; }

        void OnEnable() => input.InteractPressed += OnInteract;
        void OnDisable() => input.InteractPressed -= OnInteract;

        void Update()
        {
            Current = null;
            if (motor.IsMovementLocked || motor.IsDodging) return;

            Vector3 origin = transform.position + Vector3.up * 0.9f;
            int count = Physics.OverlapSphereNonAlloc(origin, radius, hits, mask, QueryTriggerInteraction.Collide);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var candidate = hits[i].GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject)) continue;

                Vector3 to = candidate.transform.position - transform.position;
                to.y = 0f;
                float angle = Vector3.Angle(transform.forward, to);
                if (to.magnitude > 0.5f && angle > maxAngle) continue;

                float score = to.magnitude + angle * 0.01f;
                if (score < best)
                {
                    best = score;
                    Current = candidate;
                }
            }
        }

        void OnInteract()
        {
            if (Current != null && Current.CanInteract(gameObject)) Current.Interact(gameObject);
        }
    }
}
