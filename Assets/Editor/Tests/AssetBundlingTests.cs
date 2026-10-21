using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Baseline.Tests.Editor
{
    public sealed class AssetBundlingTests
    {
        [Test]
        public void ProjectHasNoResourcesAssetsOrRuntimeLoads()
        {
            Assert.That(AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/")
                && (path.Contains("/Resources/") || path.EndsWith("/Resources"))), Is.Empty);
            foreach (string path in Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories))
                Assert.That(File.ReadAllText(path), Does.Not.Match(@"Resources\s*\.\s*Load"), path);
        }

        [Test]
        public void IndexHasNoDirectBundledContentDependencies()
        {
            Assert.That(AssetDatabase.GetDependencies("Assets/Scenes/Index.unity", true)
                .Where(path => path.StartsWith("Assets/Bundles/") || path.StartsWith("Assets/Baseline/")),
                Is.Empty, "Index must use AssetReferences to avoid duplicating bundle content in player data.");
        }

        [Test]
        public void MigratedContentHasOneAddressableOwner()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var entries = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups.Where(group => group != null))
                foreach (var entry in group.entries) entry.GatherAllAssets(entries, true, true, false);
            foreach (string folder in new[] { "Assets/Bundles/Core/Prefab/UI", "Assets/Bundles/Core/Config",
                "Assets/Bundles/Core/Font/TextMeshPro", "Assets/Bundles/Core/Settings/TextMeshPro",
                "Assets/Bundles/Core/Texture/Characters" })
                foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetDatabase.IsValidFolder(path)) continue;
                    Assert.That(entries.Count(entry => entry.guid == guid), Is.EqualTo(1), path);
                }
        }

        [Test]
        public void CoreUsesOneFolderEntryWithPathBasedAddresses()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var core = settings.FindGroup("Core");
            Assert.That(core.entries, Has.Count.EqualTo(1));
            var root = core.entries.Single();
            Assert.That(root.AssetPath, Is.EqualTo("Assets/Bundles/Core"));
            Assert.That(root.address, Is.EqualTo("Core"));

            var entries = new List<AddressableAssetEntry>();
            root.GatherAllAssets(entries, true, true, false);
            foreach (var entry in entries)
                Assert.That(entry.address, Is.EqualTo(entry.AssetPath.Substring("Assets/Bundles/".Length)),
                    entry.AssetPath);
            foreach (string path in new[] { "Prefab/UI/IndexMenu.prefab", "Prefab/UI/ui_loading.prefab",
                "Config/Generics/SharedUIContent.asset", "Settings/InputSystem_Actions.inputactions" })
                Assert.That(entries.Any(entry => entry.address == "Core/" + path), Is.True, path);
        }

        [Test]
        public void CoreAndDemoContentHaveSeparateOwners()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            var core = settings.FindGroup("Core");
            Assert.That(core, Is.Not.Null);
            Assert.That(settings.DefaultGroup, Is.SameAs(core));
            Assert.That(core.GetSchema<BundledAssetGroupSchema>().BundleMode,
                Is.EqualTo(BundledAssetGroupSchema.BundlePackingMode.PackTogether));
            var entries = new List<AddressableAssetEntry>();
            foreach (var group in settings.groups.Where(group => group != null))
                foreach (var entry in group.entries) entry.GatherAllAssets(entries, true, true, false);
            foreach (string path in AssetDatabase.GetAllAssetPaths().Where(path =>
                path.StartsWith("Assets/Bundles/Core/") || path.StartsWith("Assets/Bundles/Demo/")))
            {
                if (AssetDatabase.IsValidFolder(path)) continue;
                var owners = entries.Where(entry => entry.guid == AssetDatabase.AssetPathToGUID(path)).ToArray();
                Assert.That(owners, Has.Length.EqualTo(1), path);
                string group = owners[0].parentGroup.Name;
                if (path.StartsWith("Assets/Bundles/Demo/")) Assert.That(group, Is.EqualTo("Demo"), path);
                else Assert.That(group == "Core" || group.StartsWith("Localization-"), Is.True, path + ": " + group);
            }
            Assert.That(core.entries.Any(entry => entry.AssetPath.StartsWith("Assets/Bundles/Demo")), Is.False);
            var demo = settings.FindGroup("Demo");
            if (AssetDatabase.IsValidFolder("Assets/Bundles/Demo"))
            {
                Assert.That(demo, Is.Not.Null);
                Assert.That(demo.entries.All(entry => Baseline.Editor.DemoCleanup.IsDemoPath(entry.AssetPath)), Is.True);
            }
            else Assert.That(demo, Is.Null, "Cleanup must remove the empty demo group.");
        }
    }
}
