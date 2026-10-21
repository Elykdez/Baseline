using System;
using Hypocycloid.Core;

namespace Baseline.Quests
{
    /// <summary>
    /// Marks a const string as a quest signal tag the game raises, so the Quest Debugger and the content tests can list
    /// every tag and find a def waiting for one nothing raises. Metadata only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class QuestSignalTagAttribute : Attribute { }

    /// <summary>Marks a const string naming a live gameplay condition available to quest action guards.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class QuestConditionTagAttribute : Attribute { }

    /// <summary>
    /// Something happened that a quest run may be waiting for . The tag is free text: the code,
    /// dialog or step that raises it names the moment, and quest defs name the tags their steps and endings hear. Only
    /// <see cref="Run"/> hears it, so concurrent runs in one process stay apart.
    /// </summary>
    public struct QuestSignal : ISignal
    {
        public string Tag;
        public QuestRun Run;
        /// <summary>How much a step's <c>completeOn</c> counts it for; at least one.</summary>
        public int Amount;

        public QuestSignal(string tag, QuestRun run, int amount = 1)
        {
            Tag = tag;
            Run = run;
            Amount = amount;
        }
    }
}
