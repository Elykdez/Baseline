using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Tables;

namespace Baseline.Editor
{
    /// <summary>Removes the shipped examples without making framework code depend on them.</summary>
    public sealed class DemoCleanup : EditorWindow
    {
        public const string IndexPath = "Assets/Scenes/Index.unity";
        public const string MenuPath = "Assets/Bundles/Core/Prefab/UI/IndexMenu.prefab";
        public const string RegistryPath = "Assets/Bundles/Core/Settings/GameRegister.asset";
        public static readonly string[] DemoRoots =
        {
            "Assets/Scripts/Demo", "Assets/Editor/Demo", "Assets/Bundles/Demo",
            "Assets/StreamingAssets/Desc/DemoRelayRun_en.md", "Assets/StreamingAssets/Desc/DemoRelayRun_zh.md",
        };

        [Serializable]
        public sealed class BackupManifest { public string[] files; }

        public sealed class RemovalPlan
        {
            public readonly List<string> Assets = new();
            public readonly List<string> Blockers = new();
        }

        RemovalPlan preview;
        Vector2 scroll;

        [MenuItem("Tools/Baseline/Remove Demo Content")]
        public static void Open() => GetWindow<DemoCleanup>("Demo cleanup");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("Remove Relay Run and the example quest/dialogue content. " +
                "Remove the reserved demo script, editor and bundle folders when initializing a new project. " +
                "Keep Core, the framework, shared robot guide, UI, settings, localization and Index. " +
                "A file backup is created before changing anything. The editor returns to Index.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling))
            {
                if (GUILayout.Button("Inspect removal")) preview = Inspect();
                if (preview != null)
                {
                    EditorGUILayout.LabelField($"{preview.Assets.Count} assets; {preview.Blockers.Count} blockers");
                    scroll = EditorGUILayout.BeginScrollView(scroll);
                    foreach (string blocker in preview.Blockers) EditorGUILayout.HelpBox(blocker, MessageType.Error);
                    foreach (string asset in preview.Assets) EditorGUILayout.LabelField(asset);
                    EditorGUILayout.EndScrollView();
                    using (new EditorGUI.DisabledScope(preview.Blockers.Count != 0 || preview.Assets.Count == 0))
                        if (GUILayout.Button("Back up and remove demos"))
                        {
                            if (!EditorUtility.DisplayDialog("Remove demo content?", "The listed demo folders will be removed, " +
                                "their scene registrations and localization keys cleaned, and Index restored to its generic menu. " +
                                "Customized files inside demo folders are included in the backup.", "Back up and remove", "Cancel")) return;
                            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                            try
                            {
                                string backup = Remove();
                                preview = null;
                                EditorUtility.DisplayDialog("Demo content removed", "Backup: " + backup +
                                    "\nRebuild Addressables and the player before distributing this project.", "OK");
                            }
                            catch (Exception exception) { Debug.LogException(exception); }
                        }
                }
                if (GUILayout.Button("Restore a demo cleanup backup"))
                {
                    string folder = EditorUtility.OpenFolderPanel("Demo cleanup backup", Path.GetFullPath("DemoBackups"), "");
                    if (string.IsNullOrEmpty(folder)) return;
                    if (!EditorUtility.DisplayDialog("Restore demo backup?", "Files recorded in this backup will replace their " +
                        "current versions, including the scene registry and Addressables/localization settings.", "Restore", "Cancel")) return;
                    if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                    try { Restore(folder); preview = null; }
                    catch (Exception exception) { Debug.LogException(exception); }
                }
            }
        }

        public static bool IsDemoPath(string path) => DemoRoots.Any(root =>
            path == root || path.StartsWith(root + "/", StringComparison.Ordinal));

        public static RemovalPlan Inspect()
        {
            var plan = new RemovalPlan();
            string[] paths = AssetDatabase.GetAllAssetPaths().Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)).ToArray();
            plan.Assets.AddRange(paths.Where(IsDemoPath).OrderBy(path => path));
            var removedGuids = plan.Assets.Select(AssetDatabase.AssetPathToGUID).Where(guid => guid.Length > 0).ToHashSet();
            var demoTypes = plan.Assets.Where(path => path.EndsWith(".cs"))
                .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"\b(?:class|struct|enum|interface)\s+(\w+)")
                    .Cast<Match>().Select(match => match.Groups[1].Value)
                    .Where(type => !path.Contains("/Editor/") || type.StartsWith("Demo", StringComparison.Ordinal)))
                .Distinct().ToArray();
            var demoKeys = AssetDatabase.FindAssets("t:SharedTableData", new[] { "Assets/Locales" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<SharedTableData>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(shared => shared != null).SelectMany(shared => shared.Entries).Where(entry => IsDemoKey(entry.Key)).ToArray();
            string menuGuid = AssetDatabase.AssetPathToGUID("Assets/Bundles/Demo/RelayRun/Prefabs/DemoRelayMenu.prefab");
            foreach (string path in paths)
            {
                if (IsDemoPath(path) || AssetDatabase.IsValidFolder(path)) continue;
                if (path.StartsWith("Assets/AddressableAssetsData/", StringComparison.Ordinal)) continue;
                string extension = Path.GetExtension(path);
                if (path == RegistryPath) continue;
                if (extension == ".cs")
                {
                    if (path == "Assets/Editor/DemoCleanup.cs" || path == "Assets/Editor/Tests/DemoCleanupTests.cs") continue;
                    string code = File.ReadAllText(path);
                    if (code.Contains("Baseline.Demo") || demoTypes.Any(type => Regex.IsMatch(code, @"\b" + Regex.Escape(type) + @"\b")))
                        plan.Blockers.Add(path + " references demo code. Move it into a demo folder or remove that dependency first.");
                    foreach (var key in demoKeys)
                        if (code.Contains(key.Key)) plan.Blockers.Add(path + " uses demo localization key " + key.Key);
                    continue;
                }
                foreach (string dependency in AssetDatabase.GetDependencies(path, false).Where(IsDemoPath))
                    plan.Blockers.Add(path + " depends on " + dependency);
                // AssetReference GUIDs are strings and are not included in GetDependencies.
                if (extension != ".asset" && extension != ".prefab" && extension != ".unity") continue;
                string text = File.ReadAllText(path);
                foreach (string guid in removedGuids)
                    if (text.Contains(guid) && !(path == IndexPath && guid == menuGuid))
                        plan.Blockers.Add(path + " contains a reference to " + AssetDatabase.GUIDToAssetPath(guid));
                if (!path.StartsWith("Assets/Locales/", StringComparison.Ordinal))
                    foreach (var key in demoKeys)
                        if (text.Contains(key.Key) || Regex.IsMatch(text, @"m_KeyId:\s*" + key.Id + @"\b"))
                            plan.Blockers.Add(path + " uses demo localization key " + key.Key);
            }
            var registry = AssetDatabase.LoadAssetAtPath<GameRegister>(RegistryPath);
            if (registry == null) plan.Blockers.Add("Missing game registry: " + RegistryPath);
            else foreach (GameMeta game in registry.games.Where(game => !IsDemoGame(game)))
            {
                if (game.icon != null && IsDemoPath(AssetDatabase.GetAssetPath(game.icon)))
                    plan.Blockers.Add("Retained game " + game.sceneName + " uses a demo icon.");
                if (IsDemoKey(game.name)) plan.Blockers.Add("Retained game " + game.sceneName + " uses a demo localization key.");
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath) == null) plan.Blockers.Add("Missing generic Index menu.");
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(IndexPath) == null) plan.Blockers.Add("Missing Index scene.");
            return plan;
        }

        internal static bool IsDemoGame(GameMeta game) => game.sceneAsset != null &&
            IsDemoPath(AssetDatabase.GUIDToAssetPath(game.sceneAsset.AssetGUID));

        public static string Remove()
        {
            RequireIdleEditor();
            // Persist pending asset edits before inspecting their serialized dependencies.
            AssetDatabase.SaveAssets();
            var plan = Inspect();
            if (plan.Blockers.Count > 0) throw new InvalidOperationException(string.Join("\n", plan.Blockers));
            if (plan.Assets.Count == 0) throw new InvalidOperationException("No installed demo content to remove.");
            string backup = CreateBackup(plan);
            EditorApplication.LockReloadAssemblies();
            AssetDatabase.DisallowAutoRefresh();
            try
            {
                // Close demo scenes before removing their files or scripts.
                var scene = EditorSceneManager.OpenScene(IndexPath);
                var bootstrap = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<GameBootstrap>(true)).Single();
                var serialized = new SerializedObject(bootstrap);
                serialized.FindProperty("menuPrefab").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(MenuPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                var registry = AssetDatabase.LoadAssetAtPath<GameRegister>(RegistryPath);
                registry.games.RemoveAll(IsDemoGame);
                EditorUtility.SetDirty(registry);
                EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(entry => !IsDemoPath(entry.path)).ToArray();
                RemoveTranslations();
                var settings = AddressableAssetSettingsDefaultObject.Settings;
                if (settings != null)
                {
                    foreach (var group in settings.groups.Where(group => group != null).ToArray())
                    {
                        foreach (var entry in group.entries.Where(entry => IsDemoPath(entry.AssetPath)).ToArray())
                            settings.RemoveAssetEntry(entry.guid);
                        if (group.Name == "Demo" && group.entries.Count == 0) settings.RemoveGroup(group);
                    }
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (string root in DemoRoots)
                        if ((File.Exists(root) || Directory.Exists(root)) && !AssetDatabase.DeleteAsset(root))
                            throw new IOException("Could not remove " + root);
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.Refresh();
                Debug.Log("Demo cleanup complete. Backup: " + backup);
                return backup;
            }
            catch
            {
                RestoreFiles(backup);
                throw;
            }
            finally
            {
                AssetDatabase.AllowAutoRefresh();
                EditorApplication.UnlockReloadAssemblies();
            }
        }

        // Run only in a disposable project copy, followed by a fresh process for compilation/tests.
        public static void RemoveForBatchTest()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("This acceptance entry point requires batch mode.");
            string backup = Remove();
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/demo-cleanup-backup.txt", backup);
            if (DemoRoots.Any(path => File.Exists(path) || Directory.Exists(path) || File.Exists(path + ".meta")))
                throw new InvalidOperationException("Demo roots remain after cleanup.");
            Debug.Log("DEMO_CLEANUP_REMOVAL_PASSED");
        }

        static void RequireIdleEditor()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Exit Play Mode and wait for compilation before changing demo installation.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save or discard scene changes before changing demo installation.");
        }

        static void RemoveTranslations()
        {
            var tables = AssetDatabase.FindAssets("t:StringTable", new[] { "Assets/Locales" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<StringTable>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(table => table != null).ToArray();
            foreach (var shared in tables.Select(table => table.SharedData).Distinct())
            {
                var keys = shared.Entries.Where(entry => IsDemoKey(entry.Key)).ToArray();
                foreach (var entry in keys)
                {
                    foreach (var table in tables.Where(table => table.SharedData == shared))
                    {
                        table.RemoveEntry(entry.Id);
                        EditorUtility.SetDirty(table);
                    }
                    shared.RemoveKey(entry.Id);
                }
                EditorUtility.SetDirty(shared);
            }
        }

        static bool IsDemoKey(string key) => !string.IsNullOrEmpty(key) &&
            (key.StartsWith("DemoRelay_", StringComparison.Ordinal) || key.StartsWith("Baseline_Dialog_", StringComparison.Ordinal));

        static string CreateBackup(RemovalPlan plan)
        {
            var files = new HashSet<string>(plan.Assets.Where(File.Exists));
            foreach (string root in DemoRoots)
                if (Directory.Exists(root)) files.UnionWith(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/')));
            files.UnionWith(new[] { IndexPath, RegistryPath, "ProjectSettings/EditorBuildSettings.asset" }.Where(File.Exists));
            foreach (string root in new[] { "Assets/AddressableAssetsData", "Assets/Locales" })
                if (Directory.Exists(root)) files.UnionWith(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(path => path.Replace('\\', '/')));
            foreach (string path in files.Concat(plan.Assets).ToArray())
                if (File.Exists(path + ".meta")) files.Add(path + ".meta");
            string backup = Path.GetFullPath(Path.Combine("DemoBackups", Guid.NewGuid().ToString("N")));
            foreach (string path in files)
            {
                string destination = Path.Combine(backup, path);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.Copy(path, destination);
            }
            File.WriteAllText(Path.Combine(backup, "manifest.json"), JsonUtility.ToJson(new BackupManifest { files = files.OrderBy(path => path).ToArray() }, true));
            return backup;
        }

        public static bool IsSafeBackupPath(string path) => !string.IsNullOrEmpty(path) &&
            !Path.IsPathRooted(path) && !path.Contains("..") && !path.Contains('\\') && !path.Contains(':') &&
            (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("ProjectSettings/", StringComparison.Ordinal));

        public static void Restore(string backup)
        {
            RequireIdleEditor();
            RestoreFiles(backup);
        }

        static void RestoreFiles(string backup)
        {
            var manifest = JsonUtility.FromJson<BackupManifest>(File.ReadAllText(Path.Combine(backup, "manifest.json")));
            if (manifest?.files == null || manifest.files.Any(path => !IsSafeBackupPath(path) || !File.Exists(Path.Combine(backup, path))))
                throw new InvalidOperationException("Backup manifest is invalid or incomplete.");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (string path in manifest.files)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(Path.Combine(backup, path), path, true);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            EditorSceneManager.OpenScene(IndexPath);
            Debug.Log("Restored demo cleanup backup: " + backup);
        }
    }
}
