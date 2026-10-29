using System;

namespace PicShow
{
    public sealed class GallerySwipeDecision
    {
        private readonly float commitThreshold;
        private bool active;
        private float startX;
        private float currentX;

        public GallerySwipeDecision(float commitThreshold)
        {
            if (commitThreshold <= 0f)
                throw new ArgumentOutOfRangeException(nameof(commitThreshold));

            this.commitThreshold = commitThreshold;
        }

        public void Begin(float localX)
        {
            startX = localX;
            currentX = localX;
            active = true;
        }

        public void Update(float localX)
        {
            if (active)
                currentX = localX;
        }

        public int End(float localX)
        {
            if (!active)
                return 0;

            currentX = localX;
            active = false;
            float delta = currentX - startX;
            if (Math.Abs(delta) < commitThreshold)
                return 0;

            return delta < 0f ? 1 : -1;
        }
    }
}
