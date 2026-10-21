using System;
using System.IO;
using Baseline.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Baseline.Tests.Editor
{
    public sealed class ConfigSettingsPersistenceTests
    {
        const string TestKey = "Baseline.ConfigSettingsTests.Value";
        string root;
        IDisposable configuration;
        string ConfigDirectory => Path.Combine(root, "config");

        [SetUp]
        public void SetUp() => root = Path.Combine(Path.GetTempPath(), "BaselineConfig-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            configuration?.Dispose();
            configuration = null;
            PlayerPrefs.DeleteKey(TestKey);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        [Test]
        public void ValuesRoundTripThroughTheFile_WithoutLeavingTemporaryFiles()
        {
            configuration = ConfigSettingsPersistence.UseDirectory(ConfigDirectory);
            ConfigSettingsPersistence.SetLocale("zh");
            ConfigSettingsPersistence.SetDifficulty(GameDifficulty.Hard);
            ConfigSettingsPersistence.SetInt(TestKey, 4);
            ConfigSettingsPersistence.SetFloat("example.scale", 0.75f);
            ConfigSettingsPersistence.SetString("example.selection", "first");
            ConfigSettingsPersistence.Reload();
            Assert.That(ConfigSettingsPersistence.Locale, Is.EqualTo("zh"));
            Assert.That(ConfigSettingsPersistence.Difficulty, Is.EqualTo(GameDifficulty.Hard));
            Assert.That(ConfigSettingsPersistence.GetInt(TestKey), Is.EqualTo(4));
            Assert.That(ConfigSettingsPersistence.GetFloat("example.scale"), Is.EqualTo(0.75f));
            Assert.That(ConfigSettingsPersistence.GetString("example.selection"), Is.EqualTo("first"));
            Assert.That(Directory.GetFiles(ConfigDirectory), Is.EqualTo(new[] { ConfigSettingsPersistence.FilePath }));
        }

        [Test]
        public void IsolatedStoreLeavesPlayerPrefsAlone()
        {
            PlayerPrefs.SetInt(TestKey, 7);
            configuration = ConfigSettingsPersistence.UseDirectory(ConfigDirectory);
            Assert.That(ConfigSettingsPersistence.HasKey(TestKey), Is.False);
            Assert.That(ConfigSettingsPersistence.GetInt(TestKey, 2), Is.EqualTo(2));
            ConfigSettingsPersistence.SetInt(TestKey, 3);
            Assert.That(PlayerPrefs.GetInt(TestKey), Is.EqualTo(7));
        }

        [Test]
        public void LegacyConfigFileMovesToTheNewDirectoryWithoutLosingValues()
        {
            string legacy = Path.Combine(root, "legacy");
            configuration = ConfigSettingsPersistence.UseDirectory(legacy);
            ConfigSettingsPersistence.SetMasterVolume(0.25f);
            ConfigSettingsPersistence.SetString(TestKey, "kept");
            configuration.Dispose();
            configuration = ConfigSettingsPersistence.UseDirectory(ConfigDirectory, legacyDirectory: legacy);
            Assert.That(ConfigSettingsPersistence.MasterVolume, Is.EqualTo(0.25f));
            Assert.That(ConfigSettingsPersistence.GetString(TestKey), Is.EqualTo("kept"));
            Assert.That(File.Exists(ConfigSettingsPersistence.FilePath), Is.True);
            Assert.That(File.Exists(Path.Combine(legacy, ConfigSettingsPersistence.FileName)), Is.False);
        }

        [Test]
        public void FoundationFileRetainsDifficultyAndUsesLatestAudioDefaults()
        {
            configuration = ConfigSettingsPersistence.UseDirectory(ConfigDirectory);
            var file = new ConfigSettingsPersistence.SerializedConfigFile();
            foreach (var entry in new[] { ("settings.difficulty", "Hard"), ("settings.audio.music", "0.4"), ("settings.audio.sfx", "0.7") })
                file.entries.Add(new ConfigSettingsPersistence.SerializedConfigEntry { key = entry.Item1, value = entry.Item2 });
            Directory.CreateDirectory(ConfigDirectory);
            File.WriteAllText(ConfigSettingsPersistence.FilePath, JsonUtility.ToJson(file));
            ConfigSettingsPersistence.Reload();
            Assert.That(ConfigSettingsPersistence.Difficulty, Is.EqualTo(GameDifficulty.Hard));
            Assert.That(ConfigSettingsPersistence.MusicVolume, Is.EqualTo(0.4f));
            Assert.That(ConfigSettingsPersistence.SfxVolume, Is.EqualTo(0.7f));
            Assert.That(ConfigSettingsPersistence.UiVolume, Is.EqualTo(0.7f));
            Assert.That(ConfigSettingsPersistence.MasterVolume, Is.EqualTo(0.5f));
        }

        [Test]
        public void RawFileApiDeduplicatesEntriesAndDeleteResetsState()
        {
            configuration = ConfigSettingsPersistence.UseDirectory(ConfigDirectory);
            var file = new ConfigSettingsPersistence.SerializedConfigFile();
            file.entries.Add(null);
            file.entries.Add(new ConfigSettingsPersistence.SerializedConfigEntry { key = " " });
            file.entries.Add(new ConfigSettingsPersistence.SerializedConfigEntry { key = "data." + TestKey, value = "2" });
            file.entries.Add(new ConfigSettingsPersistence.SerializedConfigEntry { key = "data." + TestKey, value = "5" });
            ConfigSettingsPersistence.WriteSerializedFile(file);
            Assert.That(ConfigSettingsPersistence.GetInt(TestKey), Is.EqualTo(5));
            Assert.That(ConfigSettingsPersistence.ReadSerializedFile().entries.Count, Is.EqualTo(1));
            Assert.That(ConfigSettingsPersistence.ReadSerializedJson(), Does.Contain("data." + TestKey));
            File.WriteAllText(ConfigSettingsPersistence.FilePath + ".meta", "transient metadata");
            ConfigSettingsPersistence.DeleteSerializedFile();
            Assert.That(ConfigSettingsPersistence.FileExists, Is.False);
            Assert.That(File.Exists(ConfigSettingsPersistence.FilePath + ".meta"), Is.False);
            Assert.That(ConfigSettingsPersistence.HasKey(TestKey), Is.False);
        }

        [Test]
        public void BuildFilterStashesConfigurationAndMetadataAndRestoresOriginals()
        {
            string folder = Path.Combine(root, "StreamingAssets", "config");
            string stash = Path.Combine(root, "Library", "Stash");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "user_config.json"), "saved");
            File.WriteAllText(folder + ".meta", "original metadata");
            Assert.That(UserConfigBuildFilter.Stash(folder, stash), Is.True);
            Assert.That(Directory.Exists(folder) || File.Exists(folder + ".meta"), Is.False);
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "user_config.json"), "written during build");
            Assert.That(UserConfigBuildFilter.Restore(folder, stash), Is.True);
            Assert.That(File.ReadAllText(Path.Combine(folder, "user_config.json")), Is.EqualTo("saved"));
            Assert.That(File.ReadAllText(folder + ".meta"), Is.EqualTo("original metadata"));
            Assert.That(Directory.Exists(stash), Is.False);
            Assert.That(UserConfigBuildFilter.Restore(folder, stash), Is.False);
            Assert.That(UserConfigBuildFilter.Stash(Path.Combine(root, "missing"), stash), Is.False);
        }
    }
}
