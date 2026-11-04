using System.Collections.Generic;
using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace PicShow.PicoInteraction
{
    public sealed class GalleryPicoInteractionAdapter : MonoBehaviour
    {
        private sealed class Binding
        {
            public PXR_EnhancedButton Enhanced;
            public PicoButtonHoverGuard Guard;
            public UnityAction Action;
        }

        [SerializeField] private Button[] buttons;

        private readonly List<Binding> bindings = new List<Binding>();
        private readonly List<GalleryCardHover> hovers = new List<GalleryCardHover>();
        private GalleryCarousel gallery;
        private bool hoverBound;

        public void Configure(Button[] targetButtons)
        {
            Unbind();
            buttons = targetButtons;
            if (isActiveAndEnabled)
                Bind();
        }

        private void OnEnable() => Bind();
        private void OnDisable() => Unbind();

        private void Bind()
        {
            if (bindings.Count != 0)
                return;

            BindHover();
            if (buttons == null)
                return;

            foreach (Button button in buttons)
            {
                if (button == null)
                    continue;

                var enhanced = button.GetComponent<PXR_EnhancedButton>();
                if (enhanced == null)
                    continue;

                if (enhanced.onSingleClick == null)
                    enhanced.onSingleClick = new UnityEvent();
                var guard = button.GetComponent<PicoButtonHoverGuard>();
                if (guard == null)
                    guard = button.gameObject.AddComponent<PicoButtonHoverGuard>();
                UnityAction action = () =>
                {
                    if (guard != null && guard.Hovered)
                        button.onClick.Invoke();
                };
                enhanced.onSingleClick.AddListener(action);
                bindings.Add(new Binding { Enhanced = enhanced, Guard = guard, Action = action });
            }
        }

        // Recycles the same three card slots, so each slot keeps one hover binding for its lifetime.
        private void BindHover()
        {
            if (hoverBound)
                return;

            gallery = GetComponent<GalleryCarousel>();
            if (gallery == null)
                return;

            hovers.Clear();
            foreach (GalleryCarousel.Card card in gallery.Cards)
            {
                if (card == null || card.Root == null)
                    continue;

                var hover = card.Root.GetComponent<GalleryCardHover>();
                if (hover == null)
                    hover = card.Root.gameObject.AddComponent<GalleryCardHover>();
                hover.Configure(gallery, card);
                hovers.Add(hover);
            }

            PXR_InteractorManager.InputModeChangeAction += OnInputModeChanged;
            hoverBound = true;
        }

        // A modality switch invalidates every eye or controller pointer that produced the current
        // highlight, so drop it before the new rays are evaluated.
        private void OnInputModeChanged(XRInputModalityManager.InputMode inputMode)
        {
            foreach (GalleryCardHover hover in hovers)
            {
                if (hover != null)
                    hover.ResetPointers();
            }
            foreach (Binding binding in bindings)
            {
                if (binding.Guard != null)
                    binding.Guard.ResetInteractionState();
            }
            if (gallery != null)
                gallery.ClearHover();
        }

        private void Unbind()
        {
            if (hoverBound)
            {
                PXR_InteractorManager.InputModeChangeAction -= OnInputModeChanged;
                hoverBound = false;
                hovers.Clear();
            }
            foreach (Binding binding in bindings)
            {
                if (binding.Enhanced != null)
                    binding.Enhanced.onSingleClick.RemoveListener(binding.Action);
                if (binding.Guard != null)
                    binding.Guard.ResetInteractionState();
            }
            bindings.Clear();
        }
    }
}
