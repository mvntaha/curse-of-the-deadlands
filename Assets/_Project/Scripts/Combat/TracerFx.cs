using Deadlands.Core.Pooling;
using UnityEngine;

namespace Deadlands.Combat
{
    /// <summary>Short-lived bullet tracer line. Pooled; returns itself when faded.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TracerFx : MonoBehaviour, IPoolable
    {
        [SerializeField] float lifetime = 0.06f;
        LineRenderer line;
        float age;
        Color baseColor;

        void Awake()
        {
            line = GetComponent<LineRenderer>();
            baseColor = line.startColor;
        }

        public void Show(Vector3 from, Vector3 to)
        {
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            age = 0f;
        }

        void Update()
        {
            age += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(age / lifetime);
            var c = baseColor; c.a *= a;
            line.startColor = line.endColor = c;
            if (age >= lifetime) GetComponent<PooledObject>().Release();
        }

        public void OnSpawned() { age = 0f; }
        public void OnDespawned() { }
    }

}
