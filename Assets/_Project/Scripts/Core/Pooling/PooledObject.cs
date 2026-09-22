using UnityEngine;

namespace Deadlands.Core.Pooling
{
    /// <summary>Added automatically to pooled instances so they can return themselves without knowing the pool.</summary>
    public class PooledObject : MonoBehaviour
    {
        public GameObjectPool Owner { get; internal set; }

        public void Release()
        {
            if (Owner) Owner.Despawn(gameObject);
            else gameObject.SetActive(false);
        }
    }
}
