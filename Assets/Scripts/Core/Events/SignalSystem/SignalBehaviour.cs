using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hypocycloid.Core
{
    /// <summary>
    /// Base class for MonoBehaviours that use SignalBus
    /// Automatically manages subscriptions and unsubscriptions
    /// Prevents memory leaks by cleaning up on disable/destroy
    /// </summary>
    [ShowActiveSignals]
    public abstract class SignalBehaviour : MonoBehaviour
    {
        // Subscriptions that persist for the lifetime of the object
        readonly List<ISignalSubscription> lifetimeSubscriptions = new();

        // Subscriptions that are cleared when the object is disabled
        readonly List<ISignalSubscription> enableSubscriptions = new();

        [NonSerialized]
        bool isCallOnce = false;

        protected virtual void OnEnable() { }

        protected virtual void OnDisable()
        {
            UnsubscribeAll(enableSubscriptions);
        }

        protected virtual void OnDestroy()
        {
            // In standard Unity lifecycle, OnDisable is called before OnDestroy,
            // However this would never be cleared if inheritants forget to call base.OnDisable(),
            // So this is a safty measure.
            UnsubscribeAll(enableSubscriptions);
            UnsubscribeAll(lifetimeSubscriptions);
        }

        #region Synchronous Signal Subscriptions

        /// <summary>
        /// Subscribe to a signal until this object is disabled. Call from <c>OnEnable</c>.
        /// </summary>
        protected void Subscribe<TSignal>(Action<TSignal> handler)
            where TSignal : struct, ISignal
        {
            if (handler == null)
                return;

            SignalBus.Subscribe(handler, this);

            var subscription = new SyncSignalSubscription<TSignal>(handler, this);
            AddSubscription(subscription);
        }

        /// <summary>
        /// Subscribe to a signal until this object is destroyed, whether or not it is enabled.
        /// Call once, typically from <c>Awake</c>.
        /// </summary>
        protected void SubscribeLifetime<TSignal>(Action<TSignal> handler)
            where TSignal : struct, ISignal
        {
            CheckContext(() => Subscribe(handler));
        }

        #endregion

        #region Subscription Management

        void AddSubscription(ISignalSubscription subscription)
        {
            if (isCallOnce)
                lifetimeSubscriptions.Add(subscription);
            else
                enableSubscriptions.Add(subscription);
        }

        void CheckContext(Action action)
        {
            isCallOnce = true;
            try
            {
                action?.Invoke();
            }
            finally
            {
                isCallOnce = false;
            }
        }

        void UnsubscribeAll(List<ISignalSubscription> subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                try
                {
                    subscription.Unsubscribe();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Signal] Error unsubscribing in {name}: {ex}");
                }
            }
            subscriptions.Clear();
        }

        #endregion
    }
}
