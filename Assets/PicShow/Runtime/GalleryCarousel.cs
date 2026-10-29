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
        [SerializeField] private Button previous;
        [SerializeField] private Button next;
        [SerializeField] private TMP_Text styleDescription;
        [SerializeField] private TMP_Text workMeta;
        [SerializeField] private TMP_Text workTitle;
        [SerializeField] private TMP_Text workDescription;
        [SerializeField] private Image accentRule;
        [Header("Spatial layout (canvas units)")]
        [SerializeField] private float sideOffset = 760f;
        [SerializeField] private float sideDepth = 180f;
        [SerializeField] private float centerDepth = -65f;
        [SerializeField, Range(0.5f, 1f)] private float sideScale = 0.78f;
        [SerializeField] private float sideAngle = 24f;
        [SerializeField, Range(0f, 1f)] private float transitionDuration = 0.38f;

        private static readonly Color Ink = new Color32(237, 241, 233, 255);
        private static readonly Color Muted = new Color32(172, 185, 180, 255);
        private CarouselState state;
        private GalleryInputGate inputGate;
        private Coroutine transition;
        private bool bound;

        public GalleryCatalog Catalog => catalog;
        public int CategoryIndex => state?.CategoryIndex ?? 0;
        public int WorkIndex => state?.WorkIndex ?? 0;
        public IReadOnlyList<Card> Cards => cards;
        public IReadOnlyList<Tab> Tabs => tabs;
        public IReadOnlyList<PositionDot> Dots => dots;
        public Button PreviousButton => previous;
        public Button NextButton => next;
        public string CurrentTitle => workTitle.text;
        public bool IsTransitioning => transition != null;

        // Used by the editor builder; all references are serialized into the prefab.
        public void Configure(GalleryCatalog content, Card[] cardViews, Tab[] tabViews,
            PositionDot[] dotViews, Button previousButton, Button nextButton,
            TMP_Text style, TMP_Text meta, TMP_Text title, TMP_Text description, Image rule)
        {
            catalog = content;
            cards = cardViews;
            tabs = tabViews;
            dots = dotViews;
            previous = previousButton;
            next = nextButton;
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
                previous.onClick.AddListener(() => Submit(GalleryInputCommand.Move(-1)));
                next.onClick.AddListener(() => Submit(GalleryInputCommand.Move(1)));
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
            if (keyboard == null) return;
            if (keyboard.leftArrowKey.wasPressedThisFrame) Submit(GalleryInputCommand.Move(-1));
            if (keyboard.rightArrowKey.wasPressedThisFrame) Submit(GalleryInputCommand.Move(1));
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

        public void Move(int direction)
        {
            if (!EnsureState() || direction == 0) return;
            Settle();
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
                recycled.Root.localRotation = Quaternion.Euler(0, -direction * sideAngle, 0);
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
        private Quaternion Rotation(int slot) => Quaternion.Euler(0, -slot * sideAngle, 0);
        private Vector3 Scale(int slot) => Vector3.one * (slot == 0 ? 1f : sideScale);

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
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].Root.localPosition = Pose(i - 1);
                cards[i].Root.localRotation = Rotation(i - 1);
                cards[i].Root.localScale = Scale(i - 1);
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
            if (state != null) Settle();
        }
    }
}
