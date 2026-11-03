using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PicShow
{
    public sealed class ModePanelView : MonoBehaviour
    {
        [SerializeField] private Button mrButton;
        [SerializeField] private Button nonMrButton;
        [SerializeField] private TMP_Text mrLabel;
        [SerializeField] private TMP_Text nonMrLabel;
        [SerializeField] private Image mrIcon;
        [SerializeField] private Image nonMrIcon;

        private bool wired;

        public bool Visible => gameObject.activeSelf;
        public Button MrButton => mrButton;
        public Button NonMrButton => nonMrButton;

        public void Configure(Button mr, Button nonMr, TMP_Text mrText, TMP_Text nonMrText,
            Image mrIconImage, Image nonMrIconImage)
        {
            mrButton = mr;
            nonMrButton = nonMr;
            mrLabel = mrText;
            nonMrLabel = nonMrText;
            mrIcon = mrIconImage;
            nonMrIcon = nonMrIconImage;
            Wire();
        }

        public void Configure(UnityAction onMr, UnityAction onNonMr)
        {
            if (mrButton != null)
            {
                mrButton.onClick.RemoveAllListeners();
                mrButton.onClick.AddListener(onMr);
            }
            if (nonMrButton != null)
            {
                nonMrButton.onClick.RemoveAllListeners();
                nonMrButton.onClick.AddListener(onNonMr);
            }
            wired = true;
        }

        public void SetVisible(bool visible) => gameObject.SetActive(visible);

        public void SetLabels(string mrText, string nonMrText)
        {
            if (mrLabel != null) mrLabel.text = mrText;
            if (nonMrLabel != null) nonMrLabel.text = nonMrText;
        }

        public void SetIcons(Sprite mrSprite, Sprite nonMrSprite)
        {
            if (mrIcon != null) mrIcon.sprite = mrSprite;
            if (nonMrIcon != null) nonMrIcon.sprite = nonMrSprite;
        }

        private void Wire()
        {
            if (wired)
                return;
            wired = true;
        }
    }
}
