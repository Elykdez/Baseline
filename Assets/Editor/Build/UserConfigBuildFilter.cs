using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Baseline.Editor
{
    /// <summary>
    /// Keeps the editor's own settings out of player builds. In the editor, user_config.json sits in
    /// Assets/StreamingAssets/config (see <see cref="RuntimeDataPaths"/>), which Unity packs into every
    /// build, so players would start with the developer's language, settings and saved progress.
    ///
    /// The folder is moved into Library for the build and back afterwards. A failed build leaves it
    /// stashed, so it is also restored on the next domain reload.
    /// </summary>
    public sealed class UserConfigBuildFilter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        public const string ConfigFolder = "Assets/StreamingAssets/" + ConfigSettingsPersistence.ConfigFolderName;

        public int callbackOrder => 0;

        static string StashFolder =>
            Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Library", "UserConfigBuildStash");

        public void OnPreprocessBuild(BuildReport report)
        {
            Restore(ConfigFolder, StashFolder);
            if (Stash(ConfigFolder, StashFolder))
                Debug.Log($"[Config] Kept {ConfigFolder} out of the build.");
        }

        public void OnPostprocessBuild(BuildReport report) => RestoreAndRefresh();

        [InitializeOnLoadMethod]
        static void RestoreLeftovers()
        {
            if (Directory.Exists(StashFolder))
                RestoreAndRefresh();
        }

        static void RestoreAndRefresh()
        {
            if (Restore(ConfigFolder, StashFolder))
                AssetDatabase.Refresh();
        }

        /// <summary>Moves <paramref name="folder"/> and its .meta to <paramref name="stash"/>.</summary>
        public static bool Stash(string folder, string stash)
        {
            if (!Directory.Exists(folder))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(stash) ?? ".");
            Directory.Move(folder, stash);
            if (File.Exists(folder + ".meta"))
                File.Move(folder + ".meta", stash + ".meta");
            return true;
        }

        /// <summary>
        /// Moves a stashed folder back. Files written to <paramref name="folder"/> while it was away
        /// give way to the stashed ones.
        /// </summary>
        public static bool Restore(string folder, string stash)
        {
            if (!Directory.Exists(stash))
                return false;

            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(folder) ?? ".");
                Directory.Move(stash, folder);
            }
            else
            {
                foreach (string source in Directory.GetFiles(stash, "*", SearchOption.AllDirectories))
                {
                    string destination = Path.Combine(folder, Path.GetRelativePath(stash, source));
                    Directory.CreateDirectory(Path.GetDirectoryName(destination) ?? ".");
                    if (File.Exists(destination))
                        File.Delete(destination);
                    File.Move(source, destination);
                }
                Directory.Delete(stash, true);
            }

            if (File.Exists(stash + ".meta"))
            {
                if (File.Exists(folder + ".meta"))
                    File.Delete(folder + ".meta");
                File.Move(stash + ".meta", folder + ".meta");
            }
            return true;
        }
    }
}
