using System.Collections.Generic;
using ByteDance.PICO.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Inputs;

namespace PicShow.PicoInteraction
{
    public sealed class PicoButtonClickBridge : MonoBehaviour
    {
        private sealed class Binding
        {
            public PXR_EnhancedButton Enhanced;
            public UnityAction Action;
        }

        [SerializeField] private Button[] buttons;

        private readonly List<Binding> bindings = new List<Binding>();
        private bool modeSubscribed;

        public void Configure(Button[] targetButtons)
        {
            Unbind();
            buttons = targetButtons;
            if (isActiveAndEnabled)
                Bind();
        }

        private void OnEnable()
        {
            Bind();
            if (!modeSubscribed)
            {
                PXR_InteractorManager.InputModeChangeAction += OnInputModeChanged;
                modeSubscribed = true;
            }
        }

        private void OnDisable()
        {
            Unbind();
            if (modeSubscribed)
            {
                PXR_InteractorManager.InputModeChangeAction -= OnInputModeChanged;
                modeSubscribed = false;
            }
        }

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
                // PXR_EnhancedButton keeps stale press state when a button is
                // deactivated while hovered and then shown again. Forward the
                // enhanced click only while a real pointer hovers the button.
                var guard = button.GetComponent<PicoButtonHoverGuard>();
                if (guard == null)
                    guard = button.gameObject.AddComponent<PicoButtonHoverGuard>();
                UnityAction action = () =>
                {
                    if (guard != null && guard.Hovered)
                        button.onClick.Invoke();
                };
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

        private void OnInputModeChanged(XRInputModalityManager.InputMode inputMode) =>
            NotifyInputModeChanged();

        public void NotifyInputModeChanged()
        {
            foreach (IInteractionModeAware aware in
                GetComponentsInChildren<IInteractionModeAware>(true))
                aware.ResetInteractionState();
        }
    }
}
