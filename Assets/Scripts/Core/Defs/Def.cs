using UnityEngine;

namespace Baseline.Defs
{
    /// <summary>
    /// Base of every authored definition. A def is the stat block for a kind of thing, held as an
    /// asset so content can be retuned without touching code; the behaviour that reads it stays in
    /// C#. <see cref="DefName"/> is the identity used by lookups, so it
    /// must stay stable once content ships even if the label changes.
    /// </summary>
    public abstract class Def : ScriptableObject
    {
        [SerializeField, Tooltip("Stable identity used by lookups. Defaults to the asset name.")]
        string defName = string.Empty;

        [SerializeField, Tooltip("Localization key (StringTable) for the display name.")]
        string labelKey = string.Empty;

        [SerializeField, TextArea, Tooltip("Designer-facing note. Not shown in game.")]
        string editorNote = string.Empty;

        /// <summary>Stable identity of this def. Falls back to the asset name when left blank.</summary>
        public string DefName => string.IsNullOrEmpty(defName) ? name : defName;

        /// <summary>Localization key for the display name, or empty when the def has none.</summary>
        public string LabelKey => labelKey;

        /// <summary>
        /// Throws when the def is not usable. Call DefDatabase.Validate after loading content to check every registered definition.
        /// </summary>
        public virtual void Validate() { }
    }
}
