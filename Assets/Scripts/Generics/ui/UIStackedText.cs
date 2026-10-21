using TMPro;
using UnityEngine;

namespace Baseline
{
    /// <summary>
    /// A caption shown one character per line, for a label standing beside a vertical axis. Its
    /// LocalizeStringEvent sends the localized caption to <see cref="SetText"/>.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class UIStackedText : MonoBehaviour
    {
        public void SetText(string value) => GetComponent<TMP_Text>().text = string.Join("\n", (value ?? "").ToCharArray());
    }
}
