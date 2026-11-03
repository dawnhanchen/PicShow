using System;

namespace PicShow
{
    public enum AppMode
    {
        ModePanel,
        NonMrGallery,
        MrWallSelection,
        MrWallContent
    }

    public readonly struct AppModeViewState
    {
        public readonly bool ModePanelVisible;
        public readonly bool GalleryVisible;
        public readonly bool ReturnVisible;
        public readonly bool DetectionEnabled;
        public readonly bool VideoSeeThrough;

        public AppModeViewState(bool modePanelVisible, bool galleryVisible, bool returnVisible,
            bool detectionEnabled, bool videoSeeThrough)
        {
            ModePanelVisible = modePanelVisible;
            GalleryVisible = galleryVisible;
            ReturnVisible = returnVisible;
            DetectionEnabled = detectionEnabled;
            VideoSeeThrough = videoSeeThrough;
        }
    }

    public sealed class AppModeState
    {
        public AppMode Mode { get; private set; } = AppMode.ModePanel;

        public AppModeViewState View
        {
            get
            {
                switch (Mode)
                {
                    case AppMode.NonMrGallery:
                        return new AppModeViewState(false, true, true, false, false);
                    case AppMode.MrWallSelection:
                    case AppMode.MrWallContent:
                        return new AppModeViewState(false, false, true, true, true);
                    default:
                        return new AppModeViewState(true, false, false, false, false);
                }
            }
        }

        public void EnterNonMr() => Mode = AppMode.NonMrGallery;

        public void EnterMr() => Mode = AppMode.MrWallSelection;

        public void SelectWall()
        {
            Require(AppMode.MrWallSelection, "A wall can only be selected while choosing a wall.");
            Mode = AppMode.MrWallContent;
        }

        public void BackToWallSelection()
        {
            Require(AppMode.MrWallContent, "Wall content must be open before returning to wall selection.");
            Mode = AppMode.MrWallSelection;
        }

        public void ReturnToModePanel() => Mode = AppMode.ModePanel;

        private void Require(AppMode expected, string message)
        {
            if (Mode != expected)
                throw new InvalidOperationException(message);
        }
    }
}
