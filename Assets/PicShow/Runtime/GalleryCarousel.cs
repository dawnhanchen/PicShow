using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PicShow
{
    public sealed class GalleryCarousel : MonoBehaviour
    {
        [Serializable]
        public sealed class Card
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public RawImage Artwork;
            public Image Border;
            public Button Button;
            public TMP_Text Caption;
            public TMP_Text Number;
        }

        [Serializable]
        public sealed class Tab
        {
            public Button Button;
            public Image Background;
            public TMP_Text Label;
        }

        [Serializable]
        public sealed class PositionDot
        {
            public Button Button;
            public Image Marker;
        }

        [Header("Content")]
        [SerializeField] private GalleryCatalog catalog;
        [SerializeField] private Card[] cards = new Card[3];
        [SerializeField] private Tab[] tabs = new Tab[4];
        [SerializeField] private PositionDot[] dots = new PositionDot[5];
        [Header("Navigation and labels")]
        [SerializeField] private TMP_Text styleDescription;
        [SerializeField] private TMP_Text workMeta;
        [SerializeField] private TMP_Text workTitle;
        [SerializeField] private TMP_Text workDescription;
        [SerializeField] private Image accentRule;
        [Header("Spatial layout (canvas units)")]
        [SerializeField] private float sideOffset = 760f;
        // Concave arc: side cards sit closer to the viewer than the centre card.
        [SerializeField] private float sideDepth = -100f;
        [SerializeField] private float centerDepth = 60f;
        [SerializeField, Range(0.5f, 1f)] private float sideScale = 0.78f;
        [SerializeField] private float sideAngle = 24f;
        [SerializeField, Range(0f, 1f)] private float transitionDuration = 0.38f;

        private static readonly Color Ink = new Color32(237, 241, 233, 255);
        private static readonly Color Muted = new Color32(172, 185, 180, 255);
        private CarouselState state;
        private GalleryInputGate inputGate;
        private Coroutine transition;
        private bool bound;
        private GalleryHoverState[] hover;
        private Color[] baseArtwork;
        private Color[] baseBorder;
        private Vector2[] baseBorderSize;
        private Image[] hoverGlow;
        private RectTransform[] hoverVisual;

        private const float HoverGlowSpread = 76f;
        private const float HoverGlowAlpha = 0.55f;

        public const string HoverGlowLayerName = "Hover glow";
        public const string HoverVisualLayerName = "Hover visual";
        public const string HoverHitAreaLayerName = "Hover hit area";

        public GalleryCatalog Catalog => catalog;
        public int CategoryIndex => state?.CategoryIndex ?? 0;
        public int WorkIndex => state?.WorkIndex ?? 0;
        public IReadOnlyList<Card> Cards => cards;
        public IReadOnlyList<Tab> Tabs => tabs;
        public IReadOnlyList<PositionDot> Dots => dots;
        public string CurrentTitle => workTitle.text;
        public bool IsTransitioning => transition != null;

        // Used by the editor builder; all references are serialized into the prefab.
        public void Configure(GalleryCatalog content, Card[] cardViews, Tab[] tabViews,
            PositionDot[] dotViews,
            TMP_Text style, TMP_Text meta, TMP_Text title, TMP_Text description, Image rule)
        {
            catalog = content;
            cards = cardViews;
            tabs = tabViews;
            dots = dotViews;
            styleDescription = style;
            workMeta = meta;
            workTitle = title;
            workDescription = description;
            accentRule = rule;
            state = null;
            RefreshPreview();
        }

        public void RefreshPreview()
        {
            if (!EnsureState()) return;
            RefreshContent();
            Snap();
        }

        private void OnEnable()
        {
            if (!EnsureState()) return;
            if (!bound)
            {
                for (int i = 0; i < tabs.Length; i++)
                {
                    int index = i;
                    tabs[i].Button.onClick.AddListener(
                        () => Submit(GalleryInputCommand.SelectCategory(index)));
                }
                for (int i = 0; i < dots.Length; i++)
                {
                    int index = i;
                    dots[i].Button.onClick.AddListener(
                        () => Submit(GalleryInputCommand.SelectWork(index)));
                }
                foreach (var card in cards)
                {
                    Card captured = card;
                    captured.Button.onClick.AddListener(() =>
                    {
                        int slot = Array.IndexOf(cards, captured) - 1;
                        if (slot != 0) Submit(GalleryInputCommand.Move(slot));
                    });
                }
                bound = true;
            }
            RefreshPreview();
        }

        private bool EnsureState()
        {
            if (state != null) return true;
            if (catalog == null) return false;
            if (!catalog.Validate(out string error))
            {
                Debug.LogError(error, this);
                return false;
            }
            state = new CarouselState(catalog.Categories.Length, catalog.Categories[0].Works.Length);
            return true;
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame) Submit(GalleryInputCommand.Move(-1));
                if (keyboard.rightArrowKey.wasPressedThisFrame) Submit(GalleryInputCommand.Move(1));
            }
            TickHover(Time.unscaledDeltaTime);
        }

        public bool ExecuteInput(GalleryInputCommand command, float timestamp)
        {
            if (!EnsureState()) return false;
            if (inputGate == null) inputGate = new GalleryInputGate();
            if (!inputGate.TryAccept(command, timestamp)) return false;

            switch (command.Type)
            {
                case GalleryInputCommandType.Move:
                    Move(command.Value);
                    break;
                case GalleryInputCommandType.SelectCategory:
                    SelectCategory(command.Value);
                    break;
                case GalleryInputCommandType.SelectWork:
                    SelectWork(command.Value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command));
            }

            return true;
        }

        private void Submit(GalleryInputCommand command) => ExecuteInput(command, Time.unscaledTime);

        public bool SetHover(Card card, bool hovered, GalleryHoverMode mode)
        {
            EnsureHover();
            int index = Array.IndexOf(cards, card);
            // The centre slot is the focused work and never reacts to pointing.
            if (index < 0 || index == 1) return false;

            if (hovered)
            {
                hover[index].Enter(mode);
            }
            else
            {
                hover[index].Exit();
            }

            // A profile change must re-apply visuals even when the progress is already 1.
            ApplyPose(index);
            ApplyHoverTint(index, hover[index].Progress);
            return true;
        }

        public float HoverProgress(Card card)
        {
            EnsureHover();
            int index = Array.IndexOf(cards, card);
            return index < 0 ? 0f : hover[index].Progress;
        }

        public void TickHover(float deltaTime)
        {
            EnsureHover();
            if (transition != null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                if (!hover[i].Advance(deltaTime)) continue;
                ApplyPose(i);
                ApplyHoverTint(i, hover[i].Progress);
            }
        }

        public void ClearHover()
        {
            EnsureHover();
            for (int i = 0; i < hover.Length; i++)
            {
                hover[i].Clear();
                ApplyHoverTint(i, 0f);
                ApplyPose(i);
            }
        }

        private void EnsureHover()
        {
            if (hover != null) return;
            hover = new GalleryHoverState[cards.Length];
            baseArtwork = new Color[cards.Length];
            baseBorder = new Color[cards.Length];
            baseBorderSize = new Vector2[cards.Length];
            hoverGlow = new Image[cards.Length];
            hoverVisual = new RectTransform[cards.Length];
            for (int i = 0; i < hover.Length; i++) hover[i] = new GalleryHoverState();
        }

        public void Move(int direction)
        {
            if (!EnsureState() || direction == 0) return;
            Settle();
            ClearHover();
            state.Move(direction);
            if (direction == 1)
                cards = new[] { cards[1], cards[2], cards[0] };
            else if (direction == -1)
                cards = new[] { cards[2], cards[0], cards[1] };
            RefreshContent();
            if (Math.Abs((long)direction) == 1)
            {
                Card recycled = cards[direction > 0 ? 2 : 0];
                recycled.Root.localPosition = Pose(direction * 2);
                recycled.Root.localScale = Vector3.one * 0.6f;
                recycled.Root.localRotation = Rotation(direction * 2);
                recycled.Group.alpha = 0f;
            }
            else Snap();
            Animate();
        }

        public void SelectCategory(int index)
        {
            if (!EnsureState() || index == CategoryIndex) return;
            state.SelectCategory(index);
            FadeToSelection();
        }

        public void SelectWork(int index)
        {
            if (!EnsureState() || index == WorkIndex) return;
            state.SelectWork(index);
            FadeToSelection();
        }

        private void FadeToSelection()
        {
            Settle();
            ClearHover();
            RefreshContent();
            foreach (var card in cards)
            {
                card.Group.alpha = 0.25f;
                card.Root.localPosition += Vector3.down * 24f;
            }
            Animate();
        }

        private void RefreshContent()
        {
            EnsureHover();
            // Slots can be recycled to different cards, so cached visual/glow children must be
            // resolved again against the current slot owner.
            for (int i = 0; i < hoverVisual.Length; i++)
            {
                hoverVisual[i] = null;
                hoverGlow[i] = null;
            }
            var category = catalog.Categories[CategoryIndex];
            var work = category.Works[WorkIndex];
            for (int i = 0; i < cards.Length; i++)
            {
                int workIndex = state.Neighbour(i - 1);
                Card card = cards[i];
                card.Artwork.texture = category.Works[workIndex].Texture;
                // Fit the full source image, including non-3:2 replacement art.
                float ratio = (float)card.Artwork.texture.width / card.Artwork.texture.height;
                card.Artwork.rectTransform.sizeDelta = ratio >= 1.5f
                    ? new Vector2(900f, 900f / ratio) : new Vector2(600f * ratio, 600f);
                card.Artwork.color = i == 1 ? Color.white : new Color(0.78f, 0.81f, 0.8f, 1f);
                card.Border.color = i == 1 ? category.Accent : new Color32(76, 91, 84, 255);
                baseArtwork[i] = card.Artwork.color;
                baseBorder[i] = card.Border.color;
                if (baseBorderSize[i] == Vector2.zero)
                    baseBorderSize[i] = card.Border.rectTransform.sizeDelta;
                card.Caption.text = i == 1 ? "" : category.Works[workIndex].Title;
                card.Number.text = i == 1 ? "" : $"{workIndex + 1:00} / 05";
                card.Caption.rectTransform.anchoredPosition = new Vector2(i == 2 ? 225f : -225f, -365f);
                card.Caption.alignment = i == 2 ? TextAlignmentOptions.MidlineRight : TextAlignmentOptions.MidlineLeft;
                card.Number.rectTransform.anchoredPosition = new Vector2(i == 2 ? -338f : 338f, -365f);
                card.Number.alignment = i == 2 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
                card.Button.interactable = i != 1;
            }
            cards[1].Root.SetAsLastSibling();
            for (int i = 0; i < tabs.Length; i++)
            {
                tabs[i].Background.color = i == CategoryIndex ? category.Accent : Color.clear;
                tabs[i].Label.color = i == CategoryIndex ? new Color32(21, 35, 30, 255) : Ink;
            }
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].Marker.color = i == WorkIndex ? category.Accent : Muted;
                dots[i].Marker.rectTransform.sizeDelta = new Vector2(i == WorkIndex ? 44f : 10f, 10f);
            }
            styleDescription.text = category.Description;
            workMeta.text = $"{category.Name}  /  {WorkIndex + 1:00} — 05";
            workMeta.color = category.Accent;
            workTitle.text = work.Title;
            workDescription.text = work.Description;
            accentRule.color = category.Accent;
        }

        private Vector3 Pose(int slot) => new Vector3(slot * sideOffset, 0f,
            slot == 0 ? centerDepth : sideDepth + (Math.Abs(slot) - 1) * 120f);
        // Side cards turn their faces inward so the row wraps around the viewer.
        private Quaternion Rotation(int slot) => Quaternion.Euler(0, slot * sideAngle, 0);
        private Vector3 Scale(int slot) => Vector3.one * (slot == 0 ? 1f : sideScale);

        private void ApplyPose(int index)
        {
            int slot = index - 1;
            Card card = cards[index];
            card.Root.localPosition = Pose(slot);
            card.Root.localRotation = Rotation(slot);
            card.Root.localScale = Scale(slot);

            // Hover moves the visual wrapper only, so the raycast area stays aligned with the
            // card's carousel pose no matter how strongly the image reacts.
            float progress = slot == 0 ? 0f : hover[index].Progress;
            GalleryHoverProfile profile = hover[index].Profile;
            float yaw = -Math.Sign(slot) * profile.YawTurn * progress;
            RectTransform visual = ResolveVisual(index);
            if (visual != null)
            {
                visual.localPosition = new Vector3(0f, 0f, -profile.Lift * progress);
                visual.localRotation = Quaternion.Euler(0f, yaw, 0f);
                visual.localScale = Vector3.one *
                    (1f + profile.ScaleBoost / sideScale * progress);
                return;
            }

            // Cards created without the hover binding (e.g. older scenes) keep the legacy pose.
            card.Root.localPosition += new Vector3(0f, 0f, -profile.Lift * progress);
            card.Root.localRotation = Quaternion.Euler(0f, slot * sideAngle + yaw, 0f);
            card.Root.localScale = Scale(slot) + Vector3.one * (profile.ScaleBoost * progress);
        }

        private RectTransform ResolveVisual(int index)
        {
            if (hoverVisual[index] != null) return hoverVisual[index];
            Transform child = cards[index].Root.Find(HoverVisualLayerName);
            if (child == null) return null;
            hoverVisual[index] = (RectTransform)child;
            return hoverVisual[index];
        }

        private void ApplyHoverTint(int index, float progress)
        {
            if (baseArtwork == null || index >= baseArtwork.Length || baseBorderSize[index] == Vector2.zero)
                return;

            Card card = cards[index];
            GalleryHoverProfile profile = GalleryHoverProfile.For(hover[index].Mode);
            card.Artwork.color = Color.Lerp(baseArtwork[index], Color.white, progress);
            Color accent = catalog != null ? catalog.Categories[CategoryIndex].Accent : Ink;
            Color highlight = Color.Lerp(accent, Color.white, 0.92f);
            card.Border.color = Color.Lerp(baseBorder[index], highlight, progress);
            card.Border.rectTransform.sizeDelta =
                baseBorderSize[index] + Vector2.one * (profile.Glow * progress);
            if (index != 1)
            {
                card.Caption.color = Color.Lerp(Muted, Ink, progress);
                card.Number.color = Color.Lerp(Muted, Ink, progress);
            }

            // A large, saturated halo reads clearly against both artwork and passthrough.
            Image glow = ResolveGlow(index);
            if (glow != null)
            {
                glow.rectTransform.sizeDelta =
                    baseBorderSize[index] + Vector2.one * (HoverGlowSpread * progress);
                Color halo = VividHighlight(accent);
                halo.a = HoverGlowAlpha * progress;
                glow.color = halo;
            }
        }

        private static Color VividHighlight(Color accent)
        {
            Color.RGBToHSV(accent, out float hue, out float saturation, out _);
            return Color.HSVToRGB(hue, Mathf.Clamp01(saturation + 0.45f), 1f);
        }

        private Image ResolveGlow(int index)
        {
            if (hoverGlow[index] != null) return hoverGlow[index];
            RectTransform visual = ResolveVisual(index);
            Transform parent = visual != null ? visual : cards[index].Root;
            Transform child = parent.Find(HoverGlowLayerName);
            if (child == null) return null;
            hoverGlow[index] = child.GetComponent<Image>();
            return hoverGlow[index];
        }

        private void Animate()
        {
            if (!isActiveAndEnabled || !Application.isPlaying || transitionDuration <= 0)
            {
                Snap();
                return;
            }
            transition = StartCoroutine(Transition());
        }

        private IEnumerator Transition()
        {
            var starts = new Vector3[3];
            var scales = new Vector3[3];
            var rotations = new Quaternion[3];
            var alphas = new float[3];
            for (int i = 0; i < 3; i++)
            {
                starts[i] = cards[i].Root.localPosition;
                scales[i] = cards[i].Root.localScale;
                rotations[i] = cards[i].Root.localRotation;
                alphas[i] = cards[i].Group.alpha;
            }
            float elapsed = 0;
            while (elapsed < transitionDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = 1f - Mathf.Pow(1f - Mathf.Clamp01(elapsed / transitionDuration), 3f);
                for (int i = 0; i < 3; i++)
                {
                    cards[i].Root.localPosition = Vector3.Lerp(starts[i], Pose(i - 1), t);
                    cards[i].Root.localScale = Vector3.Lerp(scales[i], Scale(i - 1), t);
                    cards[i].Root.localRotation = Quaternion.Slerp(rotations[i], Rotation(i - 1), t);
                    cards[i].Group.alpha = Mathf.Lerp(alphas[i], 1f, t);
                }
                yield return null;
            }
            Snap();
            transition = null;
        }

        private void Snap()
        {
            EnsureHover();
            for (int i = 0; i < cards.Length; i++)
            {
                ApplyPose(i);
                ApplyHoverTint(i, hover[i].Progress);
                cards[i].Group.alpha = 1f;
            }
        }

        private void Settle()
        {
            if (transition != null) StopCoroutine(transition);
            transition = null;
            Snap();
        }

        private void OnDisable()
        {
            ClearHover();
            if (state != null) Settle();
        }
    }
}
