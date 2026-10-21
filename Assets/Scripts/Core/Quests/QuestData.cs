using System;

namespace Baseline.Quests
{
    [Serializable]
    public sealed class QuestData
    {
        public string id = string.Empty;
        public string textKey = string.Empty;
        /// <summary>Literal player-authored text, used only when textKey is empty.</summary>
        public string text = string.Empty;
        public bool secondary;
        /// <summary>Objectives that must be done before this step can be shown or advanced.</summary>
        public string[] requiresObjectives = Array.Empty<string>();
        /// <summary>The quest signal that shows the hidden step, such as <c>Quest.Begin</c>. Empty leaves it to a script or dialog action.</summary>
        public string showOn = string.Empty;
        /// <summary>The quest signal whose amount counts toward completing the shown step, such as <c>Enemy.Down</c>.</summary>
        public string completeOn = string.Empty;
        /// <summary>The quest signal that fails the shown step.</summary>
        public string failOn = string.Empty;
        /// <summary>The quest signal that hides the shown step again, keeping its progress until it is shown again.</summary>
        public string hideOn = string.Empty;
        /// <summary>How much progress completes it; above one, the panel shows the count.</summary>
        public int count = 1;
        /// <summary>A place the objective arrow points at while the step is active, such as <c>camp</c>.</summary>
        public string target = string.Empty;

        public int Required => Math.Max(1, count);
    }

}
