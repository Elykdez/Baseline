using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Hypocycloid.Utils
{
    public class ResourceManager : MonoSingleton<ResourceManager>
    {
        public string resPath;

        readonly Dictionary<string, string> resourceAddresses = new();
        readonly Dictionary<string, AsyncOperationHandle> loadedAssets = new();

        IEnumerator Start()
        {
            yield return ConfigReader.GetConfigFile(resPath, BuildMap);
        }

        void BuildMap(string content)
        {
            resourceAddresses.Clear();
            if (string.IsNullOrWhiteSpace(content))
            {
                return;
            }

            ConfigReader.ReadByLine(content, line =>
            {
                string[] pair = line.Split(new[] { '=' }, 2);
                if (pair.Length == 2)
                {
                    resourceAddresses[pair[0].Trim()] = pair[1].Trim();
                }
            });
        }

        public async Task<T> Load<T>(string resourceName) where T : Object
        {
            if (loadedAssets.TryGetValue(resourceName, out AsyncOperationHandle existing) && existing.IsValid())
            {
                return existing.Result as T;
            }

            if (!resourceAddresses.TryGetValue(resourceName, out string address))
            {
                LogHelper.LogError("Addressables", $"Resource key '{resourceName}' is not configured.");
                return null;
            }

            AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(address);
            var loading = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Load", $"Loading resource '{resourceName}' at '{address}'.");
            await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                LogHelper.LogError("Load", $"Resource '{resourceName}' failed after {loading.Elapsed.TotalMilliseconds:F1} ms.");
                Addressables.Release(handle);
                return null;
            }

            loadedAssets[resourceName] = handle;
            LogHelper.Log("Load", $"Resource '{resourceName}' loaded in {loading.Elapsed.TotalMilliseconds:F1} ms.");
            return handle.Result;
        }

        public void Release(string resourceName)
        {
            if (loadedAssets.Remove(resourceName, out AsyncOperationHandle handle) && handle.IsValid())
            {
                Addressables.Release(handle);
            }
        }

        protected override void OnDestroy()
        {
            foreach (AsyncOperationHandle handle in loadedAssets.Values)
            {
                if (handle.IsValid()) Addressables.Release(handle);
            }

            loadedAssets.Clear();
            base.OnDestroy();
        }
    }
}
