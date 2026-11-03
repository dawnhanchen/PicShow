using System;
using System.Collections.Generic;
using ByteDance.PICO.Interaction;
using ByteDance.PICO.XR;
using NUnit.Framework;
using PicShow;
using PicShow.PicoInteraction;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PicShow.Tests
{
    public class ModeWallDisplayTests
    {
        private static WallPlaneInfo BuildWall(bool semanticWall, bool reverseWinding)
        {
            var vertices = new[]
            {
                new Vector3(-1f, -1f, -2f),
                new Vector3(1f, -1f, -2f),
                new Vector3(1f, 1f, -2f),
                new Vector3(-1f, 1f, -2f)
            };
            var indices = reverseWinding
                ? new[] { 0, 3, 2, 0, 2, 1 }
                : new[] { 0, 1, 2, 0, 2, 3 };
            Assert.That(
                WallGeometry.TryBuild(Guid.NewGuid(), vertices, indices, semanticWall,
                    Vector3.zero, out WallPlaneInfo wall, out string error),
                Is.True, error);
            return wall;
        }

        [Test]
        public void SemanticWallIsAcceptedEvenWhenGeometryIsAmbiguous()
        {
            Assert.That(WallGeometry.IsWall(true, Vector3.up), Is.True);
        }

        [Test]
        public void UnknownLabelNoLongerUsesVerticalGeometryFallback()
        {
            Assert.That(WallGeometry.IsWall(false, Vector3.forward), Is.False);
            Assert.That(WallGeometry.IsWall(false, new Vector3(0f, 0.1f, 1f).normalized), Is.False);
        }

        [Test]
        public void HorizontalAndTiltedPlanesAreRejected()
        {
            Assert.That(WallGeometry.IsWall(false, Vector3.up), Is.False);
            Assert.That(WallGeometry.IsWall(false, new Vector3(0f, 0.4f, 1f).normalized), Is.False);
            Assert.That(WallGeometry.IsWall(true, Vector3.up), Is.True);
        }

        [Test]
        public void WallRectReportsCenterSizeAndOrthogonalBasis()
        {
            WallPlaneInfo wall = BuildWall(true, false);
            Assert.That(wall.Width, Is.EqualTo(2f).Within(0.001f));
            Assert.That(wall.Height, Is.EqualTo(2f).Within(0.001f));
            Assert.That(Vector3.Distance(wall.Center, new Vector3(0f, 0f, -2f)), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(wall.Right, wall.Up), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Dot(wall.Right, wall.Normal), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Dot(wall.Up, wall.Normal), Is.EqualTo(0f).Within(0.001f));
            Assert.That(Vector3.Distance(wall.Normal, Vector3.forward), Is.LessThan(0.001f));
            Assert.That(Vector3.Distance(wall.Rotation * Vector3.forward, -wall.Normal),
                Is.LessThan(0.001f),
                "The world-space canvas must face the room.");
        }

        [Test]
        public void ReversedWindingStillFacesTheRoom()
        {
            WallPlaneInfo wall = BuildWall(true, true);
            Assert.That(Vector3.Distance(wall.Normal, Vector3.forward), Is.LessThan(0.001f));
            Assert.That(wall.Width, Is.EqualTo(2f).Within(0.001f));
            Assert.That(wall.Height, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void DegeneratePlaneIsRejected()
        {
            var vertices = new[]
            {
                new Vector3(0f, 0f, -2f),
                new Vector3(0f, 0f, -2f),
                new Vector3(0f, 0f, -2f)
            };
            var indices = new[] { 0, 1, 2 };
            Assert.That(
                WallGeometry.TryBuild(Guid.NewGuid(), vertices, indices, true,
                    Vector3.zero, out _, out _),
                Is.False);
        }

        [Test]
        public void WideWallUsesSingleRowForFiveImages()
        {
            var ratios = new[] { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f };
            WallGridResult result = WallGridLayout.Compute(new Vector2(8f, 2f), ratios, 0.03f, 0.02f);
            Assert.That(result.Rows, Is.EqualTo(1));
            Assert.That(result.Columns, Is.EqualTo(5));
            Assert.That(result.Rects, Has.Length.EqualTo(5));
        }

        [Test]
        public void NarrowWallUsesMultipleRows()
        {
            var ratios = new[] { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f };
            WallGridResult result = WallGridLayout.Compute(new Vector2(2f, 4f), ratios, 0.03f, 0.02f);
            Assert.That(result.Rows, Is.GreaterThanOrEqualTo(2));
            Assert.That(result.Rects, Has.Length.EqualTo(5));
        }

        [Test]
        public void AllImagesKeepAspectAndStayInsideTheWall()
        {
            var ratios = new[] { 1.5f, 1.9f, 1.2f, 2.4f, 1.5f };
            var wallSize = new Vector2(3f, 2f);
            WallGridResult result = WallGridLayout.Compute(wallSize, ratios, 0.04f, 0.02f);
            Assert.That(result.Rects, Has.Length.EqualTo(5));
            for (int i = 0; i < result.Rects.Length; i++)
            {
                WallGridRect rect = result.Rects[i];
                Assert.That(rect.Size.x / rect.Size.y, Is.EqualTo(ratios[i]).Within(0.002f),
                    "Image {0} must keep its aspect ratio.", i);
                Assert.That(Mathf.Abs(rect.Center.x) + rect.Size.x / 2f,
                    Is.LessThanOrEqualTo(wallSize.x / 2f + 0.001f));
                Assert.That(Mathf.Abs(rect.Center.y) + rect.Size.y / 2f,
                    Is.LessThanOrEqualTo(wallSize.y / 2f + 0.001f));
            }
        }

        [Test]
        public void MarginsAndGapAreRespected()
        {
            var ratios = new[] { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f };
            var wallSize = new Vector2(3f, 2f);
            WallGridResult result = WallGridLayout.Compute(wallSize, ratios, 0.04f, 0.02f);
            float usableHalfWidth = wallSize.x * (1f - 0.04f * 2f) / 2f;
            foreach (WallGridRect rect in result.Rects)
                Assert.That(Mathf.Abs(rect.Center.x) + rect.Size.x / 2f,
                    Is.LessThanOrEqualTo(usableHalfWidth + 0.001f));
        }

        [Test]
        public void RecomputingForNewRatiosReplacesEveryRect()
        {
            WallGridResult first = WallGridLayout.Compute(
                new Vector2(3f, 2f), new[] { 1.5f, 1.5f, 1.5f, 1.5f, 1.5f }, 0.04f, 0.02f);
            WallGridResult second = WallGridLayout.Compute(
                new Vector2(3f, 2f), new[] { 0.8f, 0.8f, 0.8f, 0.8f, 0.8f }, 0.04f, 0.02f);
            Assert.That(second.Rects, Has.Length.EqualTo(5));
            Assert.That(second.Rects[0].Size.x / second.Rects[0].Size.y,
                Is.EqualTo(0.8f).Within(0.002f));
            Assert.That(first.Rects[0].Size.x / first.Rects[0].Size.y,
                Is.EqualTo(1.5f).Within(0.002f));
        }

        [Test]
        public void ModeStartsAtPanelWithEverythingOff()
        {
            var state = new AppModeState();
            Assert.That(state.Mode, Is.EqualTo(AppMode.ModePanel));
            Assert.That(state.View.ModePanelVisible, Is.True);
            Assert.That(state.View.GalleryVisible, Is.False);
            Assert.That(state.View.ReturnVisible, Is.False);
            Assert.That(state.View.DetectionEnabled, Is.False);
            Assert.That(state.View.VideoSeeThrough, Is.False);
        }

        [Test]
        public void NonMrTurnsOnGalleryOnly()
        {
            var state = new AppModeState();
            state.EnterNonMr();
            Assert.That(state.Mode, Is.EqualTo(AppMode.NonMrGallery));
            Assert.That(state.View.ModePanelVisible, Is.False);
            Assert.That(state.View.GalleryVisible, Is.True);
            Assert.That(state.View.ReturnVisible, Is.True);
            Assert.That(state.View.DetectionEnabled, Is.False);
            Assert.That(state.View.VideoSeeThrough, Is.False);
        }

        [Test]
        public void MrTurnsOnDetectionAndSeeThroughOnly()
        {
            var state = new AppModeState();
            state.EnterMr();
            Assert.That(state.Mode, Is.EqualTo(AppMode.MrWallSelection));
            Assert.That(state.View.ModePanelVisible, Is.False);
            Assert.That(state.View.GalleryVisible, Is.False);
            Assert.That(state.View.ReturnVisible, Is.True);
            Assert.That(state.View.DetectionEnabled, Is.True);
            Assert.That(state.View.VideoSeeThrough, Is.True);

            state.SelectWall();
            Assert.That(state.Mode, Is.EqualTo(AppMode.MrWallContent));
            Assert.That(state.View.DetectionEnabled, Is.True);
            Assert.That(state.View.VideoSeeThrough, Is.True);

            state.BackToWallSelection();
            Assert.That(state.Mode, Is.EqualTo(AppMode.MrWallSelection));
        }

        [Test]
        public void ReturnToPanelTurnsEverythingOff()
        {
            var state = new AppModeState();
            state.EnterMr();
            state.SelectWall();
            state.ReturnToModePanel();
            Assert.That(state.Mode, Is.EqualTo(AppMode.ModePanel));
            Assert.That(state.View.ModePanelVisible, Is.True);
            Assert.That(state.View.GalleryVisible, Is.False);
            Assert.That(state.View.ReturnVisible, Is.False);
            Assert.That(state.View.DetectionEnabled, Is.False);
            Assert.That(state.View.VideoSeeThrough, Is.False);
        }

        [Test]
        public void SelectingWallOutsideMrIsRejected()
        {
            var state = new AppModeState();
            Assert.Throws<InvalidOperationException>(state.SelectWall);
            state.EnterNonMr();
            Assert.Throws<InvalidOperationException>(state.SelectWall);
            Assert.Throws<InvalidOperationException>(state.BackToWallSelection);
        }

        [Test]
        public void ControllerAppliesAndRestoresGalleryState()
        {
            var root = new GameObject("Mode controller test");
            var panelObject = new GameObject("Mode panel");
            panelObject.transform.SetParent(root.transform);
            var panel = panelObject.AddComponent<ModePanelView>();
            var galleryObject = new GameObject("Gallery");
            galleryObject.transform.SetParent(root.transform);
            var folder = new GameObject("Gallery child");
            folder.transform.SetParent(galleryObject.transform);
            var returnObject = new GameObject("Return");
            returnObject.transform.SetParent(root.transform);
            var wallDisplayObject = new GameObject("Wall display");
            wallDisplayObject.transform.SetParent(root.transform);
            var wallDisplay = wallDisplayObject.AddComponent<WallDisplayController>();
            var runtime = new FakeModeRuntime();
            var source = new FakeWallSource();

            var controller = root.AddComponent<AppModeController>();
            controller.Configure(panel, galleryObject, returnObject, wallDisplay, runtime, source);
            controller.Initialize();

            try
            {
                panelObject.SetActive(true);
                Assert.That(panel.Visible, Is.True);
                Assert.That(galleryObject.activeSelf, Is.False);
                Assert.That(returnObject.activeSelf, Is.False);
                Assert.That(source.Enabled, Is.False);

                controller.EnterNonMrMode();
                Assert.That(panel.Visible, Is.False);
                Assert.That(galleryObject.activeSelf, Is.True);
                Assert.That(returnObject.activeSelf, Is.True);
                Assert.That(galleryObject.transform.childCount, Is.EqualTo(1));
                Assert.That(source.Enabled, Is.False);
                Assert.That(runtime.Enabled, Is.False);

                controller.EnterMrMode();
                Assert.That(galleryObject.activeSelf, Is.False);
                Assert.That(source.Enabled, Is.True);
                Assert.That(runtime.Enabled, Is.True);

                controller.SelectWall();
                Assert.That(wallDisplay.Active, Is.True);

                controller.ReturnToModePanel();
                Assert.That(panel.Visible, Is.True);
                Assert.That(galleryObject.activeSelf, Is.False);
                Assert.That(returnObject.activeSelf, Is.False);
                Assert.That(source.Enabled, Is.False);
                Assert.That(runtime.Enabled, Is.False);
                Assert.That(wallDisplay.ClearCount, Is.GreaterThan(0));
                Assert.That(galleryObject.transform.childCount, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ControllerPreservesCarouselSelectionAcrossMrRoundTrip()
        {
            const string prefabPath = "Assets/PicShow/Prefabs/FourRealmsGallery.prefab";
            var galleryPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(galleryPrefab, Is.Not.Null);

            var root = new GameObject("Mode controller round trip");
            var panelObject = new GameObject("Mode panel");
            panelObject.transform.SetParent(root.transform);
            var panel = panelObject.AddComponent<ModePanelView>();
            var galleryObject = UnityEngine.Object.Instantiate(galleryPrefab, root.transform);
            var gallery = galleryObject.GetComponent<GalleryCarousel>();
            var returnObject = new GameObject("Return");
            returnObject.transform.SetParent(root.transform);
            var wallDisplayObject = new GameObject("Wall display");
            wallDisplayObject.transform.SetParent(root.transform);
            var wallDisplay = wallDisplayObject.AddComponent<WallDisplayController>();
            var controller = root.AddComponent<AppModeController>();
            controller.Configure(panel, galleryObject, returnObject, wallDisplay,
                new FakeModeRuntime(), new FakeWallSource());

            try
            {
                controller.Initialize();
                gallery.SelectCategory(2);
                gallery.SelectWork(3);
                Assert.That(gallery.CategoryIndex, Is.EqualTo(2));
                Assert.That(gallery.WorkIndex, Is.EqualTo(3));

                controller.EnterNonMrMode();
                controller.EnterMrMode();
                controller.ReturnToModePanel();
                controller.EnterNonMrMode();

                Assert.That(gallery.CategoryIndex, Is.EqualTo(2));
                Assert.That(gallery.WorkIndex, Is.EqualTo(3));
                Assert.That(galleryObject.activeSelf, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static WallPlaneInfo MakeWall()
        {
            return new WallPlaneInfo(Guid.NewGuid(), new Vector3(0f, 1.5f, -2f),
                Vector3.forward, Vector3.right, Vector3.up, 2f, 2f);
        }

        private static PxrPlaneData MakePlaneData(bool semanticWall, bool horizontal)
        {
            return MakePlaneData(
                semanticWall ? PxrSemanticLabel.Wall : PxrSemanticLabel.Unknown, horizontal);
        }

        private static PxrPlaneData MakePlaneData(PxrSemanticLabel label, bool horizontal)
        {
            Vector3[] vertices = horizontal
                ? new[]
                {
                    new Vector3(-1f, -1f, -1f),
                    new Vector3(1f, -1f, -1f),
                    new Vector3(1f, -1f, 1f),
                    new Vector3(-1f, -1f, 1f)
                }
                : new[]
                {
                    new Vector3(-1f, -1f, -2f),
                    new Vector3(1f, -1f, -2f),
                    new Vector3(1f, 1f, -2f),
                    new Vector3(-1f, 1f, -2f)
                };
            return new PxrPlaneData
            {
                uuid = Guid.NewGuid(),
                vertices = vertices,
                indices = new ushort[] { 0, 1, 2, 0, 2, 3 },
                label = label
            };
        }

        [Test]
        public void OnlySemanticWallPlanesCreateWallGeometry()
        {
            Assert.That(PlaneDetectionManager.TryBuildWall(
                    MakePlaneData(PxrSemanticLabel.Unknown, false), Vector3.zero, out _), Is.False);
            Assert.That(PlaneDetectionManager.TryBuildWall(
                    MakePlaneData(PxrSemanticLabel.Door, false), Vector3.zero, out _), Is.False);
            Assert.That(PlaneDetectionManager.TryBuildWall(
                    MakePlaneData(PxrSemanticLabel.Window, false), Vector3.zero, out _), Is.False);
            Assert.That(PlaneDetectionManager.TryBuildWall(
                    MakePlaneData(PxrSemanticLabel.Unknown, true), Vector3.zero, out _), Is.False);
            Assert.That(PlaneDetectionManager.TryBuildWall(
                    MakePlaneData(PxrSemanticLabel.Wall, false), Vector3.zero,
                    out WallPlaneInfo wall), Is.True);
            Assert.That(wall.Width, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void PicoModeRuntimeStaysEditorSafe()
        {
            var go = new GameObject("Pico mode runtime");
            var runtime = go.AddComponent<PicoModeRuntime>();
            try
            {
                runtime.SetVideoSeeThrough(true);
                Assert.That(runtime.EditorValue, Is.True);
                Assert.That(runtime.LastValue, Is.True);
                runtime.SetVideoSeeThrough(false);
                Assert.That(runtime.EditorValue, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WallPanelUsesCompactSizeAndDeduplicatesSelect()
        {
            var root = new GameObject("Wall panel", typeof(RectTransform));
            var buttonObject = new GameObject("Select", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            var promptObject = new GameObject("Prompt", typeof(RectTransform));
            promptObject.transform.SetParent(root.transform, false);
            var prompt = promptObject.AddComponent<TextMeshProUGUI>();
            var indexObject = new GameObject("Index", typeof(RectTransform));
            indexObject.transform.SetParent(root.transform, false);
            var index = indexObject.AddComponent<TextMeshProUGUI>();
            var view = root.AddComponent<WallPanelView>();
            int selects = 0;
            WallPlaneInfo wall = MakeWall();
            try
            {
                view.Configure(wall, 1, () => selects++, buttonObject.GetComponent<Button>(), prompt, index);
                Assert.That(prompt.text, Is.EqualTo("选择该墙面显示"));
                Assert.That(index.text, Is.EqualTo("墙面 1"));
                RectTransform rect = (RectTransform)root.transform;
                Assert.That(rect.sizeDelta.x, Is.EqualTo(2000f).Within(0.01f));
                Assert.That(rect.sizeDelta.y, Is.EqualTo(2000f).Within(0.01f));
                RectTransform buttonRect = (RectTransform)buttonObject.transform;
                Assert.That(buttonRect.sizeDelta.x, Is.EqualTo(560f).Within(0.01f));
                Assert.That(buttonRect.sizeDelta.y, Is.EqualTo(180f).Within(0.01f));

                view.SelectAt(1f);
                view.SelectAt(1.1f);
                Assert.That(selects, Is.EqualTo(1), "Duplicate click events must be ignored.");
                view.ResetInteractionState();
                view.SelectAt(1.11f);
                Assert.That(selects, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WallGalleryShowsCategoriesThenFillsFiveImages()
        {
            const string catalogPath = "Assets/PicShow/Data/GalleryCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<GalleryCatalog>(catalogPath);
            Assert.That(catalog, Is.Not.Null);

            var root = new GameObject("Wall gallery", typeof(RectTransform));
            var view = root.AddComponent<WallGalleryView>();
            Button[] buttons = new Button[4];
            TMP_Text[] labels = new TMP_Text[4];
            RawImage[] slots = new RawImage[5];
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var go = new GameObject("Category " + i, typeof(RectTransform), typeof(Image), typeof(Button));
                    go.transform.SetParent(root.transform, false);
                    var label = new GameObject("Label", typeof(RectTransform));
                    label.transform.SetParent(go.transform, false);
                    buttons[i] = go.GetComponent<Button>();
                    labels[i] = label.AddComponent<TextMeshProUGUI>();
                }
                for (int i = 0; i < 5; i++)
                {
                    var go = new GameObject("Image " + i, typeof(RectTransform));
                    go.transform.SetParent(root.transform, false);
                    slots[i] = go.AddComponent<RawImage>();
                }
                var backObject = new GameObject("Back", typeof(RectTransform), typeof(Image), typeof(Button));
                backObject.transform.SetParent(root.transform, false);
                var back = backObject.GetComponent<Button>();

                int backs = 0;
                WallPlaneInfo wall = MakeWall();
                view.Configure(catalog, wall, () => backs++, buttons, labels, slots, back);

                Assert.That(slots[0].gameObject.activeSelf, Is.False);
                Assert.That(buttons[0].gameObject.activeSelf, Is.True);
                for (int i = 0; i < 4; i++)
                    Assert.That(labels[i].text, Is.EqualTo(catalog.Categories[i].Name));

                view.ShowCategory(2);
                Assert.That(view.CurrentCategory, Is.EqualTo(2));
                for (int i = 0; i < 5; i++)
                {
                    Assert.That(slots[i].gameObject.activeSelf, Is.True);
                    Assert.That(slots[i].texture, Is.SameAs(catalog.Categories[2].Works[i].Texture));
                    RectTransform rect = slots[i].rectTransform;
                    Assert.That(rect.sizeDelta.x, Is.GreaterThan(0f));
                    Assert.That(rect.sizeDelta.y, Is.GreaterThan(0f));
                    Assert.That(Mathf.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x / 2f,
                        Is.LessThanOrEqualTo(1000f + 0.01f));
                    Assert.That(Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y / 2f,
                        Is.LessThanOrEqualTo(1000f + 0.01f));
                }

                view.SelectCategoryAt(2, 1f);
                view.SelectCategoryAt(2, 1.1f);
                Assert.That(view.CurrentCategory, Is.EqualTo(2));
                view.SelectCategoryAt(3, 1.3f);
                Assert.That(view.CurrentCategory, Is.EqualTo(3));
                Assert.That(slots[0].texture, Is.SameAs(catalog.Categories[3].Works[0].Texture));
                view.BackAt(2f);
                Assert.That(backs, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PicoBridgeRelaysEnhancedClicksAndResetsInteractionState()
        {
            var root = new GameObject("Bridge root", typeof(RectTransform));
            var buttonObject = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            var button = buttonObject.GetComponent<Button>();
            var enhanced = buttonObject.AddComponent<PXR_EnhancedButton>();
            var bridge = root.AddComponent<PicoButtonClickBridge>();
            int clicks = 0;
            try
            {
                button.onClick.AddListener(() => clicks++);
                bridge.Configure(new[] { button });
                var guard = buttonObject.GetComponent<PicoButtonHoverGuard>();
                Assert.That(guard, Is.Not.Null,
                    "Every bridged button needs a hover guard.");
                enhanced.onSingleClick.Invoke();
                Assert.That(clicks, Is.EqualTo(0),
                    "A replayed click must not run while the button is not hovered.");

                guard.OnPointerEnter(new PointerEventData(null));
                enhanced.onSingleClick.Invoke();
                Assert.That(clicks, Is.EqualTo(1));
                guard.OnPointerExit(new PointerEventData(null));
                enhanced.onSingleClick.Invoke();
                Assert.That(clicks, Is.EqualTo(1),
                    "Leaving the button must stop forwarded clicks.");

                buttonObject.SetActive(false);
                buttonObject.SetActive(true);
                Assert.That(guard.Hovered, Is.False,
                    "Re-activating must clear the stale hover state.");
                enhanced.onSingleClick.Invoke();
                Assert.That(clicks, Is.EqualTo(1));

                var panelObject = new GameObject("Panel", typeof(RectTransform));
                panelObject.transform.SetParent(root.transform, false);
                var panel = panelObject.AddComponent<WallPanelView>();
                int panelClicks = 0;
                panel.Configure(MakeWall(), 1, () => panelClicks++);
                panel.SelectAt(1f);
                panel.SelectAt(1.05f);
                Assert.That(panelClicks, Is.EqualTo(1));
                bridge.NotifyInputModeChanged();
                panel.SelectAt(1.06f);
                Assert.That(panelClicks, Is.EqualTo(2),
                    "An input mode switch must reset stale duplicate windows.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private sealed class FakeModeRuntime : IPicoModeRuntime
        {
            public bool Enabled { get; private set; }
            public void SetVideoSeeThrough(bool enabled) => Enabled = enabled;
        }

        private sealed class FakeWallSource : IWallPlaneSource
        {
            public bool Enabled { get; private set; }
            public event Action<WallPlaneInfo> WallAdded;
            public event Action<WallPlaneInfo> WallUpdated;
            public event Action<Guid> WallRemoved;
            public void SetEnabled(bool enabled) => Enabled = enabled;
            public void ClearWalls() { }
        }
    }
}
