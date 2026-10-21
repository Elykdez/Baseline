using System;
using Baseline.Dialogue;
using UnityEngine;

namespace Baseline.Presentation
{
    public enum DialogAlertLevel { Info, Success, Warning, Danger }

    [Serializable]
    public sealed class CharacterDialogStyle
    {
        public CharacterDef character;
        public Color colour = Color.white;
    }

    [CreateAssetMenu(fileName = "CharacterDialogStyle", menuName = "Baseline/Presentation/Character Dialog")]
    public sealed class CharacterDialogStyleDef : ScriptableObject
    {
        public CharacterDialogStyle info, success, warning, danger;

        public CharacterDialogStyle For(DialogAlertLevel level) => level switch
        {
            DialogAlertLevel.Success => success,
            DialogAlertLevel.Warning => warning,
            DialogAlertLevel.Danger => danger,
            _ => info,
        };
    }
}
