using System.Reflection;
using Baseline.Dialogue;
using TMPro;
using UnityEngine;

namespace Baseline
{
    /// <summary>Authored UI shared by conversations and queued alerts in any scene.</summary>
    [CreateAssetMenu(fileName = "SharedUIContent", menuName = "Baseline/Shared UI Content")]
    public sealed class SharedUIContent : ScriptableObject
    {
        public TMP_Settings textSettings;
        public ContextManager contextPrefab;
        public TextMeshProUGUI textPrefab;
        public DialoguePanel dialoguePrefab;
        public UICharacterDialogView characterDialogPrefab;
        public UICharacterDialog characterAlertsPrefab;

        static SharedUIContent instance;
        public static SharedUIContent Instance
        {
            get
            {
#if UNITY_EDITOR
                if (instance == null && !Application.isPlaying)
                    UnityEditor.AssetDatabase.LoadAssetAtPath<SharedUIContent>(EditorAssetPath)?.Activate();
#endif
                return instance;
            }
        }

        public void Activate()
        {
            instance = this;
            // TMP has no settings setter; preserve this field for IL2CPP in link.xml.
            typeof(TMP_Settings).GetField("s_Instance", BindingFlags.Static | BindingFlags.NonPublic)
                .SetValue(null, textSettings);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => instance = null;

#if UNITY_EDITOR
        public const string EditorAssetPath = "Assets/Bundles/Core/Config/Generics/SharedUIContent.asset";

        [UnityEditor.InitializeOnLoadMethod]
        static void InitializeEditor()
        {
            UnityEditor.EditorApplication.playModeStateChanged -= RestoreEditorContent;
            UnityEditor.EditorApplication.playModeStateChanged += RestoreEditorContent;
            if (!Application.isPlaying) RestoreEditorContent(UnityEditor.PlayModeStateChange.EnteredEditMode);
        }

        static void RestoreEditorContent(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                UnityEditor.AssetDatabase.LoadAssetAtPath<SharedUIContent>(EditorAssetPath)?.Activate();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeDirectPlay()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Index")
                UnityEditor.AssetDatabase.LoadAssetAtPath<SharedUIContent>(EditorAssetPath)?.Activate();
        }
#endif
    }
}
