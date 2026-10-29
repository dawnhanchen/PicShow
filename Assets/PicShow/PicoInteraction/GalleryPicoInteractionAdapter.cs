using System.Collections.Generic;
using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PicShow.PicoInteraction
{
    public sealed class GalleryPicoInteractionAdapter : MonoBehaviour
    {
        private sealed class Binding
        {
            public PXR_EnhancedButton Enhanced;
            public UnityAction Action;
        }

        [SerializeField] private Button[] buttons;

        private readonly List<Binding> bindings = new List<Binding>();

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
            if (bindings.Count != 0 || buttons == null)
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
                UnityAction action = button.onClick.Invoke;
                enhanced.onSingleClick.AddListener(action);
                bindings.Add(new Binding { Enhanced = enhanced, Action = action });
            }
        }

        private void Unbind()
        {
            foreach (Binding binding in bindings)
            {
                if (binding.Enhanced != null)
                    binding.Enhanced.onSingleClick.RemoveListener(binding.Action);
            }
            bindings.Clear();
        }
    }
}
