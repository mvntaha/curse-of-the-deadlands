using UnityEngine;

namespace Deadlands.Core.Interaction
{
    /// <summary>
    /// Anything the player can use with the Interact button: pickups now, switches/statues/doors later.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Short verb phrase for the prompt, e.g. "Pick up Shotgun".</summary>
        string Prompt { get; }
        Transform transform { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
