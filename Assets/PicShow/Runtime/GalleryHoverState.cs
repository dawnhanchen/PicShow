using UnityEngine;

namespace PicShow
{
    public enum GalleryHoverMode
    {
        EyeGaze,
        MotionController
    }

    public sealed class GalleryHoverProfile
    {
        public readonly float Lift;
        public readonly float ScaleBoost;
        public readonly float YawTurn;
        public readonly float Glow;
        public readonly float FadeIn;
        public readonly float FadeOut;

        public GalleryHoverProfile(float lift, float scaleBoost, float yawTurn, float glow, float fadeIn, float fadeOut)
        {
            Lift = lift;
            ScaleBoost = scaleBoost;
            YawTurn = yawTurn;
            Glow = glow;
            FadeIn = fadeIn;
            FadeOut = fadeOut;
        }

        // Eye gaze needs calmer motion but a stronger halo; a ray pointer can afford a bolder lift.
        private static readonly GalleryHoverProfile EyeProfile =
            new GalleryHoverProfile(24f, 0.04f, 6f, 32f, 0.15f, 0.22f);
        private static readonly GalleryHoverProfile ControllerProfile =
            new GalleryHoverProfile(40f, 0.06f, 6f, 28f, 0.18f, 0.22f);

        public static GalleryHoverProfile For(GalleryHoverMode mode) =>
            mode == GalleryHoverMode.EyeGaze ? EyeProfile : ControllerProfile;
    }

    public sealed class GalleryHoverState
    {
        private float progress;
        private bool hovered;
        private GalleryHoverMode mode = GalleryHoverMode.MotionController;

        public float Progress => progress;
        public bool IsHovered => hovered;
        public GalleryHoverMode Mode => mode;
        public GalleryHoverProfile Profile => GalleryHoverProfile.For(mode);

        public void Enter(GalleryHoverMode nextMode)
        {
            hovered = true;
            mode = nextMode;
        }

        public void Exit() => hovered = false;

        public void Clear()
        {
            hovered = false;
            progress = 0f;
        }

        public bool Advance(float deltaTime)
        {
            float target = hovered ? 1f : 0f;
            float fade = hovered ? Profile.FadeIn : Profile.FadeOut;
            float speed = fade > 0f ? 1f / fade : 1f;
            float next = Mathf.MoveTowards(progress, target, speed * Mathf.Max(0f, deltaTime));
            bool changed = !Mathf.Approximately(next, progress);
            progress = next;
            return changed;
        }
    }
}
