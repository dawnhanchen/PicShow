using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PicShow.PicoInteraction
{
    // Adds an explicit visual hover response for the first-level mode cards.
    // Button.ColorTint alone is nearly invisible against the dark card color.
    public sealed class ModeCardHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image background;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color hoverColor = Color.white;
        [SerializeField] private Vector3 normalScale = Vector3.one;
        [SerializeField] private float hoverScale = 1.05f;

        private bool hovering;

        public bool Hovering => hovering;

        public void Configure(Image image, Color highlighted, float scale = 1.05f)
        {
            background = image;
            normalColor = image != null ? image.color : normalColor;
            normalScale = transform.localScale;
            hoverColor = highlighted;
            hoverScale = Mathf.Max(1f, scale);
        }

        public void OnPointerEnter(PointerEventData eventData) => Apply(true);

        public void OnPointerExit(PointerEventData eventData) => Apply(false);

        private void OnDisable()
        {
            if (hovering)
                Apply(false);
        }

        private void Apply(bool value)
        {
            hovering = value;
            if (background != null)
                background.color = value ? hoverColor : normalColor;
            transform.localScale = value ? normalScale * hoverScale : normalScale;
        }
    }
}
