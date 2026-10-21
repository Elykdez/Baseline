# Utilities

Source: [Utils](../Assets/Scripts/Utils).

## Singletons

`MonoSingleton<T>.Ins` creates a missing manager; `HasInstance` only observes. Configure authored
managers before any code requests their lazy instance. Preserve the base lifecycle methods when
overriding `Init`, destruction or quit handling. Some managers destroy a duplicate host's entire
GameObject, so put them on dedicated hosts rather than a root carrying unrelated scene content.

## Pooling

Use `ObjectPool.Ins.Spawn(prefab, parent)` and `Despawn(instance)` for shared repeated objects.
For a panel-owned list, `new PrefabPool(prefab, root, maxIdle)` or a bounded cache may fit better.
`IPoolable.OnSpawned()` resets per-use state after parenting/activation; `OnDespawned()` runs before
deactivation. `Awake` is not a per-spawn reset. `PrefabPool.Clear()` destroys parked objects only;
the feature must own and clean up active objects too. Keep any Addressable prefab handle alive
until its active and parked instances no longer need it.

## Audit

Run `MonoSingletonTests` or `ObjectPoolingTests` for affected lifecycle behavior. Check duplicate hosts, repeat spawn/despawn, retained listeners, active and idle object ownership, and scene teardown. IO loading helpers are documented in [Addressables](Addressables.md); inspect other utility consumers before extending their contracts.

Related: [Signals](Signals.md), [Addressables](Addressables.md), [UI](UI.md), [FX](Fx.md).
