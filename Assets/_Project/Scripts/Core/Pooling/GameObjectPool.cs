using UnityEngine;
using UnityEngine.Pool;

namespace Deadlands.Core.Pooling
{
    /// <summary>Implemented by components that need to reset themselves when taken from / returned to a pool.</summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }

    /// <summary>
    /// Pool for one prefab. Anything spawned frequently (zombies, projectiles, VFX, corpses) goes through a pool
    /// instead of Instantiate/Destroy. Objects return themselves via <see cref="PooledObject.Release"/>.
    /// </summary>
    public class GameObjectPool : MonoBehaviour
    {
        [SerializeField] GameObject prefab;
        [SerializeField] int prewarmCount = 8;
        [SerializeField] int maxSize = 64;

        ObjectPool<GameObject> pool;

        public GameObject Prefab => prefab;
        public int CountActive => pool?.CountActive ?? 0;
        public int CountInactive => pool?.CountInactive ?? 0;
        /// <summary>Total instances ever created — should stop growing after prewarm if the pool is sized right.</summary>
        public int TotalCreated { get; private set; }

        void Awake()
        {
            pool = new ObjectPool<GameObject>(Create, null, OnRelease, OnDestroyPooled,
                collectionCheck: true, defaultCapacity: prewarmCount, maxSize: maxSize);

            var warm = new GameObject[prewarmCount];
            for (int i = 0; i < prewarmCount; i++) warm[i] = pool.Get();
            for (int i = 0; i < prewarmCount; i++) pool.Release(warm[i]);
        }

        public GameObject Spawn(Vector3 position, Quaternion rotation)
        {
            // Position before activating so components (e.g. NavMeshAgent) never wake up at a stale spot.
            GameObject go = pool.Get();
            go.transform.SetPositionAndRotation(position, rotation);
            go.SetActive(true);
            foreach (var p in go.GetComponents<IPoolable>()) p.OnSpawned();
            return go;
        }

        public void Despawn(GameObject go)
        {
            foreach (var p in go.GetComponents<IPoolable>()) p.OnDespawned();
            pool.Release(go);
        }

        GameObject Create()
        {
            GameObject go = Instantiate(prefab, transform);
            go.SetActive(false);
            go.AddComponent<PooledObject>().Owner = this;
            TotalCreated++;
            return go;
        }

        static void OnRelease(GameObject go) => go.SetActive(false);
        static void OnDestroyPooled(GameObject go) => Destroy(go);
    }
}
