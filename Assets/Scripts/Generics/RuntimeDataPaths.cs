using System;
using System.IO;
using UnityEngine;

namespace Baseline
{
    /// <summary>
    /// Where the game writes its own files, adapted from Ratioscope's RuntimeDataPaths.
    ///
    /// On Windows and Linux, and in the editor, that is StreamingAssets beside the game, so a player
    /// can find, copy or delete their settings by hand. Elsewhere StreamingAssets is read-only (inside
    /// the APK, the iOS bundle or a signed macOS app), so files go to persistentDataPath. An install
    /// whose folder cannot be written, such as one under Program Files, also falls back there.
    /// </summary>
    public static class RuntimeDataPaths
    {
        static string writableRoot;

        public static string WritableRoot => writableRoot ??= ResolveWritableRoot();

#if UNITY_EDITOR || UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX
        static string ResolveWritableRoot()
        {
            string root = Application.streamingAssetsPath;
            if (CanWrite(root))
                return root;
            Debug.LogWarning($"[Config] {root} is read-only; writing to {Application.persistentDataPath} instead.");
            return Application.persistentDataPath;
        }

        static bool CanWrite(string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                // A dot file, so the editor never imports it.
                string probe = Path.Combine(directory, $".write_probe_{Guid.NewGuid():N}");
                File.WriteAllText(probe, string.Empty);
                File.Delete(probe);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return false;
            }
        }
#else
        static string ResolveWritableRoot() => Application.persistentDataPath;
#endif
    }
}
