using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Hypocycloid.Utils;
using UnityEngine;

namespace Baseline
{
    /// <summary>
    /// Versioned JSON persistence adapted from Ratioscope's ConfigSettingsPersistence: the settings
    /// panel's values and the language as typed settings, plus a keyed store for progress flags and
    /// UI memory. The file is <c>config/user_config.json</c> under <see cref="RuntimeDataPaths.WritableRoot"/>.
    /// A file at the old persistentDataPath location moves there on first use, and values the game
    /// kept in PlayerPrefs move into the file the first time they are read.
    /// </summary>
    public static class ConfigSettingsPersistence
    {
        public const string FileName = "user_config.json";
        public const string ConfigFolderName = "config";
        const int FileVersion = 1;

        const string DifficultyKey = "settings.difficulty";
        const string LocaleKey = "settings.locale";
        const string MasterVolumeKey = "settings.audio.master";
        const string MusicVolumeKey = "settings.audio.music";
        const string SfxVolumeKey = "settings.audio.sfx";
        const string UiVolumeKey = "settings.audio.ui";
        const string ScreenShakeKey = "settings.fx.shake";
        const string BoardEffectsKey = "settings.fx.board";
        const string WindowModeKey = "settings.window.mode";
        const string WindowWidthKey = "settings.window.width";
        const string WindowHeightKey = "settings.window.height";

        const string LegacyLocaleKey = "LocaleKey";

        const int MinWindowedWidth = 640;
        const int MinWindowedHeight = 480;

        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static Dictionary<string, SerializedConfigEntry> entries = new(StringComparer.Ordinal);
        static SettingsState state;
        static bool loaded;

        static string directoryOverride;
        static string legacyDirectoryOverride;
        static bool readLegacyPrefs = true;

        public static string DirectoryPath =>
            directoryOverride ?? Path.Combine(RuntimeDataPaths.WritableRoot, ConfigFolderName);
        public static string FilePath => Path.Combine(DirectoryPath, FileName);
        public static bool FileExists => File.Exists(FilePath);

        // Where the file lived before it moved beside the game. A redirected store has none unless given one.
        static string LegacyDirectoryPath =>
            directoryOverride == null
                ? Path.Combine(Application.persistentDataPath, ConfigFolderName)
                : legacyDirectoryOverride;

        /// <summary>The chosen language's locale code (such as "zh"), or empty until the player picks one.</summary>
        public static string Locale
        {
            get
            {
                EnsureLoaded();
                return state.locale;
            }
        }

        public static float MasterVolume
        {
            get
            {
                EnsureLoaded();
                return state.masterVolume;
            }
        }

        public static float MusicVolume
        {
            get
            {
                EnsureLoaded();
                return state.musicVolume;
            }
        }

        /// <summary>Background, board and match effects; stored under the original SFX key.</summary>
        public static float SfxVolume
        {
            get
            {
                EnsureLoaded();
                return state.sfxVolume;
            }
        }

        /// <summary>Interface sounds. A file from before this setting existed starts it at the SFX level.</summary>
        public static float UiVolume
        {
            get
            {
                EnsureLoaded();
                return state.uiVolume;
            }
        }

        /// <summary>Scale on every camera kick, 0 (off) to 1.</summary>
        public static float ScreenShake
        {
            get
            {
                EnsureLoaded();
                return state.screenShake;
            }
        }

        /// <summary>Whether board events show their particles.</summary>
        public static bool BoardEffects
        {
            get
            {
                EnsureLoaded();
                return state.boardEffects;
            }
        }

        /// <summary>Compatibility setting for the baseline Index difficulty selector.</summary>
        public static GameDifficulty Difficulty
        {
            get { EnsureLoaded(); return state.difficulty; }
        }

        public static void SetDifficulty(GameDifficulty value)
        {
            EnsureLoaded();
            if (state.difficulty == value) return;
            state.difficulty = value;
            Save();
        }

        public static WindowPresentationMode WindowMode
        {
            get
            {
                EnsureLoaded();
                return state.windowMode;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRuntimeState() => Reload();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void LoadBeforeScene() => EnsureLoaded();

        /// <summary>Drops the values in memory, so the next read comes from the file.</summary>
        public static void Reload()
        {
            loaded = false;
            state = default;
            entries = new Dictionary<string, SerializedConfigEntry>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Points the store at <paramref name="directory"/> until the result is disposed, for tests.
        /// PlayerPrefs are left alone unless <paramref name="migrateLegacy"/> is set, and
        /// <paramref name="legacyDirectory"/> stands in for the old persistentDataPath folder.
        /// </summary>
        public static IDisposable UseDirectory(
            string directory,
            bool migrateLegacy = false,
            string legacyDirectory = null
        )
        {
            var previous = new DirectoryScope(directoryOverride, legacyDirectoryOverride, readLegacyPrefs);
            directoryOverride = directory ?? throw new ArgumentNullException(nameof(directory));
            legacyDirectoryOverride = legacyDirectory;
            readLegacyPrefs = migrateLegacy;
            Reload();
            return previous;
        }

        public static void EnsureLoaded()
        {
            if (loaded)
                return;

            state = CreateDefaults();
            bool migrated = false;

            if (!File.Exists(FilePath))
                MoveLegacyFile();

            if (File.Exists(FilePath))
                ReadFile();
            else
            {
                migrated = MigrateLegacyPlayerPrefs();
                LogHelper.Log("Load", $"No settings file at '{FilePath}'; {(migrated ? "migrated legacy PlayerPrefs" : "using defaults")}.");
            }

            ValidateState();
            loaded = true;
            RemoveLegacySettingsKeys();

            if (migrated)
                Save();
        }

        public static void SetLocale(string code)
        {
            EnsureLoaded();
            code ??= string.Empty;
            if (string.Equals(state.locale, code, StringComparison.Ordinal))
                return;
            state.locale = code;
            Save();
        }

        /// <summary>
        /// Hands over, once, the language the game kept in PlayerPrefs. It was saved as a position in
        /// the locale list, which only the caller can turn into a locale code.
        /// </summary>
        public static bool TryTakeLegacyLocaleIndex(out int index)
        {
            index = -1;
            string key = LegacyGlobalKey(LegacyLocaleKey);
            if (!readLegacyPrefs || !PlayerPrefs.HasKey(key))
                return false;
            index = PlayerPrefs.GetInt(key, -1);
            PlayerPrefs.DeleteKey(key);
            return true;
        }

        public static void SetMasterVolume(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(state.masterVolume, value))
                return;
            state.masterVolume = value;
            Save();
        }

        public static void SetMusicVolume(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(state.musicVolume, value))
                return;
            state.musicVolume = value;
            Save();
        }

        public static void SetSfxVolume(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(state.sfxVolume, value))
                return;
            state.sfxVolume = value;
            Save();
        }

        public static void SetUiVolume(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(state.uiVolume, value))
                return;
            state.uiVolume = value;
            Save();
        }

        public static void SetScreenShake(float value)
        {
            EnsureLoaded();
            value = Mathf.Clamp01(value);
            if (Mathf.Approximately(state.screenShake, value))
                return;
            state.screenShake = value;
            Save();
        }

        public static void SetBoardEffects(bool value)
        {
            EnsureLoaded();
            if (state.boardEffects == value)
                return;
            state.boardEffects = value;
            Save();
        }

        public static void SetWindowMode(
            WindowPresentationMode value,
            int windowedWidth = 0,
            int windowedHeight = 0
        )
        {
            EnsureLoaded();
            bool changed = state.windowMode != value;
            state.windowMode = value;

            if (windowedWidth >= MinWindowedWidth && windowedHeight >= MinWindowedHeight)
            {
                changed |=
                    state.windowedWidth != windowedWidth || state.windowedHeight != windowedHeight;
                state.windowedWidth = windowedWidth;
                state.windowedHeight = windowedHeight;
            }

            if (changed)
                Save();
        }

        /// <summary>Whether a keyed value has been saved, here or still in PlayerPrefs.</summary>
        public static bool HasKey(string key)
        {
            EnsureLoaded();
            return entries.ContainsKey(DataKey(key)) || TryLegacyKey(key, out _);
        }

        public static void SetInt(string key, int value) =>
            SetDataEntry(key, typeof(int), value.ToString(Invariant));

        public static int GetInt(string key, int fallback = 0)
        {
            EnsureLoaded();
            string storageKey = DataKey(key);
            if (
                entries.TryGetValue(storageKey, out SerializedConfigEntry entry)
                && int.TryParse(entry.value, NumberStyles.Integer, Invariant, out int value)
            )
            {
                return value;
            }

            if (!TryLegacyKey(key, out string legacyKey))
                return fallback;

            value = PlayerPrefs.GetInt(legacyKey, fallback);
            MigrateDataEntry(key, legacyKey, typeof(int), value.ToString(Invariant));
            return value;
        }

        public static void SetFloat(string key, float value) =>
            SetDataEntry(key, typeof(float), value.ToString("R", Invariant));

        public static float GetFloat(string key, float fallback = 0f)
        {
            EnsureLoaded();
            string storageKey = DataKey(key);
            if (
                entries.TryGetValue(storageKey, out SerializedConfigEntry entry)
                && float.TryParse(entry.value, NumberStyles.Float, Invariant, out float value)
            )
            {
                return value;
            }

            if (!TryLegacyKey(key, out string legacyKey))
                return fallback;

            value = PlayerPrefs.GetFloat(legacyKey, fallback);
            MigrateDataEntry(key, legacyKey, typeof(float), value.ToString("R", Invariant));
            return value;
        }

        public static void SetString(string key, string value) =>
            SetDataEntry(key, typeof(string), value ?? string.Empty);

        public static string GetString(string key, string fallback = "")
        {
            EnsureLoaded();
            string storageKey = DataKey(key);
            if (entries.TryGetValue(storageKey, out SerializedConfigEntry entry))
                return entry.value ?? string.Empty;

            if (!TryLegacyKey(key, out string legacyKey))
                return fallback;

            string value = PlayerPrefs.GetString(legacyKey, fallback);
            MigrateDataEntry(key, legacyKey, typeof(string), value);
            return value;
        }

        public static Vector2Int GetWindowedResolution(int fallbackWidth, int fallbackHeight)
        {
            EnsureLoaded();
            if (
                state.windowedWidth >= MinWindowedWidth
                && state.windowedHeight >= MinWindowedHeight
            )
            {
                return new Vector2Int(state.windowedWidth, state.windowedHeight);
            }

            if (fallbackWidth >= MinWindowedWidth && fallbackHeight >= MinWindowedHeight)
                return new Vector2Int(fallbackWidth, fallbackHeight);

            return new Vector2Int(1280, 720);
        }

        public static void Save()
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(state.locale))
                entries.Remove(LocaleKey);
            else
                entries[LocaleKey] = Entry(LocaleKey, "Index_sys_lang", typeof(string), state.locale);
            entries[DifficultyKey] = Entry(DifficultyKey, "Index_sys_difficulty", typeof(GameDifficulty), state.difficulty.ToString());
            entries[MasterVolumeKey] = Entry(
                MasterVolumeKey,
                "Index_aud_master",
                typeof(float),
                state.masterVolume.ToString("R", Invariant)
            );
            entries[MusicVolumeKey] = Entry(
                MusicVolumeKey,
                "Index_aud_music",
                typeof(float),
                state.musicVolume.ToString("R", Invariant)
            );
            entries[SfxVolumeKey] = Entry(
                SfxVolumeKey,
                "Index_aud_sfx",
                typeof(float),
                state.sfxVolume.ToString("R", Invariant)
            );
            entries[UiVolumeKey] = Entry(
                UiVolumeKey,
                "Index_aud_ui",
                typeof(float),
                state.uiVolume.ToString("R", Invariant)
            );
            entries[ScreenShakeKey] = Entry(
                ScreenShakeKey,
                "Index_fx_shake",
                typeof(float),
                state.screenShake.ToString("R", Invariant)
            );
            entries[BoardEffectsKey] = Entry(
                BoardEffectsKey,
                "Index_fx_board",
                typeof(bool),
                state.boardEffects.ToString(Invariant)
            );
            entries[WindowModeKey] = Entry(
                WindowModeKey,
                "Index_sys_window_mode",
                typeof(WindowPresentationMode),
                state.windowMode.ToString()
            );
            entries[WindowWidthKey] = Entry(
                WindowWidthKey,
                string.Empty,
                typeof(int),
                state.windowedWidth.ToString(Invariant)
            );
            entries[WindowHeightKey] = Entry(
                WindowHeightKey,
                string.Empty,
                typeof(int),
                state.windowedHeight.ToString(Invariant)
            );

            WriteFile(new SerializedConfigFile { entries = entries.Values.ToList() });
        }

        // -------- Raw file access for the Save Data window --------

        public static SerializedConfigFile ReadSerializedFile()
        {
            if (!File.Exists(FilePath))
                return new SerializedConfigFile();
            try
            {
                var file = JsonUtility.FromJson<SerializedConfigFile>(File.ReadAllText(FilePath));
                if (file == null)
                    return new SerializedConfigFile();
                file.entries ??= new List<SerializedConfigEntry>();
                return file;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Config] Failed to read {FilePath}: {ex.Message}");
                return new SerializedConfigFile();
            }
        }

        public static string ReadSerializedJson()
        {
            try
            {
                return File.Exists(FilePath) ? File.ReadAllText(FilePath) : string.Empty;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Config] Failed to read {FilePath}: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>Replaces the file with <paramref name="file"/>'s entries and rereads it.</summary>
        public static void WriteSerializedFile(SerializedConfigFile file)
        {
            var written = new Dictionary<string, SerializedConfigEntry>(StringComparer.Ordinal);
            foreach (var entry in file?.entries ?? Enumerable.Empty<SerializedConfigEntry>())
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.key))
                    continue;
                written[entry.key] = Entry(
                    entry.key,
                    entry.i18nKey ?? string.Empty,
                    entry.valueType ?? string.Empty,
                    entry.value ?? string.Empty
                );
            }

            WriteFile(new SerializedConfigFile { entries = written.Values.ToList() });
            Reload();
        }

        /// <summary>Deletes the file, so every setting returns to its default.</summary>
        public static void DeleteSerializedFile()
        {
            try
            {
                DeleteWithMeta(FilePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Config] Failed to delete {FilePath}: {ex.Message}");
            }
            Reload();
        }

        static void WriteFile(SerializedConfigFile file)
        {
            string path = FilePath;
            string temporary = path + ".tmp";
            var saving = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Save", $"Saving settings to '{path}'.");
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                file.version = FileVersion;
                file.entries = file
                    .entries.OrderBy(entry => entry.key, StringComparer.Ordinal)
                    .ToList();
                // Written aside and swapped in, so a crash mid-write cannot leave half a file.
                File.WriteAllText(temporary, JsonUtility.ToJson(file, true));
                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
                LogHelper.Log("Save", $"Settings saved to '{path}' in {saving.Elapsed.TotalMilliseconds:F1} ms; entries={file.entries.Count}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Config] Failed to write {path} after {saving.Elapsed.TotalMilliseconds:F1} ms: {ex.Message}");
            }
        }

        static void ReadFile()
        {
            var loading = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Load", $"Loading settings from '{FilePath}'.");
            try
            {
                var file = JsonUtility.FromJson<SerializedConfigFile>(File.ReadAllText(FilePath));
                if (file?.entries == null)
                {
                    LogHelper.LogWarning("Load", $"Settings file '{FilePath}' has no entries; using defaults.");
                    return;
                }

                entries = file
                    .entries.Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.key))
                    .GroupBy(entry => entry.key)
                    .ToDictionary(
                        group => group.Key,
                        group => group.Last(),
                        StringComparer.Ordinal
                    );

                var values = entries.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.value,
                    StringComparer.Ordinal
                );

                state.locale = values.TryGetValue(LocaleKey, out string locale)
                    ? locale ?? string.Empty
                    : state.locale;
                state.difficulty = ReadEnum(values, DifficultyKey, state.difficulty);
                state.masterVolume = ReadFloat(values, MasterVolumeKey, state.masterVolume);
                state.musicVolume = ReadFloat(values, MusicVolumeKey, state.musicVolume);
                state.sfxVolume = ReadFloat(values, SfxVolumeKey, state.sfxVolume);
                state.uiVolume = ReadFloat(values, UiVolumeKey, state.sfxVolume);
                state.screenShake = ReadFloat(values, ScreenShakeKey, state.screenShake);
                state.boardEffects = ReadBool(values, BoardEffectsKey, state.boardEffects);
                state.windowMode = ReadEnum(values, WindowModeKey, state.windowMode);
                state.windowedWidth = ReadInt(values, WindowWidthKey, state.windowedWidth);
                state.windowedHeight = ReadInt(values, WindowHeightKey, state.windowedHeight);
                LogHelper.Log("Load", $"Settings loaded from '{FilePath}' in {loading.Elapsed.TotalMilliseconds:F1} ms; entries={entries.Count}.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Config] Failed to read {FilePath} after {loading.Elapsed.TotalMilliseconds:F1} ms: {ex.Message}");
            }
        }

        static void MoveLegacyFile()
        {
            string legacyDirectory = LegacyDirectoryPath;
            if (string.IsNullOrEmpty(legacyDirectory))
                return;

            string legacyPath = Path.Combine(legacyDirectory, FileName);
            if (!File.Exists(legacyPath))
                return;

            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.Move(legacyPath, FilePath);
                Debug.Log($"[Config] Moved {legacyPath} to {FilePath}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Debug.LogWarning($"[Config] Failed to move {legacyPath} to {FilePath}: {ex.Message}");
            }
        }

        static bool MigrateLegacyPlayerPrefs()
        {
            if (!readLegacyPrefs)
                return false;

            bool found = false;
            state.difficulty = ReadLegacyEnum(GameRegister.SCOPE_DIFFICULTY, state.difficulty, ref found);
            state.musicVolume = ReadLegacyFloat(
                GameRegister.SCOPE_AUD_MUSIC,
                state.musicVolume,
                ref found
            );
            state.sfxVolume = ReadLegacyFloat(
                GameRegister.SCOPE_AUD_SFX,
                state.sfxVolume,
                ref found
            );
            state.uiVolume = state.sfxVolume;
            state.windowMode = ReadLegacyEnum("window_mode", state.windowMode, ref found);
            state.windowedWidth = ReadLegacyInt("window_width", state.windowedWidth, ref found);
            state.windowedHeight = ReadLegacyInt("window_height", state.windowedHeight, ref found);
            return found;
        }

        static void SetDataEntry(string key, Type type, string value)
        {
            EnsureLoaded();
            string storageKey = DataKey(key);
            DeleteLegacyKey(key);
            if (
                entries.TryGetValue(storageKey, out SerializedConfigEntry current)
                && string.Equals(current.valueType, type.FullName, StringComparison.Ordinal)
                && string.Equals(current.value, value, StringComparison.Ordinal)
            )
            {
                return;
            }

            entries[storageKey] = Entry(storageKey, string.Empty, type, value);
            Save();
        }

        static void MigrateDataEntry(string key, string legacyKey, Type type, string value)
        {
            string storageKey = DataKey(key);
            entries[storageKey] = Entry(storageKey, string.Empty, type, value);
            PlayerPrefs.DeleteKey(legacyKey);
            Save();
        }

        static string DataKey(string key) => $"data.{key}";

        static bool TryLegacyKey(string key, out string legacyKey)
        {
            legacyKey = key;
            return readLegacyPrefs && PlayerPrefs.HasKey(legacyKey);
        }

        static void DeleteLegacyKey(string key)
        {
            if (readLegacyPrefs)
                PlayerPrefs.DeleteKey(key);
        }

        static void RemoveLegacySettingsKeys()
        {
            if (!readLegacyPrefs)
                return;

            foreach (
                string key in new[]
                {
                    GameRegister.SCOPE_DIFFICULTY,
                    GameRegister.SCOPE_AUD_MUSIC,
                    GameRegister.SCOPE_AUD_SFX,
                    "window_mode",
                    "window_width",
                    "window_height",
                }
            )
            {
                PlayerPrefs.DeleteKey(LegacyGlobalKey(key));
            }

            // The old language index waits for LocaleSelector unless a language is already chosen here.
            if (!string.IsNullOrEmpty(state.locale))
                PlayerPrefs.DeleteKey(LegacyGlobalKey(LegacyLocaleKey));
        }

        static string LegacyGlobalKey(string key) =>
            $"{GameRegister.SCOPE_CTX}_{GameRegister.SCOPE_GLOBAL}_{key}";

        static T ReadLegacyEnum<T>(string key, T fallback, ref bool found)
            where T : struct, Enum
        {
            string fullKey = LegacyGlobalKey(key);
            if (!PlayerPrefs.HasKey(fullKey))
                return fallback;
            found = true;
            int raw = PlayerPrefs.GetInt(fullKey, Convert.ToInt32(fallback));
            return Enum.IsDefined(typeof(T), raw) ? (T)Enum.ToObject(typeof(T), raw) : fallback;
        }

        static float ReadLegacyFloat(string key, float fallback, ref bool found)
        {
            string fullKey = LegacyGlobalKey(key);
            if (!PlayerPrefs.HasKey(fullKey))
                return fallback;
            found = true;
            return PlayerPrefs.GetFloat(fullKey, fallback);
        }

        static int ReadLegacyInt(string key, int fallback, ref bool found)
        {
            string fullKey = LegacyGlobalKey(key);
            if (!PlayerPrefs.HasKey(fullKey))
                return fallback;
            found = true;
            return PlayerPrefs.GetInt(fullKey, fallback);
        }

        static T ReadEnum<T>(Dictionary<string, string> entries, string key, T fallback)
            where T : struct, Enum
        {
            return
                entries.TryGetValue(key, out string value)
                && Enum.TryParse(value, true, out T parsed)
                && Enum.IsDefined(typeof(T), parsed)
                ? parsed
                : fallback;
        }

        static float ReadFloat(Dictionary<string, string> entries, string key, float fallback) =>
            entries.TryGetValue(key, out string value)
            && float.TryParse(value, NumberStyles.Float, Invariant, out float parsed)
                ? parsed
                : fallback;

        static bool ReadBool(Dictionary<string, string> entries, string key, bool fallback) =>
            entries.TryGetValue(key, out string value) && bool.TryParse(value, out bool parsed)
                ? parsed
                : fallback;

        static int ReadInt(Dictionary<string, string> entries, string key, int fallback) =>
            entries.TryGetValue(key, out string value)
            && int.TryParse(value, NumberStyles.Integer, Invariant, out int parsed)
                ? parsed
                : fallback;

        static void ValidateState()
        {
            if (!Enum.IsDefined(typeof(GameDifficulty), state.difficulty))
                state.difficulty = GameDifficulty.Normal;
            if (!Enum.IsDefined(typeof(WindowPresentationMode), state.windowMode))
                state.windowMode = WindowPresentationMode.Windowed;
            state.locale ??= string.Empty;
            state.masterVolume = Mathf.Clamp01(state.masterVolume);
            state.musicVolume = Mathf.Clamp01(state.musicVolume);
            state.sfxVolume = Mathf.Clamp01(state.sfxVolume);
            state.uiVolume = Mathf.Clamp01(state.uiVolume);
            state.screenShake = Mathf.Clamp01(state.screenShake);
        }

        static SettingsState CreateDefaults() =>
            new()
            {
                difficulty = GameDifficulty.Normal,
                locale = string.Empty,
                masterVolume = 0.5f,
                musicVolume = GameRegister.defMusicVol,
                sfxVolume = GameRegister.defSoundVol,
                uiVolume = GameRegister.defSoundVol,
                screenShake = 1f,
                boardEffects = true,
                windowMode = WindowPresentationMode.Windowed,
                windowedWidth = 1280,
                windowedHeight = 720,
            };

        // In the editor the file sits under Assets, where Unity gives it a .meta.
        static void DeleteWithMeta(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
            if (File.Exists(path + ".meta"))
                File.Delete(path + ".meta");
        }

        static SerializedConfigEntry Entry(string key, string i18nKey, Type type, string value) =>
            Entry(key, i18nKey, type.FullName, value);

        static SerializedConfigEntry Entry(string key, string i18nKey, string type, string value) =>
            new()
            {
                key = key,
                i18nKey = i18nKey,
                valueType = type,
                value = value,
            };

        struct SettingsState
        {
            public GameDifficulty difficulty;
            public string locale;
            public float masterVolume;
            public float musicVolume;
            public float sfxVolume;
            public float uiVolume;
            public float screenShake;
            public bool boardEffects;
            public WindowPresentationMode windowMode;
            public int windowedWidth;
            public int windowedHeight;
        }

        sealed class DirectoryScope : IDisposable
        {
            readonly string directory;
            readonly string legacyDirectory;
            readonly bool legacyPrefs;
            bool disposed;

            public DirectoryScope(string directory, string legacyDirectory, bool legacyPrefs)
            {
                this.directory = directory;
                this.legacyDirectory = legacyDirectory;
                this.legacyPrefs = legacyPrefs;
            }

            public void Dispose()
            {
                if (disposed)
                    return;
                disposed = true;
                directoryOverride = directory;
                legacyDirectoryOverride = legacyDirectory;
                readLegacyPrefs = legacyPrefs;
                Reload();
            }
        }

        [Serializable]
        public sealed class SerializedConfigFile
        {
            public int version = FileVersion;
            public List<SerializedConfigEntry> entries = new();
        }

        [Serializable]
        public sealed class SerializedConfigEntry
        {
            public string key;
            public string i18nKey;
            public string valueType;
            public string value;
        }
    }
}
