using System;

namespace PicShow
{
    public sealed class GalleryAxisLatch
    {
        private readonly float triggerThreshold;
        private readonly float releaseThreshold;
        private bool latched;

        public GalleryAxisLatch(float triggerThreshold = 0.7f, float releaseThreshold = 0.35f)
        {
            if (triggerThreshold <= 0f || triggerThreshold > 1f)
                throw new ArgumentOutOfRangeException(nameof(triggerThreshold));
            if (releaseThreshold < 0f || releaseThreshold >= triggerThreshold)
                throw new ArgumentOutOfRangeException(nameof(releaseThreshold));

            this.triggerThreshold = triggerThreshold;
            this.releaseThreshold = releaseThreshold;
        }

        public int Update(float horizontal)
        {
            float magnitude = Math.Abs(horizontal);
            if (latched)
            {
                if (magnitude <= releaseThreshold)
                    latched = false;
                return 0;
            }

            if (magnitude < triggerThreshold)
                return 0;

            latched = true;
            return horizontal > 0f ? 1 : -1;
        }
    }
}
