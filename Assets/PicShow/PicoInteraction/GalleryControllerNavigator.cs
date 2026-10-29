using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace PicShow.PicoInteraction
{
    public sealed class GalleryControllerNavigator : MonoBehaviour
    {
        [SerializeField] private GalleryCarousel gallery;
        [SerializeField] private InputActionReference leftMove;
        [SerializeField] private InputActionReference rightMove;
        [SerializeField] private float triggerThreshold = 0.7f;
        [SerializeField] private float releaseThreshold = 0.35f;

        private GalleryAxisLatch leftLatch;
        private GalleryAxisLatch rightLatch;
        private PXR_InteractorManager manager;

        public void Configure(
            GalleryCarousel targetGallery,
            InputActionReference leftMoveAction,
            InputActionReference rightMoveAction)
        {
            gallery = targetGallery;
            leftMove = leftMoveAction;
            rightMove = rightMoveAction;
            ResetLatches();
        }

        private void Awake() => ResetLatches();

        private void Update()
        {
            if (manager == null)
                manager = FindFirstObjectByType<PXR_InteractorManager>();

            bool motionController = manager != null &&
                manager.CurrentInputMode == XRInputModalityManager.InputMode.MotionController;
            float left = leftMove != null ? leftMove.action.ReadValue<Vector2>().x : 0f;
            float right = rightMove != null ? rightMove.action.ReadValue<Vector2>().x : 0f;
            ProcessAxes(left, right, motionController, Time.unscaledTime);
        }

        public bool ProcessAxes(
            float leftHorizontal,
            float rightHorizontal,
            bool motionController,
            float timestamp)
        {
            EnsureLatches();
            if (!motionController || gallery == null)
            {
                leftLatch.Update(0f);
                rightLatch.Update(0f);
                return false;
            }

            int direction = leftLatch.Update(leftHorizontal);
            int rightDirection = rightLatch.Update(rightHorizontal);
            if (direction == 0)
                direction = rightDirection;

            return direction != 0 &&
                gallery.ExecuteInput(GalleryInputCommand.Move(direction), timestamp);
        }

        private void EnsureLatches()
        {
            if (leftLatch == null || rightLatch == null)
                ResetLatches();
        }

        private void ResetLatches()
        {
            leftLatch = new GalleryAxisLatch(triggerThreshold, releaseThreshold);
            rightLatch = new GalleryAxisLatch(triggerThreshold, releaseThreshold);
        }
    }
}
