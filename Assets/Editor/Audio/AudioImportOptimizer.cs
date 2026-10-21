using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hypocycloid.Editor
{
    enum AudioImportCategory
    {
        Music,
        Effect,
    }

    readonly struct AudioImportProfile
    {
        public AudioImportProfile(
            AudioImportCategory category,
            AudioClipLoadType loadType,
            AudioCompressionFormat format,
            float quality,
            bool forceMono,
            bool preload,
            bool loadInBackground,
            AudioSampleRateSetting sampleRate,
            uint sampleRateOverride = 0
        )
        {
            Category = category;
            LoadType = loadType;
            Format = format;
            Quality = quality;
            ForceMono = forceMono;
            Preload = preload;
            LoadInBackground = loadInBackground;
            SampleRate = sampleRate;
            SampleRateOverride = sampleRateOverride;
        }

        public AudioImportCategory Category { get; }

        public AudioClipLoadType LoadType { get; }

        public AudioCompressionFormat Format { get; }

        public float Quality { get; }

        public bool ForceMono { get; }

        public bool Preload { get; }

        public bool LoadInBackground { get; }

        public AudioSampleRateSetting SampleRate { get; }

        public uint SampleRateOverride { get; }
    }

    /// <summary>
    /// Applies the project's folder-owned import profiles to every clip under Bundles/Sound.
    /// Tracks in Sound/Music stream; everything else is a short effect decoded up front.
    /// Source files stay untouched; Unity performs the runtime conversion so the original
    /// masters remain usable. Ported from InTheLine.
    /// </summary>
    public static class AudioImportOptimizer
    {
        internal const string AudioRoot = "Assets/Bundles/Core/Sound";
        internal const string MusicRoot = AudioRoot + "/Music";

        // Per-platform overrides would shadow the profile on device, so the profile owns every target.
        static readonly string[] OverridePlatforms = { "Standalone", "Android", "iOS", "WebGL" };

        const string OptimizeMenu = EditorCommons.CTX + "Audio/Optimize Library";
        const string ValidateMenu = EditorCommons.CTX + "Audio/Validate Library";

        [MenuItem(OptimizeMenu, false, 10)]
        public static void OptimizeLibrary()
        {
            string[] paths = AudioPaths();
            int changed = 0;
            try
            {
                for (int index = 0; index < paths.Length; index++)
                {
                    string path = paths[index];
                    EditorUtility.DisplayProgressBar(
                        "Optimizing audio",
                        path,
                        paths.Length == 0 ? 1f : (float)index / paths.Length
                    );
                    if (AssetImporter.GetAtPath(path) is AudioImporter importer
                        && Configure(importer, path))
                    {
                        importer.SaveAndReimport();
                        changed++;
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            AssetDatabase.SaveAssets();
            int mismatches = ValidateLibrary();
            Debug.Log(
                $"Audio optimization complete: {paths.Length} clips checked, "
                    + $"{changed} reimported, {mismatches} mismatches remain."
            );
        }

        [MenuItem(ValidateMenu, false, 11)]
        static void ValidateLibraryMenu()
        {
            int mismatches = ValidateLibrary();
            if (mismatches == 0)
            {
                Debug.Log("Audio import validation passed.");
            }
            else
            {
                Debug.LogError($"Audio import validation found {mismatches} mismatches.");
            }
        }

        internal static bool TryGetProfile(string assetPath, out AudioImportProfile profile)
        {
            string path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(AudioRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                profile = default;
                return false;
            }

            if (path.StartsWith(MusicRoot + "/", StringComparison.OrdinalIgnoreCase))
            {
                profile = new AudioImportProfile(
                    AudioImportCategory.Music,
                    AudioClipLoadType.Streaming,
                    AudioCompressionFormat.Vorbis,
                    0.7f,
                    false,
                    false,
                    true,
                    AudioSampleRateSetting.PreserveSampleRate
                );
                return true;
            }

            profile = ShortProfile(AudioImportCategory.Effect);
            return true;
        }

        internal static bool Configure(AudioImporter importer, string assetPath)
        {
            if (importer == null || !TryGetProfile(assetPath, out AudioImportProfile profile))
            {
                return false;
            }

            bool changed = !Matches(importer, profile);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = profile.LoadType;
            settings.compressionFormat = profile.Format;
            settings.quality = profile.Quality;
            settings.preloadAudioData = profile.Preload;
            settings.sampleRateSetting = profile.SampleRate;
            settings.sampleRateOverride = profile.SampleRateOverride;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = profile.ForceMono;
            importer.loadInBackground = profile.LoadInBackground;
            foreach (string platform in OverridePlatforms)
            {
                importer.ClearSampleSettingOverride(platform);
            }
            return changed;
        }

        internal static bool IsConfigured(AudioImporter importer, string assetPath)
        {
            return importer != null
                && TryGetProfile(assetPath, out AudioImportProfile profile)
                && Matches(importer, profile);
        }

        static AudioImportProfile ShortProfile(AudioImportCategory category)
        {
            return new AudioImportProfile(
                category,
                AudioClipLoadType.DecompressOnLoad,
                AudioCompressionFormat.ADPCM,
                1f,
                true,
                true,
                false,
                AudioSampleRateSetting.OptimizeSampleRate
            );
        }

        static bool Matches(AudioImporter importer, AudioImportProfile profile)
        {
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            return settings.loadType == profile.LoadType
                && settings.compressionFormat == profile.Format
                && Mathf.Approximately(settings.quality, profile.Quality)
                && settings.preloadAudioData == profile.Preload
                && settings.sampleRateSetting == profile.SampleRate
                && (
                    profile.SampleRate != AudioSampleRateSetting.OverrideSampleRate
                    || settings.sampleRateOverride == profile.SampleRateOverride
                )
                && importer.forceToMono == profile.ForceMono
                && importer.loadInBackground == profile.LoadInBackground
                && !OverridePlatforms.Any(importer.ContainsSampleSettingsOverride);
        }

        static int ValidateLibrary()
        {
            int mismatches = 0;
            foreach (string path in AudioPaths())
            {
                if (AssetImporter.GetAtPath(path) is not AudioImporter importer
                    || !IsConfigured(importer, path))
                {
                    Debug.LogError($"Audio import settings do not match its folder profile: {path}");
                    mismatches++;
                }
            }
            return mismatches;
        }

        static string[] AudioPaths()
        {
            return AssetDatabase.FindAssets("t:AudioClip", new[] { AudioRoot })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    sealed class AudioImportPostprocessor : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (assetImporter is AudioImporter importer)
            {
                AudioImportOptimizer.Configure(importer, assetPath);
            }
        }
    }
}
