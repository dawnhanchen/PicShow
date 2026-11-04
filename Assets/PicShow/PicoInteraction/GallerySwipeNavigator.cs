using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace PicShow.PicoInteraction
{
    public sealed class GallerySwipeNavigator : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IInteractionModeAware
    {
        [SerializeField] private GalleryCarousel gallery;
        [SerializeField] private float commitThreshold = 0.1f;

        private GallerySwipeDecision decision;
        private InteractorHandedness activeHand = InteractorHandedness.None;
        private bool tracking;
        private bool hovered;
        private float currentLocalX;

        public void Configure(GalleryCarousel targetGallery, float threshold)
        {
            gallery = targetGallery;
            commitThreshold = threshold;
            decision = new GallerySwipeDecision(commitThreshold);
            hovered = false;
            ResetGesture();
        }

        private void Awake() => decision = new GallerySwipeDecision(commitThreshold);

        private void OnEnable()
        {
            PXR_InteractorManager.PinchDownAction += OnPinchDown;
            PXR_InteractorManager.PinchUpAction += OnPinchUp;
            PXR_InteractorManager.InputModeChangeAction += OnInputModeChanged;
        }

        private void OnDisable()
        {
            PXR_InteractorManager.PinchDownAction -= OnPinchDown;
            PXR_InteractorManager.PinchUpAction -= OnPinchUp;
            PXR_InteractorManager.InputModeChangeAction -= OnInputModeChanged;
            hovered = false;
            ReleaseOwnership();
        }

        private void Update()
        {
            if (activeHand == InteractorHandedness.None)
                return;

            if (PXR_InteractorManager.InteractionData._scrollObject != transform)
                return;

            PXR_PinchInteractor pinch =
                PXR_InteractorManager.Instance.GetPinchInteractor(activeHand);
            if (Application.isPlaying && pinch != null)
                pinch.ProcessEvent(HandPinchEvent.PinchEvent);

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
            if (manager.CurrentInputMode != XRInputModalityManager.InputMode.TrackedHand ||
                !hovered)
                return;

            if (activeHand != InteractorHandedness.None && activeHand != hand)
                EndPinchInteraction(activeHand);

            activeHand = hand;
            tracking = false;
            manager.CurrentAimUpdateType = hand == InteractorHandedness.Left
                ? AimUpdateType.UIScrollbyLeft
                : AimUpdateType.UIScrollbyRight;
            manager.SetScrollObject(transform);

            PXR_PinchInteractor pinch = manager.GetPinchInteractor(hand);
            if (Application.isPlaying && pinch != null)
                pinch.ProcessEvent(HandPinchEvent.PinchDownEvent);
        }

        private void OnPinchUp(InteractorHandedness hand)
        {
            if (hand != activeHand)
                return;

            if (tracking)
                EndSwipe(currentLocalX, Time.unscaledTime);
            ReleaseOwnership(hand);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData) => hovered = false;

        public void ResetInteractionState() => ReleaseOwnership();

        private void OnInputModeChanged(XRInputModalityManager.InputMode inputMode)
        {
            hovered = false;
            if (inputMode != XRInputModalityManager.InputMode.TrackedHand)
                ReleaseOwnership();
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

        private void ReleaseOwnership(
            InteractorHandedness hand = InteractorHandedness.None)
        {
            InteractorHandedness owner = hand != InteractorHandedness.None
                ? hand
                : activeHand;
            EndPinchInteraction(owner);

            PXR_InteractorManager manager = PXR_InteractorManager.Instance;
            if (PXR_InteractorManager.InteractionData._scrollObject == transform)
            {
                manager.SetScrollObject(null);
                manager.CurrentAimUpdateType = AimUpdateType.EyeGaze;
            }
            ResetGesture();
        }

        private static void EndPinchInteraction(InteractorHandedness hand)
        {
            if (hand == InteractorHandedness.None)
                return;

            PXR_PinchInteractor pinch =
                PXR_InteractorManager.Instance.GetPinchInteractor(hand);
            if (Application.isPlaying && pinch != null)
                pinch.ProcessEvent(HandPinchEvent.PinchUpEvent);
        }
    }
}
