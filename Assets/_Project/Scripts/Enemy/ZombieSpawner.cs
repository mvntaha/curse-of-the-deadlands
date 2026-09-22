using System.Collections;
using Deadlands.Core.Pooling;
using UnityEngine;
using UnityEngine.AI;

namespace Deadlands.Enemy
{
    /// <summary>Keeps up to <c>maxAlive</c> zombies alive at a set of spawn points, drawing them from a pool.</summary>
    public class ZombieSpawner : MonoBehaviour
    {
        [SerializeField] GameObjectPool pool;
        [SerializeField] Transform[] spawnPoints;
        [SerializeField] int maxAlive = 3;
        [SerializeField] bool spawnOnStart = true;
        [SerializeField] bool respawn = true;
        [SerializeField] float respawnDelay = 3f;

        int nextPoint;

        public int Alive { get; private set; }
        public int TotalSpawned { get; private set; }
        public GameObjectPool Pool => pool;

        void Start()
        {
            if (!spawnOnStart) return;
            for (int i = 0; i < maxAlive; i++) SpawnOne();
        }

        public ZombieAI SpawnOne()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;
            Transform point = spawnPoints[nextPoint++ % spawnPoints.Length];
            Vector3 pos = point.position;
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 2f, NavMesh.AllAreas)) pos = hit.position;

            GameObject go = pool.Spawn(pos, point.rotation);
            var ai = go.GetComponent<ZombieAI>();
            ai.Despawned += OnZombieDespawned;
            Alive++;
            TotalSpawned++;
            return ai;
        }

        void OnZombieDespawned(ZombieAI ai)
        {
            ai.Despawned -= OnZombieDespawned;
            Alive--;
            if (respawn && isActiveAndEnabled) StartCoroutine(RespawnAfterDelay());
        }

        IEnumerator RespawnAfterDelay()
        {
            yield return new WaitForSeconds(respawnDelay);
            if (Alive < maxAlive) SpawnOne();
        }
    }
}
