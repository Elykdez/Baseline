using System.Collections.Generic;
using Hypocycloid.Utils;
using NUnit.Framework;
using UnityEngine;

namespace Baseline.Tests.Editor
{
    public sealed class MonoSingletonTests
    {
        sealed class Manager : MonoSingleton<Manager>
        {
            public int Initializations;
            protected override bool PersistAcrossScenes => false;
            protected override void Init() => Initializations++;
            public void RunAwake() => Awake();
        }

        readonly List<GameObject> hosts = new();

        Manager Make(bool active = true)
        {
            var host = new GameObject("SingletonTest");
            hosts.Add(host);
            host.SetActive(active);
            return host.AddComponent<Manager>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject host in hosts)
                if (host != null) Object.DestroyImmediate(host);
            hosts.Clear();
        }

        [Test]
        public void GetterClaimsInactiveAuthoredManagerAndInitializesOnlyOnce()
        {
            Manager manager = Make(false);
            Assert.That(Manager.Ins, Is.SameAs(manager));
            manager.RunAwake();
            Assert.That(Manager.Instance, Is.SameAs(manager));
            Assert.That(manager.Initializations, Is.EqualTo(1));
        }

        [Test]
        public void DestroyedManagerCanBeReplacedWithoutStaleRegistration()
        {
            Manager first = Make();
            Assert.That(Manager.Ins, Is.SameAs(first));
            Object.DestroyImmediate(first.gameObject);
            Assert.That(Manager.HasInstance, Is.False);

            Manager replacement = Make();
            Assert.That(Manager.Ins, Is.SameAs(replacement));
            Assert.That(replacement.Initializations, Is.EqualTo(1));
        }

        [Test]
        public void DuplicateComponentCannotReplaceOrReinitializeManager()
        {
            Manager first = Make();
            Assert.That(Manager.Ins, Is.SameAs(first));
            Manager duplicate = Make();
            GameObject duplicateHost = duplicate.gameObject;
            duplicate.RunAwake();

            Assert.That(duplicate == null, Is.True);
            Assert.That(duplicateHost != null, Is.True);
            Assert.That(Manager.Ins, Is.SameAs(first));
            Assert.That(first.Initializations, Is.EqualTo(1));
        }
    }
}
