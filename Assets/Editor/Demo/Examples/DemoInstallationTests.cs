using System.Collections.Generic;
using System.Linq;
using Baseline.Editor;
using NUnit.Framework;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;

namespace Baseline.Demo.Examples.Editor
{
    public sealed class DemoInstallationTests
    {
        [Test]
        public void DemoUsesOneFolderAndKeepsItsSceneLaunchKey()
        {
            var demo = AddressableAssetSettingsDefaultObject.Settings.FindGroup("Demo");
            Assert.That(demo, Is.Not.Null);
            Assert.That(demo.entries, Has.Count.EqualTo(2));
            var root = demo.entries.Single(entry => entry.AssetPath == "Assets/Bundles/Demo");
            Assert.That(root.address, Is.EqualTo("Demo"));
            var scene = demo.entries.Single(entry => entry.AssetPath == "Assets/Bundles/Demo/DemoRelayRun.unity");
            Assert.That(scene.address, Is.EqualTo("DemoRelayRun"));

            var entries = new List<AddressableAssetEntry>();
            root.GatherAllAssets(entries, true, true, false);
            Assert.That(entries.Any(entry => entry.guid == scene.guid), Is.False);
            foreach (var entry in entries)
                Assert.That(entry.address, Is.EqualTo(entry.AssetPath.Substring("Assets/Bundles/".Length)),
                    entry.AssetPath);
            foreach (string path in new[] { "Examples/DemoExampleDatabase.asset",
                "RelayRun/Prefabs/DemoRelayMenu.prefab", "RelayRun/Config/DemoRelayDatabase.asset" })
                Assert.That(entries.Any(entry => entry.address == "Demo/" + path), Is.True, path);
        }

        [Test]
        public void ShippedDemosHaveNoUnexpectedRetainedDependencies()
        {
            var plan = DemoCleanup.Inspect();
            Assert.That(plan.Assets, Is.Not.Empty);
            Assert.That(plan.Blockers, Is.Empty, string.Join("\n", plan.Blockers));
        }
    }
}
