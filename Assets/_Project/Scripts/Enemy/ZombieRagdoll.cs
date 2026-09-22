using System.Collections.Generic;
using UnityEngine;

namespace Deadlands.Enemy
{
    /// <summary>
    /// Switches a zombie between animated (kinematic bones) and physics ragdoll, and restores the skeleton
    /// exactly when the zombie is reused from the pool.
    /// </summary>
    public class ZombieRagdoll : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [Tooltip("Bones that aren't children of the limb they belong to (the kit's IK feet) and must follow it in ragdoll.")]
        [SerializeField] Transform[] reattachBones;
        [SerializeField] Transform[] reattachParents;

        struct BonePose
        {
            public Transform Bone;
            public Transform Parent;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        Rigidbody[] bodies;
        Collider[] colliders;
        readonly List<BonePose> pose = new List<BonePose>();

        public bool IsActive { get; private set; }

        void Awake()
        {
            bodies = animator.GetComponentsInChildren<Rigidbody>(true);
            colliders = new Collider[bodies.Length];
            for (int i = 0; i < bodies.Length; i++) colliders[i] = bodies[i].GetComponent<Collider>();

            foreach (Transform t in animator.GetComponentsInChildren<Transform>(true))
                pose.Add(new BonePose { Bone = t, Parent = t.parent, LocalPosition = t.localPosition, LocalRotation = t.localRotation });

            SetPhysics(false);
        }

        public void Activate(Vector3 impulse, Vector3 point)
        {
            if (IsActive) return;
            IsActive = true;

            animator.enabled = false;
            for (int i = 0; i < reattachBones.Length && i < reattachParents.Length; i++)
                reattachBones[i].SetParent(reattachParents[i], worldPositionStays: true);

            SetPhysics(true);

            if (impulse.sqrMagnitude > 0f && bodies.Length > 0)
            {
                Rigidbody closest = bodies[0];
                float best = float.MaxValue;
                foreach (var rb in bodies)
                {
                    float d = (rb.worldCenterOfMass - point).sqrMagnitude;
                    if (d < best) { best = d; closest = rb; }
                }
                closest.AddForceAtPosition(impulse, point, ForceMode.Impulse);
            }
        }

        /// <summary>Stops the simulation so the corpse can be moved (e.g. sunk into the ground).</summary>
        public void Freeze()
        {
            foreach (var rb in bodies)
            {
                rb.interpolation = RigidbodyInterpolation.None;
                rb.isKinematic = true;
            }
        }

        public void ResetPose()
        {
            IsActive = false;
            SetPhysics(false);
            foreach (var p in pose)
            {
                if (p.Bone.parent != p.Parent) p.Bone.SetParent(p.Parent, worldPositionStays: false);
                p.Bone.localPosition = p.LocalPosition;
                p.Bone.localRotation = p.LocalRotation;
            }
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        void SetPhysics(bool on)
        {
            for (int i = 0; i < bodies.Length; i++)
            {
                if (on)
                {
                    bodies[i].isKinematic = false;
                    bodies[i].interpolation = RigidbodyInterpolation.Interpolate;
                    bodies[i].linearVelocity = Vector3.zero;
                    bodies[i].angularVelocity = Vector3.zero;
                }
                else
                {
                    // Interpolation on kinematic, animator-driven bones drags them back to stale physics poses.
                    bodies[i].interpolation = RigidbodyInterpolation.None;
                    bodies[i].isKinematic = true;
                }
                if (colliders[i]) colliders[i].enabled = on;
            }
        }
    }
}
