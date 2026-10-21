using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Baseline
{
    [CreateAssetMenu(
        fileName = "GameRegister",
        menuName = "ScriptableObjects/GameRegister",
        order = 1
    )]
    public class GameRegister : ScriptableObject
    {
        // -------- Constants --------

        public const string DEF_STT = "StringTable";
        public const string SCOPE_CTX = "Baseline";
        public const string SCOPE_GLOBAL = "#";
        public const string SCOPE_HISCORE = "hiscore";
        public const string SCOPE_DIFFICULTY = "difficulty";
        public const string SCOPE_AUD_MUSIC = "aud_music";
        public const string SCOPE_AUD_SFX = "aud_sfx";

        public static float defMusicVol = 1f;
        public static float defSoundVol = 1f;

        // -------- Configs --------

        [Header("Registered Games")]
        public List<GameMeta> games;

        // -------- Utils --------

        public GameMeta FindGame(string scenePath) =>
            games.FirstOrDefault(game => game.sceneName == scenePath);
    }
}
