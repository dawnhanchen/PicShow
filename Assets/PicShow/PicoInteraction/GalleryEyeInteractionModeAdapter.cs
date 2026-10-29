using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace PicShow.PicoInteraction
{
    public sealed class GalleryEyeInteractionModeAdapter : MonoBehaviour
    {
        [SerializeField] private PXR_InteractorManager manager;
        [SerializeField] private PXR_AimInteractor aimInteractor;

        private bool bound;

        public void Configure(
            PXR_InteractorManager targetManager,
            PXR_AimInteractor targetAimInteractor)
        {
            Unbind();
            manager = targetManager;
            aimInteractor = targetAimInteractor;
            if (isActiveAndEnabled)
                Bind();
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (bound)
                return;

            ResolveReferences();
            PXR_InteractorManager.InputModeChangeAction += OnInputModeChanged;
            bound = true;
            ApplyInputMode(manager != null
                ? manager.CurrentInputMode
                : XRInputModalityManager.InputMode.None);
        }

        private void Unbind()
        {
            if (!bound)
                return;

            PXR_InteractorManager.InputModeChangeAction -= OnInputModeChanged;
            bound = false;
        }

        private void ResolveReferences()
        {
            if (manager == null)
                manager = FindFirstObjectByType<PXR_InteractorManager>();
            if (aimInteractor == null && manager != null)
                aimInteractor = manager.GetComponentInChildren<PXR_AimInteractor>(true);
        }

        private void OnInputModeChanged(XRInputModalityManager.InputMode inputMode) =>
            ApplyInputMode(inputMode);

        private void ApplyInputMode(XRInputModalityManager.InputMode inputMode)
        {
            if (aimInteractor == null)
                return;

            bool enableEyeInteraction =
                inputMode != XRInputModalityManager.InputMode.MotionController;
            if (aimInteractor.gameObject.activeSelf != enableEyeInteraction)
                aimInteractor.gameObject.SetActive(enableEyeInteraction);
        }
    }
}
