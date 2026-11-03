using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PicShow;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PicShow.Tests
{
    public class WallSurfaceDisplayTests
    {
        private readonly List<GameObject> objects = new();

        private GameObject Track(GameObject value)
        {
            objects.Add(value);
            return value;
        }

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject value in objects)
            {
                if (value != null)
                    UnityEngine.Object.DestroyImmediate(value);
            }
            objects.Clear();
        }

        private static GalleryCatalog Catalog =>
            AssetDatabase.LoadAssetAtPath<GalleryCatalog>("Assets/PicShow/Data/GalleryCatalog.asset");

        private static GameObject Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PicShow/Prefabs/" + name + ".prefab");

        private static WallPlaneInfo Wall(float centerX, float width = 1f, float height = 2f,
            Vector3? normal = null)
        {
            return Wall(Guid.NewGuid(), centerX, width, height, normal);
        }

        private static WallPlaneInfo Wall(Guid id, float centerX, float width = 1f,
            float height = 2f, Vector3? normal = null)
        {
            Vector3 direction = normal ?? Vector3.back;
            return new WallPlaneInfo(id, new Vector3(centerX, 0f, 2f), direction,
                Vector3.Cross(Vector3.up, direction).normalized, Vector3.up, width, height);
        }

        private WallDisplayController Display(out AppModeController mode)
        {
            var root = Track(new GameObject("Wall surface display"));
            var display = root.AddComponent<WallDisplayController>();
            display.Configure(null, Catalog, Prefab("WallPanel"), Prefab("WallGallery"), root.transform);
            mode = root.AddComponent<AppModeController>();
            mode.Configure(null, null, null, display, null, null);
            mode.Initialize();
            mode.EnterMrMode();
            return display;
        }

        [Test]
        public void SplitWallProducesExactlyOnePanel()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            WallPlaneInfo left = Wall(-0.5f);
            WallPlaneInfo right = Wall(0.5f);

            display.AddWall(left);
            display.AddWall(right);

            Assert.That(display.Panels.Count, Is.EqualTo(1),
                "A wall reported as two coplanar adjacent patches must show one button.");
            Assert.That(display.Panels.Values.First().Info.Width, Is.EqualTo(2f).Within(0.01f));
            Assert.That(display.Panels.Values.First().Info.Height, Is.EqualTo(2f).Within(0.01f));
        }

        [Test]
        public void CoplanarButSeparatedPatchesStaySeparate()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            display.AddWall(Wall(-1.5f, 0.5f));
            display.AddWall(Wall(1.5f, 0.5f));

            Assert.That(display.Panels.Count, Is.EqualTo(2),
                "Coplanar patches with a large gap are different walls.");
        }

        [Test]
        public void PerpendicularWallsStaySeparate()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            display.AddWall(Wall(-1f, 1f, 2f, Vector3.back));
            display.AddWall(Wall(0f, 1f, 2f, Vector3.right));

            Assert.That(display.Panels.Count, Is.EqualTo(2),
                "Walls with different normals must not be merged.");
        }

        [Test]
        public void RemovingOnePatchKeepsTheMergedPanel()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            WallPlaneInfo left = Wall(-0.5f);
            WallPlaneInfo right = Wall(0.5f);
            display.AddWall(left);
            display.AddWall(right);
            WallPanelView merged = display.Panels.Values.First();

            display.RemoveWall(left.Id);

            Assert.That(display.Panels.Count, Is.EqualTo(1));
            Assert.That(display.Panels.ContainsKey(merged.WallId), Is.True,
                "The surviving patch must keep the merged wall id to avoid button flicker.");
            Assert.That(display.Panels[merged.WallId].Info.Width, Is.EqualTo(1f).Within(0.01f));
            Assert.That(display.Panels[merged.WallId].Info.Center.x,
                Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void SplitGroupsKeepDistinctStableIds()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            var first = new Guid("00000000-0000-0000-0000-000000000001");
            var second = new Guid("00000000-0000-0000-0000-000000000002");
            var third = new Guid("00000000-0000-0000-0000-000000000003");
            display.AddWall(Wall(first, 0f));
            display.AddWall(Wall(second, 1f));
            display.AddWall(Wall(third, 2f));
            Assert.That(display.Panels.Count, Is.EqualTo(1));

            display.UpdateWall(Wall(first, 10f));

            Assert.That(display.Panels.Count, Is.EqualTo(2),
                "A split must not reuse the merged id on both new walls.");
            Assert.That(display.Panels.Values.Select(panel => panel.WallId).Distinct().Count(),
                Is.EqualTo(2));
        }

        [Test]
        public void RemovingMultipleStalePanelsDoesNotThrowWhenOneIsSelected()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            display.AddWall(Wall(-4f));
            display.AddWall(Wall(0f));
            display.AddWall(Wall(4f));
            List<Guid> keys = display.Panels.Keys.ToList();
            display.SelectWall(keys[0]);
            var merger = (WallSurfaceMerger)typeof(WallDisplayController)
                .GetField("surfaceMerger", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(display);
            merger.Remove(keys[0]);
            merger.Remove(keys[1]);

            Assert.DoesNotThrow(() => typeof(WallDisplayController)
                .GetMethod("SyncPanels", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(display, null));
            Assert.That(display.Panels.Count, Is.EqualTo(2),
                "The selected wall stays frozen while another stale panel is removed.");

            display.BackToWallSelection();

            Assert.That(display.Panels.Count, Is.EqualTo(1),
                "Returning to wall selection applies the detected removal.");
        }

        [Test]
        public void DetectedWallShowsFullWallVisualAndCompactButton()
        {
            var root = Track(UnityEngine.Object.Instantiate(Prefab("WallPanel")));
            var view = root.GetComponent<WallPanelView>();
            view.Configure(Wall(0f, 2f, 2f), 1, () => { });

            RectTransform rootRect = (RectTransform)root.transform;
            Assert.That(rootRect.sizeDelta.x, Is.EqualTo(2000f).Within(0.01f));
            Assert.That(rootRect.sizeDelta.y, Is.EqualTo(2000f).Within(0.01f));

            RectTransform buttonRect = (RectTransform)view.SelectButton.transform;
            Assert.That(buttonRect.sizeDelta.x, Is.EqualTo(560f).Within(0.01f));
            Assert.That(buttonRect.sizeDelta.y, Is.EqualTo(180f).Within(0.01f));
            Assert.That(buttonRect.anchoredPosition, Is.EqualTo(Vector2.zero));

            Transform fill = root.transform.Find("Fill");
            Assert.That(fill, Is.Not.Null, "The detected wall needs a visible highlight fill.");
            var fillImage = fill.GetComponent<Image>();
            Assert.That(fillImage, Is.Not.Null);
            Assert.That(fillImage.color.a, Is.LessThan(0.35f));
            Assert.That(fillImage.raycastTarget, Is.False);

            Transform border = root.transform.Find("Border");
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
        }

        [Test]
        public void WallSelectionButtonTextIsLargeAndFits()
        {
            var root = Track(UnityEngine.Object.Instantiate(Prefab("WallPanel")));
            var view = root.GetComponent<WallPanelView>();
            view.Configure(Wall(0f, 2f, 2f), 1, () => { });

            TMP_Text prompt = root.GetComponentsInChildren<TMP_Text>(true)
                .First(text => text.text == "选择该墙面显示");
            TMP_Text index = root.GetComponentsInChildren<TMP_Text>(true)
                .First(text => text.text.StartsWith("墙面 "));
            prompt.ForceMeshUpdate();
            index.ForceMeshUpdate();

            Assert.That(prompt.fontSize, Is.GreaterThanOrEqualTo(48f),
                "The wall prompt must be large enough to read.");
            Assert.That(index.fontSize, Is.GreaterThanOrEqualTo(24f));
            Assert.That(((RectTransform)prompt.transform).rect.height,
                Is.GreaterThanOrEqualTo(prompt.fontSize));
            Assert.That(prompt.preferredWidth,
                Is.LessThanOrEqualTo(((RectTransform)prompt.transform).rect.width + 1f),
                "The wall prompt must not be clipped horizontally.");
            Assert.That(prompt.preferredHeight,
                Is.LessThanOrEqualTo(((RectTransform)prompt.transform).rect.height + 1f),
                "The wall prompt must not be clipped vertically.");
            Assert.That(index.preferredWidth,
                Is.LessThanOrEqualTo(((RectTransform)index.transform).rect.width + 1f));
        }

        [Test]
        public void WallCategoryLabelsScaleWithWallSize()
        {
            var root = Track(UnityEngine.Object.Instantiate(Prefab("WallGallery")));
            var view = root.GetComponent<WallGalleryView>();
            view.Configure(Catalog, Wall(0f, 2.6f, 2.6f), () => { });

            TMP_Text label = view.CategoryButtons[0].GetComponentInChildren<TMP_Text>(true);
            label.ForceMeshUpdate();
            Assert.That(label.fontSize, Is.GreaterThanOrEqualTo(48f),
                "Wall category labels must scale up with the category button.");
            Assert.That(label.preferredWidth,
                Is.LessThanOrEqualTo(((RectTransform)label.transform).rect.width + 1f));

            Button back = root.GetComponentsInChildren<Button>(true)
                .First(button => button.name == "Change wall");
            TMP_Text backLabel = back.GetComponentInChildren<TMP_Text>(true);
            backLabel.ForceMeshUpdate();
            Assert.That(backLabel.fontSize, Is.GreaterThanOrEqualTo(32f));
            Assert.That(backLabel.preferredWidth,
                Is.LessThanOrEqualTo(((RectTransform)backLabel.transform).rect.width + 1f));
        }

        [Test]
        public void SelectedWallGeometryFreezesUntilBackToSelection()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            Guid id = Guid.NewGuid();
            display.AddWall(Wall(id, 0f, 2f, 2f));
            WallPanelView panel = display.Panels[id];
            display.SelectWall(id);
            Assert.That(display.Gallery.Wall.Width, Is.EqualTo(2f).Within(0.01f));

            display.UpdateWall(Wall(id, 5f, 4f, 3f));

            Assert.That(panel.Info.Width, Is.EqualTo(2f).Within(0.01f),
                "The selected wall must keep its frozen width.");
            Assert.That(display.Gallery.Wall.Width, Is.EqualTo(2f).Within(0.01f),
                "The selected wall content must keep its frozen layout.");
            Assert.That(panel.Info.Center.x, Is.EqualTo(0f).Within(0.01f));

            display.BackToWallSelection();

            Assert.That(display.Gallery, Is.Null);
            Assert.That(panel.Info.Width, Is.EqualTo(4f).Within(0.01f),
                "Returning to wall selection must apply the latest detected size.");
            Assert.That(panel.Info.Center.x, Is.EqualTo(5f).Within(0.01f));
        }

        [Test]
        public void SelectedWallRemovalWaitsUntilBackToSelection()
        {
            AppModeController mode;
            WallDisplayController display = Display(out mode);
            Guid id = Guid.NewGuid();
            display.AddWall(Wall(id, 0f, 2f, 2f));
            display.SelectWall(id);

            display.RemoveWall(id);

            Assert.That(display.Panels.ContainsKey(id), Is.True,
                "The selected wall must stay frozen while content is shown.");
            Assert.That(display.Gallery, Is.Not.Null);

            display.BackToWallSelection();

            Assert.That(display.Panels.Count, Is.EqualTo(0),
                "Returning to wall selection must apply the detected removal.");
            Assert.That(display.Gallery, Is.Null);
        }

        [Test]
        public void SelectionButtonUsesCompactMaximumSize()
        {
            var root = Track(new GameObject("Wall panel", typeof(RectTransform)));
            var view = root.AddComponent<WallPanelView>();
            var buttonObject = new GameObject("Select", typeof(RectTransform),
                typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            var button = buttonObject.GetComponent<Button>();

            view.Configure(Wall(0f, 2f, 2f), 1, () => { }, button, null, null);

            RectTransform rect = (RectTransform)button.transform;
            Assert.That(rect.sizeDelta.x, Is.EqualTo(560f).Within(0.01f));
            Assert.That(rect.sizeDelta.y, Is.EqualTo(180f).Within(0.01f));
        }

        [Test]
        public void SelectionButtonNeverExceedsSmallWall()
        {
            var root = Track(new GameObject("Wall panel", typeof(RectTransform)));
            var view = root.AddComponent<WallPanelView>();
            var buttonObject = new GameObject("Select", typeof(RectTransform),
                typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            var button = buttonObject.GetComponent<Button>();

            view.Configure(Wall(0f, 0.06f, 0.06f), 1, () => { }, button, null, null);

            RectTransform rect = (RectTransform)button.transform;
            Assert.That(rect.sizeDelta.x, Is.LessThanOrEqualTo(60f + 0.01f));
            Assert.That(rect.sizeDelta.y, Is.LessThanOrEqualTo(60f + 0.01f));
            Assert.That(rect.sizeDelta.x, Is.GreaterThan(0f));
            Assert.That(rect.sizeDelta.y, Is.GreaterThan(0f));
        }
    }
}
