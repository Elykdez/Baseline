using System;
using System.Collections;
using System.IO;
using System.Linq;
using Baseline.Defs;
using Baseline.Demo.RelayRun;
using Baseline.Dialogue;
using Baseline.Presentation;
using Baseline.Quests;
using Hypocycloid.Core;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Components;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class DemoRunnerIntegrationTests
    {
        const string ContentRoot = "Assets/Bundles/Demo/RelayRun";
        string directory;
        IDisposable saveScope;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "Baseline-DemoRunner-" + Guid.NewGuid().ToString("N"));
            saveScope = QuestLog.UseDirectory(directory);
        }

        [TearDown]
        public void TearDown()
        {
            saveScope?.Dispose();
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }

        static T[] Content<T>() where T : Object => AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { ContentRoot })
            .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<T>).ToArray();

        static DemoRunnerDef Definition() => Content<DemoRunnerDef>().Single();

        [Test]
        public void AuthoredDatabase_RegistersRunnerQuestAndAllConversations()
        {
            DefDatabase database = Content<DefDatabase>().Single();
            Assert.DoesNotThrow(database.Validate);
            DemoRunnerDef definition = Definition();
            Assert.That(database.Find<DemoRunnerDef>(definition.DefName), Is.SameAs(definition));
            Assert.That(database.Find<QuestDef>(definition.quest.DefName), Is.SameAs(definition.quest));
            Assert.That(definition.quest.Ending(QuestRun.StepFailedSignal).Passes, Is.False);
            DialogDef[] conversations = Content<DialogDef>();
            Assert.That(conversations, Has.Length.EqualTo(3), "Briefing, failure and success need authored conversations.");
            foreach (DialogDef dialog in conversations)
            {
                Assert.That(database.Find<DialogDef>(dialog.DefName), Is.SameAs(dialog));
                Assert.That(dialog.lines, Is.Not.Empty);
                foreach (DialogLine line in dialog.lines)
                {
                    Assert.That(line.textKey, Is.Not.Empty);
                    Assert.That(database.Find<CharacterDef>(line.speaker.DefName), Is.SameAs(line.speaker));
                }
            }
        }

        [Test]
        public void BriefingDemonstratesTwoSpeakersWithLocalizedCaptions()
        {
            DialogDef briefing = Content<DialogDef>().Single(dialog => dialog.DefName == "demo.relay.briefing");
            Assert.That(briefing.dualCharacters, Is.True);
            CharacterDef[] speakers = briefing.lines.Select(line => line.speaker).Distinct().ToArray();
            Assert.That(speakers, Has.Length.EqualTo(2));
            string[] keys = briefing.lines.Select(line => line.textKey)
                .Concat(speakers.SelectMany(speaker => new[] { speaker.nameKey, speaker.roleKey })).ToArray();
            foreach (string code in new[] { "en", "zh" })
            {
                var table = AssetDatabase.LoadAssetAtPath<UnityEngine.Localization.Tables.StringTable>(
                    $"Assets/Locales/Tables/StringTable_{code}.asset");
                foreach (string key in keys)
                    Assert.That(table.GetEntry(key)?.LocalizedValue, Is.Not.Null.And.Not.Empty, code + "/" + key);
            }
        }

        [Test]
        public void Conversations_DoNotCompleteTheGameplayQuest()
        {
            QuestDef quest = Definition().quest;
            using var run = new QuestRun(quest);
            run.Begin();
            var player = new DialogRunner();
            foreach (DialogDef dialog in Content<DialogDef>())
            {
                player.Play(dialog, allowClose: false);
                while (player.IsOpen) player.Advance(player.Revision);
            }
            Assert.That(run.IsFinished, Is.False);
            Assert.That(QuestLog.Passed(quest), Is.False);
            Assert.That(File.Exists(QuestLog.FilePath), Is.False);
        }

        [Test]
        public void BriefingObjective_GatesDistanceProgressAndActivatesDelivery()
        {
            using var run = new QuestRun(Definition().quest);
            run.Begin();
            Assert.That(run.Steps["briefing"].Active, Is.True);
            Assert.That(run.Steps["delivery"].Shown, Is.False);
            SignalBus.Fire(new QuestSignal(DemoRunnerSignals.Metre, run, 600));
            Assert.That(run.Steps["delivery"].Progress, Is.Zero);
            run.Raise(DemoRunnerSignals.Briefed);
            Assert.That(run.Steps["briefing"].Done, Is.True);
            Assert.That(run.Steps["delivery"].Active, Is.True);
            Assert.That(run.Steps["clearance"].Active, Is.True);
            Assert.That(run.IsFinished, Is.False);
        }

        [Test]
        public void Collision_FailsOnlyItsAttempt_AndRecordsExactlyOnce()
        {
            DemoRunnerDef definition = Definition();
            using var active = new QuestRun(definition.quest);
            using var other = new QuestRun(definition.quest);
            active.Begin();
            other.Begin();
            active.Raise(DemoRunnerSignals.Briefed);
            var model = new DemoRunnerModel(definition.tuning, 17);
            model.Advance(5);
            Assert.That(model.Outcome, Is.EqualTo(DemoRunnerOutcome.Crashed));
            active.Raise(DemoRunnerSignals.Crashed);
            active.Raise(DemoRunnerSignals.Crashed);
            Assert.That(active.IsFinished, Is.True);
            Assert.That(active.Ending.Passes, Is.False);
            Assert.That(other.IsFinished, Is.False);
            Assert.That(QuestLog.Value(definition.quest, active.Ending.Outcome), Is.EqualTo(1));
            Assert.That(QuestLog.Passed(definition.quest), Is.False);
            Assert.That(File.Exists(QuestLog.FilePath), Is.True);
        }

        [Test]
        public void SuccessfulGameplay_CompletesAndPersistsTheAuthoredQuest()
        {
            DemoRunnerDef definition = Definition();
            using var run = new QuestRun(definition.quest);
            run.Begin();
            var model = new DemoRunnerModel(definition.tuning, 17);
            run.Raise(DemoRunnerSignals.Briefed);
            int reported = 0;
            for (int frame = 0; frame < 10000 && model.Outcome == DemoRunnerOutcome.Running; frame++)
            {
                if (model.Grounded && model.Obstacles.Any(obstacle => obstacle.Active
                    && obstacle.X > DemoRunnerModel.RunnerX && obstacle.X - DemoRunnerModel.RunnerX < 210))
                    model.Jump();
                model.Advance(1f / 120);
                int metres = Mathf.FloorToInt(model.Distance);
                if (metres > reported)
                {
                    SignalBus.Fire(new QuestSignal(DemoRunnerSignals.Metre, run, metres - reported));
                    reported = metres;
                }
            }
            Assert.That(model.Outcome, Is.EqualTo(DemoRunnerOutcome.Complete));
            Assert.That(run.IsFinished, Is.True);
            Assert.That(run.Ending.Passes, Is.True);
            Assert.That(QuestLog.Passed(definition.quest), Is.True);
            Assert.That(QuestLog.Value(definition.quest, run.Ending.Outcome), Is.EqualTo(1));
        }

        [Test]
        public void PresentationAssets_HavePlayableClipsAndPooledVisuals()
        {
            var cues = Content<SoundCueDef>();
            Assert.That(cues.Length, Is.GreaterThanOrEqualTo(4));
            foreach (SoundCueDef cue in cues)
            {
                Assert.DoesNotThrow(cue.Validate);
                Assert.That(cue.ClipAt(0).length, Is.GreaterThan(.05f));
            }
            foreach (BgmDef music in Content<BgmDef>()) Assert.DoesNotThrow(music.Validate);
            var effects = Content<FxEventDef>();
            Assert.That(effects.Length, Is.GreaterThanOrEqualTo(3));
            foreach (FxEventDef effect in effects)
            {
                Assert.DoesNotThrow(effect.Validate);
                if (effect.name == "DemoJumpFx")
                {
                    Assert.That(effect.Sound, Is.Not.Null);
                    Assert.That(effect.Vfx, Is.Null, "Takeoff dust is rendered inside the gameplay canvas.");
                    continue;
                }
                Assert.That(effect.Vfx, Is.Not.Null);
                Assert.That(effect.Vfx.GetComponentInChildren<ParticleSystem>(), Is.Not.Null);
            }
        }

        [Test]
        public void MenuOwnsPresentationLoad_AndManagersActivateAsIndependentRoots()
        {
            var menu = AssetDatabase.LoadAssetAtPath<GameObject>(ContentRoot + "/Prefabs/DemoRelayMenu.prefab");
            Assert.That(menu, Is.Not.Null);
            var bootstrap = menu.GetComponentInChildren<DemoPresentationBootstrap>(true);
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.surface, Is.Not.Null);
            Assert.That(bootstrap.presentationPrefab.RuntimeKeyIsValid(), Is.True);
            string path = AssetDatabase.GUIDToAssetPath(bootstrap.presentationPrefab.AssetGUID);
            var presentation = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<DemoPresentationLifetime>();
            Assert.That(presentation, Is.Not.Null);
            Assert.That(presentation.sound, Is.Not.Null);
            Assert.That(presentation.effects, Is.Not.Null);
            Assert.That(presentation.sound.gameObject.activeSelf, Is.False, "SoundManager must be detached before its Awake.");
            Assert.That(presentation.effects.gameObject.activeSelf, Is.False, "FxManager must be detached before its Awake.");
            var sound = new SerializedObject(presentation.sound);
            Assert.That(sound.FindProperty("mix").objectReferenceValue, Is.Not.Null);
            Assert.That(sound.FindProperty("bgm").objectReferenceValue, Is.Not.Null);
            foreach (var text in menu.GetComponentsInChildren<TMP_Text>(true))
            {
                var localized = text.GetComponent<LocalizeStringEvent>();
                if (localized == null || !localized.enabled || localized.StringReference.TableEntryReference.Key?.StartsWith("DemoRelay_") != true) continue;
                Assert.That(localized.OnUpdateString.GetPersistentEventCount(), Is.EqualTo(1), text.name);
                Assert.That(localized.OnUpdateString.GetPersistentTarget(0), Is.SameAs(text), text.name + " must update its own TMP component.");
            }
        }

        [Test]
        public void PosterAndObstacleSprites_UseTheAuthoredSlicesInsteadOfAutomaticSpecks()
        {
            var poster = AssetDatabase.LoadAllAssetsAtPath(ContentRoot + "/Art/DemoRexRun.png").OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
            Assert.That(poster.Select(sprite => sprite.name), Is.EqualTo(new[] { "DemoRexRun_00", "DemoRexRun_01" }));
            foreach (Sprite sprite in poster)
            {
                Assert.That(sprite.rect.width, Is.GreaterThan(500));
                Assert.That(sprite.rect.height, Is.GreaterThan(500));
            }
            var obstacles = AssetDatabase.LoadAllAssetsAtPath(ContentRoot + "/Art/DemoRelayObstacles.png").OfType<Sprite>().ToArray();
            Assert.That(obstacles, Has.Length.EqualTo(2));
            foreach (Sprite obstacle in obstacles)
            {
                Assert.That(obstacle.rect.width, Is.GreaterThan(100));
                Assert.That(obstacle.rect.height, Is.GreaterThan(100));
            }
            GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(ContentRoot + "/Prefabs/DemoRelayMenu.prefab");
            var hero = menu.GetComponentsInChildren<UnityEngine.UI.Image>(true).Single(image => image.name == "Robot rex");
            Sprite lookout = AssetDatabase.LoadAssetAtPath<Sprite>(ContentRoot + "/Art/DemoRexLookout.png");
            Assert.That(lookout, Is.Not.Null);
            Assert.That(hero.sprite, Is.SameAs(lookout));
        }

        [Test]
        public void RegisteredRunnerScene_HasAuthoredControlsAndNoMissingScripts()
        {
            var register = AssetDatabase.LoadAssetAtPath<GameRegister>("Assets/Bundles/Core/Settings/GameRegister.asset");
            var game = register.games.Single(entry => entry.sceneName == "DemoRelayRun");
            string path = AssetDatabase.GUIDToAssetPath(game.sceneAsset.AssetGUID);
            Assert.That(path, Is.EqualTo("Assets/Bundles/Demo/DemoRelayRun.unity"));
            var entry = AddressableAssetSettingsDefaultObject.Settings.FindAssetEntry(game.sceneAsset.AssetGUID);
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry.parentGroup.Name, Is.EqualTo("Demo"));
            Assert.That(EditorBuildSettings.scenes.Any(scene => scene.enabled && scene.path == path), Is.False,
                "The demo scene must load from Addressables, not player scene data.");
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                var nodes = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
                var sceneMusic = nodes.Select(node => node.GetComponent<SceneMusic>()).Single(component => component != null);
                Assert.That(new SerializedObject(sceneMusic).FindProperty("music").enumValueIndex,
                    Is.EqualTo((int)MusicContext.Silence), "Gameplay must silence BGM without muting effects.");
                foreach (Transform node in nodes)
                    Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject), Is.Zero, node.name);
                var input = nodes.Select(node => node.GetComponent<InputSystemUIInputModule>()).Single(component => component != null);
                Assert.That(input.actionsAsset, Is.Not.Null);
                Assert.That(EditorUtility.IsPersistent(input.actionsAsset), Is.True, "Game UI actions must survive scene serialization.");
                Assert.That(input.point?.action, Is.Not.Null);
                Assert.That(input.leftClick?.action, Is.Not.Null);
                Assert.That(input.submit?.action, Is.Not.Null);
                DemoRunnerGame runner = nodes.Select(node => node.GetComponent<DemoRunnerGame>()).Single(component => component != null);
                Assert.That(runner.database, Is.Not.Null);
                Assert.That(runner.definition, Is.Not.Null);
                Assert.That(runner.startButton, Is.Not.Null);
                Assert.That(runner.jumpButton, Is.Not.Null);
                Assert.That(runner.retryButton, Is.Not.Null);
                Assert.That(runner.homeButton, Is.Not.Null);
                Assert.That(runner.briefingDialog, Is.Not.Null);
                Assert.That(runner.failureDialog, Is.Not.Null);
                Assert.That(runner.successDialog, Is.Not.Null);
                Assert.That(runner.rexAnimation, Is.Not.Null);
                Assert.That(runner.footFx, Is.Not.Null);
                Assert.That(runner.rexAnimation.footFx, Is.SameAs(runner.footFx));
                Assert.That(runner.rexAnimation.nearFoot, Is.Not.Null);
                Assert.That(runner.rexAnimation.farFoot, Is.Not.Null);
                Assert.That(runner.rexAnimation.animator.runtimeAnimatorController, Is.Not.Null);
                Assert.That(runner.runnerImage.enabled, Is.False, "The articulated rig supplies the visible character.");
                Assert.That(runner.obstacleViews.Length, Is.GreaterThanOrEqualTo(DemoRunnerModel.ObstacleCapacity));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        [Test]
        public void ObstacleOutlines_StayInsideTheirSceneSprites()
        {
            const string path = "Assets/Bundles/Demo/DemoRelayRun.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                DemoRunnerGame runner = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<DemoRunnerGame>(true)).Single();
                Assert.That(runner.obstacleSprites, Has.Length.EqualTo(DemoRunnerModel.AppearanceCount));
                float forgiveness = DemoRunnerModel.HitForgiveness / runner.definition.tuning.obstacleHeight;
                for (int appearance = 0; appearance < DemoRunnerModel.AppearanceCount; appearance++)
                {
                    Sprite sprite = runner.obstacleSprites[appearance];
                    // Imported textures are not readable, so decode the source image to inspect its alpha.
                    var source = new Texture2D(2, 2);
                    try
                    {
                        Assert.That(source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture))), Is.True);
                        float scaleX = (float)source.width / sprite.texture.width, scaleY = (float)source.height / sprite.texture.height;
                        Rect rect = sprite.rect;
                        foreach (DemoRunnerModel.OutlineStep step in DemoRunnerModel.Outline(appearance))
                            for (int column = Mathf.RoundToInt(step.Left * 100); column <= Mathf.RoundToInt(step.Right * 100); column++)
                            {
                                int x = Mathf.RoundToInt((rect.x + column / 100f * (rect.width - 1)) * scaleX);
                                int bottom = Mathf.RoundToInt(rect.y * scaleY), height = Mathf.RoundToInt(rect.height * scaleY);
                                int top = height;
                                while (top > 0 && source.GetPixel(x, bottom + top - 1).a <= .5f) top--;
                                Assert.That((float)top / height, Is.GreaterThanOrEqualTo(step.Top - forgiveness),
                                    $"{sprite.name} is open sky at {column}% where appearance {appearance}'s outline claims a hit.");
                            }
                    }
                    finally { Object.DestroyImmediate(source); }
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }

    public sealed class DemoRunnerRuntimeTests
    {
        [UnityTest]
        public IEnumerator MenuToBriefingToFailureToRetryAndHome_UsesTheAuthoredFlow()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Index.unity");
            yield return new EnterPlayMode();
            yield return WaitUntil(() => Object.FindFirstObjectByType<GameBootstrap>()?.IsReady == true, "Index startup");
            yield return WaitUntil(() => DemoPresentationBootstrap.IsReady, "presentation startup");
            yield return null;
            string directory = Path.Combine(Path.GetTempPath(), "Baseline-DemoRunnerLive-" + Guid.NewGuid().ToString("N"));
            IDisposable quests = QuestLog.UseDirectory(directory);
            IDisposable settings = ConfigSettingsPersistence.UseDirectory(Path.Combine(directory, "settings"));
            try
            {
                AudioSource[] musicSources = SoundManager.Ins.GetComponentsInChildren<AudioSource>()
                    .Where(source => source.name.StartsWith("Music ")).ToArray();
                Assert.That(musicSources, Has.Length.EqualTo(2));
                yield return WaitUntil(() => musicSources.Any(source => source.isPlaying), "menu music", 3);
                GameMeta game = ContextManager.Ins.gameRegister.games.Single(entry => entry.sceneName == "DemoRelayRun");
                ContextManager.Ins.JumpToScene(game);
                yield return WaitUntil(() => Object.FindFirstObjectByType<DemoRunnerGame>()?.Dialogue != null, "runner scene");
                var runner = Object.FindFirstObjectByType<DemoRunnerGame>();
                yield return null;
                yield return WaitUntil(() => musicSources.All(source => !source.isPlaying), "gameplay BGM silence", 3);
                runner.startButton.onClick.Invoke();
                Assert.That(runner.Dialogue.IsOpen, Is.True);
                Assert.That(runner.Dialogue.Runner.Definition, Is.SameAs(runner.briefingDialog));
                Assert.That(runner.Model, Is.Null, "Gameplay must wait for the briefing.");
                Assert.That(runner.Quest, Is.Not.Null, "The quest owns the briefing and the gameplay attempt.");
                Assert.That(runner.Quest.IsFinished, Is.False);
                yield return null;
                yield return null;
                runner.Dialogue.Advance();
                Assert.That(runner.Dialogue.Runner.LineIndex, Is.Zero, "Revealing the typewriter must not skip the briefing line.");
                FinishDialog(runner.Dialogue);
                yield return null;
                Assert.That(runner.Model, Is.Not.Null);
                Assert.That(runner.Quest.IsFinished, Is.False);
                yield return WaitUntil(() => CueIsPlaying(runner.rexAnimation.footstepCue), "running footstep audio", 2);
                runner.Jump();
                yield return WaitUntil(() => CueIsPlaying(runner.jumpCue), "jump audio", 2);
                Assert.That(musicSources.All(source => !source.isPlaying), Is.True);
                runner.pauseButton.onClick.Invoke();
                Assert.That(runner.IsPaused, Is.True);
                float pausedDistance = runner.Model.Distance;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(runner.Model.Distance, Is.EqualTo(pausedDistance));
                runner.resumeButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.5f);
                yield return WaitUntil(() => runner.Quest.IsFinished, "collision ending", 12);
                Assert.That(CueIsPlaying(runner.crashCue), Is.True, "Hit effects must still play with silent BGM.");
                Assert.That(runner.Quest.Ending.Passes, Is.False);
                Assert.That(runner.Dialogue.Runner.Definition, Is.SameAs(runner.failureDialog));
                Assert.That(runner.resultPanel.activeSelf, Is.False, "The ending conversation must finish before results appear.");
                Assert.That(QuestLog.Value(runner.definition.quest, runner.Quest.Ending.Outcome), Is.EqualTo(1));
                yield return new WaitForSecondsRealtime(1);
                FinishDialog(runner.Dialogue);
                yield return null;
                Assert.That(runner.resultPanel.activeSelf, Is.True);
                QuestRun previous = runner.Quest;
                runner.retryButton.onClick.Invoke();
                Assert.That(runner.Quest, Is.Not.SameAs(previous));
                Assert.That(QuestRun.Running.Contains(previous), Is.False, "Retry must dispose the previous attempt.");
                Assert.That(runner.Dialogue.Runner.Definition, Is.SameAs(runner.briefingDialog), "Every retry begins a fresh quest briefing.");
                FinishDialog(runner.Dialogue);
                Assert.That(runner.Model.Distance, Is.Zero);
                Assert.That(runner.Quest.IsFinished, Is.False);
                runner.ReturnToMenu();
                if (UICharacterDialog.BlocksInput)
                {
                    yield return WaitUntil(() => Object.FindFirstObjectByType<UICharacterDialog>()?.IsOpen == true, "return confirmation");
                    Object.FindFirstObjectByType<UICharacterDialog>().Respond(true);
                }
                yield return WaitUntil(() => Object.FindFirstObjectByType<GameBootstrap>()?.IsReady == true, "return to Index");
                yield return WaitUntil(() => Object.FindFirstObjectByType<DemoPresentationBootstrap>()?.surface.activeInHierarchy == true, "returned menu surface");
                yield return WaitUntil(() => musicSources.Any(source => source.isPlaying), "returned menu music", 3);
                yield return LocalizationSettings.InitializationOperation;
                yield return null;
                yield return null;
                Assert.That(Object.FindFirstObjectByType<DemoRunnerGame>(), Is.Null);
                Assert.That(QuestRun.Running.Any(run => run.Quest.DefName == previous.Quest.DefName), Is.False);
                var originalLocale = LocalizationSettings.SelectedLocale;
                var chinese = LocalizationSettings.AvailableLocales.Locales.First(locale => locale.Identifier.Code.StartsWith("zh"));
                try
                {
                    LocalizationSettings.SelectedLocale = chinese;
                    yield return LocalizationSettings.SelectedLocaleAsync;
                    var labels = Object.FindObjectsByType<LocalizeStringEvent>(FindObjectsSortMode.None)
                        .Where(label => label.isActiveAndEnabled && label.StringReference.TableEntryReference.Key?.StartsWith("DemoRelay_") == true).ToArray();
                    Assert.That(labels.Length, Is.GreaterThan(5), "Expected the live demo menu's localized labels.");
                    foreach (var localized in labels)
                    {
                        var expected = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(
                            GameRegister.DEF_STT, localized.StringReference.TableEntryReference, chinese);
                        UnityEngine.AddressableAssets.Addressables.ResourceManager.Acquire(expected);
                        string expectedText;
                        try
                        {
                            yield return expected;
                            expectedText = expected.Result;
                        }
                        finally { UnityEngine.AddressableAssets.Addressables.Release(expected); }
                        var text = localized.GetComponent<TMP_Text>();
                        yield return WaitUntil(() => text.text == expectedText, "Chinese caption: " + localized.StringReference.TableEntryReference.Key, 5);
                        Assert.That(text.text, Is.EqualTo(expectedText));
                    }
                    Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Any(text => text.text == "信号\n疾行"), Is.True);
                }
                finally { LocalizationSettings.SelectedLocale = originalLocale; }
                yield return new ExitPlayMode();
            }
            finally
            {
                quests.Dispose();
                settings.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        static bool CueIsPlaying(SoundCueDef cue) => cue != null &&
            SoundManager.Ins.GetComponentsInChildren<AudioSource>().Any(source =>
                source.clip == cue.ClipAt(0) && source.isPlaying);

        static void FinishDialog(DialoguePanel panel)
        {
            while (panel.Runner.IsOpen) panel.Runner.Advance(panel.Runner.Revision);
        }

        static IEnumerator WaitUntil(Func<bool> condition, string operation, float seconds = 60)
        {
            double deadline = EditorApplication.timeSinceStartup + seconds;
            while (!condition() && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, operation + " timed out.");
        }
    }
}
