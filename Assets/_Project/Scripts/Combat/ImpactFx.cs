using Deadlands.Core.Pooling;
using UnityEngine;

namespace Deadlands.Combat
{
    /// <summary>Impact burst (blood on flesh, dust on world). Pooled; returns itself when the particles finish.</summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class ImpactFx : MonoBehaviour, IPoolable
    {
        ParticleSystem ps;
        float timer;

        void Awake() => ps = GetComponent<ParticleSystem>();

        public void Play(Vector3 normal, Color color)
        {
            transform.rotation = Quaternion.LookRotation(normal.sqrMagnitude > 0f ? normal : Vector3.up);
            var main = ps.main;
            main.startColor = color;
            ps.Clear();
            ps.Play();
            timer = main.duration + main.startLifetime.constantMax;
        }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f) GetComponent<PooledObject>().Release();
        }

        public void OnSpawned() { }
        public void OnDespawned() => ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
