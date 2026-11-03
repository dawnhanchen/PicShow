using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PicShow
{
    public sealed class WallPanelView : MonoBehaviour, IInteractionModeAware
    {
        private const float MaxButtonWidthMeters = 0.56f;
        private const float MaxButtonHeightMeters = 0.18f;
        private const float ButtonWidthRatio = 0.9f;
        private const float ButtonHeightRatio = 0.55f;

        [SerializeField] private RectTransform root;
        [SerializeField] private Button selectButton;
        [SerializeField] private TMP_Text promptLabel;
        [SerializeField] private TMP_Text indexLabel;

        private Guid wallId;
        private UnityAction selectAction;
        private float lastSelectTime = float.NegativeInfinity;
        private bool wired;

        public Guid WallId => wallId;
        public WallPlaneInfo Info { get; private set; }
        public Button SelectButton => selectButton;

        public void Configure(WallPlaneInfo info, int oneBasedIndex, UnityAction onSelect,
            Button button, TMP_Text prompt, TMP_Text index)
        {
            selectButton = button;
            promptLabel = prompt;
            indexLabel = index;
            Configure(info, oneBasedIndex, onSelect);
        }

        public void Configure(WallPlaneInfo info, int oneBasedIndex, UnityAction onSelect)
        {
            wallId = info.Id;
            selectAction = onSelect;
            ApplyInfo(info);
            if (promptLabel != null) promptLabel.text = "选择该墙面显示";
            if (indexLabel != null) indexLabel.text = "墙面 " + oneBasedIndex;
            if (!wired && selectButton != null)
            {
                selectButton.onClick.AddListener(HandleSelect);
                wired = true;
            }
        }

        public void ApplyInfo(WallPlaneInfo info)
        {
            wallId = info.Id;
            Info = info;
            EnsureCanvasCamera();
            RectTransform rect = root != null ? root : transform as RectTransform;
            if (rect == null)
                return;
            rect.sizeDelta = new Vector2(info.Width * 1000f, info.Height * 1000f);
            rect.SetPositionAndRotation(info.Center + info.Normal * 0.02f, info.Rotation);
            rect.localScale = Vector3.one * 0.001f;

            float width = Mathf.Min(info.Width * ButtonWidthRatio, MaxButtonWidthMeters);
            float height = Mathf.Min(info.Height * ButtonHeightRatio, MaxButtonHeightMeters);
            if (selectButton != null)
            {
                RectTransform button = (RectTransform)selectButton.transform;
                button.anchorMin = new Vector2(0.5f, 0.5f);
                button.anchorMax = new Vector2(0.5f, 0.5f);
                button.pivot = new Vector2(0.5f, 0.5f);
                button.sizeDelta = new Vector2(width * 1000f, height * 1000f);
                button.anchoredPosition = Vector2.zero;
            }
            ApplyTextSize(height);
        }

        private void ApplyTextSize(float heightMeters)
        {
            float pixels = heightMeters * 1000f;
            if (promptLabel != null)
            {
                promptLabel.enableAutoSizing = true;
                promptLabel.fontSizeMin = 6f;
                promptLabel.fontSizeMax = Mathf.Max(16f, pixels * 0.34f);
                promptLabel.fontSize = Mathf.Clamp(pixels * 0.30f, 6f, 54f);
            }
            if (indexLabel != null)
            {
                indexLabel.enableAutoSizing = true;
                indexLabel.fontSizeMin = 5f;
                indexLabel.fontSizeMax = Mathf.Max(10f, pixels * 0.17f);
                indexLabel.fontSize = Mathf.Clamp(pixels * 0.15f, 5f, 28f);
            }
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void SelectAt(float timestamp)
        {
            if (timestamp - lastSelectTime < 0.2f)
                return;
            lastSelectTime = timestamp;
            selectAction?.Invoke();
        }

        private void HandleSelect() => SelectAt(Time.unscaledTime);

        public void ResetInteractionState() => lastSelectTime = float.NegativeInfinity;

        public void EnsureCanvasCamera()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null)
                canvas.worldCamera = Camera.main;
        }
    }
}
