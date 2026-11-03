using System;

namespace PicShow
{
    public interface IPicoModeRuntime
    {
        void SetVideoSeeThrough(bool enabled);
    }

    public interface IWallPlaneSource
    {
        event Action<WallPlaneInfo> WallAdded;
        event Action<WallPlaneInfo> WallUpdated;
        event Action<Guid> WallRemoved;
        void SetEnabled(bool enabled);
        void ClearWalls();
    }

    public interface IWallDisplay
    {
        void SetActive(bool active);
        void ClearAll();
    }
}
