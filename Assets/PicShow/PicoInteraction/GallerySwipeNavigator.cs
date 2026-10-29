using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PicShow.PicoInteraction
{
    [RequireComponent(typeof(PXR_SwipeFrame))]
    public sealed class GallerySwipeNavigator : MonoBehaviour
    {
        [SerializeField] private GalleryCarousel gallery;
        [SerializeField] private float commitThreshold = 0.1f;

        private GallerySwipeDecision decision;
        private InteractorHandedness activeHand = InteractorHandedness.None;
        private bool tracking;
        private float currentLocalX;

        public void Configure(GalleryCarousel targetGallery, float threshold)
        {
            gallery = targetGallery;
            commitThreshold = threshold;
            decision = new GallerySwipeDecision(commitThreshold);
            ResetGesture();
        }

        private void Awake() => decision = new GallerySwipeDecision(commitThreshold);

        private void OnEnable()
        {
            PXR_InteractorManager.PinchDownAction += OnPinchDown;
            PXR_InteractorManager.PinchUpAction += OnPinchUp;
        }

        private void OnDisable()
        {
            PXR_InteractorManager.PinchDownAction -= OnPinchDown;
            PXR_InteractorManager.PinchUpAction -= OnPinchUp;
            ResetGesture();
        }

        private void Update()
        {
            if (activeHand == InteractorHandedness.None)
                return;

            if (PXR_InteractorManager.InteractionData._scrollObject != transform)
                return;

            if (!TryGetHandLocalX(activeHand, out float localX))
                return;

            if (!tracking)
                BeginSwipe(localX);
            else
                UpdateSwipe(localX);
        }

        public void BeginSwipe(float localX)
        {
            EnsureDecision();
            decision.Begin(localX);
            currentLocalX = localX;
            tracking = true;
        }

        public void UpdateSwipe(float localX)
        {
            if (!tracking)
                return;

            currentLocalX = localX;
            decision.Update(localX);
        }

        public bool EndSwipe(float localX, float timestamp)
        {
            if (!tracking || gallery == null)
                return false;

            int direction = decision.End(localX);
            ResetGesture();
            return direction != 0 &&
                gallery.ExecuteInput(GalleryInputCommand.Move(direction), timestamp);
        }

        private void OnPinchDown(InteractorHandedness hand)
        {
            var manager = PXR_InteractorManager.Instance;
            if (manager.CurrentInputMode != XRInputModalityManager.InputMode.TrackedHand)
                return;

            activeHand = hand;
            tracking = false;
        }

        private void OnPinchUp(InteractorHandedness hand)
        {
            if (hand != activeHand)
                return;

            if (tracking)
                EndSwipe(currentLocalX, Time.unscaledTime);
            else
                ResetGesture();
        }

        private static bool TryGetHandLocalX(InteractorHandedness hand, out float localX)
        {
            localX = 0f;
            PXR_PinchInteractor pinch = PXR_InteractorManager.Instance.GetPinchInteractor(hand);
            Transform handTransform = pinch != null ? pinch.EventParas._handTransform : null;
            if (handTransform == null)
                return false;

            Camera camera = Camera.main;
            localX = camera != null
                ? camera.transform.InverseTransformPoint(handTransform.position).x
                : handTransform.position.x;
            return true;
        }

        private void EnsureDecision()
        {
            if (decision == null)
                decision = new GallerySwipeDecision(commitThreshold);
        }

        private void ResetGesture()
        {
            activeHand = InteractorHandedness.None;
            tracking = false;
            currentLocalX = 0f;
        }
    }
}
