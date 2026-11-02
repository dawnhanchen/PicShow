using System.Collections.Generic;
using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace PicShow.PicoInteraction
{
    public sealed class GalleryCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GalleryCarousel gallery;
        [SerializeField] private GalleryCarousel.Card card;

        private readonly Dictionary<int, Component> pointers = new Dictionary<int, Component>();
        private bool hovering;
        private GalleryHoverMode mode = GalleryHoverMode.MotionController;

        public GalleryCarousel Gallery => gallery;
        public GalleryCarousel.Card Card => card;

        public void Configure(GalleryCarousel targetGallery, GalleryCarousel.Card targetCard)
        {
            gallery = targetGallery;
            card = targetCard;
            pointers.Clear();
            hovering = false;
            EnsureVisualAndHitArea();
            EnsureGlowLayer();
        }

        // Drops every pointer and clears the hover. Called when the input modality changes so a
        // stale eye or controller pointer can never keep highlighting the wrong image.
        public void ResetPointers()
        {
            pointers.Clear();
            hovering = false;
            if (gallery != null && card != null)
                gallery.SetHover(card, false, mode);
        }

        public void ReevaluatePointers()
        {
            if (gallery == null || card == null) return;
            if (pointers.Count == 0)
            {
                if (!hovering) return;
                hovering = false;
                gallery.SetHover(card, false, mode);
                return;
            }

            hovering = true;
            mode = ResolveMode();
            gallery.SetHover(card, true, mode);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (gallery == null || card == null) return;
            pointers[eventData.pointerId] = ResolveInteractor(eventData);
            hovering = true;
            mode = ResolveMode();
            gallery.SetHover(card, true, mode);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointers.Remove(eventData.pointerId);
            if (pointers.Count != 0 || !hovering) return;
            hovering = false;
            if (gallery != null && card != null)
                gallery.SetHover(card, false, mode);
        }

        private void Update()
        {
            if (gallery == null || card == null) return;
            PruneInactivePointers();

            if (pointers.Count == 0)
            {
                if (!hovering) return;
                hovering = false;
                gallery.SetHover(card, false, mode);
                return;
            }

            if (!hovering)
            {
                ReevaluatePointers();
                return;
            }

            // Carousel navigation recycles slots and clears hover; re-assert for pointers that
            // stayed on the card so the highlight follows the card to its new slot.
            if (gallery.IsTransitioning) return;
            if (gallery.HoverProgress(card) <= 0f)
            {
                gallery.SetHover(card, true, mode);
                return;
            }

            GalleryHoverMode current = ResolveMode();
            if (current == mode) return;
            mode = current;
            gallery.SetHover(card, true, mode);
        }

        private void PruneInactivePointers()
        {
            if (pointers.Count == 0) return;
            List<int> stale = null;
            foreach (var pair in pointers)
            {
                Component interactor = pair.Value;
                if (interactor == null || interactor.gameObject.activeInHierarchy)
                    continue;
                if (stale == null) stale = new List<int>();
                stale.Add(pair.Key);
            }
            if (stale == null) return;
            foreach (int id in stale) pointers.Remove(id);
        }

        private static Component ResolveInteractor(PointerEventData eventData)
        {
            if (EventSystem.current == null) return null;
            if (!(EventSystem.current.currentInputModule is XRUIInputModule module)) return null;
            return module.GetInteractor(eventData.pointerId) as Component;
        }

        private static GalleryHoverMode ResolveMode()
        {
            var manager = Object.FindFirstObjectByType<PXR_InteractorManager>();
            return manager != null &&
                manager.CurrentInputMode == XRInputModalityManager.InputMode.TrackedHand
                ? GalleryHoverMode.EyeGaze
                : GalleryHoverMode.MotionController;
        }

        // The raycast surface stays on the carousel pose; only the visual children react.
        private void EnsureVisualAndHitArea()
        {
            if (card == null || card.Root == null) return;

            if (card.Root.Find(GalleryCarousel.HoverVisualLayerName) == null)
            {
                var visualObject = new GameObject(GalleryCarousel.HoverVisualLayerName,
                    typeof(RectTransform));
                var visual = (RectTransform)visualObject.transform;
                visual.SetParent(card.Root, false);
                visual.anchorMin = new Vector2(0.5f, 0.5f);
                visual.anchorMax = new Vector2(0.5f, 0.5f);
                visual.pivot = new Vector2(0.5f, 0.5f);
                visual.anchoredPosition = Vector2.zero;
                visual.sizeDelta = card.Root.sizeDelta;
                visual.localRotation = Quaternion.identity;
                visual.localScale = Vector3.one;

                // Collect first: reparenting mutates the child list, and moving in source order
                // keeps the shadow, frame, artwork and labels stacking exactly as before.
                int count = card.Root.childCount;
                var children = new Transform[count];
                for (int i = 0; i < count; i++)
                    children[i] = card.Root.GetChild(i);
                foreach (Transform child in children)
                    child.SetParent(visual, false);
            }

            if (card.Root.Find(GalleryCarousel.HoverHitAreaLayerName) != null) return;

            Image face = card.Button != null ? card.Button.targetGraphic as Image : null;
            var hitObject = new GameObject(GalleryCarousel.HoverHitAreaLayerName,
                typeof(RectTransform), typeof(Image));
            var hit = hitObject.GetComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            hit.raycastTarget = true;

            var rect = (RectTransform)hitObject.transform;
            rect.SetParent(card.Root, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = face != null ? face.rectTransform.sizeDelta : card.Root.sizeDelta;
            rect.SetSiblingIndex(0);

            if (face != null) face.raycastTarget = false;
            if (card.Button != null) card.Button.targetGraphic = hit;
        }

        // The carousel animates this layer; it must exist before the first hover frame.
        private void EnsureGlowLayer()
        {
            if (card == null || card.Root == null || card.Border == null)
                return;
            Transform visual = card.Root.Find(GalleryCarousel.HoverVisualLayerName);
            Transform parent = visual != null ? visual : card.Root;
            if (parent.Find(GalleryCarousel.HoverGlowLayerName) != null)
                return;

            var glowObject = new GameObject(GalleryCarousel.HoverGlowLayerName,
                typeof(RectTransform), typeof(Image));
            var glow = glowObject.GetComponent<Image>();
            var source = card.Border;
            glow.sprite = source.sprite;
            glow.type = source.type;
            glow.pixelsPerUnitMultiplier = source.pixelsPerUnitMultiplier;
            glow.raycastTarget = false;
            glow.color = new Color(1f, 1f, 1f, 0f);

            var rect = (RectTransform)glowObject.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = source.rectTransform.anchorMin;
            rect.anchorMax = source.rectTransform.anchorMax;
            rect.pivot = source.rectTransform.pivot;
            rect.anchoredPosition = source.rectTransform.anchoredPosition;
            rect.sizeDelta = source.rectTransform.sizeDelta;
            rect.SetSiblingIndex(source.rectTransform.GetSiblingIndex());
        }
    }
}
