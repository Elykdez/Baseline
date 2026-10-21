using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

using Object = UnityEngine.Object;

namespace Hypocycloid.Utils
{
    public abstract class AssetLoadBase<T> : MonoBehaviour where T : Object
    {
        public AssetReference asset;

        [SerializeField] UnityEvent<T> onAssetLoad;
        public UnityEvent<T> OnAssetLoad => onAssetLoad ??= new UnityEvent<T>();

        AsyncOperationHandle<T> loadHandle;
        bool ownsHandle;
        System.Diagnostics.Stopwatch loading;

        void Awake()
        {
            if (asset != null && asset.RuntimeKeyIsValid())
            {
                loading = System.Diagnostics.Stopwatch.StartNew();
                LogHelper.Log("Load", $"Loading addressable asset <{typeof(T).Name}:{asset.RuntimeKey}>.");
                loadHandle = asset.LoadAssetAsync<T>();
                ownsHandle = true;
                loadHandle.Completed += OnLoadCompleted;
            }
        }

        void OnLoadCompleted(AsyncOperationHandle<T> handle)
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                LogHelper.Log("Load", $"Loaded addressable asset <{typeof(T).Name}:{asset.RuntimeKey}> in {loading.Elapsed.TotalMilliseconds:F1} ms.");
                OnAssetLoad.Invoke(handle.Result);
            }
            else
            {
                LogHelper.LogError($"Failed to load addressable asset '{asset.RuntimeKey}'.");
            }
        }

        void OnDestroy()
        {
            if (ownsHandle && loadHandle.IsValid())
            {
                loadHandle.Completed -= OnLoadCompleted;
                Addressables.Release(loadHandle);
            }

            ownsHandle = false;
        }
    }
}
