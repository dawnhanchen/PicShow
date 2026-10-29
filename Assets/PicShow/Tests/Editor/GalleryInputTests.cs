using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace PicShow.Tests
{
    public class GalleryInputTests
    {
        [Test]
        public void MoveCommandRejectsZeroAndLargeOffsets()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GalleryInputCommand.Move(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GalleryInputCommand.Move(2));
        }

        [Test]
        public void SelectionCommandsRejectNegativeIndexes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GalleryInputCommand.SelectCategory(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => GalleryInputCommand.SelectWork(-1));
        }

        [Test]
        public void GateRejectsOnlyConsecutiveDuplicateCommandsInsideWindow()
        {
            var gate = new GalleryInputGate(0.2f);
            var move = GalleryInputCommand.Move(1);

            Assert.That(gate.TryAccept(move, 1f), Is.True);
            Assert.That(gate.TryAccept(move, 1.199f), Is.False);
            Assert.That(gate.TryAccept(GalleryInputCommand.Move(-1), 1.1f), Is.True);
            Assert.That(gate.TryAccept(move, 1.15f), Is.True,
                "A different accepted command ends the duplicate sequence.");
            Assert.That(gate.TryAccept(move, 1.2f), Is.False);
            Assert.That(gate.TryAccept(move, 1.36f), Is.True);
        }

        [Test]
        public void AxisLatchRequiresReleaseBeforeRepeating()
        {
            var latch = new GalleryAxisLatch(0.7f, 0.35f);

            Assert.That(latch.Update(0.69f), Is.Zero);
            Assert.That(latch.Update(0.7f), Is.EqualTo(1));
            Assert.That(latch.Update(1f), Is.Zero);
            Assert.That(latch.Update(-1f), Is.Zero);
            Assert.That(latch.Update(0.36f), Is.Zero);
            Assert.That(latch.Update(0.35f), Is.Zero);
            Assert.That(latch.Update(-0.7f), Is.EqualTo(-1));
        }

        [Test]
        public void SwipeCommitsAtMostOnceWhenThresholdIsReached()
        {
            var swipe = new GallerySwipeDecision(0.1f);

            swipe.Begin(0.25f);
            swipe.Update(0.1f);
            Assert.That(swipe.End(0.1f), Is.EqualTo(1), "Dragging left should show the next work.");
            Assert.That(swipe.End(-0.2f), Is.Zero, "A completed swipe cannot commit twice.");
        }

        [Test]
        public void SwipeCancelsBelowThresholdAndResetsForNextGesture()
        {
            var swipe = new GallerySwipeDecision(0.1f);

            swipe.Begin(0f);
            swipe.Update(0.09f);
            Assert.That(swipe.End(0.09f), Is.Zero);

            swipe.Begin(0f);
            Assert.That(swipe.End(0.11f), Is.EqualTo(-1), "Dragging right should show the previous work.");
        }

        [Test]
        public void CarouselCommandEntryDeduplicatesEquivalentCallbacks()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/PicShow/Prefabs/FourRealmsGallery.prefab");
            var instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var gallery = instance.GetComponent<GalleryCarousel>();
                gallery.RefreshPreview();

                Assert.That(gallery.ExecuteInput(GalleryInputCommand.Move(1), 1f), Is.True);
                Assert.That(gallery.ExecuteInput(GalleryInputCommand.Move(1), 1.1f), Is.False);
                Assert.That(gallery.WorkIndex, Is.EqualTo(1));

                Assert.That(gallery.ExecuteInput(GalleryInputCommand.Move(-1), 1.1f), Is.True);
                Assert.That(gallery.WorkIndex, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }
    }
}
