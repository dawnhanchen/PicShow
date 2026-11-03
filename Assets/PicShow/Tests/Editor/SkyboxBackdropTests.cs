using System;
using System.Linq;
using NUnit.Framework;
using PicShow.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace PicShow.Tests
{
    public class SkyboxBackdropTests
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string SkyTexturePath = "Assets/PicShow/Art/Skybox/SkyGradientSeamless.png";
        private const string SkyMaterialPath = "Assets/PicShow/Art/Skybox/SkyGradient.mat";
        private const string AltSkyTexturePath = "Assets/PicShow/Art/Skybox/GalleryAtmosphereSeamless.png";
        private const string AltSkyMaterialPath = "Assets/PicShow/Art/Skybox/GalleryAtmosphere.mat";
        private static readonly Color TransparentBlack = new Color(0f, 0f, 0f, 0f);

        [Test]
        public void PanoramaWrapsWithoutAVisibleSeam()
        {
            const int width = 256;
            const int height = 128;
            Color32[] pixels = ModeSceneBuilder.BuildSeamlessSkyPixels(width, height);
            AssertPanoramaWraps(pixels, width, height);
        }

        [Test]
        public void GalleryAtmosphereWrapsWithoutAVisibleSeam()
        {
            const int width = 256;
            const int height = 128;
            Color32[] pixels = ModeSceneBuilder.BuildGalleryAtmospherePixels(width, height);
            AssertPanoramaWraps(pixels, width, height);
        }

        private static void AssertPanoramaWraps(Color32[] pixels, int width, int height)
        {
            Assert.That(pixels, Has.Length.EqualTo(width * height));
            for (int y = 0; y < height; y++)
            {
                float delta = MaxChannelDelta(pixels[y * width], pixels[y * width + width - 1]);
                Assert.That(delta, Is.LessThanOrEqualTo(4f),
                    $"Seam detected at row {y}: {delta} levels between the first and last column.");
            }

            foreach (int y in new[] { 0, height - 1 })
            {
                var row = new Color32[width];
                Array.Copy(pixels, y * width, row, 0, width);
                float span = RowSpan(row);
                Assert.That(span, Is.LessThanOrEqualTo(1f),
                    $"Pole row {y} must stay horizontally uniform to avoid pinching.");
            }
        }

        private static float RowSpan(Color32[] row)
        {
            byte minR = 255, maxR = 0, minG = 255, maxG = 0, minB = 255, maxB = 0;
            foreach (Color32 pixel in row)
            {
                minR = Math.Min(minR, pixel.r);
                maxR = Math.Max(maxR, pixel.r);
                minG = Math.Min(minG, pixel.g);
                maxG = Math.Max(maxG, pixel.g);
                minB = Math.Min(minB, pixel.b);
                maxB = Math.Max(maxB, pixel.b);
            }
            return Math.Max(Math.Max(maxR - minR, maxG - minG), maxB - minB);
        }

        private static float MaxChannelDelta(Color32 a, Color32 b)
        {
            return Mathf.Max(Mathf.Abs((int)a.r - b.r), Mathf.Abs((int)a.g - b.g),
                Mathf.Abs((int)a.b - b.b));
        }

        [Test]
        public void SkyGradientTextureIsImportedAsASmoothPanorama()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SkyTexturePath);
            Assert.That(texture, Is.Not.Null, SkyTexturePath);
            Assert.That((float)texture.width / texture.height, Is.EqualTo(2f).Within(0.01f),
                "An equirectangular skybox needs a 2:1 panorama.");

            var importer = (TextureImporter)AssetImporter.GetAtPath(SkyTexturePath);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed),
                "Uncompressed keeps the soft gradient free of banding.");
        }

        [Test]
        public void GalleryAtmosphereIsImportedAndKeptBesideTheSkyGradient()
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AltSkyTexturePath);
            Assert.That(texture, Is.Not.Null, AltSkyTexturePath);
            Assert.That((float)texture.width / texture.height, Is.EqualTo(2f).Within(0.01f),
                "An equirectangular backdrop needs a 2:1 panorama.");

            var material = AssetDatabase.LoadAssetAtPath<Material>(AltSkyMaterialPath);
            Assert.That(material, Is.Not.Null, AltSkyMaterialPath);
            Assert.That(material.shader.name, Is.EqualTo("Skybox/Panoramic"));
            Assert.That(material.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(material.GetFloat("_Mapping"), Is.EqualTo(1f).Within(0.01f));
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath), Is.Not.Null,
                "The previous sky gradient material must stay available for switching.");
        }

        [Test]
        public void SkyMaterialUsesThePanoramicLatitudeLongitudeMapping()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            Assert.That(material, Is.Not.Null, SkyMaterialPath);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Skybox/Panoramic"));
            Assert.That(material.GetTexture("_MainTex"), Is.Not.Null);
            Assert.That(material.GetFloat("_Mapping"), Is.EqualTo(1f).Within(0.01f),
                "Latitude Longitude mapping is required for a 2:1 panorama.");
            Assert.That(material.GetFloat("_ImageType"), Is.EqualTo(0f).Within(0.01f),
                "The skybox must sample a plain 2D texture.");
        }

        [Test]
        public void NonMrModeRendersSkyboxAndTransparentModeRestoresTheBackground()
        {
            Material previousSkybox = RenderSettings.skybox;
            var cameraObject = new GameObject("Skybox Test Camera");
            var backdropObject = new GameObject("Skybox Test Backdrop");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                var backdrop = backdropObject.AddComponent<SkyboxBackdrop>();
                var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
                backdrop.Configure(camera, material);

                backdrop.Apply(true);
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
                Assert.That(RenderSettings.skybox, Is.SameAs(material));

                backdrop.Apply(false);
                Assert.That(camera.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
                Assert.That(camera.backgroundColor, Is.EqualTo(TransparentBlack),
                    "MR mode must keep the VST background transparent.");
                Assert.That(RenderSettings.skybox, Is.Not.SameAs(material),
                    "Leaving the gallery must clear the gallery skybox from the render settings.");
            }
            finally
            {
                RenderSettings.skybox = previousSkybox;
                Object.DestroyImmediate(backdropObject);
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void SceneWiresTheSkyboxBackdropToTheMainCamera()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool close = !scene.isLoaded;
            if (close)
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                var controller = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<AppModeController>(true))
                    .SingleOrDefault();
                Assert.That(controller, Is.Not.Null);
                var serialized = new SerializedObject(controller);
                var backdropReference = serialized.FindProperty("skyboxBackdrop");
                Assert.That(backdropReference, Is.Not.Null);
                var backdrop = (SkyboxBackdrop)backdropReference.objectReferenceValue;
                Assert.That(backdrop, Is.Not.Null, "The scene must carry a SkyboxBackdrop.");

                var backdropSerialized = new SerializedObject(backdrop);
                var camera = (Camera)backdropSerialized.FindProperty("targetCamera")
                    .objectReferenceValue;
                var material = (Material)backdropSerialized.FindProperty("skyMaterial")
                    .objectReferenceValue;
                Assert.That(camera, Is.Not.Null, "The backdrop needs the XR main camera.");
                Assert.That(camera.CompareTag("MainCamera"), Is.True);
                Assert.That(material, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(material), Is.EqualTo(AltSkyMaterialPath));
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath), Is.Not.Null,
                    "The previous sky gradient material must stay available for switching.");
            }
            finally
            {
                if (close)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
