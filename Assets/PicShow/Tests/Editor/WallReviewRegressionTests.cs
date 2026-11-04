using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using ByteDance.PICO.XR;
using NUnit.Framework;
using PicShow;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR;

namespace PicShow.Tests
{
    public sealed class DeferredPlaneSource : PlaneDetectionManager
    {
        public readonly Queue<TaskCompletionSource<PxrResult>> Starts = new();
        public readonly List<string> Calls = new();
        public bool NativeRunning;
        public PxrResult StopResult = PxrResult.SUCCESS;

        protected override async Task<PxrResult> StartProviderAsync()
        {
            Calls.Add("start");
            var completion = new TaskCompletionSource<PxrResult>();
            Starts.Enqueue(completion);
            PxrResult result = await completion.Task;
            NativeRunning = result == PxrResult.SUCCESS;
            return result;
        }

        protected override PxrResult StopProvider()
        {
            Calls.Add("stop");
            if (StopResult == PxrResult.SUCCESS) NativeRunning = false;
            return StopResult;
        }
    }

    public class WallReviewRegressionTests
    {
        private readonly List<GameObject> objects = new();

        private GameObject Track(GameObject value) { objects.Add(value); return value; }
        private static GalleryCatalog Catalog =>
            AssetDatabase.LoadAssetAtPath<GalleryCatalog>("Assets/PicShow/Data/GalleryCatalog.asset");
        private static GameObject Prefab(string name) =>
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PicShow/Prefabs/" + name + ".prefab");
        private static WallPlaneInfo Wall(float width = 2f, float height = 2f) =>
            WallAt(0f, width, height);

        private static WallPlaneInfo WallAt(float centerX, float width = 2f, float height = 2f) =>
            new(Guid.NewGuid(), new Vector3(centerX, 0, 2), Vector3.back,
                Vector3.left, Vector3.up, width, height);

        [TearDown]
        public void Cleanup()
        {
            foreach (GameObject value in objects)
                if (value != null) UnityEngine.Object.DestroyImmediate(value);
            objects.Clear();
        }

        [Test]
        public void PlaneLocalVerticesUseTranslatedRotatedPose()
        {
            var data = new PxrPlaneData
            {
                uuid = Guid.NewGuid(),
                position = new Vector3(3, 2, 4),
                rotation = Quaternion.Euler(0, 90, 0),
                vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0), new Vector3(-1, 1, 0) },
                indices = new ushort[] { 0, 1, 2, 0, 2, 3 },
                label = PxrSemanticLabel.Wall
            };
            Assert.That(PlaneDetectionManager.TryBuildWall(data, new Vector3(5, 2, 4),
                out WallPlaneInfo wall), Is.True);
            Assert.That(Vector3.Distance(wall.Center, new Vector3(3, 2, 4)), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(wall.Normal, Vector3.right), Is.GreaterThan(0.999f));
            Assert.That(wall.Width, Is.EqualTo(2).Within(0.001f));
        }

        [Test]
        public void PlaneEventsUseTrackingOriginWithoutApplyingHeadPoseTwice()
        {
            var origin = Track(new GameObject("Tracking origin"));
            origin.transform.SetPositionAndRotation(new Vector3(10, 1, -3), Quaternion.Euler(0, 90, 0));
            origin.transform.localScale = Vector3.one * 2;
            var camera = Track(new GameObject("Tracked camera")).AddComponent<Camera>();
            camera.transform.SetParent(origin.transform, false);
            camera.transform.localPosition = new Vector3(5, 2, 4);
            camera.transform.localRotation = Quaternion.Euler(10, 20, 0);
            var source = Track(new GameObject("Plane source")).AddComponent<DeferredPlaneSource>();
            typeof(PlaneDetectionManager).GetField("_camera", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(source, camera.transform);
            var data = new PxrPlaneData
            {
                uuid = Guid.NewGuid(), position = new Vector3(3, 2, 4),
                rotation = Quaternion.Euler(0, 90, 0), label = PxrSemanticLabel.Wall,
                vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0),
                    new Vector3(1, 1, 0), new Vector3(-1, 1, 0) },
                indices = new ushort[] { 0, 1, 2, 0, 2, 3 }, state = MeshChangeState.Added
            };
            WallPlaneInfo emitted = default;
            source.WallAdded += wall => emitted = wall;
            typeof(PlaneDetectionManager).GetMethod("PlaneUpdateQueue",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(source, new object[] { data });
            Assert.That(Vector3.Distance(emitted.Center, new Vector3(18, 5, -9)), Is.LessThan(0.001f));
            Assert.That(Vector3.Dot(emitted.Normal, Vector3.back), Is.GreaterThan(0.999f));
            Assert.That(emitted.Width, Is.EqualTo(4).Within(0.001f));
            Assert.That(emitted.Height, Is.EqualTo(4).Within(0.001f));
        }

        [Test]
        public void PendingRemovalOverridesEarlierUpdate()
        {
            var source = Track(new GameObject("Plane source")).AddComponent<DeferredPlaneSource>();
            Guid id = Guid.NewGuid();

            DeliverPlaneChanges(source,
                new PxrPlaneData { uuid = id, state = MeshChangeState.Updated });
            DeliverPlaneChanges(source,
                new PxrPlaneData { uuid = id, state = MeshChangeState.Removed });

            Assert.That(PendingPlaneChanges(source), Contains.Key(id));
            Assert.That(PendingPlaneChanges(source)[id].state, Is.EqualTo(MeshChangeState.Removed));
        }

        [Test]
        public void UnchangedDoesNotDiscardPendingUpdate()
        {
            var source = Track(new GameObject("Plane source")).AddComponent<DeferredPlaneSource>();
            Guid id = Guid.NewGuid();

            DeliverPlaneChanges(source,
                new PxrPlaneData { uuid = id, state = MeshChangeState.Updated });
            DeliverPlaneChanges(source,
                new PxrPlaneData { uuid = id, state = MeshChangeState.Unchanged });

            Assert.That(PendingPlaneChanges(source), Contains.Key(id));
            Assert.That(PendingPlaneChanges(source)[id].state, Is.EqualTo(MeshChangeState.Updated));
        }

        private static void DeliverPlaneChanges(PlaneDetectionManager source,
            params PxrPlaneData[] changes)
        {
            typeof(PlaneDetectionManager).GetProperty(nameof(PlaneDetectionManager.DetectionEnabled))
                .SetValue(source, true);
            typeof(PlaneDetectionManager).GetMethod("PXR_OnPlaneDetectionDataUpdated",
                    BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(source, new object[] { new List<PxrPlaneData>(changes) });
        }

        private static Dictionary<Guid, PxrPlaneData> PendingPlaneChanges(
            PlaneDetectionManager source) =>
            (Dictionary<Guid, PxrPlaneData>)typeof(PlaneDetectionManager)
                .GetField("needUpdatePlaneList", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(source);

        [TestCase(false)]
        [TestCase(true)]
        public void LateWallCannotReplaceContentUntilBackToSelection(bool staleClick)
        {
            var root = Track(new GameObject("Mode regression"));
            var display = root.AddComponent<WallDisplayController>();
            display.Configure(null, Catalog, Prefab("WallPanel"), Prefab("WallGallery"), root.transform);
            var mode = root.AddComponent<AppModeController>();
            mode.Configure(null, null, null, display, null, null);
            mode.Initialize();
            mode.EnterMrMode();
            WallPlaneInfo first = Wall();
            WallPlaneInfo second = WallAt(3f);
            display.AddWall(first);
            display.SelectWall(first.Id);
            WallGalleryView content = display.Gallery;
            display.AddWall(second);
            if (!staleClick)
                Assert.That(display.Panels[second.Id].gameObject.activeSelf, Is.False);
            Assert.DoesNotThrow(() => display.SelectWall(second.Id));
            Assert.That(display.SelectedWall, Is.EqualTo(first.Id));
            Assert.That(display.Gallery, Is.SameAs(content));
            Assert.That(mode.Mode, Is.EqualTo(AppMode.MrWallContent));
            display.BackToWallSelection();
            Assert.That(display.Panels[second.Id].gameObject.activeSelf, Is.True);
            display.SelectWall(second.Id);
            Assert.That(display.SelectedWall, Is.EqualTo(second.Id));
        }

        private WallGalleryView Gallery(WallPlaneInfo wall)
        {
            var root = Track(UnityEngine.Object.Instantiate(Prefab("WallGallery")));
            var view = root.GetComponent<WallGalleryView>();
            view.Configure(Catalog, wall, () => { });
            return view;
        }

        [TestCase(2f, 2f)]
        [TestCase(1f, 0.06f)]
        public void BackAndCategoriesHaveDisjointClickableRects(float width, float height)
        {
            var view = Gallery(Wall(width, height));
            Canvas.ForceUpdateCanvases();
            Button[] buttons = view.GetComponentsInChildren<Button>();
            for (int i = 0; i < buttons.Length; i++)
                for (int j = i + 1; j < buttons.Length; j++)
                    Assert.That(Bounds(buttons[i]).Overlaps(Bounds(buttons[j])), Is.False,
                        buttons[i].name + " overlaps " + buttons[j].name);
        }

        private static Rect Bounds(Button button)
        {
            var rect = (RectTransform)button.transform;
            return new Rect(rect.anchoredPosition - rect.sizeDelta / 2, rect.sizeDelta);
        }

        [TestCase(1f, 0.06f)]
        [TestCase(0.06f, 1f)]
        public void AllFiveImagesStayInsideSmallAcceptedWall(float width, float height)
        {
            var view = Gallery(Wall(width, height));
            for (int category = 0; category < 4; category++)
            {
                view.ShowCategory(category);
                foreach (RawImage image in view.ImageSlots)
                {
                    RectTransform rect = image.rectTransform;
                    Assert.That(Mathf.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x / 2,
                        Is.LessThanOrEqualTo(width * 500 + 0.001f));
                    Assert.That(Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y / 2,
                        Is.LessThanOrEqualTo(height * 500 + 0.001f));
                    Assert.That(rect.sizeDelta.x / rect.sizeDelta.y,
                        Is.EqualTo((float)image.texture.width / image.texture.height).Within(0.001f));
                }
            }
        }

        [UnityTest]
        public IEnumerator ExitWhileStartingStopsTheLateProvider()
        {
            var source = Track(new GameObject("Deferred provider")).AddComponent<DeferredPlaneSource>();
            source.SetEnabled(true);
            source.SetEnabled(false);
            source.Starts.Dequeue().SetResult(PxrResult.SUCCESS);
            yield return null;
            yield return null;
            Assert.That(source.NativeRunning, Is.False, "A late start must be stopped after it completes.");
            Assert.That(source.DetectionEnabled, Is.False);
            Assert.That(source.Calls, Is.EqualTo(new[] { "start", "stop" }));
        }

        [UnityTest]
        public IEnumerator ReenterWhileStartingDoesNotStartASecondProvider()
        {
            var source = Track(new GameObject("Deferred provider")).AddComponent<DeferredPlaneSource>();
            source.SetEnabled(true);
            source.SetEnabled(false);
            source.SetEnabled(true);
            // Complete every request so a failed assertion cannot strand a continuation.
            while (source.Starts.Count > 0) source.Starts.Dequeue().SetResult(PxrResult.SUCCESS);
            yield return null;
            yield return null;
            Assert.That(source.Calls, Is.EqualTo(new[] { "start" }));
            Assert.That(source.DetectionEnabled, Is.True);
            source.SetEnabled(false);
            Assert.That(source.NativeRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator FailedStartCanBeRetried()
        {
            var source = Track(new GameObject("Deferred provider")).AddComponent<DeferredPlaneSource>();
            source.SetEnabled(true);
            source.Starts.Dequeue().SetResult(PxrResult.ERROR_PERMISSION_INSUFFICIENT);
            yield return null;
            yield return null;
            Assert.That(source.DetectionEnabled, Is.False);
            source.SetEnabled(true);
            Assert.That(source.Starts.Count, Is.EqualTo(1));
            source.Starts.Dequeue().SetResult(PxrResult.SUCCESS);
            yield return null;
            yield return null;
            Assert.That(source.DetectionEnabled, Is.True);
        }

        [UnityTest]
        public IEnumerator DestroyWhileStartingStillStopsLateCompletion()
        {
            var source = Track(new GameObject("Deferred provider")).AddComponent<DeferredPlaneSource>();
            source.SetEnabled(true);
            var pending = source.Starts.Dequeue();
            UnityEngine.Object.DestroyImmediate(source.gameObject);
            pending.SetResult(PxrResult.SUCCESS);
            yield return null;
            yield return null;
            Assert.That(source.NativeRunning, Is.False);
            Assert.That(source.Calls, Is.EqualTo(new[] { "start", "stop" }));
        }

        [UnityTest]
        public IEnumerator ReenterAfterFailedStopRestoresDelivery()
        {
            var source = Track(new GameObject("Deferred provider")).AddComponent<DeferredPlaneSource>();
            source.SetEnabled(true);
            source.Starts.Dequeue().SetResult(PxrResult.SUCCESS);
            yield return null;
            yield return null;
            source.StopResult = PxrResult.ERROR_PERMISSION_INSUFFICIENT;
            source.SetEnabled(false);
            source.SetEnabled(true);
            source.StopResult = PxrResult.SUCCESS;
            Assert.That(source.DetectionEnabled, Is.True);
            Assert.That(source.Calls, Is.EqualTo(new[] { "start", "stop" }));
        }

        [UnityTest]
        public IEnumerator ReplacementWaitsForDestroyedInstancesPendingStartAndStop()
        {
            var first = Track(new GameObject("First provider")).AddComponent<DeferredPlaneSource>();
            first.SetEnabled(true);
            var firstStart = first.Starts.Dequeue();
            UnityEngine.Object.DestroyImmediate(first.gameObject);
            var second = Track(new GameObject("Second provider")).AddComponent<DeferredPlaneSource>();
            second.SetEnabled(true);
            bool overlapped = second.Starts.Count != 0;
            firstStart.SetResult(PxrResult.SUCCESS);
            for (int i = 0; i < 10 && second.Starts.Count == 0; i++) yield return null;
            Assert.That(second.Starts.Count, Is.EqualTo(1));
            second.Starts.Dequeue().SetResult(PxrResult.SUCCESS);
            yield return null;
            yield return null;
            Assert.That(overlapped, Is.False, "Replacement must not race the previous native start.");
            Assert.That(first.Calls, Is.EqualTo(new[] { "start", "stop" }));
            Assert.That(second.NativeRunning, Is.True);
            Assert.That(second.DetectionEnabled, Is.True);
        }
    }
}
