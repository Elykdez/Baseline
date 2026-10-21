using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Recycles instances of one prefab. Kept a plain class rather than a component so it can be
    /// exercised without a scene, and keyed on the prefab reference rather than on a name, which
    /// is what made the previous pool mix up two prefabs that happened to share a name.
    /// </summary>
    public sealed class PrefabPool
    {
        readonly GameObject prefab;
        readonly Transform root;
        readonly Vector3 prefabScale;
        readonly Stack<PooledObject> idle;

        /// <summary>Idle instances kept before further returns are destroyed instead.</summary>
        public int MaxIdle { get; }

        public GameObject Prefab => prefab;

        /// <summary>Instances currently parked and available for reuse.</summary>
        public int IdleCount => idle.Count;

        /// <summary>Instances created by this pool and not yet destroyed.</summary>
        public int LiveCount { get; private set; }

        public PrefabPool(GameObject prefab, Transform root, int maxIdle)
        {
            if (prefab == null)
            {
                throw new ArgumentNullException(nameof(prefab));
            }
            if (maxIdle < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxIdle), maxIdle, "A pool cannot keep fewer than zero idle instances.");
            }

            this.prefab = prefab;
            this.root = root;
            prefabScale = prefab.transform.localScale;
            MaxIdle = maxIdle;
            idle = new Stack<PooledObject>(Mathf.Min(maxIdle, 16));
        }

        /// <summary>Creates instances up front so the first spawns of a burst do not stall.</summary>
        public void Prewarm(int count)
        {
            for (int index = idle.Count; index < count; index++)
            {
                PooledObject instance = Create();
                Park(instance);
            }
        }

        /// <summary>Takes an instance from the pool, creating one when none is parked.</summary>
        public GameObject Spawn(Transform parent)
        {
            PooledObject instance = idle.Count > 0 ? idle.Pop() : Create();

            instance.IsPooled = false;
            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent, false);
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;
            instanceTransform.localScale = prefabScale;
            instance.gameObject.SetActive(true);
            Notify(instance, spawned: true);
            return instance.gameObject;
        }

        /// <summary>
        /// Parks an instance for reuse, or destroys it once the pool is full. Returning the same
        /// instance twice is ignored rather than corrupting the stack with a duplicate.
        /// </summary>
        public void Despawn(PooledObject instance)
        {
            if (instance == null || instance.IsPooled)
            {
                return;
            }

            // A despawn callback can return the object again, including while it is destroyed.
            instance.IsPooled = true;
            Notify(instance, spawned: false);
            if (idle.Count >= MaxIdle)
            {
                LiveCount--;
                ObjectHelper.DestroyObject(instance.gameObject);
                return;
            }
            Park(instance);
        }

        /// <summary>Destroys every parked instance. Instances still in use are left alone.</summary>
        public void Clear()
        {
            while (idle.Count > 0)
            {
                PooledObject instance = idle.Pop();
                if (instance != null)
                {
                    LiveCount--;
                    ObjectHelper.DestroyObject(instance.gameObject);
                }
            }
        }

        PooledObject Create()
        {
            GameObject instance = UnityEngine.Object.Instantiate(prefab, root);
            instance.name = prefab.name;
            PooledObject pooled = instance.AddComponent<PooledObject>();
            pooled.Owner = this;
            LiveCount++;
            return pooled;
        }

        void Park(PooledObject instance)
        {
            instance.IsPooled = true;
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(root, false);
            idle.Push(instance);
        }

        // Allocation-free would need a cached buffer per instance; spawning is not a per-frame
        // path here, and GetComponents on a small unit prefab is a handful of microseconds.
        static void Notify(PooledObject instance, bool spawned)
        {
            IPoolable[] listeners = instance.GetComponentsInChildren<IPoolable>(true);
            for (int index = 0; index < listeners.Length; index++)
            {
                if (spawned)
                {
                    listeners[index].OnSpawned();
                }
                else
                {
                    listeners[index].OnDespawned();
                }
            }
        }
    }
}
