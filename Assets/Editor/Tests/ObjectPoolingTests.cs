using Hypocycloid.Utils;
using NUnit.Framework;
using UnityEngine;

namespace Baseline.Tests.Editor
{
    /// <summary>
    /// Reuse semantics of <see cref="PrefabPool"/> and registry clearing through <see cref="ObjectPool"/>.
    /// </summary>
    public sealed class ObjectPoolingTests
    {
        /// <summary>Counts the reuse callbacks so a test can prove they fired, and how often.</summary>
        sealed class SpawnCounter : MonoBehaviour, IPoolable
        {
            public int Spawns;
            public int Despawns;

            public void OnSpawned() => Spawns++;

            public void OnDespawned() => Despawns++;
        }

        sealed class ReturnOnDespawn : MonoBehaviour, IPoolable
        {
            public int Despawns;

            public void OnSpawned() { }

            public void OnDespawned()
            {
                Despawns++;
                PooledObject pooled = GetComponent<PooledObject>();
                pooled.Owner.Despawn(pooled);
            }
        }

        GameObject prefab;
        Transform root;
        Transform activeRoot;

        [SetUp]
        public void SetUp()
        {
            prefab = new GameObject("PoolTestPrefab");
            prefab.AddComponent<SpawnCounter>();
            prefab.SetActive(false);
            root = new GameObject("PoolRoot").transform;
            activeRoot = new GameObject("ActivePoolTestObjects").transform;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(activeRoot.gameObject);
            Object.DestroyImmediate(prefab);
            Object.DestroyImmediate(root.gameObject);
        }

        [Test]
        public void ADespawnedInstanceIsHandedBackOutInsteadOfANewOne()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);

            GameObject first = pool.Spawn(activeRoot);
            Assert.That(pool.LiveCount, Is.EqualTo(1));

            pool.Despawn(first.GetComponent<PooledObject>());
            Assert.That(pool.IdleCount, Is.EqualTo(1));

            GameObject second = pool.Spawn(activeRoot);
            Assert.That(second, Is.SameAs(first), "the pool instantiated a second object instead of reusing the parked one");
            Assert.That(pool.LiveCount, Is.EqualTo(1), "reuse must not count as another instantiation");
            Assert.That(pool.IdleCount, Is.EqualTo(0));
        }

        [Test]
        public void SpawnActivatesAndDespawnDeactivates()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);

            GameObject instance = pool.Spawn(activeRoot);
            Assert.That(instance.activeSelf, Is.True, "a spawned instance must be active even when the prefab is not");

            pool.Despawn(instance.GetComponent<PooledObject>());
            Assert.That(instance.activeSelf, Is.False);
            Assert.That(instance.transform.parent, Is.SameAs(root), "a parked instance belongs under the pool root");
        }

        [Test]
        public void EveryReuseRaisesThePoolableCallbacksExactlyOnce()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);

            GameObject instance = pool.Spawn(activeRoot);
            SpawnCounter counter = instance.GetComponent<SpawnCounter>();
            Assert.That(counter.Spawns, Is.EqualTo(1));
            Assert.That(counter.Despawns, Is.EqualTo(0));

            pool.Despawn(instance.GetComponent<PooledObject>());
            Assert.That(counter.Despawns, Is.EqualTo(1));

            pool.Spawn(activeRoot);
            Assert.That(counter.Spawns, Is.EqualTo(2));
        }

        [Test]
        public void ReturningTheSameInstanceTwiceIsIgnored()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);

            GameObject instance = pool.Spawn(activeRoot);
            PooledObject pooled = instance.GetComponent<PooledObject>();
            pool.Despawn(pooled);
            pool.Despawn(pooled);

            Assert.That(pool.IdleCount, Is.EqualTo(1), "a double return duplicated the instance in the pool");
            Assert.That(instance.GetComponent<SpawnCounter>().Despawns, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(1)]
        public void ReturningFromADespawnCallbackDoesNotReenterThePool(int maxIdle)
        {
            prefab.AddComponent<ReturnOnDespawn>();
            var pool = new PrefabPool(prefab, root, maxIdle);
            GameObject instance = pool.Spawn(activeRoot);
            ReturnOnDespawn listener = instance.GetComponent<ReturnOnDespawn>();

            pool.Despawn(instance.GetComponent<PooledObject>());

            Assert.That(pool.IdleCount, Is.EqualTo(maxIdle));
            Assert.That(pool.LiveCount, Is.EqualTo(maxIdle));
            if (maxIdle > 0) Assert.That(listener.Despawns, Is.EqualTo(1));
        }

        [Test]
        public void ReturnsBeyondTheIdleLimitAreDestroyedRatherThanHoarded()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 1);

            GameObject first = pool.Spawn(activeRoot);
            GameObject second = pool.Spawn(activeRoot);
            Assert.That(pool.LiveCount, Is.EqualTo(2));

            pool.Despawn(first.GetComponent<PooledObject>());
            pool.Despawn(second.GetComponent<PooledObject>());

            Assert.That(pool.IdleCount, Is.EqualTo(1), "the pool kept more idle instances than its limit");
            Assert.That(pool.LiveCount, Is.EqualTo(1), "the surplus instance was not destroyed");
        }

        [Test]
        public void PrewarmCreatesInstancesUpFrontAndSpawnConsumesThem()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);
            pool.Prewarm(3);

            Assert.That(pool.IdleCount, Is.EqualTo(3));
            Assert.That(pool.LiveCount, Is.EqualTo(3));

            pool.Spawn(activeRoot);
            Assert.That(pool.IdleCount, Is.EqualTo(2));
            Assert.That(pool.LiveCount, Is.EqualTo(3), "prewarmed instances must be reused, not duplicated");
        }

        [Test]
        public void ClearDestroysParkedInstancesOnly()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);
            GameObject kept = pool.Spawn(activeRoot);
            GameObject parked = pool.Spawn(activeRoot);
            pool.Despawn(parked.GetComponent<PooledObject>());

            pool.Clear();

            Assert.That(pool.IdleCount, Is.EqualTo(0));
            Assert.That(kept == null, Is.False, "an instance still in use was destroyed by Clear");
        }

        [Test]
        public void ASpawnedInstanceCarriesTheBookkeepingThatRoutesItHome()
        {
            PrefabPool pool = new PrefabPool(prefab, root, 8);
            GameObject instance = pool.Spawn(activeRoot);

            PooledObject pooled = instance.GetComponent<PooledObject>();
            Assert.That(pooled, Is.Not.Null, "the pool did not attach its bookkeeping component");
            Assert.That(pooled.Owner, Is.SameAs(pool));
            Assert.That(pooled.IsPooled, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RegistryClearKeepsOutstandingInstancesReusable(bool clearAll)
        {
            ObjectPool registry = root.gameObject.AddComponent<ObjectPool>();
            GameObject outstanding = registry.Spawn(prefab, activeRoot);
            GameObject parked = registry.Spawn(prefab, activeRoot);
            registry.Despawn(parked);

            if (clearAll)
                registry.ClearAll();
            else
                registry.Clear(prefab);

            Assert.That(parked == null, Is.True, "clearing must destroy idle instances");
            Assert.That(outstanding != null && outstanding.activeSelf, Is.True, "clearing must leave active instances alone");

            GameObject next = registry.Spawn(prefab, activeRoot);
            Assert.That(next.GetComponent<PooledObject>().Owner, Is.SameAs(outstanding.GetComponent<PooledObject>().Owner));
            registry.Despawn(outstanding);
            GameObject reused = registry.Spawn(prefab, activeRoot);
            Assert.That(reused, Is.SameAs(outstanding), "an instance returned after clearing must remain reusable");

            registry.Despawn(reused);
            registry.ClearAll();
            Assert.That(reused == null, Is.True, "later clearing must still reach returned instances");
        }

        [Test]
        public void ClearingOnePrefabLeavesOtherPoolsIntact()
        {
            ObjectPool registry = root.gameObject.AddComponent<ObjectPool>();
            GameObject otherPrefab = Object.Instantiate(prefab, root);
            GameObject first = registry.Spawn(prefab, activeRoot);
            GameObject other = registry.Spawn(otherPrefab, activeRoot);
            registry.Despawn(first);
            registry.Despawn(other);

            registry.Clear(prefab);

            Assert.That(first == null, Is.True);
            Assert.That(registry.Spawn(otherPrefab, activeRoot), Is.SameAs(other));
        }
    }
}
