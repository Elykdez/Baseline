using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Baseline.Tests.Editor
{
    public sealed class UIParallaxTests
    {
        GameObject root;
        RectTransform frame;
        UIParallax parallax;
        UIParallax.Layer[] layers;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Parallax tests", typeof(RectTransform));
            frame = (RectTransform)root.transform;
            frame.sizeDelta = new Vector2(1280, 720);
            parallax = root.AddComponent<UIParallax>();
            layers = new[] { Layer("Distant", -.00144f, .0084f), Layer("Middle", .00864f), Layer("Near", .018f) };
            Set("layers", layers);
            Invoke("OnEnable");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        UIParallax.Layer Layer(string name, float depth, float zoom = 0)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rect.SetParent(frame, false);
            return new UIParallax.Layer { target = rect, depth = depth, zoom = zoom };
        }

        void Set(string name, object value) => typeof(UIParallax).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(parallax, value);
        object Invoke(string name, params object[] args) => typeof(UIParallax).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(parallax, args);

        [TestCase(1280, 720)]
        [TestCase(720, 1280)]
        [TestCase(2560, 720)]
        [TestCase(640, 640)]
        public void LayersCoverViewportAtEveryExtremeAndFocalPoint(float width, float height)
        {
            frame.sizeDelta = new Vector2(width, height);
            foreach (float depth in new[] { .018f, .15f })
                foreach (Vector2 focus in new[] { Vector2.zero, Vector2.one * .5f, Vector2.one })
                    foreach (Vector2 view in new[] { Vector2.zero, Vector2.left, Vector2.right, Vector2.up, Vector2.down, Vector2.one, -Vector2.one })
                    {
                        layers[2].depth = depth;
                        Set("focalPoint", focus);
                        parallax.ApplyView(view);
                        foreach (UIParallax.Layer layer in layers)
                        {
                            Vector2 half = layer.target.rect.size * layer.target.localScale.x * .5f;
                            Vector2 position = layer.target.anchoredPosition;
                            Assert.That(position.x - half.x, Is.LessThanOrEqualTo(-width * .5f + .01f));
                            Assert.That(position.x + half.x, Is.GreaterThanOrEqualTo(width * .5f - .01f));
                            Assert.That(position.y - half.y, Is.LessThanOrEqualTo(-height * .5f + .01f));
                            Assert.That(position.y + half.y, Is.GreaterThanOrEqualTo(height * .5f - .01f));
                            Assert.That(layer.target.rect.width / layer.target.rect.height, Is.EqualTo(16f / 9f).Within(.001f));
                        }
                    }
        }

        [Test]
        public void DepthsMoveInOppositeDirectionsAndClampExcessSteering()
        {
            parallax.ApplyView(new Vector2(10, 0));
            Assert.That(layers[0].target.anchoredPosition.x, Is.EqualTo(1.8432f).Within(.001f));
            Assert.That(layers[1].target.anchoredPosition.x, Is.EqualTo(-11.0592f).Within(.001f));
            Assert.That(layers[2].target.anchoredPosition.x, Is.EqualTo(-23.04f).Within(.001f));
            Assert.That(layers[0].target.localScale.x, Is.EqualTo(1.0084f).Within(.0001f));
        }

        [Test]
        public void MissingLayerDoesNotSuppressOtherLayersAndDisableRecenters()
        {
            Set("layers", new[] { null, new UIParallax.Layer(), layers[2] });
            parallax.ApplyView(Vector2.right);
            Assert.That(layers[2].target.anchoredPosition.x, Is.LessThan(0));
            for (int i = 0; i < 3; i++)
            {
                Invoke("OnDisable");
                Assert.That(layers[2].target.anchoredPosition, Is.EqualTo(Vector2.zero));
                Invoke("OnEnable");
                Assert.That(layers[2].target.anchoredPosition, Is.EqualTo(Vector2.zero));
                parallax.ApplyView(Vector2.left);
            }
        }

        [Test]
        public void PointerUsesPanelBoundsAndIgnoresPositionsOutsideIt()
        {
            frame.position = new Vector3(600, 400);
            frame.sizeDelta = new Vector2(400, 200);
            Assert.That((Vector2)Invoke("NormalizePointer", new Vector2(600, 400), null), Is.EqualTo(Vector2.zero));
            Vector2 nearCorner = (Vector2)Invoke("NormalizePointer", new Vector2(780, 490), null);
            Assert.That(nearCorner.x, Is.EqualTo(.9f).Within(.001f));
            Assert.That(nearCorner.y, Is.EqualTo(.9f).Within(.001f));
            Assert.That((Vector2)Invoke("NormalizePointer", new Vector2(820, 400), null), Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ReleasedTouchDoesNotKeepSteeringTowardItsLastPosition()
        {
            Touchscreen touch = InputSystem.AddDevice<Touchscreen>();
            try
            {
                frame.position = new Vector3(640, 360);
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Began, position = new Vector2(1000, 500) });
                InputSystem.Update();
                var read = typeof(UIParallax).GetMethod("ReadPointer", BindingFlags.NonPublic | BindingFlags.Static);
                Assert.That(read.Invoke(null, null), Is.EqualTo(new Vector2(1000, 500)));
                InputSystem.QueueStateEvent(touch, new TouchState { touchId = 1, phase = UnityEngine.InputSystem.TouchPhase.Ended, position = new Vector2(1000, 500) });
                InputSystem.Update();
                Assert.That(read.Invoke(null, null), Is.Null);
            }
            finally { InputSystem.RemoveDevice(touch); }
        }

        [Test]
        public void IndexUsesReusableBackdropWithStaticControlsAndNoDecorativeRaycasts()
        {
            const string path = "Assets/Bundles/Core/Prefab/UI/IndexMenu.prefab";
            GameObject menu = PrefabUtility.LoadPrefabContents(path);
            try
            {
                UIParallax backdrop = menu.GetComponentInChildren<UIParallax>(true);
                Assert.That(backdrop, Is.Not.Null);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(backdrop), Is.Not.Null);
                Assert.That(backdrop.GetComponentsInChildren<Graphic>(true).All(graphic => !graphic.raycastTarget), Is.True);
                Assert.That(backdrop.GetComponentsInChildren<Selectable>(true), Is.Empty);
                var entries = new SerializedObject(backdrop).FindProperty("layers");
                Assert.That(entries.arraySize, Is.EqualTo(3));
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var target = entries.GetArrayElementAtIndex(i).FindPropertyRelative("target").objectReferenceValue as RectTransform;
                    Assert.That(target, Is.Not.Null);
                    Assert.That(target.parent, Is.EqualTo(backdrop.transform));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(menu); }
        }
    }

    public sealed class UIParallaxRuntimeTests
    {
        [UnityTest]
        public IEnumerator IndexBackdropMovesWhilePausedAndControlsStayInteractive()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Index.unity");
            yield return new EnterPlayMode();
            double deadline = EditorApplication.timeSinceStartup + 60;
            UIParallax backdrop;
            while ((backdrop = Object.FindFirstObjectByType<UIParallax>()) == null && EditorApplication.timeSinceStartup < deadline)
                yield return null;
            Assert.That(backdrop, Is.Not.Null, "Index did not load its parallax backdrop.");
            yield return UnityEngine.Localization.Settings.LocalizationSettings.InitializationOperation;
            yield return null;

            var manager = new SerializedObject(Object.FindFirstObjectByType<IndexManager>());
            var help = (Button)manager.FindProperty("helpBtn").objectReferenceValue;
            var settings = (Button)manager.FindProperty("settingsBtn").objectReferenceValue;
            var helpPanel = (UIHelpPanel)manager.FindProperty("helpPanel").objectReferenceValue;
            var settingsPanel = (UIConfigPanel)manager.FindProperty("configPanel").objectReferenceValue;
            var serialized = new SerializedObject(backdrop);
            serialized.FindProperty("pointerInfluence").floatValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var layer = (RectTransform)serialized.FindProperty("layers").GetArrayElementAtIndex(2).FindPropertyRelative("target").objectReferenceValue;
            Vector2 start = layer.anchoredPosition;
            Vector3 helpPosition = help.transform.position;
            Vector3 settingsPosition = settings.transform.position;
            float previous = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                deadline = EditorApplication.timeSinceStartup + .5;
                while (EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.That(Time.deltaTime, Is.Zero);
                Assert.That(Vector2.Distance(start, layer.anchoredPosition), Is.GreaterThan(.01f));
                Assert.That(help.transform.position, Is.EqualTo(helpPosition));
                Assert.That(settings.transform.position, Is.EqualTo(settingsPosition));
                help.onClick.Invoke();
                Assert.That(helpPanel.Visible, Is.True);
                helpPanel.Close();
                settings.onClick.Invoke();
                Assert.That(settingsPanel.Visible, Is.True);
                settingsPanel.Close();
                backdrop.enabled = false;
                Assert.That(layer.anchoredPosition, Is.EqualTo(Vector2.zero));
                backdrop.enabled = true;
                Assert.That(layer.anchoredPosition, Is.EqualTo(Vector2.zero));
            }
            finally { Time.timeScale = previous; }
            yield return new ExitPlayMode();
        }
    }
}
