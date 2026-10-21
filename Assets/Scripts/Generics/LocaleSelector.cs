using System;
using System.Collections;
using System.Collections.Generic;
using Hypocycloid.Core;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace Baseline
{
    /// <summary>
    /// Changes and remembers the language; menus ask for one with <see cref="LocaleRequestedSignal"/>.
    /// The saved language is applied while Localization starts, by <see cref="SavedLocaleSelector"/>.
    /// </summary>
    public class LocaleSelector : SignalBehaviour
    {
        bool active = false;
        public bool IsChanging => active;

        // Compatibility for the retained baseline menu; persistence uses locale codes.
        public int GetLocale() => LocalizationSettings.AvailableLocales.Locales.IndexOf(
            LocalizationSettings.SelectedLocale);

        protected override void OnEnable()
        {
            base.OnEnable();
            Subscribe<LocaleRequestedSignal>(signal => ChangeLocale(signal.Index));
        }

        public void ChangeLocale(int localeID)
        {
            if (active)
                return;
            StartCoroutine(SetLocale(localeID));
        }

        /// <summary>
        /// The saved language among <paramref name="locales"/>, or null while the player has chosen none,
        /// so startup follows the system language. A language saved by an older build moves over here.
        /// </summary>
        public static Locale Saved(IList<Locale> locales)
        {
            if (locales == null || locales.Count == 0)
                return null;

            string code = ConfigSettingsPersistence.Locale;
            if (
                string.IsNullOrEmpty(code)
                && ConfigSettingsPersistence.TryTakeLegacyLocaleIndex(out int index)
                && (uint)index < (uint)locales.Count
                && locales[index] != null
            )
            {
                code = locales[index].Identifier.Code;
                ConfigSettingsPersistence.SetLocale(code);
            }

            if (string.IsNullOrEmpty(code))
                return null;
            foreach (Locale locale in locales)
                if (locale != null && locale.Identifier.Code == code)
                    return locale;
            return null;
        }

        IEnumerator SetLocale(int _localeID)
        {
            active = true;
            yield return LocalizationSettings.InitializationOperation;
            var locales = LocalizationSettings.AvailableLocales.Locales;
            if ((uint)_localeID >= (uint)locales.Count)
            {
                active = false;
                yield break;
            }
            var locale = locales[_localeID];
            if (LocalizationSettings.SelectedLocale != locale)
            {
                LocalizationSettings.SelectedLocale = locale;
                yield return null;
            }
            ConfigSettingsPersistence.SetLocale(locale.Identifier.Code);
            active = false;
        }

        [Button("Set EN")]
        void _Switch00() => ChangeLocale(0);

        [Button("Set CN")]
        void _Switch01() => ChangeLocale(1);
    }

    /// <summary>
    /// Starts Localization on the saved language, so nothing loads in another language first. It sits
    /// in the Localization Settings' startup selectors after the command line; returning null defers to
    /// the system language and then the project default.
    /// </summary>
    [Serializable]
    public sealed class SavedLocaleSelector : IStartupLocaleSelector
    {
        public Locale GetStartupLocale(ILocalesProvider availableLocales) =>
            LocaleSelector.Saved(availableLocales?.Locales);
    }
}
