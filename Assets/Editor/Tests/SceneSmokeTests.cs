using System.Collections.Generic;
using System.Linq;
using Baseline;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace Baseline.Tests.Editor
{
    public class SceneSmokeTests
    {
        const string SceneFolder = "Assets/Scenes";

        [TestCase("Index", "IndexManager")]
        public void Scene_HasNoBrokenObjectsAndContainsItsController(
            string sceneName,
            string controllerTypeName
        )
        {
            string scenePath = $"{SceneFolder}/{sceneName}.unity";
            Assert.That(
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
                Is.Not.Null,
                $"Scene asset is missing: {scenePath}"
            );

            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool openedForTest = !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            }

            try
            {
                var issues = new List<string>();
                var controllerPaths = new List<string>();
                var inputModules = new List<InputSystemUIInputModule>();
                int cameraCount = 0;

                var roots = new List<GameObject>(scene.GetRootGameObjects());
                var bootstrap = roots.SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
                var startup = new SerializedObject(bootstrap);
                string contentGuid = startup.FindProperty("content").FindPropertyRelative("m_AssetGUID").stringValue;
                var content = AssetDatabase.LoadAssetAtPath<SharedUIContent>(AssetDatabase.GUIDToAssetPath(contentGuid));
                Assert.That(content, Is.Not.Null);
                Assert.That(content.contextPrefab, Is.Not.Null);
                string menuGuid = startup.FindProperty("menuPrefab").FindPropertyRelative("m_AssetGUID").stringValue;
                var menu = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(menuGuid));
                Assert.That(menu, Is.Not.Null, "Index must reference its authored menu prefab");
                roots.Add(menu);

                foreach (GameObject root in roots)
                {
                    cameraCount += root.GetComponentsInChildren<Camera>(true).Length;
                    inputModules.AddRange(
                        root.GetComponentsInChildren<InputSystemUIInputModule>(true)
                    );

                    foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                    {
                        GameObject gameObject = transform.gameObject;
                        int missingScripts =
                            GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
                        if (missingScripts > 0)
                        {
                            issues.Add(
                                $"{GetPath(transform)} has {missingScripts} missing script(s)"
                            );
                        }

                        if (
                            PrefabUtility.GetPrefabInstanceStatus(gameObject)
                            == PrefabInstanceStatus.MissingAsset
                        )
                        {
                            issues.Add($"{GetPath(transform)} has a missing prefab asset");
                        }

                        foreach (Component component in gameObject.GetComponents<Component>())
                        {
                            if (component != null && component.GetType().Name == controllerTypeName)
                            {
                                controllerPaths.Add(GetPath(transform));
                            }
                        }
                    }
                }

                Assert.That(issues, Is.Empty, string.Join("\n", issues));
                Assert.That(cameraCount, Is.GreaterThan(0), $"{sceneName} has no Camera");
                Assert.That(
                    controllerPaths,
                    Has.Count.EqualTo(1),
                    $"Expected one {controllerTypeName}, found: {string.Join(", ", controllerPaths)}"
                );
                Assert.That(
                    inputModules,
                    Has.Count.EqualTo(1),
                    $"{sceneName} must contain exactly one InputSystemUIInputModule"
                );
                AssertInputModuleIsConfigured(sceneName, inputModules.Single());
            }
            finally
            {
                if (openedForTest)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        static void AssertInputModuleIsConfigured(
            string sceneName,
            InputSystemUIInputModule inputModule
        )
        {
            Assert.That(inputModule.enabled, Is.True, $"{sceneName} input module is disabled");
            Assert.That(
                EditorUtility.IsPersistent(inputModule)
                    ? inputModule.GetComponentsInParent<Transform>(true).All(node => node.gameObject.activeSelf)
                    : inputModule.gameObject.activeInHierarchy,
                Is.True,
                $"{sceneName} EventSystem is inactive"
            );
            Assert.That(
                inputModule.actionsAsset,
                Is.Not.Null,
                $"{sceneName} input module has no actions asset"
            );

            AssertActionReference(sceneName, "Point", inputModule.point);
            AssertActionReference(sceneName, "Click", inputModule.leftClick);
            AssertActionReference(sceneName, "Move", inputModule.move);
            AssertActionReference(sceneName, "Submit", inputModule.submit);
            AssertActionReference(sceneName, "Cancel", inputModule.cancel);
        }

        static void AssertActionReference(
            string sceneName,
            string actionName,
            InputActionReference actionReference
        )
        {
            Assert.That(
                actionReference,
                Is.Not.Null,
                $"{sceneName} input module has no {actionName} action reference"
            );
            Assert.That(
                actionReference.action,
                Is.Not.Null,
                $"{sceneName} input module's {actionName} action cannot be resolved"
            );
        }

        [Test]
        public void GameRegister_MapsEveryGameToAnAvailableScene()
        {
            GameRegister register = AssetDatabase.LoadAssetAtPath<GameRegister>(
                "Assets/Bundles/Core/Settings/GameRegister.asset"
            );
            Assert.That(register, Is.Not.Null);
            Assert.That(register.games, Is.Not.Empty, "The index scene must be registered first");
            Assert.That(
                register.games.Select(game => game.sceneName).Distinct().Count(),
                Is.EqualTo(register.games.Count),
                "Registered scene names must be unique"
            );

            HashSet<string> enabledBuildScenes = EditorBuildSettings
                .scenes.Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToHashSet();
            AddressableAssetSettings addressables = AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(addressables, Is.Not.Null);

            for (int i = 0; i < register.games.Count; i++)
            {
                GameMeta game = register.games[i];
                if (i > 0) Assert.That(game.sceneAsset, Is.Not.Null, game.sceneName);
                string scenePath = i == 0 ? $"{SceneFolder}/{game.sceneName}.unity"
                    : AssetDatabase.GUIDToAssetPath(game.sceneAsset.AssetGUID);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath),
                    Is.Not.Null,
                    $"Registered scene is missing: {scenePath}"
                );
                Assert.That(System.IO.Path.GetFileNameWithoutExtension(scenePath), Is.EqualTo(game.sceneName));

                if (i == 0)
                {
                    Assert.That(
                        enabledBuildScenes,
                        Does.Contain(scenePath),
                        "The index scene must be enabled in Build Settings"
                    );
                    continue;
                }

                string guid = AssetDatabase.AssetPathToGUID(scenePath);
                AddressableAssetEntry entry = addressables.FindAssetEntry(guid);
                Assert.That(
                    entry,
                    Is.Not.Null,
                    $"{SceneNameForMessage(game)} is not Addressable"
                );
                Assert.That(
                    entry.address,
                    Is.EqualTo(game.sceneName),
                    $"{SceneNameForMessage(game)} has the wrong Addressable address"
                );
            }
        }

        static string GetPath(Transform transform)
        {
            string path = transform.name;
            while (transform.parent != null)
            {
                transform = transform.parent;
                path = $"{transform.name}/{path}";
            }

            return path;
        }

        static string SceneNameForMessage(GameMeta game) => $"Scene '{game.sceneName}'";
    }
}
