using System.IO;
using System.Linq;
using ByteDance.PICO.Interaction;
using NUnit.Framework;
using PicShow.PicoInteraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.Tests
{
    public class AppModeAssetTests
    {
        private const string PanelPrefabPath = "Assets/PicShow/Prefabs/ModeSelection.prefab";
        private const string WallPanelPrefabPath = "Assets/PicShow/Prefabs/WallPanel.prefab";
        private const string WallGalleryPrefabPath = "Assets/PicShow/Prefabs/WallGallery.prefab";
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string FontPath = "Assets/PicShow/Fonts/ModeChinese SDF.asset";
        private const string PlaneMaterialPath =
            "Assets/PICO_MCP/SpatialMesh/TriangleFadeOutFromCenter.mat";

        [Test]
        public void ModePanelPrefabHasTwoEnhancedIcons()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<ModePanelView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.MrButton, Is.Not.Null);
            Assert.That(view.NonMrButton, Is.Not.Null);
            Assert.That(view.MrButton.GetComponent<PXR_EnhancedButton>(), Is.Not.Null);
            Assert.That(view.NonMrButton.GetComponent<PXR_EnhancedButton>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(prefab.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);

            var bridge = prefab.GetComponent<PicoButtonClickBridge>();
            Assert.That(bridge, Is.Not.Null);
            var buttons = new SerializedObject(bridge).FindProperty("buttons");
            Assert.That(buttons.arraySize, Is.EqualTo(2));

            var serialized = new SerializedObject(view);
            Assert.That(serialized.FindProperty("mrIcon").objectReferenceValue, Is.Not.Null);
            Assert.That(serialized.FindProperty("nonMrIcon").objectReferenceValue, Is.Not.Null);
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.font.HasCharacters(text.text), Is.True, text.text);
        }

        [Test]
        public void ModePanelUsesBalancedLayoutAndTimedFollow()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            RectTransform root = (RectTransform)prefab.transform;
            Assert.That(root.sizeDelta.x, Is.EqualTo(1200f).Within(0.01f));
            Assert.That(root.sizeDelta.y, Is.EqualTo(700f).Within(0.01f));

            var view = prefab.GetComponent<ModePanelView>();
            RectTransform[] cards =
            {
                (RectTransform)view.MrButton.transform,
                (RectTransform)view.NonMrButton.transform
            };
            Assert.That(cards[0].sizeDelta.x, Is.EqualTo(420f).Within(0.01f));
            Assert.That(cards[0].sizeDelta.y, Is.EqualTo(470f).Within(0.01f));
            Assert.That(cards[1].sizeDelta, Is.EqualTo(cards[0].sizeDelta));
            float halfWidth = root.sizeDelta.x / 2f;
            float halfHeight = root.sizeDelta.y / 2f;
            foreach (RectTransform card in cards)
            {
                Assert.That(Mathf.Abs(card.anchoredPosition.x) + card.sizeDelta.x / 2f,
                    Is.LessThanOrEqualTo(halfWidth + 0.01f));
                Assert.That(Mathf.Abs(card.anchoredPosition.y) + card.sizeDelta.y / 2f,
                    Is.LessThanOrEqualTo(halfHeight + 0.01f));
            }

            var follow = prefab.GetComponent<LazyFollow>();
            Assert.That(follow, Is.Not.Null, "The mode panel needs LazyFollow.");
            var timed = prefab.GetComponent<TimedLazyFollow>();
            Assert.That(timed, Is.Not.Null, "The mode panel needs the 3-second follow stop.");
            var timedSerialized = new SerializedObject(timed);
            Assert.That(timedSerialized.FindProperty("followSeconds").floatValue,
                Is.EqualTo(3f).Within(0.01f));
            Assert.That(timedSerialized.FindProperty("lazyFollow").objectReferenceValue,
                Is.SameAs(follow));
            var followSerialized = new SerializedObject(follow);
            Vector3 offset = followSerialized.FindProperty("m_TargetOffset").vector3Value;
            Assert.That(offset.z, Is.EqualTo(1.6f).Within(0.01f));
            Assert.That(followSerialized.FindProperty("m_PositionFollowMode").intValue,
                Is.EqualTo((int)LazyFollow.PositionFollowMode.Follow));
            Assert.That(followSerialized.FindProperty("m_RotationFollowMode").intValue,
                Is.EqualTo((int)LazyFollow.RotationFollowMode.LookAt));
        }

        [Test]
        public void ModeCardsProvideVisibleHoverFeedback()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath);
            var view = prefab.GetComponent<ModePanelView>();
            foreach (Button card in new[] { view.MrButton, view.NonMrButton })
                Assert.That(card.GetComponent<ModeCardHover>(), Is.Not.Null,
                    "Every mode card needs a hover visual component.");

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            Assert.That(instance, Is.Not.Null);
            try
            {
                var instanceView = instance.GetComponent<ModePanelView>();
                Button card = instanceView.MrButton;
                var hover = card.GetComponent<ModeCardHover>();
                var background = card.GetComponent<Image>();
                Color normalColor = background.color;
                Vector3 normalScale = card.transform.localScale;

                hover.OnPointerEnter(null);

                Assert.That(hover.Hovering, Is.True);
                Assert.That(background.color, Is.Not.EqualTo(normalColor));
                Assert.That(card.transform.localScale.x,
                    Is.GreaterThan(normalScale.x + 0.01f));

                hover.OnPointerExit(null);

                Assert.That(hover.Hovering, Is.False);
                Assert.That(background.color, Is.EqualTo(normalColor));
                Assert.That(card.transform.localScale, Is.EqualTo(normalScale));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void WallPanelPrefabHasWallVisualAndCompactButton()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallPanelPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<WallPanelView>();
            Assert.That(view, Is.Not.Null);
            Assert.That(view.SelectButton, Is.Not.Null);
            Assert.That(view.SelectButton.GetComponent<PXR_EnhancedButton>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(prefab.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            var prompt = prefab.GetComponentsInChildren<TMP_Text>(true)
                .FirstOrDefault(text => text.text == "选择该墙面显示");
            Assert.That(prompt, Is.Not.Null);
            RectTransform surface = (RectTransform)view.SelectButton.transform;
            Assert.That(surface.anchorMin.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(surface.anchorMin.y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(surface.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(surface.anchorMax.y, Is.EqualTo(0.5f).Within(0.001f));
            Transform fill = prefab.transform.Find("Fill");
            Assert.That(fill, Is.Not.Null, "The wall panel needs a detection highlight fill.");
            Assert.That(fill.GetComponent<Image>().raycastTarget, Is.False);
            Assert.That(fill.GetComponent<Image>().color.a, Is.LessThan(0.35f));
            Transform border = prefab.transform.Find("Border");
            Assert.That(border, Is.Not.Null);
            Assert.That(border.GetComponent<Image>(), Is.Null,
                "Border must be an edge container, not a full-wall fill.");
            Image[] bars = border.GetComponentsInChildren<Image>(true);
            Assert.That(bars, Has.Length.EqualTo(4),
                "The wall highlight needs four thin edge bars.");
            foreach (Image bar in bars)
            {
                Assert.That(bar.raycastTarget, Is.False);
                Assert.That(bar.color.a, Is.GreaterThanOrEqualTo(0.5f));
            }
            Assert.That(prefab.GetComponent<PicoButtonClickBridge>(), Is.Not.Null);
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.font.HasCharacters(text.text), Is.True, text.text);
        }

        [Test]
        public void WallGalleryPrefabHasFourCategoriesAndFiveArtworks()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WallGalleryPrefabPath);
            Assert.That(prefab, Is.Not.Null);
            var view = prefab.GetComponent<WallGalleryView>();
            Assert.That(view, Is.Not.Null);
            var serialized = new SerializedObject(view);
            Assert.That(serialized.FindProperty("categoryButtons").arraySize, Is.EqualTo(4));
            Assert.That(serialized.FindProperty("categoryLabels").arraySize, Is.EqualTo(4));
            Assert.That(serialized.FindProperty("imageSlots").arraySize, Is.EqualTo(5));
            Assert.That(serialized.FindProperty("backButton").objectReferenceValue, Is.Not.Null);
            Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(prefab.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<RawImage>(true).Length, Is.EqualTo(5));
            Assert.That(prefab.GetComponent<PicoButtonClickBridge>(), Is.Not.Null);
            foreach (TMP_Text text in prefab.GetComponentsInChildren<TMP_Text>(true))
                Assert.That(text.font.HasCharacters(text.text), Is.True, text.text);
        }

        [Test]
        public void SceneStartsWithModePanelAndDetectionOff()
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
                Assert.That(serialized.FindProperty("modePanel").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("galleryRoot").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("returnButton").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("wallDisplay").objectReferenceValue, Is.Not.Null);
                Assert.That(serialized.FindProperty("picoRuntimeProvider").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(serialized.FindProperty("wallSourceProvider").objectReferenceValue,
                    Is.Not.Null);

                GameObject gallery = (GameObject)serialized.FindProperty("galleryRoot")
                    .objectReferenceValue;
                Assert.That(gallery.activeSelf, Is.False, "The gallery must wait for non-MR mode.");
                GameObject returnButton = (GameObject)serialized.FindProperty("returnButton")
                    .objectReferenceValue;
                Assert.That(returnButton.activeSelf, Is.False,
                    "The return entry must stay hidden while the mode panel is open.");
                Assert.That(returnButton.GetComponentInChildren<Button>(true), Is.Not.Null);
                RectTransform returnRect = (RectTransform)returnButton.transform;
                // Position stays editable by hand in the scene, so only its attachment is pinned.
                Assert.That(returnRect.parent.GetComponent<Camera>(), Is.Not.Null,
                    "The return entry must stay attached to the camera.");
                Assert.That(returnRect.localPosition.z, Is.GreaterThan(0f),
                    "The return entry must stay in front of the camera.");
                Assert.That(returnRect.sizeDelta.x, Is.EqualTo(160f).Within(0.01f));
                Assert.That(returnRect.sizeDelta.y, Is.EqualTo(60f).Within(0.01f));
                TMP_Text returnLabel = returnButton.GetComponentInChildren<TMP_Text>(true);
                Assert.That(returnLabel, Is.Not.Null);
                Assert.That(returnLabel.text, Is.EqualTo("上一页"));

                var wallDisplay = (WallDisplayController)serialized.FindProperty("wallDisplay")
                    .objectReferenceValue;
                var wallSerialized = new SerializedObject(wallDisplay);
                Assert.That(wallSerialized.FindProperty("wallPanelPrefab").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(wallSerialized.FindProperty("wallGalleryPrefab").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(wallSerialized.FindProperty("catalog").objectReferenceValue, Is.Not.Null);
                Assert.That(wallSerialized.FindProperty("wallSourceProvider").objectReferenceValue,
                    Is.Not.Null);

                PlaneDetectionManager plane = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PlaneDetectionManager>(true))
                    .SingleOrDefault();
                Assert.That(plane, Is.Not.Null);
                Assert.That(plane.DetectionEnabled, Is.False,
                    "Plane detection must stay off until MR mode is selected.");
                Assert.That(scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true))
                    .Count(), Is.EqualTo(1));

                var panelCanvas = controller.GetComponentsInChildren<Canvas>(true)
                    .FirstOrDefault(canvas => canvas.renderMode == RenderMode.WorldSpace);
                Assert.That(panelCanvas, Is.Not.Null);
                Assert.That(panelCanvas.worldCamera, Is.Not.Null);
            }
            finally
            {
                if (close)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void ModeFontHasEveryChineseGlyphUsedByThePanel()
        {
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            Assert.That(font, Is.Not.Null);
            string required = "MR 模式非选择该墙面显示换墙面返回上一页沉浸展示检测轮播浏览或·" +
                "水彩治愈现代都市赛博朋克超现实奇幻";
            Assert.That(font.HasCharacters(required), Is.True);
            Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        }

        [Test]
        public void ModeIconsAreImportedAsSprites()
        {
            var mr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PicShow/UI/ModeMr.png");
            var nonMr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/PicShow/UI/ModeNonMr.png");
            Assert.That(mr, Is.Not.Null);
            Assert.That(nonMr, Is.Not.Null);
            Assert.That(mr.rect.width, Is.GreaterThan(64f));
            Assert.That(nonMr.rect.width, Is.GreaterThan(64f));
        }

        [Test]
        public void DebugPlaneShaderUsesOneVisibleForwardPass()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(PlaneMaterialPath);
            Assert.That(material, Is.Not.Null, PlaneMaterialPath);
            ShaderData data = ShaderUtil.GetShaderData(material.shader);
            ShaderData.Subshader subshader = data.GetSerializedSubshader(0);
            ShaderData.Pass pass = subshader.GetPass(0);

            Assert.That(subshader.PassCount, Is.EqualTo(1),
                "URP selects one matching forward pass and would skip a second color pass.");
            Assert.That(pass.FindTagValue(new ShaderTagId("LightMode")).name,
                Is.EqualTo("UniversalForward"));
        }

        [Test]
        public void UiSharpnessBaselineIsConfigured()
        {
            string pipeline = File.ReadAllText(
                Path.Combine(Application.dataPath, "Settings/Mobile_RPAsset.asset"));
            Assert.That(pipeline, Does.Contain("m_RenderScale: 1"),
                "The mobile pipeline must render at native eye resolution.");
            Assert.That(pipeline, Does.Contain("m_MSAA: 2"),
                "The mobile pipeline must keep 2x MSAA for UI edges.");

            string xrSettings = File.ReadAllText(
                Path.Combine(Application.dataPath, "Resources/PXR_ProjectSetting.asset"));
            Assert.That(xrSettings, Does.Contain("qualitySharpening: 1"),
                "PICO quality sharpening must stay enabled for UI clarity.");

            var roundedImporter = (TextureImporter)AssetImporter.GetAtPath(
                "Assets/PicShow/UI/RoundedPanel.png");
            Assert.That(roundedImporter, Is.Not.Null);
            Assert.That(roundedImporter.spriteBorder.x, Is.EqualTo(160f).Within(0.01f),
                "The rounded nine-slice border must match the 512 px source.");
            var roundedTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/PicShow/UI/RoundedPanel.png");
            Assert.That(roundedTexture, Is.Not.Null);
            Assert.That(roundedTexture.width, Is.EqualTo(512));

            var mrTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/PicShow/UI/ModeMr.png");
            Assert.That(mrTexture, Is.Not.Null);
            Assert.That(mrTexture.width, Is.EqualTo(512),
                "Mode icons must be generated at 512 px.");
        }
    }
}
