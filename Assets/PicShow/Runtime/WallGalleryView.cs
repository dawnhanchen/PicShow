using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PicShow
{
    public sealed class WallGalleryView : MonoBehaviour, IInteractionModeAware
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Button[] categoryButtons = new Button[4];
        [SerializeField] private TMP_Text[] categoryLabels = new TMP_Text[4];
        [SerializeField] private RawImage[] imageSlots = new RawImage[5];
        [SerializeField] private Button backButton;

        private GalleryCatalog catalog;
        private WallPlaneInfo wall;
        private UnityAction backAction;
        private readonly float[] lastCategoryTimes = new float[4];
        private float lastBackTime = float.NegativeInfinity;
        private bool wired;
        private int currentCategory = -1;

        public WallPlaneInfo Wall => wall;
        public RawImage[] ImageSlots => imageSlots;
        public Button[] CategoryButtons => categoryButtons;
        public int CurrentCategory => currentCategory;

        public void Configure(GalleryCatalog content, WallPlaneInfo info, UnityAction onBack,
            Button[] categories, TMP_Text[] labels, RawImage[] slots, Button back)
        {
            catalog = content;
            categoryButtons = categories;
            categoryLabels = labels;
            imageSlots = slots;
            backButton = back;
            Configure(content, info, onBack);
        }

        public void Configure(GalleryCatalog content, WallPlaneInfo info, UnityAction onBack)
        {
            catalog = content;
            wall = info;
            backAction = onBack;
            ApplyPose();
            WireButtons();
            for (int i = 0; i < lastCategoryTimes.Length; i++)
                lastCategoryTimes[i] = float.NegativeInfinity;
            ShowCategories();
        }

        public void ShowCategories()
        {
            currentCategory = -1;
            for (int i = 0; i < imageSlots.Length; i++)
            {
                if (imageSlots[i] != null) imageSlots[i].gameObject.SetActive(false);
            }
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                if (categoryButtons[i] != null) categoryButtons[i].gameObject.SetActive(true);
            }
            if (backButton != null) backButton.gameObject.SetActive(true);
            LayoutCategoryButtons();
        }

        public void ShowCategory(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.Categories.Length)
                throw new ArgumentOutOfRangeException(nameof(index));
            currentCategory = index;
            var category = catalog.Categories[index];
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                Color accent = category.Accent;
                if (categoryButtons[i] != null)
                {
                    var image = categoryButtons[i].GetComponent<Image>();
                    if (image != null)
                        image.color = i == index ? accent : new Color(0.12f, 0.16f, 0.14f, 0.9f);
                }
                if (categoryLabels[i] != null)
                    categoryLabels[i].color = i == index
                        ? new Color(0.08f, 0.14f, 0.11f, 1f) : new Color(0.93f, 0.95f, 0.92f, 1f);
            }

            var ratios = new float[category.Works.Length];
            float band = wall.Height * 0.12f;
            for (int i = 0; i < imageSlots.Length && i < category.Works.Length; i++)
            {
                RawImage slot = imageSlots[i];
                if (slot == null) continue;
                Texture texture = category.Works[i].Texture;
                slot.texture = texture;
                ratios[i] = texture != null && texture.height > 0
                    ? (float)texture.width / texture.height : 1.5f;
                slot.gameObject.SetActive(true);
            }

            WallGridResult layout = WallGridLayout.Compute(
                new Vector2(wall.Width, wall.Height - band), ratios, 0.03f, 0.02f);
            for (int i = 0; i < imageSlots.Length && i < layout.Rects.Length; i++)
            {
                RawImage slot = imageSlots[i];
                if (slot == null) continue;
                WallGridRect rect = layout.Rects[i];
                RectTransform transform = slot.rectTransform;
                transform.anchorMin = new Vector2(0.5f, 0.5f);
                transform.anchorMax = new Vector2(0.5f, 0.5f);
                transform.pivot = new Vector2(0.5f, 0.5f);
                transform.sizeDelta = new Vector2(rect.Size.x * 1000f, rect.Size.y * 1000f);
                transform.anchoredPosition = new Vector2(
                    rect.Center.x * 1000f,
                    (rect.Center.y - band / 2f) * 1000f);
                slot.color = Color.white;
            }
            LayoutCategoryButtons();
        }

        public void SelectCategoryAt(int index, float timestamp)
        {
            if (timestamp - lastCategoryTimes[index] < 0.2f)
                return;
            lastCategoryTimes[index] = timestamp;
            ShowCategory(index);
        }

        public void BackAt(float timestamp)
        {
            if (timestamp - lastBackTime < 0.2f)
                return;
            lastBackTime = timestamp;
            backAction?.Invoke();
        }

        public void ApplyWallInfo(WallPlaneInfo info)
        {
            wall = info;
            ApplyPose();
            LayoutCategoryButtons();
            if (currentCategory >= 0)
                ShowCategory(currentCategory);
        }

        public void ResetInteractionState()
        {
            for (int i = 0; i < lastCategoryTimes.Length; i++)
                lastCategoryTimes[i] = float.NegativeInfinity;
            lastBackTime = float.NegativeInfinity;
        }

        private void ApplyPose()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;
            RectTransform rect = root != null ? root : transform as RectTransform;
            if (rect == null || wall.Id == Guid.Empty)
                return;
            rect.sizeDelta = new Vector2(wall.Width * 1000f, wall.Height * 1000f);
            rect.SetPositionAndRotation(wall.Center + wall.Normal * 0.02f, wall.Rotation);
            rect.localScale = Vector3.one * 0.001f;
        }

        private void WireButtons()
        {
            if (wired)
                return;
            if (catalog != null)
            {
                for (int i = 0; i < categoryButtons.Length; i++)
                {
                    if (categoryButtons[i] == null) continue;
                    int captured = i;
                    categoryButtons[i].onClick.AddListener(
                        () => SelectCategoryAt(captured, Time.unscaledTime));
                    if (i < categoryLabels.Length && categoryLabels[i] != null &&
                        i < catalog.Categories.Length)
                        categoryLabels[i].text = catalog.Categories[i].Name;
                }
            }
            if (backButton != null)
                backButton.onClick.AddListener(() => BackAt(Time.unscaledTime));
            wired = true;
        }

        private void LayoutCategoryButtons()
        {
            float band = wall.Height * 0.12f;
            float buttonHeight = band * 0.62f;
            float backWidth = backButton != null ? wall.Width * 0.12f : 0f;
            float gap = wall.Width * 0.02f;
            float left = -wall.Width / 2f + gap;
            float categoryLeft = left + (backButton != null ? backWidth + gap : 0f);
            float categoryRight = wall.Width / 2f - gap;
            float buttonWidth = (categoryRight - categoryLeft -
                (categoryButtons.Length - 1) * gap) / categoryButtons.Length;
            float startX = categoryLeft + buttonWidth / 2f;
            float y = wall.Height / 2f - band / 2f;
            float categoryFont = Mathf.Clamp(buttonHeight * 1000f * 0.30f, 18f, 64f);
            float backFont = Mathf.Clamp(buttonHeight * 1000f * 0.22f, 16f, 48f);
            for (int i = 0; i < categoryButtons.Length; i++)
            {
                if (categoryButtons[i] == null) continue;
                RectTransform rect = (RectTransform)categoryButtons[i].transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(buttonWidth * 1000f, buttonHeight * 1000f);
                rect.anchoredPosition = new Vector2((startX + i * (buttonWidth + gap)) * 1000f,
                    y * 1000f);
                if (i < categoryLabels.Length && categoryLabels[i] != null)
                {
                    TMP_Text label = categoryLabels[i];
                    RectTransform labelRect = (RectTransform)label.transform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(8f, 6f);
                    labelRect.offsetMax = new Vector2(-8f, -6f);
                    label.enableAutoSizing = true;
                    label.fontSizeMin = 12f;
                    label.fontSizeMax = categoryFont;
                    label.fontSize = categoryFont;
                }
            }
            if (backButton != null)
            {
                RectTransform rect = (RectTransform)backButton.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(backWidth * 1000f, buttonHeight * 1000f);
                rect.anchoredPosition = new Vector2(
                    (left + backWidth / 2f) * 1000f, y * 1000f);
                TMP_Text backLabel = backButton.GetComponentInChildren<TMP_Text>();
                if (backLabel != null)
                {
                    RectTransform labelRect = (RectTransform)backLabel.transform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = new Vector2(6f, 5f);
                    labelRect.offsetMax = new Vector2(-6f, -5f);
                    backLabel.enableAutoSizing = true;
                    backLabel.fontSizeMin = 10f;
                    backLabel.fontSizeMax = backFont;
                    backLabel.fontSize = backFont;
                }
            }
        }
    }
}
