#if ENABLE_PICO_XR_SDK
using ByteDance.PICO.XR;
#endif
using UnityEngine;

namespace PicShow.PicoInteraction
{
    public sealed class PicoModeRuntime : MonoBehaviour, PicShow.IPicoModeRuntime
    {
        public bool EditorValue { get; private set; }
        public bool LastValue { get; private set; }

        public void SetVideoSeeThrough(bool enabled)
        {
            LastValue = enabled;
#if UNITY_EDITOR || !ENABLE_PICO_XR_SDK
            EditorValue = enabled;
#else
            PXR_Manager.EnableVideoSeeThrough = enabled;
#endif
        }
    }
}
