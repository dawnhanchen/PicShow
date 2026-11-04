using System.Linq;
using System.Reflection;
using ByteDance.PICO.Interaction;
using ByteDance.PICO.XR;
using NUnit.Framework;
using PicShow.PicoInteraction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unity.XR.CoreUtils;

namespace PicShow.Tests
{
    public class GalleryInteractionAssetTests
    {
        private const string PrefabPath = "Assets/PicShow/Prefabs/FourRealmsGallery.prefab";

        [Test]
        public void SavedPrefabUsesPicoEnhancedButtonsAndOneTrackedRaycaster()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var gallery = prefab.GetComponent<GalleryCarousel>();
            var interactiveButtons = gallery.Tabs.Select(tab => tab.Button)
                .Concat(gallery.Dots.Select(dot => dot.Button))
                .Concat(gallery.Cards.Select(card => card.Button))
                .ToArray();

            Assert.That(prefab.transform.Find("Previous"), Is.Null,
                "The left arrow button must not be part of the gallery.");
            Assert.That(prefab.transform.Find("Next"), Is.Null,
                "The right arrow button must not be part of the gallery.");
            Assert.That(prefab.GetComponent<GalleryPicoInteractionAdapter>(), Is.Not.Null);
            Assert.That(interactiveButtons, Has.Length.EqualTo(12));
            Assert.That(interactiveButtons.All(button =>
                button.GetComponent<PXR_EnhancedButton>() != null), Is.True);
            Assert.That(prefab.GetComponentsInChildren<TrackedDeviceGraphicRaycaster>(true),
                Has.Length.EqualTo(1));

            var controller = prefab.GetComponent<GalleryControllerNavigator>();
            var serializedController = new SerializedObject(controller);
            var left = (InputActionReference)serializedController.FindProperty("leftMove")
                .objectReferenceValue;
            var right = (InputActionReference)serializedController.FindProperty("rightMove")
                .objectReferenceValue;
            Assert.That(left.action.actionMap.name, Is.EqualTo("XRI Left Locomotion"));
            Assert.That(left.action.name, Is.EqualTo("Move"));
            Assert.That(right.action.actionMap.name, Is.EqualTo("XRI Right Locomotion"));
            Assert.That(right.action.name, Is.EqualTo("Move"));

            Transform stage = prefab.transform.Find("Three card stage");
            Assert.That(stage.GetComponent<PXR_SwipeFrame>(), Is.Null,
                "GallerySwipeNavigator must own swipe state so it can release global ownership.");
            Assert.That(stage.GetComponent<GallerySwipeNavigator>(), Is.Not.Null);
            Assert.That(stage.GetComponent<Image>().raycastTarget, Is.True);
        }

        [Test]
        public void EveryCardKeepsPicoBindingAfterCarouselRotation()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var gallery = prefab.GetComponent<GalleryCarousel>();
            var adapter = prefab.GetComponent<GalleryPicoInteractionAdapter>();
            var property = new SerializedObject(adapter).FindProperty("buttons");
            Button[] bound = Enumerable.Range(0, property.arraySize)
                .Select(index => property.GetArrayElementAtIndex(index).objectReferenceValue)
                .OfType<Button>()
                .ToArray();

            Assert.That(gallery.Cards.All(card => card.Button.GetComponent<PXR_EnhancedButton>() != null),
                Is.True,
                "Every card slot must keep the PICO enhanced button so side cards stay pinchable after rotation.");
            Assert.That(gallery.Cards.All(card => bound.Contains(card.Button)), Is.True,
                "Every card slot must stay registered with the PICO interaction adapter after rotation.");

            var instance = Object.Instantiate(prefab);
            try
            {
                var runtimeGallery = instance.GetComponent<GalleryCarousel>();
                runtimeGallery.RefreshPreview();
                runtimeGallery.Move(1);
                Assert.That(runtimeGallery.Cards.All(card =>
                        card.Button.GetComponent<PXR_EnhancedButton>() != null), Is.True,
                    "Rotation must not leave a side card without the PICO enhanced button.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void EnhancedClickRequiresHoverFromCurrentActivation()
        {
            var root = new GameObject("Gallery click guard test");
            var buttonObject = new GameObject("Button");
            buttonObject.transform.SetParent(root.transform);
            try
            {
                var button = buttonObject.AddComponent<Button>();
                var enhanced = buttonObject.AddComponent<PXR_EnhancedButton>();
                var adapter = root.AddComponent<GalleryPicoInteractionAdapter>();
                int clickCount = 0;
                button.onClick.AddListener(() => clickCount++);
                adapter.Configure(new[] { button });

                enhanced.onSingleClick.Invoke();
                Assert.That(clickCount, Is.Zero,
                    "An enhanced click without a current hover must be discarded.");

                var guard = button.GetComponent<PicoButtonHoverGuard>();
                Assert.That(guard, Is.Not.Null);
                guard.OnPointerEnter(new PointerEventData(null));
                enhanced.onSingleClick.Invoke();
                Assert.That(clickCount, Is.EqualTo(1));

                adapter.Configure(new[] { button });
                enhanced.onSingleClick.Invoke();
                Assert.That(clickCount, Is.EqualTo(1),
                    "Rebinding must require a new pointer enter before accepting clicks.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void InputModeChangeRequiresFreshHoverBeforeEnhancedClick()
        {
            var instance = Object.Instantiate(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            var managerObject = new GameObject("PICO input mode manager");
            try
            {
                var manager = managerObject.AddComponent<PXR_InteractorManager>();
                var gallery = instance.GetComponent<GalleryCarousel>();
                Button button = gallery.Tabs[0].Button;
                var enhanced = button.GetComponent<PXR_EnhancedButton>();
                var guard = button.GetComponent<PicoButtonHoverGuard>();
                var adapter = instance.GetComponent<GalleryPicoInteractionAdapter>();
                int clickCount = 0;
                button.onClick.AddListener(() => clickCount++);
                adapter.Configure(new[] { button });

                manager.CurrentInputMode = XRInputModalityManager.InputMode.TrackedHand;
                guard.OnPointerEnter(new PointerEventData(null));
                enhanced.onSingleClick.Invoke();
                Assert.That(clickCount, Is.EqualTo(1),
                    "The positive control must prove the enhanced listener is bound.");

                manager.CurrentInputMode = XRInputModalityManager.InputMode.MotionController;
                enhanced.onSingleClick.Invoke();

                Assert.That(clickCount, Is.EqualTo(1),
                    "A modality switch must invalidate hover from the previous pointer.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void ControllerNavigatorUsesMotionControllerModeAndAxisLatch()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                var gallery = instance.GetComponent<GalleryCarousel>();
                var navigator = instance.AddComponent<GalleryControllerNavigator>();
                navigator.Configure(gallery, null, null);
                gallery.RefreshPreview();

                Assert.That(navigator.ProcessAxes(0.8f, 0f, false, 1f), Is.False);
                Assert.That(gallery.WorkIndex, Is.Zero);
                Assert.That(navigator.ProcessAxes(0.8f, 0f, true, 1f), Is.True);
                Assert.That(gallery.WorkIndex, Is.EqualTo(1));
                Assert.That(navigator.ProcessAxes(1f, 0f, true, 1.1f), Is.False);
                Assert.That(navigator.ProcessAxes(0f, 0f, true, 1.2f), Is.False);
                Assert.That(navigator.ProcessAxes(-0.8f, 0f, true, 1.3f), Is.True);
                Assert.That(gallery.WorkIndex, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void EyeInteractionModeAdapterDisablesAimForControllersAndRestoresItForHands()
        {
            var root = new GameObject("PICO interaction mode test");
            var aimRoot = new GameObject("AimInteractor");
            aimRoot.transform.SetParent(root.transform);
            try
            {
                var manager = root.AddComponent<PXR_InteractorManager>();
                var aimInteractor = aimRoot.AddComponent<PXR_AimInteractor>();
                var adapter = root.AddComponent<GalleryEyeInteractionModeAdapter>();
                adapter.Configure(manager, aimInteractor);

                manager.motionControllerModeStarted();
                Assert.That(aimRoot.activeSelf, Is.False,
                    "Controller mode must hide the eye cursor and stop eye-driven UI hover.");

                manager.trackedHandModeStarted();
                Assert.That(aimRoot.activeSelf, Is.True,
                    "Hand mode must restore the eye cursor and eye-driven UI hover.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SwipeNavigatorCommitsOneMovePerGesture()
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
            try
            {
                var gallery = instance.GetComponent<GalleryCarousel>();
                var navigator = instance.AddComponent<GallerySwipeNavigator>();
                navigator.Configure(gallery, 0.1f);
                gallery.RefreshPreview();

                navigator.BeginSwipe(0f);
                navigator.UpdateSwipe(-0.15f);
                Assert.That(navigator.EndSwipe(-0.15f, 1f), Is.True);
                Assert.That(gallery.WorkIndex, Is.EqualTo(1));
                Assert.That(navigator.EndSwipe(-0.3f, 1.1f), Is.False);
                Assert.That(gallery.WorkIndex, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void SwipeNavigatorIgnoresPinchOutsideHoveredSurface()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool closeScene = !scene.isLoaded;
            if (closeScene)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            GameObject instance = null;
            PXR_InteractorManager manager = null;
            XRInputModalityManager.InputMode previousMode = XRInputModalityManager.InputMode.None;
            var dataProperty = typeof(PXR_InteractorManager).GetProperty(
                "InteractionData",
                BindingFlags.Public | BindingFlags.Static);
            object previousData = dataProperty.GetValue(null);
            try
            {
                manager = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PXR_InteractorManager>(true))
                    .Single();
                previousMode = manager.CurrentInputMode;
                if (previousData == null)
                    dataProperty.SetValue(
                        null,
                        System.Activator.CreateInstance(dataProperty.PropertyType));

                instance = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                var navigator = instance.GetComponentInChildren<GallerySwipeNavigator>();
                manager.CurrentInputMode = XRInputModalityManager.InputMode.TrackedHand;
                manager.SetScrollObject(null);

                typeof(GallerySwipeNavigator).GetMethod(
                    "OnPinchDown",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(navigator, new object[] { InteractorHandedness.Left });

                var activeHand = (InteractorHandedness)typeof(GallerySwipeNavigator).GetField(
                    "activeHand",
                    BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(navigator);
                Assert.That(activeHand, Is.EqualTo(InteractorHandedness.None));
                Assert.That(ScrollObject(), Is.Null,
                    "A global pinch outside the gallery surface must not claim swipe ownership.");
            }
            finally
            {
                if (manager != null)
                {
                    manager.SetScrollObject(null);
                    manager.CurrentInputMode = previousMode;
                }
                dataProperty.SetValue(null, previousData);
                if (instance != null)
                    Object.DestroyImmediate(instance);
                if (closeScene)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        [Test]
        public void SwipeNavigatorReleasesGlobalOwnershipOnDisable()
        {
            WithSwipeNavigator((manager, navigator) =>
            {
                manager.SetScrollObject(navigator.transform);
                manager.CurrentAimUpdateType = AimUpdateType.UIScrollbyLeft;

                typeof(GallerySwipeNavigator).GetMethod(
                        "OnDisable", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(navigator, null);

                Assert.That(ScrollObject(), Is.Null);
                Assert.That(manager.CurrentAimUpdateType, Is.EqualTo(AimUpdateType.EyeGaze));
            });
        }

        [Test]
        public void SwipeNavigatorReleasesGlobalOwnershipWhenInputModeChanges()
        {
            WithSwipeNavigator((manager, navigator) =>
            {
                manager.CurrentInputMode = XRInputModalityManager.InputMode.TrackedHand;
                manager.SetScrollObject(navigator.transform);
                manager.CurrentAimUpdateType = AimUpdateType.UIScrollbyLeft;

                manager.CurrentInputMode = XRInputModalityManager.InputMode.MotionController;

                Assert.That(ScrollObject(), Is.Null);
                Assert.That(manager.CurrentAimUpdateType, Is.EqualTo(AimUpdateType.EyeGaze));
            });
        }

        [Test]
        public void SwipeNavigatorRequiresFreshHoverAfterInputModeChange()
        {
            WithSwipeNavigator((manager, navigator) =>
            {
                manager.CurrentInputMode = XRInputModalityManager.InputMode.TrackedHand;
                var pointer = (IPointerEnterHandler)navigator;
                pointer.OnPointerEnter(new PointerEventData(null));

                manager.CurrentInputMode = XRInputModalityManager.InputMode.MotionController;
                manager.CurrentInputMode = XRInputModalityManager.InputMode.TrackedHand;
                InvokePinch(navigator, "OnPinchDown", InteractorHandedness.Left);

                Assert.That(ActiveHand(navigator), Is.EqualTo(InteractorHandedness.None));
                Assert.That(ScrollObject(), Is.Null,
                    "A modality switch must require a new pointer enter before swipe ownership.");
                Assert.That(manager.CurrentAimUpdateType, Is.EqualTo(AimUpdateType.EyeGaze));
            });
        }

        [Test]
        public void SecondHandTakesSwipeOwnership()
        {
            WithSwipeNavigator((manager, navigator) =>
            {
                manager.CurrentInputMode = XRInputModalityManager.InputMode.TrackedHand;
                var pointer = navigator.GetComponent(typeof(IPointerEnterHandler))
                    as IPointerEnterHandler;
                Assert.That(pointer, Is.SameAs(navigator),
                    "The local navigator must own hover before accepting pinch gestures.");
                pointer.OnPointerEnter(new PointerEventData(null));

                InvokePinch(navigator, "OnPinchDown", InteractorHandedness.Left);
                Assert.That(ActiveHand(navigator), Is.EqualTo(InteractorHandedness.Left));
                Assert.That(manager.CurrentAimUpdateType, Is.EqualTo(AimUpdateType.UIScrollbyLeft));
                Assert.That(ScrollObject(),
                    Is.EqualTo(navigator.transform));

                InvokePinch(navigator, "OnPinchDown", InteractorHandedness.Right);
                Assert.That(ActiveHand(navigator), Is.EqualTo(InteractorHandedness.Right));
                Assert.That(manager.CurrentAimUpdateType, Is.EqualTo(AimUpdateType.UIScrollbyRight));

                InvokePinch(navigator, "OnPinchUp", InteractorHandedness.Left);
                Assert.That(ActiveHand(navigator), Is.EqualTo(InteractorHandedness.Right));
                Assert.That(ScrollObject(),
                    Is.EqualTo(navigator.transform));

                InvokePinch(navigator, "OnPinchUp", InteractorHandedness.Right);
                Assert.That(ScrollObject(), Is.Null);
                Assert.That(manager.CurrentAimUpdateType, Is.EqualTo(AimUpdateType.EyeGaze));
            });
        }

        private static void WithSwipeNavigator(
            System.Action<PXR_InteractorManager, GallerySwipeNavigator> test)
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool closeScene = !scene.isLoaded;
            if (closeScene)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            GameObject instance = null;
            PXR_InteractorManager manager = null;
            XRInputModalityManager.InputMode previousMode = XRInputModalityManager.InputMode.None;
            AimUpdateType previousAim = AimUpdateType.EyeGaze;
            var dataProperty = typeof(PXR_InteractorManager).GetProperty(
                "InteractionData", BindingFlags.Public | BindingFlags.Static);
            object previousData = dataProperty.GetValue(null);
            try
            {
                manager = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<PXR_InteractorManager>(true))
                    .Single();
                previousMode = manager.CurrentInputMode;
                previousAim = manager.CurrentAimUpdateType;
                if (dataProperty.GetValue(null) == null)
                    dataProperty.SetValue(
                        null,
                        System.Activator.CreateInstance(dataProperty.PropertyType));

                instance = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
                var navigator = instance.GetComponentInChildren<GallerySwipeNavigator>();
                InvokeLifecycle(navigator, "OnDisable");
                InvokeLifecycle(navigator, "OnEnable");
                manager.SetScrollObject(null);
                test(manager, navigator);
            }
            finally
            {
                if (instance != null)
                {
                    var navigator = instance.GetComponentInChildren<GallerySwipeNavigator>();
                    InvokeLifecycle(navigator, "OnDisable");
                }
                if (manager != null)
                {
                    manager.SetScrollObject(null);
                    manager.CurrentAimUpdateType = previousAim;
                    manager.CurrentInputMode = previousMode;
                }
                dataProperty.SetValue(null, previousData);
                if (instance != null)
                    Object.DestroyImmediate(instance);
                if (closeScene)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void InvokePinch(GallerySwipeNavigator navigator, string method,
            InteractorHandedness hand) =>
            typeof(GallerySwipeNavigator).GetMethod(
                    method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(navigator, new object[] { hand });

        private static InteractorHandedness ActiveHand(GallerySwipeNavigator navigator) =>
            (InteractorHandedness)typeof(GallerySwipeNavigator).GetField(
                    "activeHand", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(navigator);

        private static void InvokeLifecycle(GallerySwipeNavigator navigator, string method) =>
            typeof(GallerySwipeNavigator).GetMethod(
                    method, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(navigator, null);

        private static Transform ScrollObject()
        {
            var dataProperty = typeof(PXR_InteractorManager).GetProperty(
                "InteractionData", BindingFlags.Public | BindingFlags.Static);
            object data = dataProperty.GetValue(null);
            return (Transform)data.GetType().GetField("_scrollObject").GetValue(data);
        }

        [Test]
        public void SavedSceneUsesOneInteractionPackRigAndPreservesPicoCapabilities()
        {
            const string scenePath = "Assets/Scenes/SampleScene.unity";
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            bool closeScene = !scene.isLoaded;
            if (closeScene)
                scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);

            try
            {
                GameObject[] roots = scene.GetRootGameObjects();
                var origins = roots.SelectMany(root =>
                    root.GetComponentsInChildren<XROrigin>(true)).ToArray();
                var cameras = roots.SelectMany(root =>
                    root.GetComponentsInChildren<Camera>(true))
                    .Where(camera => camera.enabled && camera.gameObject.activeInHierarchy)
                    .ToArray();
                var listeners = roots.SelectMany(root =>
                    root.GetComponentsInChildren<AudioListener>(true))
                    .Where(listener => listener.enabled && listener.gameObject.activeInHierarchy)
                    .ToArray();
                var eventSystems = roots.SelectMany(root =>
                    root.GetComponentsInChildren<EventSystem>(true))
                    .Where(system => system.enabled && system.gameObject.activeInHierarchy)
                    .ToArray();

                Assert.That(origins, Has.Length.EqualTo(1));
                Assert.That(cameras, Has.Length.EqualTo(1));
                Assert.That(listeners, Has.Length.EqualTo(1));
                Assert.That(eventSystems, Has.Length.EqualTo(1));

                XROrigin origin = origins.Single();
                Assert.That(origin.GetComponent<PXR_Manager>(), Is.Not.Null);
                var manager = origin.GetComponentInChildren<PXR_InteractorManager>(true);
                var aimInteractor = origin.GetComponentInChildren<PXR_AimInteractor>(true);
                Assert.That(manager, Is.Not.Null);
                Assert.That(origin.GetComponentsInChildren<PXR_PinchInteractor>(true),
                    Has.Length.EqualTo(2));
                Assert.That(aimInteractor, Is.Not.Null);
                var eyeModeAdapter = origin.GetComponent<GalleryEyeInteractionModeAdapter>();
                Assert.That(eyeModeAdapter, Is.Not.Null);
                var serializedEyeModeAdapter = new SerializedObject(eyeModeAdapter);
                Assert.That(
                    serializedEyeModeAdapter.FindProperty("manager").objectReferenceValue,
                    Is.EqualTo(manager));
                Assert.That(
                    serializedEyeModeAdapter.FindProperty("aimInteractor").objectReferenceValue,
                    Is.EqualTo(aimInteractor));
                Assert.That(origin.transform.Find(
                    "Camera Offset/Left Controller/Near-Far Interactor")
                    ?.GetComponent<NearFarInteractor>(), Is.Not.Null);
                Assert.That(origin.transform.Find(
                    "Camera Offset/Right Controller/Near-Far Interactor")
                    ?.GetComponent<NearFarInteractor>(), Is.Not.Null);

                Camera mainCamera = cameras.Single();
                Assert.That(mainCamera.GetComponent<PXR_CameraEffectBlock>(), Is.Not.Null);
                Assert.That(origin.transform.Find("[PICO_MCP] VST Marker"), Is.Not.Null);
                Transform plane = origin.transform.Find("[PICO_MCP] Plane");
                Assert.That(plane, Is.Not.Null);
                Assert.That(plane.GetComponents<Component>()
                    .Any(component => component.GetType().Name == "PlaneDetectionManager"), Is.True);
            }
            finally
            {
                if (closeScene)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }

    }
}
