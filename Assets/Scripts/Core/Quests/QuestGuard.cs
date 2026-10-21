using System;
using Baseline.Dialogue;

namespace Baseline.Quests
{
    [Serializable]
    public sealed class QuestGuard
    {
        public string action = string.Empty;
        public string condition = string.Empty;
        public string hintKey = string.Empty;
        public string hint = string.Empty;
        public string target = string.Empty;
        public DialogDef dialogue;
    }
}
