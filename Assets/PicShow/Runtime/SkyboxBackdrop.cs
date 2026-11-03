using UnityEngine;

namespace PicShow
{
    /// <summary>
    /// Switches the XR camera between the gallery skybox and the transparent VST background.
    /// The skybox is only rendered while video see-through is off.
    /// </summary>
    public sealed class SkyboxBackdrop : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Material skyMaterial;
        [SerializeField] private Color transparentBackground = new Color(0f, 0f, 0f, 0f);

        private Material initialSkybox;
        private bool initialCaptured;

        public void Configure(Camera camera, Material material)
        {
            targetCamera = camera;
            skyMaterial = material;
        }

        public void Initialize()
        {
            if (targetCamera == null)
                targetCamera = Camera.main;
            CaptureInitialSkybox();
        }

        /// <summary>
        /// Shows the gallery skybox when <paramref name="showSkybox"/> is true and restores the
        /// transparent background otherwise, so MR mode keeps the real environment visible.
        /// </summary>
        public void Apply(bool showSkybox)
        {
            if (targetCamera == null)
                return;
            CaptureInitialSkybox();

            if (showSkybox)
            {
                if (skyMaterial != null)
                    RenderSettings.skybox = skyMaterial;
                targetCamera.clearFlags = CameraClearFlags.Skybox;
                return;
            }

            targetCamera.clearFlags = CameraClearFlags.SolidColor;
            targetCamera.backgroundColor = transparentBackground;
            if (skyMaterial != null && RenderSettings.skybox == skyMaterial)
                RenderSettings.skybox = initialSkybox;
        }

        private void CaptureInitialSkybox()
        {
            if (initialCaptured)
                return;
            initialSkybox = RenderSettings.skybox;
            initialCaptured = true;
        }
    }
}
