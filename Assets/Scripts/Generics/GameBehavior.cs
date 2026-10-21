using Hypocycloid.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Baseline
{
    public abstract class GameBehavior<T> : AbstractInputSystem
        where T : GameBehavior<T>
    {
        [Header("Game Generics")]
        public GameObject gamePad;

        [SerializeField]
        protected TouchDetector touchInput;

        [SerializeField]
        protected TextMeshProUGUI scoreText;

        [SerializeField]
        protected TextMeshProUGUI hiscoreText;

        protected GameDifficulty Localdifficulty
        {
            get => ContextManager.Ins.GetDifficulty();
        }

        public static T Ins { get; private set; }

        protected int score;
        public int Score => score;

        protected GameMeta CurrentSceneName
        {
            get => ContextManager.Ins.gameRegister.FindGame(SceneManager.GetActiveScene().name);
        }

        protected virtual void Start()
        {
            LogHelper.Log("Game started.");
        }

        protected virtual void Awake()
        {
            // local singleton
            if (Ins != null)
            {
                DestroyImmediate(gameObject);
            }
            else
            {
                Ins = this as T;
                // DontDestroyOnLoad(gameObject);
                Application.targetFrameRate = 60;
            }
        }

        protected virtual void Update()
        {
            if (GetKeyDown(Key.Escape))
            {
                QuitToIndex();
            }
        }

        protected virtual void OnDestroy()
        {
            Time.timeScale = 1f;
        }

        // -------- public API --------

        public virtual void QuitToIndex()
        {
            ContextManager.Ins.ReturnToIndex();
        }

        public void Reload()
        {
            ContextManager.Ins.ReloadCurrentScene();
        }

        public void SetScore(int score)
        {
            this.score = score;
            scoreText.text = $"{this.score}";
            SaveHiscore();
        }

        public void IncreaseScore(int points)
        {
            SetScore(score + points);
        }

        public void SetHiscore()
        {
            hiscoreText.text = $"{LoadHiscore()}";
        }

        // -------- public API end --------

        void SaveHiscore()
        {
            int hiscore = LoadHiscore();

            if (score > hiscore)
            {
                ContextManager.Ins.SaveGameDataInt(GameRegister.SCOPE_HISCORE, score);
            }
        }

        int LoadHiscore()
        {
            return ContextManager.Ins.GetGameDataInt(GameRegister.SCOPE_HISCORE);
        }
    }
}
