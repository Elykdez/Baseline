using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace Baseline
{
    public class UIIndexButton : UIButton
    {
        public TextMeshProUGUI gameName;
        public LocalizeStringEvent locale;
        public Image gameIcon;
        public string gamePath;
        public UnityAction onButtonClick;

        void Start()
        {
            GetComponent<Button>().onClick.AddListener(onButtonClick);
        }
    }
}
