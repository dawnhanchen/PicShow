using System;

namespace PicShow
{
    public sealed class CarouselState
    {
        private readonly int categoryCount;
        public int CategoryIndex { get; private set; }
        public int WorkIndex { get; private set; }
        public int WorkCount { get; }

        public CarouselState(int categoryCount, int workCount)
        {
            if (categoryCount <= 0) throw new ArgumentOutOfRangeException(nameof(categoryCount));
            if (workCount <= 0) throw new ArgumentOutOfRangeException(nameof(workCount));
            this.categoryCount = categoryCount;
            WorkCount = workCount;
        }

        public void Move(int offset) => WorkIndex = Neighbour(offset);

        public void SelectCategory(int index)
        {
            if (index < 0 || index >= categoryCount) throw new ArgumentOutOfRangeException(nameof(index));
            CategoryIndex = index;
        }

        public void SelectWork(int index)
        {
            if (index < 0 || index >= WorkCount) throw new ArgumentOutOfRangeException(nameof(index));
            WorkIndex = index;
        }

        public int Neighbour(int offset)
        {
            long value = (long)WorkIndex + offset;
            return (int)((value % WorkCount + WorkCount) % WorkCount);
        }
    }
}
