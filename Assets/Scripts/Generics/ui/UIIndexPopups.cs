using Hypocycloid.Utils;
using UnityEngine;

namespace Baseline
{
    [RequireComponent(typeof(UIFadeAnim))]
    public class UIIndexPopups : MonoBehaviour
    {
        public UIFadeAnim fadeAnim;

        void Awake()
        {
            GetComponent<UIFadeAnim>().enabled = true;
        }
    }
}
