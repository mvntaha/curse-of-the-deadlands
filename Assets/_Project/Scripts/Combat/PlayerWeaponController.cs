using System;
using System.Collections.Generic;
using Deadlands.Core.Combat;
using Deadlands.Core.Input;
using Deadlands.Core.Pooling;
using Deadlands.Player;
using UnityEngine;

namespace Deadlands.Combat
{
    /// <summary>Runtime state of one carried weapon (ammo in the magazine, melee durability).</summary>
    [Serializable]
    public class WeaponInstance
    {
        public WeaponData data;
        public int ammoInMagazine;
        public int durability;

        public WeaponInstance(WeaponData data)
        {
            this.data = data;
            ammoInMagazine = data.IsRanged ? data.magazineSize : 0;
            durability = data.maxDurability;
        }

        public bool IsBroken => data.Breakable && durability <= 0;
    }

    /// <summary>
    /// The player's weapons: switching, aiming, firing (hitscan), reloading from <see cref="AmmoStore"/>,
    /// melee swings that wear down durability and break. Exposes events for audio/VFX/HUD to hook into.
    /// </summary>
    public class PlayerWeaponController : MonoBehaviour
    {
        [Serializable]
        struct StartingWeapon
        {
            public WeaponData weapon;
            [Tooltip("-1 = full magazine.")]
            public int loadedAmmo;
        }

        [Header("References")]
        [SerializeField] PlayerInputReader input;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerAnimationController animationController;
        [SerializeField] AmmoStore ammoStore;
        [SerializeField] Health ownerHealth;
        [SerializeField] Camera aimCamera;
        [Tooltip("Parent of the kit's hand-attached weapon meshes (ZAK: Middle1.L).")]
        [SerializeField] Transform weaponModelRoot;

        [Header("Loadout")]
        [SerializeField] StartingWeapon[] startingLoadout;
        [Tooltip("Used when nothing else is usable (and when a melee weapon breaks).")]
        [SerializeField] WeaponData fists;
        [SerializeField] float switchTime = 0.3f;

        [Header("Hit detection")]
        [SerializeField] LayerMask shotMask = ~0;
        [SerializeField] LayerMask meleeMask = ~0;
        [SerializeField] float chestHeight = 1.35f;

        [Header("Effects (pooled)")]
        [SerializeField] GameObjectPool tracerPool;
        [SerializeField] GameObjectPool impactPool;
        [SerializeField] Light muzzleFlash;
        [SerializeField] float muzzleFlashTime = 0.05f;
        [SerializeField] Color fleshColor = new Color(0.45f, 0.02f, 0.02f);
        [SerializeField] Color worldColor = new Color(0.55f, 0.52f, 0.48f);

        readonly List<WeaponInstance> loadout = new List<WeaponInstance>();
        readonly Dictionary<string, Transform> handModels = new Dictionary<string, Transform>();
        readonly Collider[] meleeHits = new Collider[16];
        readonly HashSet<IDamageable> meleeVictims = new HashSet<IDamageable>();

        WeaponInstance fistsInstance;
        int currentIndex = -1;
        float busyUntil;          // switching / reloading / swinging
        float nextShotTime;
        float reloadCompleteAt = -1f;
        float meleeHitAt = -1f;
        float meleeEndAt = -1f;
        float muzzleOffAt;
        bool meleeLocked;

        public WeaponInstance Current => currentIndex >= 0 && currentIndex < loadout.Count ? loadout[currentIndex] : fistsInstance;
        public IReadOnlyList<WeaponInstance> Loadout => loadout;
        public bool IsReloading => reloadCompleteAt > 0f;
        public AmmoStore Ammo => ammoStore;

        public event Action<WeaponInstance> Equipped;
        public event Action<WeaponInstance> Fired;
        public event Action<WeaponInstance> DryFired;
        public event Action<WeaponInstance> ReloadStarted;
        public event Action<WeaponInstance> Reloaded;
        public event Action<WeaponInstance> MeleeSwung;
        public event Action<WeaponInstance, int> MeleeConnected;
        public event Action<WeaponInstance> MeleeBroke;

        void Awake()
        {
            // Only mesh children are weapons; the same parent also holds finger bones that must stay active.
            if (weaponModelRoot)
                foreach (Transform t in weaponModelRoot)
                    if (t.GetComponent<MeshRenderer>()) handModels[t.name] = t;

            fistsInstance = new WeaponInstance(fists);
            foreach (var s in startingLoadout)
            {
                if (!s.weapon) continue;
                var w = new WeaponInstance(s.weapon);
                if (s.weapon.IsRanged && s.loadedAmmo >= 0) w.ammoInMagazine = Mathf.Min(s.loadedAmmo, s.weapon.magazineSize);
                loadout.Add(w);
            }
            if (muzzleFlash) muzzleFlash.enabled = false;
        }

        void Start() => Equip(loadout.Count > 0 ? 0 : -1, instant: true);

        void OnEnable()
        {
            input.AttackPressed += OnAttackPressed;
            input.ReloadPressed += TryReload;
            input.NextWeaponPressed += NextWeapon;
            input.PrevWeaponPressed += PrevWeapon;
            input.SlotPressed += SelectSlot;
            motor.DodgeStarted += CancelReload; // rolling interrupts a reload
        }

        void OnDisable()
        {
            input.AttackPressed -= OnAttackPressed;
            input.ReloadPressed -= TryReload;
            input.NextWeaponPressed -= NextWeapon;
            input.PrevWeaponPressed -= PrevWeapon;
            input.SlotPressed -= SelectSlot;
            motor.DodgeStarted -= CancelReload;
            EndMeleeLock();
        }

        // Our own melee swing locks movement too, so only foreign locks (grabs, cutscenes) block actions.
        bool CanAct => !(ownerHealth && ownerHealth.IsDead) && !motor.IsDodging && (!motor.IsMovementLocked || meleeLocked);

        // ---------------- Loadout / switching ----------------

        /// <summary>Adds a weapon (pickups/unlocks, Phase 4). Returns the new instance.</summary>
        public WeaponInstance AddWeapon(WeaponData data)
        {
            var existing = loadout.Find(w => w.data == data);
            if (existing != null) return existing;
            var w = new WeaponInstance(data);
            loadout.Add(w);
            return w;
        }

        /// <summary>Restores a melee weapon to full durability (replacement pickup).</summary>
        public void RestoreDurability(WeaponData data)
        {
            var w = loadout.Find(x => x.data == data);
            if (w != null) w.durability = data.maxDurability;
            else AddWeapon(data);
        }

        void SelectSlot(int slot)
        {
            if (slot < loadout.Count && !loadout[slot].IsBroken) Equip(slot);
        }

        void NextWeapon() => Cycle(1);
        void PrevWeapon() => Cycle(-1);

        void Cycle(int dir)
        {
            if (loadout.Count == 0) return;
            int i = currentIndex;
            for (int n = 0; n < loadout.Count; n++)
            {
                i = (i + dir + loadout.Count) % loadout.Count;
                if (!loadout[i].IsBroken) { Equip(i); return; }
            }
        }

        void Equip(int index, bool instant = false)
        {
            if (index == currentIndex && !instant) return;
            CancelReload();
            currentIndex = index;
            var w = Current;

            foreach (var kv in handModels) kv.Value.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(w.data.handModelName) && handModels.TryGetValue(w.data.handModelName, out Transform model))
            {
                model.gameObject.SetActive(true);
                model.localScale = Vector3.one * w.data.handModelScale;
            }

            animationController.SetArmed(w.data.IsRanged);
            if (!instant) busyUntil = Time.time + switchTime;
            Equipped?.Invoke(w);
        }

        // ---------------- Update loop ----------------

        void Update()
        {
            float now = Time.time;
            if (muzzleFlash && muzzleFlash.enabled && now >= muzzleOffAt) muzzleFlash.enabled = false;

            if (reloadCompleteAt > 0f && now >= reloadCompleteAt) FinishReload();
            if (meleeHitAt > 0f && now >= meleeHitAt) { meleeHitAt = -1f; ResolveMeleeHit(); }
            if (meleeEndAt > 0f && now >= meleeEndAt) { meleeEndAt = -1f; EndMeleeLock(); }

            // Full-auto keeps firing while held.
            var w = Current;
            if (w.data.IsRanged && w.data.fireMode == FireMode.FullAuto && input.AttackHeld) TryFire();
        }

        void OnAttackPressed()
        {
            var w = Current;
            if (w.data.IsRanged) TryFire(pressed: true);
            else TrySwing();
        }

        // ---------------- Ranged ----------------

        void TryFire(bool pressed = false)
        {
            var w = Current;
            if (!CanAct || Time.time < busyUntil || Time.time < nextShotTime) return;
            if (!motor.IsAiming) return; // RE-style: firearms fire only while aiming

            if (w.ammoInMagazine <= 0)
            {
                if (pressed) DryFired?.Invoke(w);
                nextShotTime = Time.time + 0.25f;
                if (ammoStore.Get(w.data.ammoType) > 0) TryReload();
                return;
            }

            w.ammoInMagazine--;
            nextShotTime = Time.time + w.data.fireInterval;
            animationController.AddRecoil(w.data.recoilKick);

            Vector3 muzzle = MuzzlePosition();
            Ray center = aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            // Start the ray level with the player so nothing between camera and player can be hit.
            Vector3 chest = transform.position + Vector3.up * chestHeight;
            float skip = Mathf.Max(0f, Vector3.Dot(chest - center.origin, center.direction));
            Vector3 origin = center.origin + center.direction * skip;

            int pellets = Mathf.Max(1, w.data.pellets);
            for (int i = 0; i < pellets; i++)
            {
                Vector3 dir = ApplySpread(center.direction, w.data.spread);
                FireRay(w, origin, dir, muzzle);
            }

            if (muzzleFlash)
            {
                muzzleFlash.transform.position = muzzle;
                muzzleFlash.enabled = true;
                muzzleOffAt = Time.time + muzzleFlashTime;
            }
            Fired?.Invoke(w);
        }

        void FireRay(WeaponInstance w, Vector3 origin, Vector3 dir, Vector3 muzzle)
        {
            Vector3 end = origin + dir * w.data.range;
            if (Physics.Raycast(origin, dir, out RaycastHit hit, w.data.range, shotMask, QueryTriggerInteraction.Ignore))
            {
                // The muzzle can't see the point (e.g. wall right beside the player): hit the wall instead.
                if (Physics.Linecast(muzzle, hit.point - dir * 0.05f, out RaycastHit blocked, shotMask, QueryTriggerInteraction.Ignore)
                    && blocked.collider != hit.collider)
                    hit = blocked;

                end = hit.point;
                var target = hit.collider.GetComponentInParent<IDamageable>();
                float damage = w.data.damage;
                if (hit.collider.GetComponentInParent<IHitZoneProvider>() is IHitZoneProvider zones)
                    damage *= zones.GetDamageMultiplier(hit.point, out _);

                bool flesh = false;
                if (target != null && !ReferenceEquals(target, ownerHealth))
                    flesh = target.TakeDamage(new DamageInfo(damage, hit.point, dir, w.data.knockbackForce, gameObject))
                            && hit.collider.GetComponentInParent<IHitZoneProvider>() != null;
                SpawnImpact(hit.point, hit.normal, flesh);
            }

            if (tracerPool)
            {
                var tracer = tracerPool.Spawn(muzzle, Quaternion.identity).GetComponent<TracerFx>();
                tracer.Show(muzzle, end);
            }
        }

        static Vector3 ApplySpread(Vector3 dir, float degrees)
        {
            if (degrees <= 0f) return dir;
            Vector2 r = UnityEngine.Random.insideUnitCircle * Mathf.Tan(degrees * Mathf.Deg2Rad);
            Quaternion basis = Quaternion.LookRotation(dir);
            return (basis * new Vector3(r.x, r.y, 1f)).normalized;
        }

        Vector3 MuzzlePosition()
        {
            var w = Current;
            if (!string.IsNullOrEmpty(w.data.handModelName) && handModels.TryGetValue(w.data.handModelName, out Transform model))
            {
                var r = model.GetComponent<Renderer>();
                if (r) return r.bounds.center + transform.forward * r.bounds.extents.magnitude * 0.6f;
            }
            return transform.position + Vector3.up * chestHeight + transform.forward * 0.5f;
        }

        void SpawnImpact(Vector3 point, Vector3 normal, bool flesh)
        {
            if (!impactPool) return;
            var fx = impactPool.Spawn(point + normal * 0.02f, Quaternion.identity).GetComponent<ImpactFx>();
            fx.Play(normal, flesh ? fleshColor : worldColor);
        }

        void TryReload()
        {
            var w = Current;
            if (!w.data.IsRanged || IsReloading || !CanAct || Time.time < busyUntil) return;
            if (w.ammoInMagazine >= w.data.magazineSize || ammoStore.Get(w.data.ammoType) <= 0) return;

            reloadCompleteAt = Time.time + w.data.reloadTime;
            busyUntil = reloadCompleteAt;
            animationController.PlayReload(w.data.reloadTime);
            ReloadStarted?.Invoke(w);
        }

        void FinishReload()
        {
            reloadCompleteAt = -1f;
            var w = Current;
            int needed = w.data.magazineSize - w.ammoInMagazine;
            w.ammoInMagazine += ammoStore.Take(w.data.ammoType, needed);
            Reloaded?.Invoke(w);
        }

        void CancelReload()
        {
            if (reloadCompleteAt <= 0f) return;
            busyUntil = Time.time;
            reloadCompleteAt = -1f;
            animationController.CancelReload();
        }

        // ---------------- Melee ----------------

        void TrySwing()
        {
            var w = Current;
            if (!CanAct || meleeLocked || Time.time < busyUntil) return;

            busyUntil = Time.time + w.data.swingDuration;
            meleeHitAt = Time.time + w.data.swingHitTime;
            meleeEndAt = Time.time + w.data.swingDuration;
            meleeLocked = true;
            motor.PushMovementLock();
            if (motor.IsAiming) motor.FaceDirection(aimCamera.transform.forward);
            animationController.PlayMelee(string.IsNullOrEmpty(w.data.handModelName) ? 0 : 1);
            MeleeSwung?.Invoke(w);
        }

        void ResolveMeleeHit()
        {
            var w = Current;
            Vector3 center = transform.position + Vector3.up * 1.1f + transform.forward * w.data.reach;
            int count = Physics.OverlapSphereNonAlloc(center, w.data.hitRadius, meleeHits, meleeMask, QueryTriggerInteraction.Ignore);
            meleeVictims.Clear();
            int connected = 0;
            for (int i = 0; i < count; i++)
            {
                var target = meleeHits[i].GetComponentInParent<IDamageable>();
                if (target == null || ReferenceEquals(target, ownerHealth) || !meleeVictims.Add(target)) continue;
                Vector3 point = meleeHits[i].ClosestPoint(center);
                if (target.TakeDamage(new DamageInfo(w.data.damage, point, transform.forward, w.data.knockbackForce, gameObject)))
                {
                    connected++;
                    SpawnImpact(point, -transform.forward, meleeHits[i].GetComponentInParent<IHitZoneProvider>() != null);
                }
            }

            if (connected == 0) return;

            if (w.data.Breakable) w.durability = Mathf.Max(0, w.durability - w.data.durabilityLossPerHit);
            MeleeConnected?.Invoke(w, connected);
            if (w.IsBroken) BreakMelee(w);
        }

        void BreakMelee(WeaponInstance w)
        {
            MeleeBroke?.Invoke(w);
            // Broken weapons leave the hand; fall back to the next usable weapon, or fists.
            int next = loadout.FindIndex(x => x != w && !x.IsBroken);
            Equip(next, instant: true);
        }

        void EndMeleeLock()
        {
            if (!meleeLocked) return;
            meleeLocked = false;
            motor.PopMovementLock();
        }
    }
}
