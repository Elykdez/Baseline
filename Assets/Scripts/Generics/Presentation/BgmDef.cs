using System;
using System.Collections.Generic;
using UnityEngine;

namespace Baseline.Presentation
{
    /// <summary>Where the game is, for music.</summary>
    public enum MusicContext
    {
        Menu,
        Battle,
        /// <summary>Fade the current music to silence.</summary>
        Silence,
    }

    /// <summary>The menu score and the shuffled battle playlist.</summary>
    [CreateAssetMenu(fileName = "Bgm", menuName = "Baseline/Presentation/BGM")]
    public sealed class BgmDef : ScriptableObject
    {
        [SerializeField, Tooltip("Looped on the Index screen.")]
        AudioClip menu;

        [SerializeField, Tooltip("Shuffled during a match. Empty keeps the menu track playing.")]
        AudioClip[] battle = Array.Empty<AudioClip>();

        public AudioClip Menu => menu;
        public IReadOnlyList<AudioClip> Battle => battle;

        /// <summary>Throws when the definition cannot play.</summary>
        public void Validate()
        {
            if (menu == null)
                throw new InvalidOperationException($"BGM def '{name}' has no menu track.");
            var unique = new HashSet<AudioClip>();
            for (int i = 0; i < battle.Length; i++)
            {
                if (battle[i] == null)
                    throw new InvalidOperationException($"BGM def '{name}' has an empty battle track at {i}.");
                if (!unique.Add(battle[i]))
                    throw new InvalidOperationException($"BGM def '{name}' lists '{battle[i].name}' twice.");
            }
        }
    }

    /// <summary>Shuffle bag that avoids repeats within a cycle and across the seam between cycles.</summary>
    public sealed class BgmPlaylist
    {
        readonly IReadOnlyList<AudioClip> tracks;
        readonly int[] order;
        readonly System.Random random;
        int cursor;
        int previous = -1;

        public BgmPlaylist(IReadOnlyList<AudioClip> tracks, System.Random random = null)
        {
            this.tracks = tracks ?? throw new ArgumentNullException(nameof(tracks));
            this.random = random ?? new System.Random();
            order = new int[tracks.Count];
            cursor = order.Length;
        }

        public AudioClip Next()
        {
            if (order.Length == 0)
                return null;
            if (cursor >= order.Length)
                Shuffle();
            previous = order[cursor++];
            return tracks[previous];
        }

        void Shuffle()
        {
            for (int i = 0; i < order.Length; i++)
                order[i] = i;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int other = random.Next(i + 1);
                (order[other], order[i]) = (order[i], order[other]);
            }
            if (order.Length > 1 && order[0] == previous)
                (order[0], order[1]) = (order[1], order[0]);
            cursor = 0;
        }
    }
}
