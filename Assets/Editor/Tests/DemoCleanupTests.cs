using System;
using System.Collections.Generic;
using System.IO;
using Baseline.Defs;
using Baseline.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Baseline.Tests.Editor
{
    public sealed class DemoCleanupTests
    {
        [TestCase("Assets/Scripts/Demo/RelayRun/DemoRunnerGame.cs", true)]
        [TestCase("Assets/Bundles/Demo/Examples/DemoExampleQuest.asset", true)]
        [TestCase("Assets/Bundles/Demo/DemoRelayRun.unity", true)]
        [TestCase("Assets/StreamingAssets/Desc/DemoRelayRun_en.md", true)]
        [TestCase("Assets/StreamingAssets/Desc/DemoRelayRun_zh.md", true)]
        [TestCase("Assets/StreamingAssets/Desc/OtherGame_en.md", false)]
        [TestCase("Assets/Editor/Demo/RelayRun/DemoRunnerModelTests.cs", true)]
        [TestCase("Assets/Scripts/Demo/CustomExample/Game.cs", true)]
        [TestCase("Assets/Scripts/DemoCustom/Game.cs", false)]
        [TestCase("Assets/Bundles/DemoCustom/Keep.asset", false)]
        [TestCase("Assets/Game/DemoNamedFeature.cs", false)]
        [TestCase("Assets/Scripts/Core/Defs/QuestDef.cs", false)]
        [TestCase("Assets/Bundles/Core/Config/Dialogue/GuideCharacter.asset", false)]
        [TestCase("Assets/Bundles/Core/Prefab/UI/IndexMenu.prefab", false)]
        public void RemovalOwnershipUsesExactRoots(string path, bool expected)
            => Assert.That(DemoCleanup.IsDemoPath(path), Is.EqualTo(expected));

        [TestCase("Assets/Scripts/Demo/RelayRun/DemoRunnerGame.cs", true)]
        [TestCase("ProjectSettings/EditorBuildSettings.asset", true)]
        [TestCase("Assets/../../outside.txt", false)]
        [TestCase("C:/outside.txt", false)]
        [TestCase("Assets/file:stream", false)]
        [TestCase("Packages/manifest.json", false)]
        public void BackupRestoreRejectsPathsOutsideItsOwnedDirectories(string path, bool expected)
            => Assert.That(DemoCleanup.IsSafeBackupPath(path), Is.EqualTo(expected));

        [Test]
        public void ASceneNameAloneDoesNotMakeACustomGameRemovable()
        {
            var game = new GameMeta
            {
                sceneName = "DemoRelayRun",
                sceneAsset = new AssetReference(AssetDatabase.AssetPathToGUID(DemoCleanup.IndexPath))
            };
            Assert.That(DemoCleanup.IsDemoGame(game), Is.False);
            game.sceneAsset = null;
            Assert.That(DemoCleanup.IsDemoGame(game), Is.False);
        }

        [Test]
        public void RetainedReferenceToCustomDemoRuntimeTypeBlocksRemoval()
        {
            const string demoFolder = "Assets/Scripts/Demo";
            const string demoPath = demoFolder + "/CleanupRuntimeProbe.cs";
            const string retainedPath = "Assets/CleanupConsumerProbe.cs";
            bool createdFolder = !AssetDatabase.IsValidFolder(demoFolder);
            try
            {
                if (createdFolder) AssetDatabase.CreateFolder("Assets/Scripts", "Demo");
                File.WriteAllText(demoPath, "namespace CustomExample { public class CleanupRuntimeProbe {} }");
                File.WriteAllText(retainedPath, "public class CleanupConsumerProbe { CustomExample.CleanupRuntimeProbe value; }");
                AssetDatabase.ImportAsset(demoPath);
                AssetDatabase.ImportAsset(retainedPath);
                Assert.That(DemoCleanup.Inspect().Blockers.Exists(message =>
                    message.StartsWith(retainedPath, StringComparison.Ordinal)), Is.True);
            }
            finally
            {
                AssetDatabase.DeleteAsset(retainedPath);
                AssetDatabase.DeleteAsset(demoPath);
                if (createdFolder) AssetDatabase.DeleteAsset(demoFolder);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RetainedReferencesToDemoContentBlockRemoval(bool addressableReference)
        {
            var folders = new List<string>();
            string id = Guid.NewGuid().ToString("N");
            string probeFolder = "Assets/Bundles/Demo/Examples/CleanupProbe" + id;
            string retainedPath = "Assets/CleanupProbe" + id + ".asset";
            try
            {
                EnsureFolder(probeFolder);
                var quest = ScriptableObject.CreateInstance<QuestDef>();
                AssetDatabase.CreateAsset(quest, probeFolder + "/Quest.asset");
                ScriptableObject retained;
                if (addressableReference)
                {
                    var registry = ScriptableObject.CreateInstance<GameRegister>();
                    registry.games = new List<GameMeta> { new() { sceneName = "Retained custom scene",
                        sceneAsset = new AssetReference(AssetDatabase.AssetPathToGUID(probeFolder + "/Quest.asset")) } };
                    retained = registry;
                }
                else
                {
                    var database = ScriptableObject.CreateInstance<DefDatabase>();
                    var serialized = new SerializedObject(database);
                    var entries = serialized.FindProperty("definitions");
                    entries.arraySize = 1;
                    entries.GetArrayElementAtIndex(0).objectReferenceValue = quest;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    retained = database;
                }
                AssetDatabase.CreateAsset(retained, retainedPath);
                AssetDatabase.SaveAssets();
                var plan = DemoCleanup.Inspect();
                Assert.That(plan.Blockers.Exists(message => message.StartsWith(retainedPath, StringComparison.Ordinal)), Is.True);
                Assert.That(AssetDatabase.LoadAssetAtPath<QuestDef>(probeFolder + "/Quest.asset"), Is.Not.Null,
                    "Inspection must not remove any content.");
            }
            finally
            {
                AssetDatabase.DeleteAsset(retainedPath);
                foreach (string folder in folders) AssetDatabase.DeleteAsset(folder);
            }

            void EnsureFolder(string path)
            {
                if (AssetDatabase.IsValidFolder(path)) return;
                int slash = path.LastIndexOf('/');
                string parent = path.Substring(0, slash);
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
                folders.Insert(0, path);
            }
        }
    }
}
