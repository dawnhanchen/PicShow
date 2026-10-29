using System;

namespace PicShow
{
    public enum GalleryInputCommandType
    {
        Move,
        SelectCategory,
        SelectWork
    }

    public readonly struct GalleryInputCommand : IEquatable<GalleryInputCommand>
    {
        private GalleryInputCommand(GalleryInputCommandType type, int value)
        {
            Type = type;
            Value = value;
        }

        public GalleryInputCommandType Type { get; }
        public int Value { get; }

        public static GalleryInputCommand Move(int direction)
        {
            if (direction != -1 && direction != 1)
                throw new ArgumentOutOfRangeException(nameof(direction));

            return new GalleryInputCommand(GalleryInputCommandType.Move, direction);
        }

        public static GalleryInputCommand SelectCategory(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return new GalleryInputCommand(GalleryInputCommandType.SelectCategory, index);
        }

        public static GalleryInputCommand SelectWork(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index));

            return new GalleryInputCommand(GalleryInputCommandType.SelectWork, index);
        }

        public bool Equals(GalleryInputCommand other) => Type == other.Type && Value == other.Value;
        public override bool Equals(object obj) => obj is GalleryInputCommand other && Equals(other);
        public override int GetHashCode() => ((int)Type * 397) ^ Value;
    }
}
