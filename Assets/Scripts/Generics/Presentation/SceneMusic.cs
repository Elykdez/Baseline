using Hypocycloid.Core;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>Asks the persistent <see cref="SoundManager"/> for this scene's music (<see cref="MusicRequestedSignal"/>).</summary>
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [SerializeField]
        MusicContext music = MusicContext.Menu;

        // Start runs after every Awake, so the scene's sound manager has claimed its place by now.
        void Start() => SignalBus.Fire(new MusicRequestedSignal { Music = music }, this);
    }
}
