using System.Collections.Generic;
using System.Text.RegularExpressions;
using Hypocycloid.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Baseline.Tests.Editor
{
    /// <summary>
    /// The signal bus ported from Landstrum, including what this port changed: a handler may
    /// unsubscribe itself or another mid-fire, and a destroyed owner's handlers really are skipped.
    /// </summary>
    public class SignalBusTests
    {
        struct PingSignal : ISignal
        {
            public int Value;
        }

        struct OtherSignal : ISignal { }

        /// <summary>Exposes the protected subscription helpers and lifecycle so EditMode can drive them.</summary>
        sealed class Listener : SignalBehaviour
        {
            public readonly List<int> Heard = new();

            public void ListenWhileEnabled() => Subscribe<PingSignal>(signal => Heard.Add(signal.Value));

            public void ListenForLife() => SubscribeLifetime<PingSignal>(signal => Heard.Add(-signal.Value));

            public void Enable() => OnEnable();

            public void Disable() => OnDisable();

            public void Destroyed() => OnDestroy();
        }

        readonly List<(System.Action<PingSignal> handler, Object owner)> subscribed = new();

        void Listen(System.Action<PingSignal> handler, Object owner = null)
        {
            SignalBus.Subscribe(handler, owner);
            subscribed.Add((handler, owner));
        }

        [TearDown]
        public void TearDown()
        {
            foreach ((System.Action<PingSignal> handler, Object owner) in subscribed)
                SignalBus.Unsubscribe(handler, owner);
            subscribed.Clear();
        }

        [Test]
        public void AFire_ReachesEverySubscriberInOrder_UntilTheyUnsubscribe()
        {
            var heard = new List<string>();
            System.Action<PingSignal> first = signal => heard.Add("a" + signal.Value);
            System.Action<PingSignal> second = signal => heard.Add("b" + signal.Value);
            Listen(first);
            Listen(second);

            SignalBus.Fire(new PingSignal { Value = 1 });
            SignalBus.Unsubscribe(first);
            SignalBus.Fire(new PingSignal { Value = 2 });
            SignalBus.Fire(new OtherSignal());

            Assert.That(heard, Is.EqualTo(new[] { "a1", "b1", "b2" }));
        }

        [Test]
        public void AHandler_MayUnsubscribeItselfOrALaterOne_WithoutCuttingTheFireShort()
        {
            var heard = new List<string>();
            System.Action<PingSignal> later = _ => heard.Add("later");
            System.Action<PingSignal> last = _ => heard.Add("last");
            System.Action<PingSignal> self = null;
            self = _ =>
            {
                heard.Add("self");
                SignalBus.Unsubscribe(self);
                SignalBus.Unsubscribe(later);
            };
            Listen(self);
            Listen(later);
            Listen(last);

            SignalBus.Fire(new PingSignal());
            SignalBus.Fire(new PingSignal());

            Assert.That(heard, Is.EqualTo(new[] { "self", "last", "last" }), "an unsubscribed handler ran, or the walk stopped early");
        }

        [Test]
        public void AHandlerThatThrows_IsLogged_AndTheOthersStillRun()
        {
            var heard = new List<int>();
            Listen(_ => throw new System.InvalidOperationException("boom"));
            Listen(signal => heard.Add(signal.Value));

            LogAssert.Expect(LogType.Error, new Regex("Error in sync handler for PingSignal"));
            SignalBus.Fire(new PingSignal { Value = 7 });

            Assert.That(heard, Is.EqualTo(new[] { 7 }));
        }

        [Test]
        public void HandlersOfADestroyedOwner_AreSkippedAndDropped()
        {
            var owner = new GameObject("SignalOwner");
            var heard = new List<int>();
            Listen(signal => heard.Add(signal.Value), owner);
            int before = SignalBus.GetSubscriberCount<PingSignal>();

            Object.DestroyImmediate(owner);
            SignalBus.Fire(new PingSignal { Value = 1 });

            Assert.That(heard, Is.Empty, "a destroyed owner's handler still ran");
            Assert.That(SignalBus.GetSubscriberCount<PingSignal>(), Is.EqualTo(before - 1));
        }

        [Test]
        public void SignalBehaviour_DropsEnableSubscriptionsWhenDisabled_AndLifetimeOnesWhenDestroyed()
        {
            var host = new GameObject("SignalBehaviourHost");
            try
            {
                Listener listener = host.AddComponent<Listener>();
                listener.Enable();
                listener.ListenWhileEnabled();
                listener.ListenForLife();

                SignalBus.Fire(new PingSignal { Value = 1 });
                listener.Disable();
                SignalBus.Fire(new PingSignal { Value = 2 });
                listener.Destroyed();
                SignalBus.Fire(new PingSignal { Value = 3 });

                Assert.That(listener.Heard, Is.EqualTo(new[] { 1, -1, -2 }));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
