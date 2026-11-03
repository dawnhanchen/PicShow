using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.Tests
{
    public class GalleryAssetTests
    {
        private const string PrefabPath = "Assets/PicShow/Prefabs/FourRealmsGallery.prefab";
        private const string CatalogPath = "Assets/PicShow/Data/GalleryCatalog.asset";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private static readonly string[] RemovedDecorationNames =
        {
            "Backdrop",
            "Header rule",
            "Brand",
            "Brand caption",
            "Edition",
            "Footer rule",
            "Input hint",
            "Collection note"
        };

        [Test]
        public void SavedCatalogContainsTwentyDistinctTexturesInMatchingOrder()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<GalleryCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null);
            Assert.That(catalog.Validate(out string error), Is.True, error);
            var works = catalog.Categories.SelectMany(c => c.Works).ToArray();
            Assert.That(works.Select(w => w.Texture).Distinct().Count(), Is.EqualTo(20));
            foreach (var work in works)
            {
                Assert.That((float)work.Texture.width / work.Texture.height, Is.EqualTo(1.5f).Within(0.002f));
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(work.Texture));
                Assert.That(importer.isReadable, Is.False);
                Assert.That(importer.GetPlatformTextureSettings("Android").format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));
            }
        }

        [Test]
        public void SavedPrefabHasExactlyThreeCardsAndWorldSpaceRaycasters()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var gallery = prefab.GetComponent<GalleryCarousel>();
            Assert.That(gallery.Cards.Count, Is.EqualTo(3));
            Assert.That(prefab.GetComponentsInChildren<RawImage>(true).Length, Is.EqualTo(3));
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            Assert.That(gallery.Tabs.Count, Is.EqualTo(4));
            Assert.That(gallery.Dots.Count, Is.EqualTo(5));
            foreach (var tab in gallery.Tabs)
                Assert.That(tab.Background.sprite, Is.Not.Null, "Rounded tab sprite must be serialized.");
            foreach (var card in gallery.Cards)
            {
                Assert.That(card.Root, Is.Not.Null);
                Assert.That(card.Button, Is.Not.Null);
                Assert.That(card.Artwork.texture, Is.Not.Null);
            }
        }

        [Test]
        public void SavedPrefabFollowsHeadWithoutTilting()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var followers = prefab.GetComponentsInChildren<LazyFollow>(true);
            Assert.That(followers.Length, Is.EqualTo(1), "The gallery root must hold exactly one follower.");

            var follow = followers[0];
            Assert.That(follow.gameObject, Is.SameAs(prefab));
            Assert.That(follow.target, Is.Null, "The target must stay unset so the follower picks up the main camera.");
            Assert.That(follow.targetOffset, Is.EqualTo(new Vector3(0f, 0f, 2f)));
            Assert.That(follow.followInLocalSpace, Is.False);
            Assert.That(follow.applyTargetInLocalSpace, Is.False);
            Assert.That(follow.positionFollowMode, Is.EqualTo(LazyFollow.PositionFollowMode.Follow));
            Assert.That(follow.rotationFollowMode, Is.EqualTo(LazyFollow.RotationFollowMode.LookAtWithWorldUp));
            Assert.That(follow.snapOnEnable, Is.False);
        }

        [Test]
        public void SavedPrefabOmitsRemovedHeaderAndFooterDecorations()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);

            foreach (string objectName in RemovedDecorationNames)
                Assert.That(prefab.transform.Find(objectName), Is.Null, objectName);
        }

        [Test]
        public void SavedSceneOmitsRemovedDecorationsAndUnusedOverrides()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool closeScene = !scene.isLoaded;
            if (closeScene)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var gallery = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<GalleryCarousel>(true))
                    .Single();
                foreach (string objectName in RemovedDecorationNames)
                    Assert.That(gallery.transform.Find(objectName), Is.Null, objectName);

                var unusedOverrides = PrefabUtility.GetPropertyModifications(gallery.gameObject)
                    .Where(modification => modification.target == null)
                    .ToArray();
                Assert.That(unusedOverrides, Is.Empty, "The saved scene contains unused prefab overrides.");
            }
            finally
            {
                if (closeScene)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void AllVisibleChineseTextHasStaticGlyphsAndDoesNotBlockInput()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(text.font, Is.Not.Null);
                Assert.That(text.font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
                Assert.That(text.font.HasCharacters(text.text), Is.True, text.text);
                Assert.That(text.raycastTarget, Is.False, text.name);
            }
            var catalog = AssetDatabase.LoadAssetAtPath<GalleryCatalog>(CatalogPath);
            var font = prefab.GetComponentInChildren<TMP_Text>(true).font;
            foreach (var category in catalog.Categories)
            {
                Assert.That(font.HasCharacters(category.Name + category.Description), Is.True);
                foreach (var work in category.Works)
                    Assert.That(font.HasCharacters(work.Title + work.Description), Is.True);
            }
        }

        [Test]
        public void MalformedCatalogReportsErrorWithoutThrowing()
        {
            var catalog = ScriptableObject.CreateInstance<GalleryCatalog>();
            try
            {
                Assert.That(catalog.Validate(out _), Is.False);
                catalog.Categories = new GalleryCatalog.Category[4];
                Assert.That(catalog.Validate(out _), Is.False);
            }
            finally { Object.DestroyImmediate(catalog); }
        }
    }
}
