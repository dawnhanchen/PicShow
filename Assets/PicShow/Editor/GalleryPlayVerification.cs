using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.Interaction;
using PicShow;
using PicShow.PicoInteraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PicShow.Editor
{
    // Executes actual screen-space raycasts and UI events in the current Play Mode scene.
    public static class GalleryPlayVerification
    {
        private static GalleryCarousel gallery;
        private static string resultPath;
        private static int step;
        private static double nextStepTime;
        private static readonly List<string> evidence = new List<string>();

        public static void Run(string outputPath)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            // A visible Game View is required for the Editor Player Loop to advance.
            var gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            gameView.Show();
            gameView.Focus();
            EditorApplication.QueuePlayerLoopUpdate();
            gallery = UnityEngine.Object.FindAnyObjectByType<GalleryCarousel>();
            if (gallery == null) throw new InvalidOperationException("No gallery in the active scene.");
            resultPath = outputPath;
            step = 0;
            evidence.Clear();
            nextStepTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < nextStepTime) return;
            nextStepTime = EditorApplication.timeSinceStartup + 0.6;
            try
            {
                if (!Application.isPlaying) throw new InvalidOperationException("Play Mode ended before verification.");
                if (step == 0)
                {
                    AssertView(0, 0);
                    Click(gallery.PreviousButton);
                }
                else if (step == 1)
                {
                    AssertView(0, 4);
                    Click(gallery.NextButton);
                }
                else if (step == 2)
                {
                    AssertView(0, 0);
                    Click(gallery.Cards[2].Button);
                }
                else if (step == 3)
                {
                    AssertView(0, 1);
                    Click(gallery.Cards[0].Button);
                }
                else if (step == 4)
                {
                    AssertView(0, 0);
                    Click(gallery.Dots[3].Button);
                }
                else if (step == 5)
                {
                    AssertView(0, 3);
                    Click(gallery.Tabs[2].Button);
                }
                else if (step == 6)
                {
                    AssertView(2, 3);
                    for (int c = 0; c < 4; c++)
                    {
                        Click(gallery.Tabs[c].Button);
                        for (int w = 0; w < 5; w++)
                        {
                            Click(gallery.Dots[w].Button);
                            AssertView(c, w);
                        }
                    }
                    for (int i = 0; i < 21; i++) Click(gallery.NextButton);
                    Click(gallery.Tabs[1].Button);
                    gallery.enabled = false;
                    gallery.enabled = true;
                }
                else if (step == 7)
                {
                    AssertView(1, 0);
                    Require(!gallery.IsTransitioning, "Disable/re-enable must cancel the transition.");
                    foreach (var card in gallery.Cards)
                        Require(Mathf.Approximately(card.Group.alpha, 1f), "Card alpha must settle.");
                    Click(gallery.NextButton);
                }
                else if (step == 8)
                {
                    AssertView(1, 1);
                    Require(!gallery.IsTransitioning, "Animation did not settle.");
                    Click(gallery.Tabs[0].Button);
                    Click(gallery.Dots[0].Button);
                }
                else if (step == 9)
                {
                    AssertView(0, 0);
                    Click(gallery.NextButton);
                    gallery.NextButton.GetComponent<PXR_EnhancedButton>().onSingleClick.Invoke();
                }
                else if (step == 10)
                {
                    AssertView(0, 1);
                    Click(gallery.Dots[0].Button);
                }
                else if (step == 11)
                {
                    AssertView(0, 0);
                    var controller = gallery.GetComponent<GalleryControllerNavigator>();
                    Require(controller != null, "Controller navigator is missing.");
                    controller.ProcessAxes(0.8f, 0f, false, Time.unscaledTime);
                    AssertView(0, 0);
                    controller.ProcessAxes(0.8f, 0f, true, Time.unscaledTime + 0.01f);
                }
                else if (step == 12)
                {
                    AssertView(0, 1);
                    var controller = gallery.GetComponent<GalleryControllerNavigator>();
                    controller.ProcessAxes(0f, 0f, true, Time.unscaledTime);
                    controller.ProcessAxes(-0.8f, 0f, true, Time.unscaledTime + 0.01f);
                }
                else if (step == 13)
                {
                    AssertView(0, 0);
                    var swipe = gallery.GetComponentInChildren<GallerySwipeNavigator>();
                    Require(swipe != null, "Swipe navigator is missing.");
                    swipe.BeginSwipe(0f);
                    swipe.UpdateSwipe(-0.2f);
                    Require(swipe.EndSwipe(-0.2f, Time.unscaledTime), "Swipe did not commit.");
                }
                else if (step == 14)
                {
                    AssertView(0, 1);
                    var manager = UnityEngine.Object.FindAnyObjectByType<PXR_InteractorManager>();
                    Require(manager != null, "PICO interactor manager is missing.");
                    var aim = UnityEngine.Object.FindAnyObjectByType<PXR_AimInteractor>(
                        FindObjectsInactive.Include);
                    Require(aim != null, "PICO eye-gaze aim interactor is missing.");
                    manager.trackedHandModeStarted();
                    Require(aim.gameObject.activeSelf,
                        "Hand mode precondition must enable eye interaction.");
                    manager.motionControllerModeStarted();
                    Require(!aim.gameObject.activeSelf,
                        "Controller mode must disable the eye cursor and eye-driven UI hover.");
                    AssertView(0, 1);
                }
                else if (step == 15)
                {
                    AssertView(0, 1);
                    var manager = UnityEngine.Object.FindAnyObjectByType<PXR_InteractorManager>();
                    Require(manager != null, "PICO interactor manager is missing.");
                    var aim = UnityEngine.Object.FindAnyObjectByType<PXR_AimInteractor>(FindObjectsInactive.Include);
                    Require(aim != null, "PICO eye-gaze aim interactor is missing.");
                    manager.trackedHandModeStarted();
                    Require(aim.gameObject.activeSelf,
                        "Hand mode must restore the eye cursor and eye-driven UI hover.");
                    AssertView(0, 1);
                    Click(gallery.Dots[0].Button);
                }
                else
                {
                    AssertView(0, 0);
                    File.WriteAllLines(resultPath, new[] { "PASS: Play Mode screen raycasts and UI events." }.Concat(evidence));
                    Debug.Log("PicShow Play Mode verification passed: " + resultPath);
                    EditorApplication.update -= Tick;
                }
                step++;
            }
            catch (Exception error)
            {
                File.WriteAllText(resultPath, "FAIL: step " + step + "\n" + error);
                Debug.LogException(error);
                EditorApplication.update -= Tick;
            }
        }

        private static void Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var data = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(Camera.main, button.transform.position),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(data, hits);
            var target = hits.FirstOrDefault().gameObject;
            Require(target != null, "No screen raycast hit: " + button.name);
            Require(target.GetComponentInParent<Button>() == button, "Wrong screen hit for " + button.name + ": " + target.name);
            data.pointerCurrentRaycast = hits[0];
            data.pointerPressRaycast = hits[0];
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerExitHandler);
        }

        private static void AssertView(int category, int work)
        {
            Require(gallery.CategoryIndex == category && gallery.WorkIndex == work, "Selected state mismatch.");
            var content = gallery.Catalog.Categories[category];
            Require(gallery.CurrentTitle == content.Works[work].Title, "Title mismatch.");
            Require(gallery.GetComponentsInChildren<RawImage>().Length == 3, "Only three images may exist.");
            for (int i = 0; i < 3; i++)
                Require(gallery.Cards[i].Artwork.texture == content.Works[(work + i + 4) % 5].Texture, "Slot texture mismatch.");
            evidence.Add($"Category {category}, work {work}: title and three textures synchronized.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
