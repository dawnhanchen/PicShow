using NUnit.Framework;
using PicShow;

namespace PicShow.Tests
{
    public class GalleryHoverTests
    {
        [Test]
        public void EyeGazeHoverFillsWithinFifteenHundredths()
        {
            var state = new GalleryHoverState();
            state.Enter(GalleryHoverMode.EyeGaze);

            state.Advance(0.075f);

            Assert.That(state.Progress, Is.EqualTo(0.5f).Within(0.01f));
            state.Advance(0.075f);
            Assert.That(state.Progress, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void HoverFadesOutAfterExit()
        {
            var state = new GalleryHoverState();
            state.Enter(GalleryHoverMode.MotionController);
            state.Advance(1f);
            state.Exit();

            state.Advance(0.11f);
            Assert.That(state.Progress, Is.EqualTo(0.5f).Within(0.02f));
            state.Advance(0.11f);
            Assert.That(state.Progress, Is.Zero.Within(0.001f));
            Assert.That(state.IsHovered, Is.False);
        }

        [Test]
        public void ClearResetsHoverImmediately()
        {
            var state = new GalleryHoverState();
            state.Enter(GalleryHoverMode.EyeGaze);
            state.Advance(0.2f);
            Assert.That(state.IsHovered, Is.True);
            Assert.That(state.Progress, Is.EqualTo(1f));

            state.Clear();

            Assert.That(state.Progress, Is.Zero);
            Assert.That(state.IsHovered, Is.False);
        }

        [Test]
        public void EyeProfileMovesLessButGlowsMoreThanController()
        {
            var eye = GalleryHoverProfile.For(GalleryHoverMode.EyeGaze);
            var controller = GalleryHoverProfile.For(GalleryHoverMode.MotionController);

            Assert.That(eye.Lift, Is.LessThan(controller.Lift));
            Assert.That(eye.ScaleBoost, Is.LessThan(controller.ScaleBoost));
            Assert.That(eye.Glow, Is.GreaterThan(controller.Glow));
            Assert.That(eye.FadeIn, Is.EqualTo(0.15f).Within(0.0001f));
            Assert.That(controller.FadeIn, Is.EqualTo(0.18f).Within(0.0001f));
        }

        [Test]
        public void ProgressClampsWithoutOvershoot()
        {
            var state = new GalleryHoverState();
            state.Enter(GalleryHoverMode.EyeGaze);

            Assert.That(state.Advance(5f), Is.True);
            Assert.That(state.Progress, Is.EqualTo(1f));

            state.Exit();
            Assert.That(state.Advance(5f), Is.True);
            Assert.That(state.Progress, Is.Zero);
        }

        [Test]
        public void ZeroDeltaDoesNotChangeProgress()
        {
            var state = new GalleryHoverState();
            state.Enter(GalleryHoverMode.EyeGaze);
            state.Advance(0.075f);

            Assert.That(state.Advance(0f), Is.False);
            Assert.That(state.Progress, Is.EqualTo(0.5f).Within(0.01f));
        }

        [Test]
        public void ReEnterUpdatesModeWhileHovered()
        {
            var state = new GalleryHoverState();
            state.Enter(GalleryHoverMode.EyeGaze);
            state.Advance(0.2f);

            state.Enter(GalleryHoverMode.MotionController);

            Assert.That(state.Mode, Is.EqualTo(GalleryHoverMode.MotionController));
            Assert.That(state.Profile.Lift,
                Is.EqualTo(GalleryHoverProfile.For(GalleryHoverMode.MotionController).Lift));
            Assert.That(state.Progress, Is.EqualTo(1f));
        }
    }
}
