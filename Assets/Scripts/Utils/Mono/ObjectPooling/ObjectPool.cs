using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Scene-wide registry of <see cref="PrefabPool"/>s, one per prefab. Access through
    /// <c>ObjectPool.Ins</c>. Prefabs need no setup to be poolable: the bookkeeping component is
    /// attached to instances as they are created.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObjectPool : MonoSingleton<ObjectPool>
    {
        [SerializeField, Tooltip("Idle instances kept per prefab before further returns are destroyed.")]
        int maxIdlePerPrefab = 64;

        readonly Dictionary<GameObject, PrefabPool> pools = new Dictionary<GameObject, PrefabPool>();

        /// <summary>Distinct prefabs this pool has been asked for.</summary>
        public int PoolCount => pools.Count;

        /// <summary>Creates instances of a prefab up front so a later burst of spawns does not stall.</summary>
        public void Prewarm(GameObject prefab, int count)
        {
            PoolFor(prefab).Prewarm(count);
        }

        /// <summary>Takes an instance of a prefab, creating one when the pool is empty.</summary>
        public GameObject Spawn(GameObject prefab, Transform parent = null)
        {
            return PoolFor(prefab).Spawn(parent);
        }

        /// <summary>Takes an instance of a component's prefab and returns that same component on it.</summary>
        public T Spawn<T>(T prefab, Transform parent = null)
            where T : Component
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }
            return Spawn(prefab.gameObject, parent).GetComponent<T>();
        }

        /// <summary>
        /// Returns an instance to the pool it came from. Objects this pool never handed out are
        /// destroyed instead, so a caller cannot silently leak them by calling the wrong method.
        /// </summary>
        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            if (instance.TryGetComponent(out PooledObject pooled) && pooled.Owner != null)
            {
                pooled.Owner.Despawn(pooled);
                return;
            }
            ObjectHelper.DestroyObject(instance);
        }

        /// <summary>Returns a component's instance to the pool it came from.</summary>
        public void Despawn(Component instance)
        {
            if (instance != null)
            {
                Despawn(instance.gameObject);
            }
        }

        /// <summary>Destroys parked instances, retaining the pool so active instances can return for reuse.</summary>
        public void Clear(GameObject prefab)
        {
            if (prefab != null && pools.TryGetValue(prefab, out PrefabPool pool))
            {
                pool.Clear();
            }
        }

        /// <summary>Destroys all parked instances, retaining pools so active instances can return for reuse.</summary>
        public void ClearAll()
        {
            foreach (PrefabPool pool in pools.Values)
            {
                pool.Clear();
            }
        }

        protected override void OnDestroy()
        {
            ClearAll();
            base.OnDestroy();
        }

        PrefabPool PoolFor(GameObject prefab)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }

            if (!pools.TryGetValue(prefab, out PrefabPool pool))
            {
                pool = new PrefabPool(prefab, transform, maxIdlePerPrefab);
                pools.Add(prefab, pool);
            }
            return pool;
        }
    }
}
