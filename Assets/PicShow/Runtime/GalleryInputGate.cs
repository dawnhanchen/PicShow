using System;

namespace PicShow
{
    public sealed class GalleryInputGate
    {
        private readonly float duplicateWindowSeconds;
        private GalleryInputCommand lastCommand;
        private float lastTimestamp;
        private bool hasAcceptedCommand;

        public GalleryInputGate(float duplicateWindowSeconds = 0.2f)
        {
            if (duplicateWindowSeconds < 0f)
                throw new ArgumentOutOfRangeException(nameof(duplicateWindowSeconds));

            this.duplicateWindowSeconds = duplicateWindowSeconds;
        }

        public bool TryAccept(GalleryInputCommand command, float timestamp)
        {
            if (hasAcceptedCommand &&
                command.Equals(lastCommand) &&
                timestamp >= lastTimestamp &&
                timestamp - lastTimestamp < duplicateWindowSeconds)
            {
                return false;
            }

            lastCommand = command;
            lastTimestamp = timestamp;
            hasAcceptedCommand = true;
            return true;
        }
    }
}
