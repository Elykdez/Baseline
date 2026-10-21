using System;
using Baseline.Defs;
using UnityEngine;

namespace Baseline.Dialogue
{
    [Serializable]
    public sealed class DialogLine
    {
        public CharacterDef speaker;
        public string expression = "default";
        [Tooltip("Inherit the character's default facing, or override it for this line.")]
        public PortraitFacingOverride facing;
        [TextArea(2, 6)] public string text;
        public string textKey;
        public Sprite illustration;
    }

    [CreateAssetMenu(fileName = "Dialog", menuName = "Baseline/Defs/Dialog")]
    public sealed class DialogDef : Def
    {
        public string title;
        public string titleKey;
        [Tooltip("Show both recent speakers on wide layouts. Compact layouts still show only the active speaker.")]
        public bool dualCharacters = true;
        public DialogLine[] lines = Array.Empty<DialogLine>();

        public override void Validate()
        {
            if (lines == null || lines.Length == 0)
                throw new InvalidOperationException($"Dialog '{DefName}' needs at least one line.");
            for (int i = 0; i < lines.Length; i++)
            {
                DialogLine line = lines[i];
                if (line == null || line.speaker == null || string.IsNullOrWhiteSpace(line.text))
                    throw new InvalidOperationException($"Dialog '{DefName}', line {i + 1}, needs a speaker and fallback text.");
                line.speaker.Validate();
            }
        }
    }
}
