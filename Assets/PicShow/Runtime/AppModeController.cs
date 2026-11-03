using UnityEngine;
using UnityEngine.UI;

namespace PicShow
{
    public sealed class AppModeController : MonoBehaviour
    {
        [SerializeField] private ModePanelView modePanel;
        [SerializeField] private GameObject galleryRoot;
        [SerializeField] private GameObject returnButton;
        [SerializeField] private WallDisplayController wallDisplay;
        [SerializeField] private MonoBehaviour picoRuntimeProvider;
        [SerializeField] private MonoBehaviour wallSourceProvider;
        [SerializeField] private SkyboxBackdrop skyboxBackdrop;

        private AppModeState state;
        private IPicoModeRuntime picoRuntime;
        private IWallPlaneSource wallSource;
        private bool returnWired;

        public AppModeState State => state ?? (state = new AppModeState());
        public AppMode Mode => State.Mode;
        public ModePanelView ModePanel => modePanel;
        public GameObject GalleryRoot => galleryRoot;
        public GameObject ReturnButton => returnButton;

        public void Configure(ModePanelView panel, GameObject gallery, GameObject returnControl,
            WallDisplayController display, IPicoModeRuntime runtime, IWallPlaneSource source,
            SkyboxBackdrop backdrop = null)
        {
            modePanel = panel;
            galleryRoot = gallery;
            returnButton = returnControl;
            wallDisplay = display;
            picoRuntime = runtime;
            wallSource = source;
            skyboxBackdrop = backdrop;
        }

        public void SetProvidersForTests(IPicoModeRuntime runtime, IWallPlaneSource source)
        {
            picoRuntime = runtime;
            wallSource = source;
        }

        private void Start() => Initialize();

        public void Initialize()
        {
            if (state == null)
                state = new AppModeState();
            ResolveReferences();
            if (skyboxBackdrop != null)
                skyboxBackdrop.Initialize();
            if (modePanel != null)
                modePanel.Configure(EnterMrMode, EnterNonMrMode);
            WireReturnButton();
            if (wallDisplay != null)
            {
                wallDisplay.WallSelected = OnWallSelected;
                wallDisplay.WallSelectionClosed = OnWallSelectionClosed;
                wallDisplay.Initialize();
            }
            ApplyView();
        }

        public void EnterNonMrMode()
        {
            State.EnterNonMr();
            if (wallDisplay != null) wallDisplay.ClearAll();
            ApplyView();
        }

        public void EnterMrMode()
        {
            State.EnterMr();
            if (wallDisplay != null) wallDisplay.ClearAll();
            ApplyView();
        }

        public void SelectWall()
        {
            State.SelectWall();
            if (wallDisplay != null) wallDisplay.SetActive(true);
            ApplyView();
        }

        public void BackToWallSelection()
        {
            State.BackToWallSelection();
            if (wallDisplay != null) wallDisplay.SetActive(true);
            ApplyView();
        }

        public void ReturnToModePanel()
        {
            State.ReturnToModePanel();
            if (wallDisplay != null) wallDisplay.ClearAll();
            ApplyView();
        }

        private void OnWallSelected(System.Guid id) => SelectWall();

        private void OnWallSelectionClosed() => BackToWallSelection();

        private void ResolveReferences()
        {
            if (picoRuntime == null && picoRuntimeProvider != null)
                picoRuntime = picoRuntimeProvider as IPicoModeRuntime;
            if (wallSource == null && wallSourceProvider != null)
                wallSource = wallSourceProvider as IWallPlaneSource;
            if (wallDisplay == null)
                wallDisplay = GetComponentInChildren<WallDisplayController>(true);
        }

        private void ApplyView()
        {
            AppModeViewState view = State.View;
            if (modePanel != null) modePanel.SetVisible(view.ModePanelVisible);
            if (galleryRoot != null) galleryRoot.SetActive(view.GalleryVisible);
            if (returnButton != null) returnButton.SetActive(view.ReturnVisible);
            if (wallDisplay != null) wallDisplay.SetActive(view.DetectionEnabled);
            if (picoRuntime != null) picoRuntime.SetVideoSeeThrough(view.VideoSeeThrough);
            if (wallSource != null) wallSource.SetEnabled(view.DetectionEnabled);
            if (skyboxBackdrop != null) skyboxBackdrop.Apply(!view.VideoSeeThrough);
        }

        private void WireReturnButton()
        {
            if (returnWired || returnButton == null)
                return;
            var button = returnButton.GetComponentInChildren<Button>(true);
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ReturnToModePanel);
            returnWired = true;
        }
    }
}
