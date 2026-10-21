using System.Linq;
using Hypocycloid.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Baseline.Tests.Editor
{
    public sealed class AudioImportTests
    {
        [Test]
        public void EveryShippedClipUsesItsFolderOwnedImportProfile()
        {
            string[] paths = AssetDatabase.FindAssets("t:AudioClip", new[] { AudioImportOptimizer.AudioRoot })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray();
            Assert.That(paths, Is.Not.Empty);
            foreach (string path in paths)
            {
                var importer = (AudioImporter)AssetImporter.GetAtPath(path);
                Assert.That(AudioImportOptimizer.IsConfigured(importer, path), Is.True, path);
                Assert.That(AudioImportOptimizer.Configure(importer, path), Is.False, path);
            }
        }

        [TestCase("Assets/Bundles/Core/Sound/Music/track.mp3", AudioClipLoadType.Streaming, false, false)]
        [TestCase("Assets\\Bundles\\Core\\Sound\\Music\\track.mp3", AudioClipLoadType.Streaming, false, false)]
        [TestCase("Assets/Bundles/Core/Sound/Fx/click.wav", AudioClipLoadType.DecompressOnLoad, true, true)]
        [TestCase("Assets/Bundles/Core/Sound/Voice/Guide/reaction.wav", AudioClipLoadType.DecompressOnLoad, true, true)]
        public void ProfilesFollowTheOwningFolder(string path, AudioClipLoadType load, bool mono, bool preload)
        {
            Assert.That(AudioImportOptimizer.TryGetProfile(path, out AudioImportProfile profile), Is.True);
            Assert.That(profile.LoadType, Is.EqualTo(load));
            Assert.That(profile.ForceMono, Is.EqualTo(mono));
            Assert.That(profile.Preload, Is.EqualTo(preload));
        }

        [TestCase("Assets/Bundles/Other/zap.mp3")]
        [TestCase("Assets/Bundles/Core/Sounds/zap.mp3")]
        public void UnownedFolderHasNoProfile(string path)
            => Assert.That(AudioImportOptimizer.TryGetProfile(path, out _), Is.False);
    }
}
