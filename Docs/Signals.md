# Signals

Source: [SignalBus](../Assets/Scripts/Core/Events/SignalSystem/SignalBus.cs) and
[SignalBehaviour](../Assets/Scripts/Core/Events/SignalSystem/SignalBehaviour.cs).

Declare a small struct implementing `Hypocycloid.Core.ISignal`. A `Requested` signal asks the
owning manager to act; a `Changed` signal reports the applied state. Gameplay reports facts;
presentation consumes those facts and requests sounds or visuals.

For a component whose listener should exist only while enabled:

```csharp
using Hypocycloid.Core;
using UnityEngine;

public struct ScoreChangedSignal : ISignal
{
    public int Score;
}

public sealed class ScoreObserver : SignalBehaviour
{
    protected override void OnEnable()
    {
        base.OnEnable();
        Subscribe<ScoreChangedSignal>(OnScoreChanged);
    }

    void OnScoreChanged(ScoreChangedSignal signal) => Debug.Log(signal.Score);
}
```

The producer calls `SignalBus.Fire(new ScoreChangedSignal { Score = score })`. Use
`SubscribeLifetime<T>()` once, typically in `Awake`, only when a disabled component should still
listen. Preserve `base.OnDisable()` and `base.OnDestroy()` when overriding those methods.

Plain C# sessions pair `SignalBus.Subscribe<T>(handler)` with `Unsubscribe<T>(handler)` in their
disposal path. The optional owner is a `UnityEngine.Object`, not an arbitrary session object.
Keep the same handler and owner for unsubscription; avoid an anonymous callback you cannot remove.

Dispatch is synchronous, in subscription order. Listeners added during dispatch wait for a later
fire; removed listeners and destroyed owners are skipped. A callback exception is logged and later
listeners continue. Avoid recursive request/change loops. Firing a signal does not instantiate
its manager, queue it for a future listener, or prove anything handled it.

## Audit

Run `SignalBusTests` and the changed consumer's checks. Use **Tools > Hypocycloid > Audit > Signal Bus Tracker** to inspect subscriptions and fires across enable/disable and scene transitions. Verify the visible consumer result as well; a recorded fire alone does not prove it.

Related: [Quests](Quests.md), [Audio](Audio.md), [FX](Fx.md), [Auditing](Auditing.md).
