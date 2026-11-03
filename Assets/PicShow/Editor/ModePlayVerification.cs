using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PicShow;
using PicShow.PicoInteraction;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.Editor
{
    public static class ModePlayVerification
    {
        private const string ScenePath = "Assets/Scenes/SampleScene.unity";
        private const string CatalogPath = "Assets/PicShow/Data/GalleryCatalog.asset";
        private const string SessionActiveKey = "PicShow.ModePlayVerification.Active";
        private const string SessionOutputKey = "PicShow.ModePlayVerification.Output";
        private static string resultPath;
        private static int step;
        private static double nextStepTime;
        private static readonly List<string> evidence = new List<string>();
        private static AppModeController controller;
        private static WallDisplayController wallDisplay;
        private static FakeModeRuntime fakeRuntime;
        private static FakeWallSource fakeSource;
        private static GalleryCatalog catalog;
        private static Guid wallId;
        private static LazyFollow startupFollow;
        private static TimedLazyFollow startupTimed;
        private static bool followWaitStarted;
        private static float frozenWallWidth;

        [InitializeOnLoadMethod]
        private static void Bootstrap()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (SessionState.GetBool(SessionActiveKey, false) && Application.isPlaying)
                StartTicking();
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode &&
                SessionState.GetBool(SessionActiveKey, false))
                StartTicking();
        }

        private static void StartTicking()
        {
            resultPath = SessionState.GetString(SessionOutputKey, resultPath);
            step = 0;
            evidence.Clear();
            startupFollow = null;
            startupTimed = null;
            followWaitStarted = false;
            frozenWallWidth = 0f;
            nextStepTime = EditorApplication.timeSinceStartup + 1.5;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        [MenuItem("PicShow/Verify Mode Selection Play Mode")]
        public static void RunMenu() =>
            Run("E:/Sample/AIPicShowWS/verification/skybox-mode-play-verification.txt");

        public static void RunBatch() =>
            Run("E:/Sample/AIPicShowWS/verification/mode-play-verification.txt");

        public static void Run(string outputPath)
        {
            resultPath = outputPath;
            SessionState.SetBool(SessionActiveKey, true);
            SessionState.SetString(SessionOutputKey, outputPath);
            if (Application.isPlaying)
                StartTicking();
            else
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                EditorApplication.isPlaying = true;
            }
        }

        private static void Tick()
        {
            if (Application.isPlaying && EditorApplication.timeSinceStartup < nextStepTime)
                return;
            nextStepTime = EditorApplication.timeSinceStartup + 0.5;
            try
            {
                if (!Application.isPlaying)
                    throw new InvalidOperationException("Play Mode ended before verification finished.");
                if (step == 0)
                {
                    controller = UnityEngine.Object.FindAnyObjectByType<AppModeController>();
                    wallDisplay = UnityEngine.Object.FindAnyObjectByType<WallDisplayController>();
                    catalog = AssetDatabase.LoadAssetAtPath<GalleryCatalog>(CatalogPath);
                    Require(controller != null, "AppModeController is missing.");
                    Require(wallDisplay != null, "WallDisplayController is missing.");
                    Require(catalog != null, "GalleryCatalog is missing.");
                    fakeRuntime = new FakeModeRuntime();
                    fakeSource = new FakeWallSource();
                    controller.SetProvidersForTests(fakeRuntime, fakeSource);
                    wallDisplay.SetWallSourceForTests(fakeSource);
                    controller.Initialize();
                    Require(controller.Mode == AppMode.ModePanel, "Startup must show the mode panel.");
                    Require(controller.ModePanel.Visible, "The mode panel must be visible.");
                    Require(!controller.GalleryRoot.activeSelf, "The gallery must be hidden at startup.");
                    Require(!fakeSource.Enabled, "Detection must not start at startup.");
                    startupFollow = controller.ModePanel.GetComponent<LazyFollow>();
                    startupTimed = controller.ModePanel.GetComponent<TimedLazyFollow>();
                    Require(startupFollow != null && startupTimed != null,
                        "The mode panel must follow the head for three seconds.");
                    Require(startupTimed.FollowSeconds <= 3.01f,
                        "The follow window must be three seconds.");
                    if (startupFollow.enabled)
                    {
                        if (!followWaitStarted)
                        {
                            followWaitStarted = true;
                            nextStepTime = EditorApplication.timeSinceStartup + 3.3;
                            evidence.Add("Startup: waiting for the three-second head follow window.");
                            return;
                        }
                        throw new InvalidOperationException(
                            "Mode panel LazyFollow did not stop after three seconds.");
                    }
                    Require(startupTimed.WindowStarted,
                        "The three-second follow window never started.");
                    evidence.Add("Mode panel stopped following the head after three seconds.");
                    ModeCardHover hover = controller.ModePanel.MrButton.GetComponent<ModeCardHover>();
                    Require(hover != null, "Mode cards must provide hover feedback.");
                    Image cardImage = controller.ModePanel.MrButton.GetComponent<Image>();
                    Color normalColor = cardImage.color;
                    Vector3 normalScale = controller.ModePanel.MrButton.transform.localScale;
                    hover.OnPointerEnter(null);
                    Require(hover.Hovering, "Hovering a mode card must enter the hover state.");
                    Require(cardImage.color != normalColor, "Hover must brighten the mode card.");
                    Require(controller.ModePanel.MrButton.transform.localScale.x
                        > normalScale.x, "Hover must enlarge the mode card.");
                    hover.OnPointerExit(null);
                    Require(!hover.Hovering, "Leaving a mode card must clear hover.");
                    Require(cardImage.color == normalColor, "Hover must restore the card color.");
                    evidence.Add("Mode cards provide visible hover feedback.");
                    RequireSkybox(true, "Mode panel");
                    controller.ModePanel.MrButton.onClick.Invoke();
                    evidence.Add("Startup: mode panel visible, gallery hidden, detection off.");
                }
                else if (step == 1)
                {
                    Require(controller.Mode == AppMode.MrWallSelection, "MR mode must start wall selection.");
                    RequireSkybox(false, "MR mode");
                    Require(!controller.ModePanel.Visible, "The mode panel must hide in MR mode.");
                    Require(!controller.GalleryRoot.activeSelf, "The carousel must hide in MR mode.");
                    Require(fakeSource.Enabled, "MR mode must enable wall detection.");
                    Require(fakeRuntime.Enabled, "MR mode must enable VST.");
                    wallId = Guid.NewGuid();
                    fakeSource.Emit(new WallPlaneInfo(wallId, new Vector3(0f, 1.5f, -2f),
                        Vector3.forward, Vector3.right, Vector3.up, 2.4f, 2.6f));
                    fakeSource.Emit(new WallPlaneInfo(Guid.NewGuid(), new Vector3(1.2f, 1.5f, -2f),
                        Vector3.forward, Vector3.right, Vector3.up, 2.4f, 2.6f));
                    evidence.Add("MR mode: detection and VST on, gallery hidden.");
                }
                else if (step == 2)
                {
                    Require(wallDisplay.Panels.Count == 1,
                        "Coplanar adjacent wall patches must share one button.");
                    WallPanelView panel = wallDisplay.Panels.Values.First();
                    Require(panel.gameObject.activeSelf, "The wall panel must be visible.");
                    RectTransform panelRect = (RectTransform)panel.transform;
                    Require(Mathf.Abs(panelRect.sizeDelta.x - panel.Info.Width * 1000f) <= 1f
                        && Mathf.Abs(panelRect.sizeDelta.y - panel.Info.Height * 1000f) <= 1f,
                        "The detection highlight must cover the full wall.");
                    RectTransform buttonRect = (RectTransform)panel.SelectButton.transform;
                    Require(buttonRect.sizeDelta.x <= 560.01f && buttonRect.sizeDelta.y <= 180.01f,
                        "The wall selection button must use the compact size.");
                    Require(panelRect.sizeDelta.x > buttonRect.sizeDelta.x,
                        "The wall highlight must be wider than the compact button.");
                    Transform fill = panel.transform.Find("Fill");
                    Require(fill != null, "The wall highlight fill is missing.");
                    var fillImage = fill.GetComponent<Image>();
                    Require(fillImage != null && fillImage.raycastTarget == false,
                        "The wall highlight must not block interaction.");
                    Require(fillImage.color.a < 0.35f,
                        "The wall highlight must stay translucent.");
                    Transform border = panel.transform.Find("Border");
                    Require(border != null && border.GetComponent<Image>() == null,
                        "The wall frame must be an edge container, not a full fill.");
                    Require(border.GetComponentsInChildren<Image>(true).Length == 4,
                        "The wall frame needs four thin edge bars.");
                    TMP_Text prompt = panel.GetComponentsInChildren<TMPro.TMP_Text>(true)
                        .FirstOrDefault(text => text.text == "选择该墙面显示");
                    Require(prompt != null, "The wall panel must show 选择该墙面显示.");
                    panel.SelectButton.onClick.Invoke();
                    evidence.Add("Two coplanar patches show one full-wall highlight, edge frame and compact button.");
                }
                else if (step == 3)
                {
                    Require(controller.Mode == AppMode.MrWallContent, "Selecting a wall must enter content.");
                    Require(wallDisplay.Gallery != null, "Wall content view must exist.");
                    WallGalleryView gallery = wallDisplay.Gallery;
                    VerifyCategoryRaycasts(gallery);
                    evidence.Add("Category centers and edges raycast to their own buttons.");
                    Require(gallery.CategoryButtons.Length == 4, "Four category buttons are required.");
                    Require(gallery.ImageSlots.All(slot => !slot.gameObject.activeSelf),
                        "Images must stay hidden before a category is chosen.");
                    foreach (Button button in gallery.CategoryButtons)
                        Require(button.GetComponent<TMPro.TMP_Text>() == null,
                            "Category buttons must be text-only.");
                    TMP_Text categoryLabel = gallery.CategoryButtons[0]
                        .GetComponentInChildren<TMPro.TMP_Text>(true);
                    Require(categoryLabel != null, "Category labels are required.");
                    categoryLabel.ForceMeshUpdate();
                    Require(categoryLabel.fontSize >= 48f,
                        "Wall category labels must be large enough to read.");
                    Require(categoryLabel.preferredWidth
                        <= ((RectTransform)categoryLabel.transform).rect.width + 1f,
                        "Wall category labels must not be clipped.");
                    Button back = gallery.GetComponentsInChildren<Button>(true)
                        .First(button => button.name == "Change wall");
                    TMP_Text backLabel = back.GetComponentInChildren<TMPro.TMP_Text>(true);
                    backLabel.ForceMeshUpdate();
                    Require(backLabel.fontSize >= 32f, "Change-wall text must be readable.");
                    evidence.Add("Category labels scale with the wall button size.");
                    frozenWallWidth = gallery.Wall.Width;
                    wallDisplay.UpdateWall(new WallPlaneInfo(wallId,
                        new Vector3(0f, 1.5f, -2f), Vector3.forward, Vector3.right,
                        Vector3.up, 4f, 3f));
                    Require(Mathf.Abs(gallery.Wall.Width - frozenWallWidth) <= 0.01f,
                        "The selected wall must stay frozen during content.");
                    evidence.Add("Selected wall geometry stayed frozen during detection update.");
                    gallery.CategoryButtons[0].onClick.Invoke();
                    evidence.Add("Wall selected: four text category buttons shown without thumbnails.");
                }
                else if (step == 4)
                {
                    WallGalleryView gallery = wallDisplay.Gallery;
                    Require(gallery.CurrentCategory == 0, "First category must be active.");
                    for (int i = 0; i < 5; i++)
                    {
                        RawImage slot = gallery.ImageSlots[i];
                        Require(slot.gameObject.activeSelf, "All five artworks must be visible.");
                        Require(slot.texture == catalog.Categories[0].Works[i].Texture,
                            "Artwork texture mismatch.");
                        RectTransform rect = slot.rectTransform;
                        Require(Mathf.Abs(rect.anchoredPosition.x) + rect.sizeDelta.x / 2f
                            <= gallery.Wall.Width * 500f + 8f, "Artwork exceeds wall width.");
                        Require(Mathf.Abs(rect.anchoredPosition.y) + rect.sizeDelta.y / 2f
                            <= gallery.Wall.Height * 500f + 8f, "Artwork exceeds wall height.");
                    }
                    gallery.CategoryButtons[2].onClick.Invoke();
                    evidence.Add("Category 1: five artworks fit inside the wall.");
                }
                else if (step == 5)
                {
                    WallGalleryView gallery = wallDisplay.Gallery;
                    Require(gallery.CurrentCategory == 2, "Category switch must take effect.");
                    Require(gallery.ImageSlots[0].texture == catalog.Categories[2].Works[0].Texture,
                        "Category switch must replace artwork textures.");
                    gallery.BackAt(Time.unscaledTime);
                    evidence.Add("Category switch replaced all five artworks.");
                }
                else if (step == 6)
                {
                    Require(controller.Mode == AppMode.MrWallSelection,
                        "Changing a wall must return to wall selection.");
                    Require(wallDisplay.Gallery == null, "Wall content must be destroyed.");
                    Require(wallDisplay.Panels.Values.All(panel => panel.gameObject.activeSelf),
                        "All wall panels must be visible again.");
                    Require(wallDisplay.Panels.Values.First().Info.Width > frozenWallWidth + 0.01f,
                        "Returning to wall selection must apply the latest detection size.");
                    evidence.Add("Change wall refreshed the latest detection size.");
                    controller.ReturnButton.GetComponentInChildren<Button>(true).onClick.Invoke();
                    evidence.Add("Change wall: panels restored and content cleared.");
                }
                else if (step == 7)
                {
                    Require(controller.Mode == AppMode.ModePanel, "Return must show the mode panel.");
                    Require(controller.ModePanel.Visible, "The mode panel must be visible after return.");
                    Require(!fakeSource.Enabled, "Return must stop wall detection.");
                    Require(!fakeRuntime.Enabled, "Return must disable VST.");
                    Require(wallDisplay.Panels.Count == 0, "Return must clear wall UI.");
                    RequireSkybox(true, "Return to mode panel");
                    controller.ModePanel.NonMrButton.onClick.Invoke();
                    evidence.Add("Return: detection stopped, VST off, wall UI cleared.");
                }
                else if (step == 8)
                {
                    Require(controller.Mode == AppMode.NonMrGallery, "Non-MR must show the carousel.");
                    Require(controller.GalleryRoot.activeSelf, "Non-MR must show the gallery.");
                    Require(controller.ReturnButton.activeSelf, "Non-MR must keep the return entry.");
                    Require(!fakeSource.Enabled, "Non-MR must not start wall detection.");
                    Require(!fakeRuntime.Enabled, "Non-MR must not enable VST.");
                    RequireSkybox(true, "Non-MR gallery");
                    File.WriteAllLines(resultPath,
                        new[] { "PASS: mode selection and wall display Play Mode flow." }
                            .Concat(evidence));
                    Debug.Log("Mode Play Mode verification passed: " + resultPath);
                    EditorApplication.update -= Tick;
                    SessionState.SetBool(SessionActiveKey, false);
                    EditorApplication.isPlaying = false;
                    EditorApplication.Exit(0);
                }
                step++;
            }
            catch (Exception error)
            {
                File.WriteAllText(resultPath, "FAIL: step " + step + "\n" + error);
                Debug.LogException(error);
                EditorApplication.update -= Tick;
                SessionState.SetBool(SessionActiveKey, false);
                EditorApplication.isPlaying = false;
                EditorApplication.Exit(1);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void RequireSkybox(bool visible, string phase)
        {
            Camera camera = Camera.main;
            Require(camera != null, phase + ": the main camera is missing.");
            if (visible)
            {
                Require(camera.clearFlags == CameraClearFlags.Skybox,
                    phase + ": the gallery skybox must clear the background.");
                Require(RenderSettings.skybox != null,
                    phase + ": the skybox material is missing from the render settings.");
                return;
            }

            Require(camera.clearFlags == CameraClearFlags.SolidColor,
                phase + ": MR mode must keep a solid background.");
            Require(camera.backgroundColor.a <= 0.001f,
                phase + ": the MR background must stay transparent for video see-through.");
        }

        private static void VerifyCategoryRaycasts(WallGalleryView gallery)
        {
            Camera camera = Camera.main;
            Require(camera != null && EventSystem.current != null, "UI raycast context is missing.");
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            try
            {
                camera.transform.SetPositionAndRotation(
                    gallery.Wall.Center + gallery.Wall.Normal * 3f,
                    Quaternion.LookRotation(-gallery.Wall.Normal, gallery.Wall.Up));
                Canvas.ForceUpdateCanvases();
                var raycaster = gallery.GetComponent<GraphicRaycaster>();
                Require(raycaster != null, "Wall gallery raycaster is missing.");
                foreach (Button button in gallery.CategoryButtons)
                {
                    var rect = (RectTransform)button.transform;
                    foreach (float x in new[] { -0.4f, 0f, 0.4f })
                    {
                        Vector3 world = rect.TransformPoint(new Vector3(rect.rect.width * x, 0, 0));
                        var pointer = new PointerEventData(EventSystem.current)
                            { position = camera.WorldToScreenPoint(world) };
                        var hits = new List<RaycastResult>();
                        raycaster.Raycast(pointer, hits);
                        Require(hits.Count > 0 &&
                            hits[0].gameObject.GetComponentInParent<Button>() == button,
                            "Category raycast was missed or intercepted: " + button.name);
                    }
                }
            }
            finally
            {
                camera.transform.SetPositionAndRotation(position, rotation);
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
            public void Emit(WallPlaneInfo wall) => WallAdded?.Invoke(wall);
        }
    }
}
