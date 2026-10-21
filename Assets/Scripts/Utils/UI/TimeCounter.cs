using UnityEngine;
using TMPro;

namespace Hypocycloid.Utils
{
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class UITimeCounter : MonoBehaviour
    {
        public float timeCounter;
        public bool isCD;
        [SerializeField] bool isPaused;

        public float Timer { get; private set; }

        TextMeshProUGUI timerText;

        void Awake()
        {
            timerText = GetComponent<TextMeshProUGUI>();
            Timer = timeCounter;
        }

        void Update()
        {
            if (!isPaused)
            {
                if (isCD) Timer -= Time.deltaTime;
                else Timer += Time.deltaTime;
                timerText.text = FormatTime(Timer);
            }
        }

        public void Pause(bool isPaused)
        {
            this.isPaused = isPaused;
        }

        public void ResetState()
        {
            Timer = timeCounter = 0;
            timerText.text = $"{0:00}:{0:00}";
            Pause(false);
        }

        public static string FormatTime(float timer)
        {
            int minutes = Mathf.FloorToInt(timer / 60f);
            int seconds = Mathf.FloorToInt(timer - minutes * 60);
            return $"{minutes:00}:{seconds:00}";
        }

    }
}
