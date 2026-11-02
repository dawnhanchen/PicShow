using NUnit.Framework;
using PicShow;
using PicShow.PicoInteraction;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PicShow.Tests
{
    public class GalleryCardHoverTests
    {
        private const string PrefabPath = "Assets/PicShow/Prefabs/FourRealmsGallery.prefab";

        [Test]
        public void SideCardPointerEnterRequestsHover()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var hover = left.Root.gameObject.AddComponent<GalleryCardHover>();
                hover.Configure(gallery, left);

                hover.OnPointerEnter(new PointerEventData(null));
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void PointerExitReleasesHover()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var hover = left.Root.gameObject.AddComponent<GalleryCardHover>();
                hover.Configure(gallery, left);
                hover.OnPointerEnter(new PointerEventData(null));
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));

                hover.OnPointerExit(new PointerEventData(null));
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(left), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void CentreCardPointerEnterIsIgnored()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var center = gallery.Cards[1];
                var leftHover = left.Root.gameObject.AddComponent<GalleryCardHover>();
                leftHover.Configure(gallery, left);
                var centerHover = center.Root.gameObject.AddComponent<GalleryCardHover>();
                centerHover.Configure(gallery, center);

                centerHover.OnPointerEnter(new PointerEventData(null));
                leftHover.OnPointerEnter(new PointerEventData(null));
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(center), Is.Zero);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void HoverSurvivesUntilEveryPointerExits()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var hover = left.Root.gameObject.AddComponent<GalleryCardHover>();
                hover.Configure(gallery, left);
                var first = new PointerEventData(null) { pointerId = 1 };
                var second = new PointerEventData(null) { pointerId = 2 };

                hover.OnPointerEnter(first);
                hover.OnPointerEnter(second);
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));

                hover.OnPointerExit(first);
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f),
                    "One pointer leaving must not cancel another pointer's hover.");

                hover.OnPointerExit(second);
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void ModeChangeResetReleasesHover()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var left = gallery.Cards[0];
                var hover = left.Root.gameObject.AddComponent<GalleryCardHover>();
                hover.Configure(gallery, left);
                hover.OnPointerEnter(new PointerEventData(null));
                gallery.TickHover(1f);
                Assert.That(gallery.HoverProgress(left), Is.EqualTo(1f).Within(0.001f));

                hover.ResetPointers();
                gallery.TickHover(1f);

                Assert.That(gallery.HoverProgress(left), Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        [Test]
        public void AdapterAddsHoverComponentsToEveryCard()
        {
            GalleryCarousel gallery = CreateGallery(out GameObject instance);
            try
            {
                var adapter = instance.GetComponent<GalleryPicoInteractionAdapter>();
                Assert.That(adapter, Is.Not.Null);

                adapter.Configure(new UnityEngine.UI.Button[0]);

                foreach (var card in gallery.Cards)
                {
                    var hover = card.Root.GetComponent<GalleryCardHover>();
                    Assert.That(hover, Is.Not.Null,
                        "Every card slot must keep its hover binding so recycled cards stay hoverable.");
                    Assert.That(hover.Card, Is.EqualTo(card));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static GalleryCarousel CreateGallery(out GameObject instance)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            instance = Object.Instantiate(prefab);
            var gallery = instance.GetComponent<GalleryCarousel>();
            Assert.That(gallery, Is.Not.Null);
            gallery.RefreshPreview();
            return gallery;
        }
    }
}
