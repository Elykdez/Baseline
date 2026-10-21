using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Hypocycloid.Core
{
    /// <summary>
    /// High-performance, zero-allocation SignalBus, ported from Landstrum.
    /// <para>
    /// Differences from the original: only the synchronous half is carried over (the async half
    /// needs UniTask, which this project does not use, and nothing here awaits a signal); a fire
    /// walks a pooled copy of its handlers, so a handler may subscribe or unsubscribe anything,
    /// itself included, without cutting the walk short; handlers whose owner has been destroyed
    /// are actually skipped and cleaned (the original's check compared with Unity's overloaded
    /// null and never matched); and every registry is cleared at subsystem registration, for
    /// Enter Play Mode without a domain reload.
    /// </para>
    /// </summary>
    public static class SignalBus
    {
        /// <summary>Clears every signal type's handlers; each registry adds itself on first use.</summary>
        static readonly List<Action> registryResets = new();

        /// <summary>
        /// Static dictionary to hold all signal types.
        /// Uses generic static classes to avoid boxing and ensure type safety
        /// </summary>
        static class SignalRegistry<TSignal>
            where TSignal : struct, ISignal
        {
            // Use LinkedList for O(1) insertion/removal with minimal allocation
            public static readonly LinkedList<SignalHandler<TSignal>> SyncHandlers = new();

            // Snapshots a fire walks; one per nesting level, reused afterwards.
            public static readonly Stack<List<SignalHandler<TSignal>>> Snapshots = new();

#if UNITY_EDITOR
            public static TriggerDebugInfo LastTrigger;
#endif

            static SignalRegistry()
            {
                registryResets.Add(() =>
                {
                    SyncHandlers.Clear();
                    Snapshots.Clear();
                });
            }
        }

        // Separate debug tracking storage (not constrained to struct)
#if UNITY_EDITOR
        static readonly Dictionary<Object, List<HandlerDebugInfo>> globalHandlerOwners = new();

        static readonly Dictionary<Type, List<HandlerDebugInfo>> globalSignalSubscribers = new();

        /// <summary>
        /// Event fired when any signal occurs (Editor only).
        /// Signature: (SignalType, SubscriberCount, Invoker)
        /// </summary>
        public static Action<Type, int, Object> OnSignalFired;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            foreach (Action reset in registryResets)
                reset();
#if UNITY_EDITOR
            globalHandlerOwners.Clear();
            globalSignalSubscribers.Clear();
#endif
        }

        #region Synchronous API

        /// <summary>
        /// Subscribe to a signal synchronously (zero allocation after first registration)
        /// </summary>
        public static void Subscribe<TSignal>(Action<TSignal> handler, Object owner = null)
            where TSignal : struct, ISignal
        {
            if (handler == null)
            {
                Debug.LogWarning("[Signal] Attempted to subscribe with null handler");
                return;
            }

            var signalHandler = new SignalHandler<TSignal>(handler, owner);
            var node = SignalRegistry<TSignal>.SyncHandlers.AddLast(signalHandler);
            signalHandler.Node = node;

            TrackSubscription(typeof(TSignal), handler, owner, false);
            UpdateHubInfo<TSignal>();
        }

        /// <summary>
        /// Unsubscribe from a signal
        /// </summary>
        public static void Unsubscribe<TSignal>(Action<TSignal> handler, Object owner = null)
            where TSignal : struct, ISignal
        {
            if (handler == null)
                return;

            var node = SignalRegistry<TSignal>.SyncHandlers.First;
            while (node != null)
            {
                var next = node.Next;
                if (node.Value.Handler == handler && ReferenceEquals(node.Value.Owner, owner))
                {
                    SignalRegistry<TSignal>.SyncHandlers.Remove(node);
                    UntrackSubscription(typeof(TSignal), handler, owner);
                    UpdateHubInfo<TSignal>();
                    break;
                }
                node = next;
            }
        }

        /// <summary>
        /// Fire a signal synchronously to all subscribers.
        /// Every handler subscribed when the fire starts runs once, unless it is unsubscribed
        /// (or its owner destroyed) before its turn; handlers added meanwhile wait for the next fire.
        /// </summary>
        public static void Fire<TSignal>(ref TSignal signal, Object invoker = null)
            where TSignal : struct, ISignal
        {
            // Clean up null subscribers first (destroyed Unity objects)
            CleanNullSubscribers<TSignal>();

            LinkedList<SignalHandler<TSignal>> handlers = SignalRegistry<TSignal>.SyncHandlers;
            if (handlers.Count > 0)
            {
                Stack<List<SignalHandler<TSignal>>> snapshots = SignalRegistry<TSignal>.Snapshots;
                List<SignalHandler<TSignal>> snapshot = snapshots.Count > 0 ? snapshots.Pop() : new List<SignalHandler<TSignal>>(handlers.Count);
                for (var node = handlers.First; node != null; node = node.Next)
                    snapshot.Add(node.Value);

                try
                {
                    foreach (SignalHandler<TSignal> entry in snapshot)
                    {
                        // Unsubscribed by an earlier handler of this same fire.
                        if (entry.Node.List == null || IsDestroyed(entry.Owner))
                            continue;
                        try
                        {
                            entry.Handler?.Invoke(signal);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[Signal] Error in sync handler for {typeof(TSignal).Name}: {ex}");
                        }
                    }
                }
                finally
                {
                    snapshot.Clear();
                    snapshots.Push(snapshot);
                }
            }

            UpdateHubInfo<TSignal>(invoker);
        }

        /// <summary>
        /// Fire a signal (convenience overload without ref)
        /// </summary>
        public static void Fire<TSignal>(TSignal signal, Object invoker = null)
            where TSignal : struct, ISignal
        {
            Fire(ref signal, invoker);
        }

        #endregion

        #region Cleanup & Utilities

        /// <summary>An owner was given and has since been destroyed (Unity's null, not a missing owner).</summary>
        static bool IsDestroyed(Object owner) => owner is not null && owner == null;

        /// <summary>
        /// Clean up handlers with destroyed Unity Object owners
        /// Called automatically before firing signals
        /// </summary>
        static void CleanNullSubscribers<TSignal>()
            where TSignal : struct, ISignal
        {
            var syncNode = SignalRegistry<TSignal>.SyncHandlers.First;
            while (syncNode != null)
            {
                var next = syncNode.Next;
                if (IsDestroyed(syncNode.Value.Owner))
                {
                    SignalRegistry<TSignal>.SyncHandlers.Remove(syncNode);
                    UntrackSubscription(typeof(TSignal), syncNode.Value.Handler, syncNode.Value.Owner);
                }
                syncNode = next;
            }
        }

        /// <summary>
        /// Removes all signal subscriptions registered by the specified owner.
        /// Intended as a last-resort safety net, not a primary unsubscription strategy.
        /// </summary>
        /// <remarks>
        /// <b>Preferred pattern:</b> classes that extend <see cref="SignalBehaviour"/> have their
        /// subscriptions cleaned up automatically in <c>OnDisable</c> via
        /// <see cref="ISignalSubscription"/>. For any class that calls
        /// <see cref="Subscribe{TSignal}"/> directly, explicitly call
        /// <see cref="Unsubscribe{TSignal}"/> per signal in <c>OnDisable</c> instead.
        /// </remarks>
        [Obsolete("Do not call this. Consider explicit Unsubscribe<T> calls per signal in OnDisable for O(1) removal.")]
        public static void UnsubscribeAll(Object owner)
        {
            if (owner == null)
                return;

            Debug.LogWarning(
                $"[Signal] UnsubscribeAll called for {owner.name}! This function does not work, check its description."
            );
        }

        /// <summary>
        /// Get subscriber count for a signal (for debugging)
        /// </summary>
        public static int GetSubscriberCount<TSignal>()
            where TSignal : struct, ISignal
        {
            CleanNullSubscribers<TSignal>();
            return SignalRegistry<TSignal>.SyncHandlers.Count;
        }

        #endregion

        #region Debug Tracking (Editor Only)

        static void TrackSubscription(Type signalType, object handler, Object owner, bool isAsync)
        {
#if UNITY_EDITOR
            if (owner == null)
                return;

            var debugInfo = new HandlerDebugInfo
            {
                SignalType = signalType,
                Handler = handler,
                Owner = owner,
                IsAsync = isAsync,
                SubscriptionTime = DateTime.Now,
            };

            if (!globalHandlerOwners.TryGetValue(owner, out var infos))
            {
                infos = new List<HandlerDebugInfo>();
                globalHandlerOwners[owner] = infos;
            }
            infos.Add(debugInfo);

            if (!globalSignalSubscribers.TryGetValue(signalType, out var subs))
            {
                subs = new List<HandlerDebugInfo>();
                globalSignalSubscribers[signalType] = subs;
            }
            subs.Add(debugInfo);
#endif
        }

        static void UntrackSubscription(Type signalType, object handler, Object owner)
        {
#if UNITY_EDITOR
            // Cast to object to avoid Unity's custom null check for destroyed objects
            // We want to be able to untrack destroyed objects
            if ((object)owner is not null && globalHandlerOwners.TryGetValue(owner, out var infos))
            {
                infos.RemoveAll(info => info.SignalType == signalType && info.Handler == handler);

                if (infos.Count == 0)
                {
                    globalHandlerOwners.Remove(owner);
                }
            }

            if (globalSignalSubscribers.TryGetValue(signalType, out var subs))
            {
                subs.RemoveAll(info => info.Handler == handler);
                if (subs.Count == 0)
                    globalSignalSubscribers.Remove(signalType);
            }
#endif
        }

        static void UpdateHubInfo<TSignal>(Object invoker = null)
            where TSignal : struct, ISignal
        {
#if UNITY_EDITOR
            if (invoker != null)
                SignalRegistry<TSignal>.LastTrigger = new TriggerDebugInfo
                {
                    Invoker = invoker,
                    TriggerTime = DateTime.Now,
                    SignalType = typeof(TSignal),
                };

            OnSignalFired?.Invoke(typeof(TSignal), SignalRegistry<TSignal>.SyncHandlers.Count, invoker);
#endif
        }

#if UNITY_EDITOR
        public static List<HandlerDebugInfo> GetSubscriptionsFor(Object owner)
        {
            if (owner == null)
                return null;

            if (globalHandlerOwners.TryGetValue(owner, out var list))
            {
                // Return a copy to avoid modification during iteration issues, though usually read-only
                var results = new List<HandlerDebugInfo>(list);
                // Sort by signal name
                results.Sort((a, b) => string.Compare(a.SignalType.Name, b.SignalType.Name, StringComparison.Ordinal));
                return results;
            }

            return null;
        }

        public static List<HandlerDebugInfo> GetSubscribers(Type signalType)
        {
            if (globalSignalSubscribers.TryGetValue(signalType, out var list))
            {
                return new List<HandlerDebugInfo>(list);
            }
            return new List<HandlerDebugInfo>();
        }
#endif

        #endregion
    }
}
