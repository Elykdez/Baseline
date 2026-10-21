using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hypocycloid.Utils
{
    /// <summary>
    /// Owns non-generic runtime state so every closed singleton type can be reset at Unity's
    /// subsystem boundary, including when Enter Play Mode Options disable domain reload.
    /// </summary>
    static class MonoSingletonRuntimeState
    {
        internal sealed class Entry
        {
            internal bool IsInitialized;
            internal bool IsQuitting;
            internal MonoBehaviour Instance;
        }

        static readonly Dictionary<Type, Entry> states = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            states.Clear();
#if UNITY_EDITOR
            editorResetHook = null;
#endif
        }

#if UNITY_EDITOR
        static EditorPlayModeResetHook editorResetHook;

        /// <summary>
        /// The editor raises OnApplicationQuit while leaving Play Mode even though the process is
        /// staying alive. An editor-owned object clears that latch only after teardown reaches
        /// EnteredEditMode. Its instance callback is removed in OnDestroy, so the static editor
        /// event never owns an unmanaged static delegate.
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void InstallEditorResetHook()
        {
            if (editorResetHook != null)
            {
                UnityEngine.Object.DestroyImmediate(editorResetHook);
            }

            editorResetHook = ScriptableObject.CreateInstance<EditorPlayModeResetHook>();
            editorResetHook.hideFlags = HideFlags.HideAndDontSave;
            editorResetHook.Arm();
        }

        sealed class EditorPlayModeResetHook : ScriptableObject
        {
            internal void Arm()
            {
                UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            }

            void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
            {
                if (change == UnityEditor.PlayModeStateChange.EnteredEditMode)
                {
                    states.Clear();
                }
            }

            void OnDestroy()
            {
                UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
                if (editorResetHook == this)
                {
                    editorResetHook = null;
                }
            }
        }
#endif

        internal static Entry Get(Type singletonType)
        {
            if (!states.TryGetValue(singletonType, out Entry state))
            {
                state = new Entry();
                states.Add(singletonType, state);
            }

            return state;
        }
    }

    /// <summary>
    /// Lazy Unity singleton with deterministic initialization, duplicate rejection and safe
    /// teardown. Use <see cref="Ins"/> or <see cref="Instance"/>; both return the same object.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class MonoSingleton<T> : MonoBehaviour
        where T : MonoSingleton<T>
    {
        static MonoSingletonRuntimeState.Entry RuntimeState =>
            MonoSingletonRuntimeState.Get(typeof(T));

        /// <summary>The singleton instance, created on first access when no authored instance exists.</summary>
        public static T Ins => GetOrCreate();

        /// <summary>Compatibility alias for code that uses the conventional Instance name.</summary>
        public static T Instance => GetOrCreate();

        /// <summary>True only while a live instance is already registered; never creates one.</summary>
        public static bool HasInstance => RuntimeState.Instance as T != null;

        /// <summary>Override for a scene-owned singleton that must not survive scene changes.</summary>
        protected virtual bool PersistAcrossScenes => true;

        /// <summary>
        /// Override for a manager whose object exists only to carry it, such as a persistent
        /// manager prefab placed in every scene: a later scene's copy then removes its whole object
        /// rather than leaving an empty one (or its other components) behind.
        /// </summary>
        protected virtual bool DestroyDuplicateObject => false;

        static T GetOrCreate()
        {
            MonoSingletonRuntimeState.Entry state = RuntimeState;
            T current = state.Instance as T;

            // Unity's destroyed-object null semantics can leave a managed reference behind.
            if (current == null)
            {
                state.Instance = null;
                state.IsInitialized = false;
            }

            if (state.IsQuitting)
            {
                return null;
            }
            if (current != null)
            {
                return current;
            }

            current = FindFirstObjectByType<T>(FindObjectsInactive.Include);
            if (current != null)
            {
                current.Claim(state);
                return state.Instance as T;
            }

            var host = new GameObject("_" + typeof(T).FullName);
            T created = host.AddComponent<T>();
            // Awake claims synchronously in Play Mode. EditMode callers do not receive Awake, so
            // claim explicitly there; Claim is idempotent when Awake already did the work.
            if (state.Instance as T == null && created != null)
            {
                created.Claim(state);
            }
            current = state.Instance as T;
            if (current == null)
            {
                DestroySafely(host);
                LogHelper.LogWarning($"Problem during the creation of {typeof(T)}.");
            }
            return current;
        }

        protected virtual void Awake()
        {
            MonoSingletonRuntimeState.Entry state = RuntimeState;
            T current = state.Instance as T;
            if (current == null)
            {
                state.Instance = null;
                state.IsInitialized = false;
            }
            else if (current != this)
            {
                LogHelper.Log($"Another instance of {typeof(T)} already exists; removing the duplicate {(DestroyDuplicateObject ? "object" : "component")}.");
                DestroySafely(DestroyDuplicateObject ? gameObject : this);
                return;
            }

            if (state.IsQuitting)
            {
                DestroySafely(this);
                return;
            }

            Claim(state);
        }

        void Claim(MonoSingletonRuntimeState.Entry state)
        {
            state.Instance = this;
            if (state.IsInitialized)
            {
                return;
            }

            // Set before Init so recursive access from Init returns this instance instead of creating
            // another GameObject.
            state.IsInitialized = true;
            try
            {
                if (PersistAcrossScenes && Application.isPlaying)
                {
                    DontDestroyOnLoad(gameObject);
                }
                Init();
            }
            catch
            {
                if (state.Instance == this)
                {
                    state.Instance = null;
                    state.IsInitialized = false;
                }
                throw;
            }
        }

        /// <summary>Called exactly once each time this object claims a freshly reset runtime entry.</summary>
        protected virtual void Init() { }

        protected virtual void OnDestroy()
        {
            MonoSingletonRuntimeState.Entry state = RuntimeState;
            if (state.Instance == this)
            {
                state.Instance = null;
                state.IsInitialized = false;
            }
        }

        protected virtual void OnApplicationQuit()
        {
            RuntimeState.IsQuitting = true;
        }

        static void DestroySafely(UnityEngine.Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
