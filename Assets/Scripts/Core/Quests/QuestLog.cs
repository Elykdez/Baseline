using System;
using System.Collections.Generic;
using System.IO;
using Baseline.Defs;
using Hypocycloid.Utils;
using Newtonsoft.Json;
using UnityEngine;

namespace Baseline.Quests
{
    /// <summary>One named number in a quest's record: how often an outcome came, or <see cref="QuestLog.PassedValue"/>.</summary>
    [Serializable]
    public sealed class QuestValue
    {
        public string id;
        public int value;
    }

    [Serializable]
    public sealed class QuestRecord
    {
        public string id;
        public List<QuestValue> values = new();

        public int Get(string name) => values.Find(entry => entry.id == name)?.value ?? 0;

        public void Add(string name, int amount)
        {
            QuestValue entry = values.Find(value => value.id == name);
            if (entry == null) values.Add(entry = new QuestValue { id = name });
            entry.value = checked(entry.value + amount);
        }
    }

    [Serializable]
    public sealed class QuestLogData
    {
        public const int CurrentVersion = 2;
        public int version = CurrentVersion;
        public List<QuestRecord> quests = new();

        public QuestRecord Find(string id) => quests.Find(entry => entry.id == id);
    }

    /// <summary>
    /// Every quest's lasting result in one save: per quest, named values its endings
    /// add to. Written safely, keeping the previous copy, and read back from it if the latest is damaged.
    /// </summary>
    public static class QuestLog
    {
        public const string FileName = "Quests.json";
        /// <summary>The value a passing ending adds to: what quest requirements read.</summary>
        public const string PassedValue = "passed";
        const string LegacyDone = "done", LegacySkipped = "skipped", LegacyFailed = "failed";
        static string directory;
        static QuestLogData data;
        public static event Action Changed;

        public static string FilePath => Path.Combine(directory ?? Application.persistentDataPath, FileName);
        static QuestLogData Data
        {
            get
            {
                if (data != null) return data;
                var loading = System.Diagnostics.Stopwatch.StartNew();
                string source = FilePath;
                LogHelper.Log("Load", $"Loading quest outcomes from '{source}'.");
                data = Read(source);
                if (data == null)
                {
                    source = FilePath + ".bak";
                    data = Read(source);
                    if (data != null) LogHelper.Log("Load", $"Recovered quest outcomes from backup '{source}'.");
                }
                if (data == null)
                {
                    source = "defaults";
                    data = new QuestLogData();
                }
                LogHelper.Log("Load", $"Quest outcomes loaded from '{source}' for '{FilePath}' in {loading.Elapsed.TotalMilliseconds:F1} ms; quests={data.quests.Count}.");
                return data;
            }
        }

        /// <summary>Points the log at another folder until disposed, for tests.</summary>
        public static IDisposable UseDirectory(string path)
        {
            string previous = directory;
            directory = path;
            data = null;
            return new Restore(() => { directory = previous; data = null; });
        }

        /// <summary>A named value of <paramref name="quest"/>'s record, such as how often it was done; zero when never recorded.</summary>
        public static int Value(QuestDef quest, string id) => quest != null && Data.Find(quest.DefName) is { } record ? record.Get(id) : 0;

        /// <summary>Every named value of <paramref name="quest"/>'s record, in the order first recorded.</summary>
        public static IReadOnlyList<QuestValue> Values(QuestDef quest) =>
            quest != null && Data.Find(quest.DefName) is { } record ? record.values : Array.Empty<QuestValue>();

        /// <summary>A passing ending has been recorded: what unlocks the quests that follow.</summary>
        public static bool Passed(QuestDef quest) => Value(quest, PassedValue) > 0;

        /// <summary>Not passed, and a requirement hasn't passed either: the quest can't be played yet.</summary>
        public static bool Locked(QuestDef quest)
        {
            if (Passed(quest)) return false;
            foreach (QuestDef required in quest.Requires)
                if (!Passed(required)) return true;
            return false;
        }

        /// <summary>Records how a run ended: one more of its outcome, a pass if it passes, and the same for the quests it also records.</summary>
        public static void Record(QuestDef quest, QuestEnding ending)
        {
            if (quest == null) throw new ArgumentNullException(nameof(quest));
            if (ending == null) throw new ArgumentNullException(nameof(ending));
            Apply(quest.DefName, ending.Outcome, ending.Passes);
            foreach (QuestDef other in ending.AlsoRecord) Apply(other.DefName, ending.Outcome, ending.Passes);
            Save();
        }

        /// <summary>Forgets <paramref name="quest"/> and every quest that follows from it, so its chain starts again.</summary>
        public static void Reset(QuestDef quest, IEnumerable<QuestDef> all)
        {
            var reset = new HashSet<string> { quest.DefName };
            var quests = new List<QuestDef>(all);
            for (bool grew = true; grew;)
            {
                grew = false;
                foreach (QuestDef candidate in quests)
                    if (candidate != null && !reset.Contains(candidate.DefName)
                        && new List<QuestDef>(candidate.Requires).Exists(required => reset.Contains(required.DefName)))
                        grew |= reset.Add(candidate.DefName);
            }
            if (Data.quests.RemoveAll(entry => reset.Contains(entry.id)) > 0) Save();
        }

        static void Apply(string id, string outcome, bool passes)
        {
            QuestRecord record = Data.Find(id);
            if (record == null) Data.quests.Add(record = new QuestRecord { id = id });
            record.Add(outcome, 1);
            if (passes) record.Add(PassedValue, 1);
        }

        static void Save()
        {
            string path = FilePath;
            var saving = System.Diagnostics.Stopwatch.StartNew();
            LogHelper.Log("Save", $"Saving quest outcomes to '{path}'.");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path + ".tmp", JsonUtility.ToJson(Data, true));
                // Keep a valid backup when recovering from a damaged primary file.
                if (File.Exists(path) && Read(path) != null) File.Replace(path + ".tmp", path, path + ".bak");
                else if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
                else File.Move(path + ".tmp", path);
                LogHelper.Log("Save", $"Quest outcomes saved to '{path}' in {saving.Elapsed.TotalMilliseconds:F1} ms; quests={Data.quests.Count}.");
            }
            catch (Exception error)
            {
                LogHelper.LogWarning("Save", $"Failed to save quest outcomes to '{path}' after {saving.Elapsed.TotalMilliseconds:F1} ms: {error.Message}");
                throw;
            }
            Changed?.Invoke();
        }

        static QuestLogData Read(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                string json = File.ReadAllText(path);
                // JsonUtility supplies empty collections for missing fields, hiding incomplete saves.
                LegacyLog schema = JsonConvert.DeserializeObject<LegacyLog>(json);
                if (schema == null || schema.quests == null) return null;
                QuestLogData value = schema.version == 1 ? Upgrade(schema)
                    : schema.version == QuestLogData.CurrentVersion ? JsonUtility.FromJson<QuestLogData>(json) : null;
                return value != null && value.version == QuestLogData.CurrentVersion && value.quests != null
                    && value.quests.TrueForAll(entry => entry != null && !string.IsNullOrEmpty(entry.id) && entry.values != null
                        && entry.values.TrueForAll(named => named != null && !string.IsNullOrEmpty(named.id))) ? value : null;
            }
            catch (ArgumentException) { return null; }
            catch (JsonException) { return null; }
            catch (IOException) { return null; }
        }

        /// <summary>Version 1 kept fixed flags and counts; they become named outcome values.</summary>
        static QuestLogData Upgrade(LegacyLog legacy)
        {
            var upgraded = new QuestLogData();
            foreach (LegacyRecord entry in legacy.quests)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) return null;
                var record = new QuestRecord { id = entry.id };
                if (entry.done) record.Add(LegacyDone, Math.Max(1, entry.completions));
                if (entry.skipped) record.Add(LegacySkipped, 1);
                if (entry.failures > 0) record.Add(LegacyFailed, entry.failures);
                if (entry.done || entry.skipped) record.Add(PassedValue, 1);
                upgraded.quests.Add(record);
            }
            return upgraded;
        }

        [Serializable]
        sealed class LegacyLog
        {
            public int version = -1;
            public List<LegacyRecord> quests = null;
        }

        [Serializable]
        sealed class LegacyRecord
        {
            public string id = string.Empty;
            public bool done = false, skipped = false;
            public int completions = 0, failures = 0;
        }

        sealed class Restore : IDisposable
        {
            Action undo;
            public Restore(Action action) => undo = action;
            public void Dispose() { undo?.Invoke(); undo = null; }
        }
    }
}
