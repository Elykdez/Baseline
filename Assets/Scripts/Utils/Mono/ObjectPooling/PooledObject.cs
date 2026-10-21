using UnityEngine;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Bookkeeping the pool attaches to every instance it hands out. Holding the owning pool on
    /// the instance is what lets <see cref="ObjectPool.Despawn(GameObject)"/> take any object and
    /// find its way home without the caller having to remember which prefab it came from.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        /// <summary>Pool this instance belongs to.</summary>
        public PrefabPool Owner { get; internal set; }

        /// <summary>True while the instance is parked in its pool rather than in use.</summary>
        public bool IsPooled { get; internal set; }
    }
}
