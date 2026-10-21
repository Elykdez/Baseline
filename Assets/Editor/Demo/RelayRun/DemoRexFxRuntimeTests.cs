using System;
using System.Collections;
using System.IO;
using System.Linq;
using Baseline.Demo.RelayRun;
using Baseline.Quests;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Baseline.Tests.Editor
{
    public sealed class DemoRexFxRuntimeTests
    {
        [UnityTest]
        public IEnumerator Gameplay_EmitsContactsAndBursts_FreezesOnPause_AndClearsOnRetry()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Index.unity");
            yield return new EnterPlayMode();
            yield return WaitFor(() => Object.FindFirstObjectByType<GameBootstrap>()?.IsReady == true);
            yield return WaitFor(() => DemoPresentationBootstrap.IsReady);
            string directory = Path.Combine(Path.GetTempPath(), "Baseline-RexFxLive-" + Guid.NewGuid().ToString("N"));
            IDisposable quests = QuestLog.UseDirectory(directory);
            IDisposable settings = ConfigSettingsPersistence.UseDirectory(Path.Combine(directory, "settings"));
            try
            {
                ConfigSettingsPersistence.SetBoardEffects(true);
                ContextManager.Ins.JumpToScene(ContextManager.Ins.gameRegister.games.Single(g => g.sceneName == "DemoRelayRun"));
                yield return WaitFor(() => Object.FindFirstObjectByType<DemoRunnerGame>()?.Dialogue != null);
                var game = Object.FindFirstObjectByType<DemoRunnerGame>();
                yield return null;
                game.BeginRun();
                while (game.Dialogue.Runner.IsOpen) game.Dialogue.Runner.Advance(game.Dialogue.Runner.Revision);
                yield return WaitFor(() => game.footFx.puffs.Any(p => p.gameObject.activeSelf));
                Assert.That(game.Model.Jumps, Is.Zero, "The run clip must emit its own foot contacts.");
                yield return new WaitForSeconds(.06f);
                Capture("run");

                game.TogglePause();
                var puff = game.footFx.puffs.First(p => p.gameObject.activeSelf);
                Vector2 position = puff.rectTransform.anchoredPosition;
                Color color = puff.color;
                yield return new WaitForSecondsRealtime(.15f);
                Assert.That(puff.rectTransform.anchoredPosition, Is.EqualTo(position));
                Assert.That(puff.color, Is.EqualTo(color));
                ConfigSettingsPersistence.SetBoardEffects(false);
                yield return null;
                yield return null;
                Assert.That(game.footFx.puffs.Any(p => p.gameObject.activeSelf), Is.False);
                ConfigSettingsPersistence.SetBoardEffects(true);
                game.TogglePause();
                game.Jump();
                yield return WaitFor(() => game.Model.Jumps == 1);
                Assert.That(game.footFx.puffs.Count(p => p.gameObject.activeSelf), Is.GreaterThanOrEqualTo(3));
                Capture("takeoff");
                yield return WaitFor(() => game.Model.Height > 130);
                Capture("airborne");
                yield return WaitFor(() => game.Model.Landings == 1);
                Assert.That(game.footFx.puffs.Count(p => p.gameObject.activeSelf), Is.GreaterThanOrEqualTo(6));
                Capture("land");
                yield return WaitFor(() => game.Quest.IsFinished);
                Assert.That(game.footFx.puffs.Any(p => p.gameObject.activeSelf), Is.False);
                while (game.Dialogue.Runner.IsOpen) game.Dialogue.Runner.Advance(game.Dialogue.Runner.Revision);
                yield return null;
                game.BeginRun();
                Assert.That(game.footFx.puffs.Any(p => p.gameObject.activeSelf), Is.False);
                Assert.That(game.Model, Is.Null);
                yield return new ExitPlayMode();
            }
            finally
            {
                quests.Dispose();
                settings.Dispose();
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        static IEnumerator WaitFor(Func<bool> condition)
        {
            double deadline = EditorApplication.timeSinceStartup + 60;
            while (!condition() && EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, "Runtime condition timed out.");
        }

        static void Capture(string name)
        {
            Camera camera = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).First(c => c.enabled);
            RenderTexture previous = camera.targetTexture, active = RenderTexture.active;
            var target = new RenderTexture(1600, 900, 24);
            var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Logs/RexFx");
                File.WriteAllBytes("Logs/RexFx/" + name + ".png", pixels.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                Object.Destroy(target);
                Object.Destroy(pixels);
            }
        }
    }
}
