using System;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace Hypocycloid.Core
{
    /// <summary>
    /// Marker interface for signals.
    /// All signals must be structs to avoid allocation.
    /// </summary>
    public interface ISignal
    {
        // Marker interface - no members required
    }

    /// <summary>
    /// Interface for tracking subscriptions
    /// </summary>
    public interface ISignalSubscription
    {
        void Unsubscribe();
    }

    #region Handler & Subscription

    /// <summary>
    /// Synchronous signal subscription tracker
    /// </summary>
    internal class SyncSignalSubscription<TSignal> : ISignalSubscription
        where TSignal : struct, ISignal
    {
        Action<TSignal> handler;
        Object owner;

        public SyncSignalSubscription(Action<TSignal> handler, Object owner)
        {
            this.handler = handler;
            this.owner = owner;
        }

        public void Unsubscribe()
        {
            if (handler != null)
            {
                SignalBus.Unsubscribe(handler, owner);
                handler = null;
                owner = null;
            }
        }
    }

    /// <summary>
    /// Synchronous signal handler wrapper
    /// Stores handler, owner, and linked list node for O(1) removal
    /// </summary>
    internal class SignalHandler<TSignal>
        where TSignal : struct, ISignal
    {
        public Action<TSignal> Handler;
        public Object Owner;
        public LinkedListNode<SignalHandler<TSignal>> Node;

        public SignalHandler(Action<TSignal> handler, Object owner)
        {
            Handler = handler;
            Owner = owner;
        }
    }

    #endregion

    #region Debug Info

#if UNITY_EDITOR
    public struct HandlerDebugInfo
    {
        public Type SignalType;
        public object Handler;
        public Object Owner;

        /// <summary>Always false here: this port carries only the synchronous half of the bus.</summary>
        public bool IsAsync;
        public DateTime SubscriptionTime;
    }

    public struct TriggerDebugInfo
    {
        public Object Invoker;
        public DateTime TriggerTime;
        public Type SignalType;
    }
#endif

    #endregion
}
