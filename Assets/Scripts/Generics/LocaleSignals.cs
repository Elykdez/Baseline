using Hypocycloid.Core;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Baseline
{
    /// <summary>Turns the Localization package's locale change into a <see cref="LocaleChangedSignal"/>.</summary>
    public static class LocaleSignals
    {
        // After the first scene's Awake, so the Localization settings asset is loaded; before any
        // Start, which is where Index startup first changes the locale.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bridge()
        {
            // Removing first keeps a single bridge when Enter Play Mode skips the domain reload.
            LocalizationSettings.SelectedLocaleChanged -= Raise;
            LocalizationSettings.SelectedLocaleChanged += Raise;
        }

        static void Raise(Locale locale) => SignalBus.Fire(new LocaleChangedSignal { Locale = locale });
    }
}
