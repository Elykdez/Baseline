using Baseline.Presentation;
using UnityEngine;

namespace Baseline.Demo.RelayRun
{
    /// <summary>Activates the authored singleton hosts as roots before their Awake methods run.</summary>
    public sealed class DemoPresentationLifetime : MonoBehaviour
    {
        public SoundManager sound;
        public FxManager effects;
        public bool IsReady => sound != null && effects != null;

        void Awake()
        {
            if (!Application.isPlaying) return;
            DontDestroyOnLoad(gameObject);
            Activate(sound.gameObject);
            Activate(effects.gameObject);
        }

        static void Activate(GameObject host)
        {
            host.transform.SetParent(null, false);
            host.SetActive(true);
        }

        void OnDestroy()
        {
            if (!Application.isPlaying) return;
            if (sound != null) Destroy(sound.gameObject);
            if (effects != null) Destroy(effects.gameObject);
        }
    }
}
