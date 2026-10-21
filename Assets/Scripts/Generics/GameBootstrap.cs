using System.Collections;
using Hypocycloid.Utils;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Baseline
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] AssetReferenceT<SharedUIContent> content;
        [SerializeField] AssetReferenceGameObject menuPrefab;

        AsyncOperationHandle<GameObject> menuHandle;
        GameObject menu;
        static AsyncOperationHandle<SharedUIContent> contentHandle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            Application.quitting -= ReleaseContent;
            contentHandle = default;
        }

        public bool IsReady => menu != null;

        IEnumerator Start()
        {
            var startup = System.Diagnostics.Stopwatch.StartNew();
            var loading = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Load", $"Loading shared UI content (cached={contentHandle.IsValid()}).");
            // Keep shared prefabs and TMP defaults loaded while any scene can use them.
            if (!contentHandle.IsValid())
            {
                contentHandle = Addressables.LoadAssetAsync<SharedUIContent>(content);
                Application.quitting += ReleaseContent;
            }
            yield return contentHandle;
            if (contentHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[Load]: Unable to load shared UI content after {loading.Elapsed.TotalMilliseconds:F1} ms.");
                yield break;
            }
            contentHandle.Result.Activate();
            LogHelper.Log("Load", $"Shared UI content loaded in {loading.Elapsed.TotalMilliseconds:F1} ms.");
            if (!ContextManager.HasInstance) Instantiate(contentHandle.Result.contextPrefab);
            loading.Restart();
            LogHelper.Log("Load", $"Loading the Index menu at '{menuPrefab.RuntimeKey}'.");
            menuHandle = Addressables.LoadAssetAsync<GameObject>(menuPrefab);
            yield return menuHandle;
            if (menuHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[Load]: Unable to load the Index menu after {loading.Elapsed.TotalMilliseconds:F1} ms.");
                yield break;
            }

            menu = Instantiate(menuHandle.Result, transform);
            LogHelper.Log("Load", $"Index menu loaded in {loading.Elapsed.TotalMilliseconds:F1} ms.");
            LogHelper.Log("System", $"Game bootstrap online in {startup.Elapsed.TotalMilliseconds:F1} ms.");
        }

        void OnDestroy()
        {
            if (menu != null) Destroy(menu);
            if (menuHandle.IsValid()) Addressables.Release(menuHandle);
        }

        static void ReleaseContent()
        {
            Application.quitting -= ReleaseContent;
            if (contentHandle.IsValid()) Addressables.Release(contentHandle);
            contentHandle = default;
        }
    }
}
