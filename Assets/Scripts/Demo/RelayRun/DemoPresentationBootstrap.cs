using System.Collections;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Baseline.Demo.RelayRun
{
    /// <summary>Keeps presentation assets loaded for as long as their persistent managers use them.</summary>
    public sealed class DemoPresentationBootstrap : MonoBehaviour
    {
        public AssetReferenceGameObject presentationPrefab;
        public GameObject surface;

        static AsyncOperationHandle<GameObject> handle;
        static DemoPresentationLifetime lifetime;
        static bool quitting;
        public static bool IsReady => lifetime != null && lifetime.IsReady;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState()
        {
            Application.quitting -= Release;
            handle = default;
            lifetime = null;
            quitting = false;
        }

        void Awake()
        {
            if (surface != null) surface.SetActive(false);
            if (handle.IsValid()) return;
            Application.quitting += Release;
            handle = Addressables.LoadAssetAsync<GameObject>(presentationPrefab);
            // A static callback still finishes when the player leaves the menu during the load.
            handle.Completed += Loaded;
        }

        static void Loaded(AsyncOperationHandle<GameObject> loaded)
        {
            if (quitting) return;
            if (loaded.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Relay Run presentation assets could not be loaded.");
                return;
            }
            if (lifetime != null) return;
            GameObject instance = Instantiate(loaded.Result);
            lifetime = instance.GetComponent<DemoPresentationLifetime>();
            if (lifetime == null)
            {
                Destroy(instance);
                Debug.LogError("Relay Run presentation prefab needs DemoPresentationLifetime.");
            }
        }

        IEnumerator Start()
        {
            if (handle.IsValid()) yield return handle;
            if (IsReady && surface != null) surface.SetActive(true);
        }

        static void Release()
        {
            quitting = true;
            Application.quitting -= Release;
            if (handle.IsValid())
            {
                handle.Completed -= Loaded;
                Addressables.Release(handle);
            }
            handle = default;
        }
    }
}
