# Localization

The collection is `StringTable` (`GameRegister.DEF_STT`); its shared data and `StringTable_en`
and `StringTable_zh` assets are under `Assets/Locales/Tables`.

| Text | Authoring location |
| --- | --- |
| Fixed caption/title/placeholder | Prefab `LocalizeStringEvent.StringReference`, with its update event wired to the TMP label. |
| Fixed options/tabs | Inspector-assigned `LocalizedString` labels on `UIOptionGroup` / `UITabs`, in matching order. |
| Runtime localized sentence/status | A serialized `LocalizedString` on the feature component or a feature text-settings ScriptableObject, with runtime arguments. |
| Definition display name | The def's `labelKey`, resolved by the game's presentation code. |
| User-entered name or literal numeric data | `ShowPlain(value)` so an earlier localization binding cannot overwrite it. |

Author keys and both translations through the localization tools; preserve shared key IDs.
Avoid English fallback strings, concatenated localization keys and fixed UI copy embedded in C#.
A feature-specific text-settings type can organize runtime sentences. Reuse shared entries and
references rather than duplicating translations across assets.

[UILocalizedText](../Assets/Scripts/Generics/ui/UILocalizedText.cs) provides these extensions:

```csharp
using Baseline;
using TMPro;
using UnityEngine.Localization;

// label is an authored TMP_Text; caption is an inspector-assigned LocalizedString.
label.Localize(caption);
// For unlocalized runtime data on a reused label:
label.ShowPlain(playerChosenName);
```

`Localize` adds and wires a `LocalizeStringEvent` only if none exists. An existing component
must already have the correct TMP update listener. `ShowPlain` disables that localizer before
assigning text. Clear placeholder localization keys on templates intended for literal runtime data.
For stacked text, route `LocalizeStringEvent.OnUpdateString` to `UIStackedText.SetText` so it can
format the vertical layout; assigning directly to TMP text would bypass that formatting.
Check the actual font/fallback chain for Chinese glyphs; successful string lookup does not prove
the font can render them. `ClearSans-Regular SDF` falls back to `SmileySans-Oblique SDF`. Text set at
runtime in Clear Sans, such as dropdown captions, slider values and manuals, can therefore show
Chinese in either language. Clear Sans is a dynamic font, so previewing text in the editor adds
glyphs to its asset. Leave that atlas churn out of commits.

`UILocalizedText.Create(parent, name, englishFont)` instantiates the shared catalog's authored
`ui_text` template, preserving its English/Chinese font and material variants. An optional English
font override changes only that variant. `UILocalizationReady` delays the template's
`GameObjectLocalizer` during runtime startup until localization is ready.

`LocaleSelector` accepts `LocaleRequestedSignal { Index = index }`, applies the chosen locale
and stores its language code through settings. `LocaleSignals` bridges package locale changes
to `LocaleChangedSignal` during runtime initialization. The retained bootstrap applies the saved
locale after localization initializes. `SavedLocaleSelector` is also available as a startup selector,
but do not assume it is configured in the localization asset without inspecting that asset.

In Edit Mode, runtime-only UnityEvent listeners may not update a preview. Verify live locale
switching in Play Mode, or implement an
editor-only preview that evaluates the authored references without writing preview translations
back as fixed text. Restore the previous locale and scene after inspection.

## Audit

Run the localization cases in `BasicUITests`. Inspect both retained languages, font fallback coverage, long text and live locale changes on the actual authored prefab. Check every new key/reference and translation; existing component tests do not enumerate all future content.

Related: [UI](UI.md), [Saves](Saves.md), [Scenes](Scenes.md).
