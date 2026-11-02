using NUnit.Framework;
using PicShow;
using PicShow.PicoInteraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace PicShow.Tests
{
    public class GalleryHoverApplicationTests
    {
        private const string PrefabPath = "Assets/PicShow/Prefabs/FourRealmsGallery.prefab";
        private const float SideScale = 0.78f;

        [Test]
        public void ControllerHoverLiftsAndScalesOnlyTheHoveredSideCard()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var center = gallery.Cards[1];
                RectTransform leftVisual = Visual(left);
                RectTransform centerVisual = Visual(center);
                Vector3 rootPosition = left.Root.localPosition;
                Vector3 rootScale = left.Root.localScale;

                Assert.That(gallery.SetHover(left, true, GalleryHoverMode.MotionController), Is.True);
                gallery.TickHover(1f);

                Assert.That(leftVisual.localPosition.z, Is.EqualTo(-40f).Within(0.2f));
                Assert.That(leftVisual.localScale.x, Is.EqualTo(1f + 0.06f / SideScale).Within(0.0001f));
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));
                Assert.That(left.Root.localPosition, Is.EqualTo(rootPosition));
                Assert.That(left.Root.localScale, Is.EqualTo(rootScale));
                Assert.That(centerVisual.localPosition.z, Is.Zero.Within(0.001f));
                Assert.That(centerVisual.localScale.x, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void EyeGazeHoverUsesCalmerLiftAndWiderGlowThanController()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                RectTransform leftVisual = Visual(left);
                float baseBorderWidth = left.Border.rectTransform.sizeDelta.x;

                gallery.SetHover(left, true, GalleryHoverMode.EyeGaze);
                gallery.TickHover(1f);

                Assert.That(leftVisual.localPosition.z, Is.EqualTo(-24f).Within(0.2f));
                Assert.That(leftVisual.localScale.x, Is.EqualTo(1f + 0.04f / SideScale).Within(0.0001f));
                Assert.That(left.Border.rectTransform.sizeDelta.x, Is.EqualTo(baseBorderWidth + 32f).Within(0.2f));

                gallery.ClearHover();
                gallery.SetHover(left, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);

                Assert.That(leftVisual.localPosition.z, Is.EqualTo(-40f).Within(0.2f));
                Assert.That(left.Border.rectTransform.sizeDelta.x, Is.EqualTo(baseBorderWidth + 28f).Within(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HoverBrightensArtworkAndCaption()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                Color baseArtwork = left.Artwork.color;
                float baseCaptionLuminance = left.Caption.color.grayscale;

                gallery.SetHover(left, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);

                Assert.That(left.Artwork.color.r, Is.GreaterThan(baseArtwork.r));
                Assert.That(left.Artwork.color.g, Is.GreaterThan(baseArtwork.g));
                Assert.That(left.Caption.color.grayscale, Is.GreaterThan(baseCaptionLuminance));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ChangingModeWhileHoveredReappliesTheNewProfile()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                RectTransform leftVisual = Visual(left);
                float baseBorderWidth = left.Border.rectTransform.sizeDelta.x;

                gallery.SetHover(left, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);

                gallery.SetHover(left, true, GalleryHoverMode.EyeGaze);
                gallery.TickHover(1f);

                Assert.That(leftVisual.localPosition.z, Is.EqualTo(-24f).Within(0.2f));
                Assert.That(leftVisual.localScale.x, Is.EqualTo(1f + 0.04f / SideScale).Within(0.0001f));
                Assert.That(left.Border.rectTransform.sizeDelta.x,
                    Is.EqualTo(baseBorderWidth + 32f).Within(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HoverRendersAVividWideGlowLayer()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var glow = Visual(left).Find(GalleryCarousel.HoverGlowLayerName).GetComponent<Image>();
                Assert.That(glow, Is.Not.Null, "Cards must render a dedicated glow layer.");
                Assert.That(glow.raycastTarget, Is.False);
                Assert.That(glow.color.a, Is.Zero.Within(0.001f));

                gallery.SetHover(left, true, GalleryHoverMode.EyeGaze);
                gallery.TickHover(1f);

                Assert.That(left.Border.color.grayscale, Is.GreaterThan(0.9f),
                    "The hover frame must use a bright highlight colour.");
                Assert.That(glow.color.a, Is.GreaterThan(0.45f), "The halo must be clearly visible.");
                Color.RGBToHSV(glow.color, out _, out float saturation, out _);
                Assert.That(saturation, Is.GreaterThan(0.5f), "The halo must use a vivid highlight hue.");
                Assert.That(glow.rectTransform.sizeDelta.x,
                    Is.GreaterThan(left.Border.rectTransform.sizeDelta.x + 30f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void NavigationHoverUsesTheCardNowInTheSlot()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];

                gallery.SetHover(left, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);
                Assert.That(Visual(left).localPosition.z, Is.EqualTo(-40f).Within(0.2f));

                gallery.Move(1);
                // The left card has moved to the right slot; the slot now holds another card.
                gallery.SetHover(gallery.Cards[0], true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);

                Assert.That(Visual(gallery.Cards[0]).localPosition.z, Is.EqualTo(-40f).Within(0.2f),
                    "Hover must animate the card that currently occupies the slot.");
                Assert.That(Visual(left).localPosition.z, Is.Zero.Within(0.001f),
                    "The previous slot owner must not receive the new hover.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void NavigationReattachesHoverForPointersThatNeverLeft()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var hover = left.Root.GetComponent<GalleryCardHover>();
                Assert.That(hover, Is.Not.Null);
                hover.OnPointerEnter(new UnityEngine.EventSystems.PointerEventData(null));
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));

                gallery.Move(1);
                hover.ReevaluatePointers();
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f),
                    "A pointer that never left must keep its card highlighted after navigation.");
                Assert.That(Visual(left).localPosition.z, Is.EqualTo(-40f).Within(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void VisualWrapperPreservesPaintingAboveTheFrame()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                RectTransform visual = Visual(gallery.Cards[0]);
                int shadow = visual.Find("Shadow").GetSiblingIndex();
                int frame = visual.Find("Frame").GetSiblingIndex();
                int artwork = visual.Find("Artwork").GetSiblingIndex();

                Assert.That(shadow, Is.LessThan(frame), "The shadow must stay behind the frame.");
                Assert.That(artwork, Is.GreaterThan(frame),
                    "The artwork must render above the opaque frame.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HoverNeverMovesTheRaycastArea()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                RectTransform hitArea = (RectTransform)left.Root.Find(GalleryCarousel.HoverHitAreaLayerName);
                Assert.That(hitArea, Is.Not.Null, "Cards need a dedicated raycast area.");
                Assert.That(hitArea.GetComponent<Image>().raycastTarget, Is.True);
                Assert.That(left.Button.targetGraphic, Is.EqualTo(hitArea.GetComponent<Image>()));

                var before = new Vector3[4];
                hitArea.GetWorldCorners(before);
                Vector3 rootPosition = left.Root.localPosition;
                Vector3 rootScale = left.Root.localScale;

                gallery.SetHover(left, true, GalleryHoverMode.EyeGaze);
                gallery.TickHover(1f);

                Assert.That(left.Root.localPosition, Is.EqualTo(rootPosition));
                Assert.That(left.Root.localScale, Is.EqualTo(rootScale));
                var after = new Vector3[4];
                hitArea.GetWorldCorners(after);
                for (int i = 0; i < 4; i++)
                    Assert.That(Vector3.Distance(after[i], before[i]), Is.LessThan(0.0001f),
                        "Hover visuals must not move the raycast area.");
                Assert.That(Visual(left).localPosition.z, Is.LessThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void BothSideCardsStayHoveredIndependently()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var right = gallery.Cards[2];

                gallery.SetHover(left, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);
                gallery.SetHover(right, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));
                Assert.That(gallery.HoverProgress(right), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void MoveClearsHoverAndRestoresTheBasePose()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                RectTransform leftVisual = Visual(left);
                float baseBorderWidth = left.Border.rectTransform.sizeDelta.x;

                gallery.SetHover(left, true, GalleryHoverMode.MotionController);
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));

                gallery.Move(1);

                Assert.That(gallery.HoverProgress(left), Is.Zero);
                Assert.That(leftVisual.localPosition.z, Is.Zero.Within(0.001f));
                Assert.That(leftVisual.localScale.x, Is.EqualTo(1f).Within(0.001f));
                Assert.That(left.Root.localScale.x, Is.EqualTo(SideScale).Within(0.001f));
                Assert.That(left.Border.rectTransform.sizeDelta.x, Is.EqualTo(baseBorderWidth).Within(0.2f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CentreCardNeverAcceptsHover()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var center = gallery.Cards[1];
                RectTransform centerVisual = Visual(center);

                Assert.That(gallery.SetHover(center, true, GalleryHoverMode.EyeGaze), Is.False);
                Assert.That(gallery.SetHover(left, true, GalleryHoverMode.EyeGaze), Is.True);
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(center), Is.Zero);
                Assert.That(centerVisual.localPosition.z, Is.Zero.Within(0.001f));
                Assert.That(centerVisual.localScale.x, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static RectTransform Visual(GalleryCarousel.Card card) =>
            (RectTransform)card.Root.Find(GalleryCarousel.HoverVisualLayerName);

        private static GalleryCarousel CreateGallery(out GameObject instance)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            instance = Object.Instantiate(prefab);
            var gallery = instance.GetComponent<GalleryCarousel>();
            Assert.That(gallery, Is.Not.Null);
            var adapter = instance.GetComponent<GalleryPicoInteractionAdapter>();
            Assert.That(adapter, Is.Not.Null);
            // Binding creates the visual wrapper, the fixed raycast area and the glow layer.
            adapter.Configure(new Button[0]);
            gallery.RefreshPreview();
            return gallery;
        }
    }
}
