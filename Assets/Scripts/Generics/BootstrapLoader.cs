using System.Collections;
using Hypocycloid.Utils;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Baseline
{
    public sealed class BootstrapLoader : MonoBehaviour
    {
        [SerializeField]
        UnityEvent onCompleted;
        public UnityEvent OnCompleted => onCompleted ??= new UnityEvent();

        IEnumerator Start()
        {
            var loading = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Load", "Initializing localization and loading the saved locale.");
            yield return LocalizationSettings.InitializationOperation;

            var locales = LocalizationSettings.AvailableLocales.Locales;
            var saved = LocaleSelector.Saved(locales);
            if (saved != null && LocalizationSettings.SelectedLocale != saved)
                LocalizationSettings.SelectedLocale = saved;

            yield return LocalizationSettings.SelectedLocaleAsync;
            // Applying a saved locale releases the previous localization operation handles.
            var selection = LocalizationSettings.SelectedLocaleAsync;
            if (selection.IsValid() && selection.Status == AsyncOperationStatus.Succeeded && selection.Result != null)
                LogHelper.Log("System", $"Localization online; locale={selection.Result.Identifier.Code}, elapsed={loading.Elapsed.TotalMilliseconds:F1} ms.");
            else
                LogHelper.LogError("Load", $"Localization initialization failed after {loading.Elapsed.TotalMilliseconds:F1} ms.");
            OnCompleted.Invoke();
        }
    }
}
