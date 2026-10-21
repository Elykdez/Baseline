using Baseline.Quests;

namespace Baseline.Demo.RelayRun
{
    public static class DemoRunnerSignals
    {
        [QuestSignalTag] public const string Briefed = "Relay.Briefed";
        [QuestSignalTag] public const string Metre = "Relay.Metre";
        [QuestSignalTag] public const string Cleared = "Relay.Cleared";
        [QuestSignalTag] public const string Crashed = "Relay.Crashed";
    }
}
