using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.TestTools;

namespace Baseline.Tests.Editor
{
    public sealed partial class BaselineIntegrationTests
    {
        [Test]
        public void AddressableEntriesResolveToExistingAssets()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            foreach (var group in settings.groups)
            {
                Assert.That(group, Is.Not.Null, "Missing Addressables group");
                foreach (var entry in group.entries)
                    Assert.That(AssetDatabase.GUIDToAssetPath(entry.guid), Is.Not.Empty,
                        $"Missing Addressable asset: {group.Name}/{entry.address}");
            }
        }

        [UnityTest]
        public IEnumerator IndexStartsAndLoadsSharedAddressableContent()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Index.unity");
            yield return new EnterPlayMode();
            yield return WaitForIndex();

            var initialization = LocalizationSettings.InitializationOperation;
            double deadline = EditorApplication.timeSinceStartup + 60;
            while (!initialization.IsDone && EditorApplication.timeSinceStartup < deadline)
                yield return null;

            Assert.That(initialization.IsDone, Is.True, "Localization initialization timed out");
            Assert.That(initialization.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
            Assert.That(LocalizationSettings.AvailableLocales.Locales.Count, Is.GreaterThan(0));
            Assert.That(Object.FindFirstObjectByType<ContextManager>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<IndexManager>(), Is.Not.Null);
            SharedUIContent shared = SharedUIContent.Instance;
            Assert.That(shared, Is.Not.Null, "Index must load the bundled catalog before creating UI.");
            Assert.That(TMP_Settings.instance, Is.SameAs(shared.textSettings));
            var catalog = Addressables.LoadAssetAsync<SharedUIContent>(
                AssetDatabase.AssetPathToGUID(SharedUIContent.EditorAssetPath));
            yield return catalog;
            try
            {
                Assert.That(catalog.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                Assert.That(catalog.Result, Is.SameAs(shared));
            }
            finally { if (catalog.IsValid()) Addressables.Release(catalog); }

            var prefab = Addressables.LoadAssetAsync<GameObject>("Core/Prefab/UI/ui_loading.prefab");
            try
            {
                deadline = EditorApplication.timeSinceStartup + 60;
                while (!prefab.IsDone && EditorApplication.timeSinceStartup < deadline)
                    yield return null;
                Assert.That(prefab.IsDone, Is.True, "Shared prefab load timed out");
                Assert.That(prefab.Status, Is.EqualTo(AsyncOperationStatus.Succeeded));
                Assert.That(prefab.Result, Is.Not.Null);
            }
            finally
            {
                if (prefab.IsValid()) Addressables.Release(prefab);
            }

            var manager = Object.FindFirstObjectByType<IndexManager>();
            var context = ContextManager.Ins;
            context.ReloadCurrentScene();
            deadline = EditorApplication.timeSinceStartup + 60;
            while (manager != null && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(manager == null, Is.True, "Reload did not unload the previous menu");
            yield return WaitForIndex();
            Assert.That(ContextManager.Ins, Is.SameAs(context));
            Assert.That(SharedUIContent.Instance, Is.SameAs(shared));
            Assert.That(Object.FindObjectsByType<ContextManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
            Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

            yield return new ExitPlayMode();
        }

        static IEnumerator WaitForIndex()
        {
            double deadline = EditorApplication.timeSinceStartup + 60;
            GameBootstrap bootstrap;
            do
            {
                bootstrap = Object.FindFirstObjectByType<GameBootstrap>();
                if (bootstrap != null && bootstrap.IsReady) yield break;
                yield return null;
            } while (EditorApplication.timeSinceStartup < deadline);
            Assert.Fail("Index menu did not finish loading");
        }
    }
}
