using System;
using NUnit.Framework;

namespace PicShow.Tests
{
    public class CarouselStateTests
    {
        [Test]
        public void FirstWorkHasLastAndSecondNeighbours()
        {
            var state = new CarouselState(4, 5);
            Assert.That(state.CategoryIndex, Is.Zero);
            Assert.That(state.WorkIndex, Is.Zero);
            Assert.That(state.Neighbour(-1), Is.EqualTo(4));
            Assert.That(state.Neighbour(1), Is.EqualTo(1));
        }

        [Test]
        public void MovesWrapInBothDirections()
        {
            var state = new CarouselState(4, 5);
            state.Move(-1);
            Assert.That(state.WorkIndex, Is.EqualTo(4));
            state.Move(1);
            Assert.That(state.WorkIndex, Is.Zero);
            state.Move(12);
            Assert.That(state.WorkIndex, Is.EqualTo(2));
        }

        [Test]
        public void CategorySelectionPreservesWorkAcrossRapidInput()
        {
            var state = new CarouselState(4, 5);
            state.SelectWork(3);
            state.SelectCategory(2);
            Assert.That(state.WorkIndex, Is.EqualTo(3));
            Assert.That(state.CategoryIndex, Is.EqualTo(2));
            for (int i = 0; i < 21; i++) state.Move(1);
            state.SelectCategory(1);
            Assert.That(state.WorkIndex, Is.EqualTo(4));
            Assert.That(state.CategoryIndex, Is.EqualTo(1));
        }

        [TestCase(0, 5)]
        [TestCase(4, 0)]
        [TestCase(-1, 5)]
        public void EmptyOrInvalidCatalogIsRejected(int categories, int works)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CarouselState(categories, works));
        }

        [Test]
        public void InvalidSelectionsDoNotMutateState()
        {
            var state = new CarouselState(4, 5);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectCategory(4));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectCategory(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectWork(5));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.SelectWork(-1));
            Assert.That(state.WorkIndex, Is.Zero);
            Assert.That(state.CategoryIndex, Is.Zero);
        }
    }
}
