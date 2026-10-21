using System;
using System.Collections;
using Baseline.Dialogue;
using Hypocycloid.Core;
using Hypocycloid.Utils;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace Baseline
{
    [Serializable]
    public struct GameMeta
    {
        public string sceneName;
        public AssetReference sceneAsset;
        public string name;
        public Sprite icon;

        public object RuntimeKey => sceneAsset != null && sceneAsset.RuntimeKeyIsValid()
            ? sceneAsset.RuntimeKey : sceneName;
    }

    public enum GameDifficulty
    {
        Easy,
        Normal,
        Hard,
    }

    public enum WindowPresentationMode
    {
        Windowed,
        Borderless,
        Fullscreen,
    }

    // Context Management
    public class ContextManager : MonoSingleton<ContextManager>
    {
        [Header("Context")]
        public GameRegister gameRegister;
        public GameDifficulty globalDifficulty;

        [SerializeField]
        UnityEvent onValentineDay;
        public UnityEvent OnValentineDay
        {
            get
            {
                onValentineDay ??= new UnityEvent();
                return onValentineDay;
            }
        }

        bool sceneTransitionInProgress;

        protected override bool DestroyDuplicateObject => true;

        protected override void Init()
        {
            base.Init();
            SignalBus.Subscribe<WindowModeRequestedSignal>(OnWindowModeRequested, this);
        }

        protected override void OnDestroy()
        {
            SignalBus.Unsubscribe<WindowModeRequestedSignal>(OnWindowModeRequested, this);
            base.OnDestroy();
        }

        void OnWindowModeRequested(WindowModeRequestedSignal signal)
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            SetWindowMode(signal.Mode);
#endif
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        const float WindowSaveDelaySeconds = 0.5f;

        Coroutine windowTrackingCoroutine;
        WindowPresentationMode observedWindowMode;
        Vector2Int observedWindowedResolution;
        float windowStateChangedAt;
        bool windowTrackingReady;
        bool windowStateDirty;
#endif

        void Start()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            WindowPresentationMode savedWindowMode = ConfigSettingsPersistence.WindowMode;
            ApplyWindowMode(savedWindowMode);
            BeginWindowTracking(savedWindowMode);
#endif
            globalDifficulty = ConfigSettingsPersistence.Difficulty;
            LogHelper.Log("System", $"Context online; scene={SceneManager.GetActiveScene().name}, difficulty={globalDifficulty}.");
            if (IsValentinesDay())
                OnValentineDay.Invoke();
        }

        void Update()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            TrackWindowChanges();
#endif

            if (
                !UICharacterDialog.BlocksInput
                && Keyboard.current != null
                && Keyboard.current.escapeKey.wasPressedThisFrame
                && SceneManager.GetActiveScene().name == gameRegister.games[0].sceneName
            )
            {
                foreach (var dialogue in FindObjectsByType<DialoguePanel>(FindObjectsSortMode.None))
                    if (dialogue.BlocksInput) return;
                ExitGame();
                return;
            }
        }

        // -------- global api --------

        public void SetDifficulty(GameDifficulty difficulty)
        {
            globalDifficulty = difficulty;
            ConfigSettingsPersistence.SetDifficulty(difficulty);
        }

        // Hard is not actually hard, nor the hardest.
        public GameDifficulty GetDifficulty(
            GameDifficulty defaultDifficulty = GameDifficulty.Normal
        )
        {
            globalDifficulty = ConfigSettingsPersistence.Difficulty;
            return globalDifficulty;
        }

#if UNITY_STANDALONE || UNITY_EDITOR
        public WindowPresentationMode GetWindowMode() =>
            Screen.fullScreenMode switch
            {
                FullScreenMode.ExclusiveFullScreen => WindowPresentationMode.Fullscreen,
                FullScreenMode.FullScreenWindow => WindowPresentationMode.Borderless,
                _ => WindowPresentationMode.Windowed,
            };

        public void SetWindowMode(WindowPresentationMode mode)
        {
            WindowPresentationMode current = GetWindowMode();
            if (
                current == WindowPresentationMode.Windowed
                && mode != WindowPresentationMode.Windowed
            )
                ConfigSettingsPersistence.SetWindowMode(mode, Screen.width, Screen.height);
            else
                ConfigSettingsPersistence.SetWindowMode(mode);

            ApplyWindowMode(mode);
            BeginWindowTracking(mode);
        }

        void ApplyWindowMode(WindowPresentationMode mode)
        {
            int displayWidth =
                Display.main.systemWidth > 0
                    ? Display.main.systemWidth
                    : Screen.currentResolution.width;
            int displayHeight =
                Display.main.systemHeight > 0
                    ? Display.main.systemHeight
                    : Screen.currentResolution.height;

            switch (mode)
            {
                case WindowPresentationMode.Fullscreen:
                    Screen.SetResolution(
                        displayWidth,
                        displayHeight,
                        FullScreenMode.ExclusiveFullScreen
                    );
                    break;
                case WindowPresentationMode.Borderless:
                    Screen.SetResolution(
                        displayWidth,
                        displayHeight,
                        FullScreenMode.FullScreenWindow
                    );
                    break;
                default:
                    Vector2Int size = GetWindowedResolution();
                    Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                    break;
            }
        }

        Vector2Int GetWindowedResolution()
        {
            return ConfigSettingsPersistence.GetWindowedResolution(Screen.width, Screen.height);
        }

        void BeginWindowTracking(WindowPresentationMode requestedMode)
        {
            if (windowTrackingCoroutine != null)
                StopCoroutine(windowTrackingCoroutine);

            windowTrackingReady = false;
            windowStateDirty = false;
            windowTrackingCoroutine = StartCoroutine(
                ResumeWindowTrackingAfterResolutionChange(requestedMode)
            );
        }

        IEnumerator ResumeWindowTrackingAfterResolutionChange(WindowPresentationMode requestedMode)
        {
            // Screen.SetResolution is applied at the end of the frame. Waiting prevents the
            // launch dimensions from overwriting the saved dimensions before that happens.
            yield return new WaitForEndOfFrame();
            yield return null;

            Vector2Int requestedWindowedResolution = GetWindowedResolution();
            const int MaxSettleFrames = 30;
            for (int i = 0; i < MaxSettleFrames; i++)
            {
                bool modeSettled = GetWindowMode() == requestedMode;
                bool sizeSettled =
                    requestedMode != WindowPresentationMode.Windowed
                    || (
                        Screen.width == requestedWindowedResolution.x
                        && Screen.height == requestedWindowedResolution.y
                    );
                if (modeSettled && sizeSettled)
                    break;

                yield return null;
            }

            observedWindowMode = GetWindowMode();
            observedWindowedResolution =
                observedWindowMode == WindowPresentationMode.Windowed
                    ? new Vector2Int(Screen.width, Screen.height)
                    : requestedWindowedResolution;
            windowTrackingReady = true;
            windowTrackingCoroutine = null;

            bool appliedDifferently =
                observedWindowMode != requestedMode
                || (
                    observedWindowMode == WindowPresentationMode.Windowed
                    && observedWindowedResolution != requestedWindowedResolution
                );
            if (appliedDifferently)
                SaveTrackedWindowState();
        }

        void TrackWindowChanges()
        {
            if (!windowTrackingReady)
                return;

            WindowPresentationMode currentMode = GetWindowMode();
            bool changed = currentMode != observedWindowMode;
            observedWindowMode = currentMode;

            if (currentMode == WindowPresentationMode.Windowed)
            {
                var currentResolution = new Vector2Int(Screen.width, Screen.height);
                if (currentResolution != observedWindowedResolution)
                {
                    observedWindowedResolution = currentResolution;
                    changed = true;
                }
            }

            if (changed)
            {
                windowStateDirty = true;
                windowStateChangedAt = Time.unscaledTime;
            }

            if (
                windowStateDirty
                && Time.unscaledTime - windowStateChangedAt >= WindowSaveDelaySeconds
            )
            {
                SaveTrackedWindowState();
            }
        }

        void SaveTrackedWindowState()
        {
            if (!windowTrackingReady)
                return;

            observedWindowMode = GetWindowMode();
            if (observedWindowMode == WindowPresentationMode.Windowed)
                observedWindowedResolution = new Vector2Int(Screen.width, Screen.height);

            ConfigSettingsPersistence.SetWindowMode(
                observedWindowMode,
                observedWindowedResolution.x,
                observedWindowedResolution.y
            );
            windowStateDirty = false;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && windowStateDirty)
                SaveTrackedWindowState();
        }

        protected override void OnApplicationQuit()
        {
            SaveTrackedWindowState();
            base.OnApplicationQuit();
        }
#endif

        public void ExitGame()
        {
#if UNITY_STANDALONE || UNITY_EDITOR
            SaveTrackedWindowState();
#endif
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void JumpToScene(GameMeta game)
        {
            if (!sceneTransitionInProgress)
            {
                if (gameRegister != null && gameRegister.games.Count > 0
                    && game.sceneName == gameRegister.games[0].sceneName)
                    StartCoroutine(LoadBuiltInScene(game.sceneName));
                else
                    StartCoroutine(LoadGameScene(game.RuntimeKey));
            }
        }

        public void ReturnToIndex()
        {
            if (gameRegister != null)
            {
                if (!sceneTransitionInProgress)
                {
                    StartCoroutine(LoadBuiltInScene(gameRegister.games[0].sceneName));
                }
            }
            else
            {
                ExitGame();
            }
        }

        public void SaveGameDataInt(string key, int value, bool isGlobal = false)
        {
            string contextKey = GetCTX(isGlobal, key);
            if (IsLocalePreference(key, isGlobal))
                PlayerPrefs.SetInt(contextKey, value);
            else
                ConfigSettingsPersistence.SetInt(contextKey, value);
        }

        public int GetGameDataInt(string key, int defaultValue = 0, bool isGlobal = false)
        {
            string contextKey = GetCTX(isGlobal, key);
            return IsLocalePreference(key, isGlobal)
                ? PlayerPrefs.GetInt(contextKey, defaultValue)
                : ConfigSettingsPersistence.GetInt(contextKey, defaultValue);
        }

        public void SaveGameDataFloat(string key, float value, bool isGlobal = false) =>
            ConfigSettingsPersistence.SetFloat(GetCTX(isGlobal, key), value);

        public float GetGameDataFloat(string key, float defaultValue = 0f, bool isGlobal = false) =>
            ConfigSettingsPersistence.GetFloat(GetCTX(isGlobal, key), defaultValue);

        public void SaveGameDataEnum<T>(string key, T value, bool isGlobal = false)
            where T : Enum =>
            ConfigSettingsPersistence.SetInt(GetCTX(isGlobal, key), (int)(object)value);

        // Almost nothing to worry about boxing unless running this at least a hundred thousand times.
        public T GetGameDataEnum<T>(string key, T defaultValue = default, bool isGlobal = false)
            where T : Enum =>
            (T)
                (object)
                    ConfigSettingsPersistence.GetInt(
                        GetCTX(isGlobal, key),
                        (int)(object)defaultValue
                    );

        public void SaveGameDataString(string key, string value, bool isGlobal = false) =>
            ConfigSettingsPersistence.SetString(GetCTX(isGlobal, key), value);

        public string GetGameDataString(
            string key,
            string defaultValue = "",
            bool isGlobal = false
        ) => ConfigSettingsPersistence.GetString(GetCTX(isGlobal, key), defaultValue);

        // -------- Private --------

        string GetCTX(bool isGlobal, string key) =>
            $"{GameRegister.SCOPE_CTX}_{(isGlobal ? GameRegister.SCOPE_GLOBAL : SceneManager.GetActiveScene().name)}_{key}";

        static bool IsLocalePreference(string key, bool isGlobal) =>
            isGlobal && string.Equals(key, "LocaleKey", StringComparison.Ordinal);

        IEnumerator LoadGameScene(object sceneAddress)
        {
            yield return LoadAddressableScene(sceneAddress);
        }

        public void ReloadCurrentScene()
        {
            if (sceneTransitionInProgress)
                return;

            string sceneName = SceneManager.GetActiveScene().name;
            if (gameRegister != null && sceneName == gameRegister.games[0].sceneName)
                StartCoroutine(LoadBuiltInScene(sceneName));
            else
            {
                GameMeta game = gameRegister != null ? gameRegister.FindGame(sceneName) : default;
                StartCoroutine(LoadGameScene(game.RuntimeKey ?? sceneName));
            }
        }

        IEnumerator LoadBuiltInScene(string sceneName)
        {
            var loading = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Load", $"Loading built-in scene '{sceneName}' from '{SceneManager.GetActiveScene().name}'.");
            sceneTransitionInProgress = true;
            yield return LocalizationSettings.InitializationOperation;

            LocaleSelector localeSelector = FindFirstObjectByType<LocaleSelector>();
            while (localeSelector != null && localeSelector.IsChanging)
            {
                yield return null;
            }

            // Let property variants finish applying before their scene is unloaded.
            yield return null;
            yield return null;

            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            LogHelper.Log("Load", $"Scene '{sceneName}' loaded in {loading.Elapsed.TotalMilliseconds:F1} ms.");
            sceneTransitionInProgress = false;
        }

        IEnumerator LoadAddressableScene(object sceneAddress)
        {
            var loading = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Load", $"Loading addressable scene '{sceneAddress}' from '{SceneManager.GetActiveScene().name}'.");
            sceneTransitionInProgress = true;
            yield return LocalizationSettings.InitializationOperation;

            LocaleSelector localeSelector = FindFirstObjectByType<LocaleSelector>();
            while (localeSelector != null && localeSelector.IsChanging)
            {
                yield return null;
            }

            // Let property variants finish applying before their scene is unloaded.
            yield return null;
            yield return null;

            AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(
                sceneAddress,
                LoadSceneMode.Single,
                true,
                100,
                SceneReleaseMode.ReleaseSceneWhenSceneUnloaded
            );
            yield return handle;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LogHelper.LogError(
                    "Addressables",
                    $"Failed to load game scene '{sceneAddress}' after {loading.Elapsed.TotalMilliseconds:F1} ms: {handle.OperationException}"
                );
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            else
            {
                LogHelper.Log("Load", $"Scene '{handle.Result.Scene.name}' at '{sceneAddress}' loaded in {loading.Elapsed.TotalMilliseconds:F1} ms.");
            }

            sceneTransitionInProgress = false;
        }

        [Button("Set Difficulty")]
        void ApplyDifficulty()
        {
            ConfigSettingsPersistence.SetDifficulty(globalDifficulty);
        }

        public static bool IsValentinesDay()
        {
            DateTime today = DateTime.Today;
            return today.Month == 2 && today.Day == 14;
        }
    }
}
