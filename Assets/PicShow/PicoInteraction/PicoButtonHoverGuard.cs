using UnityEngine;
using UnityEngine.EventSystems;

namespace PicShow.PicoInteraction
{
    // PICO's PXR_EnhancedButton keeps its hover and press fields across
    // SetActive(false)/SetActive(true). A persistent button that was hidden
    // while hovered can therefore replay a single click on its next Update.
    // This guard records real pointer hover for the current activation so the
    // click bridge can discard those replayed clicks.
    public sealed class PicoButtonHoverGuard : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler
    {
        private bool hovered;

        public bool Hovered => hovered;

        private void OnEnable() => hovered = false;

        private void OnDisable() => hovered = false;

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData) => hovered = false;
    }
}
