using System;
using UnityEngine;
using UnityEngine.UI;

namespace SbossClient.Client.Presentation
{
    /// <summary>
    /// Mobile-first bottom action bar for shell controls only.
    /// No backend-owned gameplay decisions are performed here.
    /// </summary>
    public sealed class MobileBottomActionBarController : MonoBehaviour
    {
        [SerializeField] private Button placeFrameButton;
        [SerializeField] private Button placeDeckButton;
        [SerializeField] private Button placeDiagonalButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private GameObject pendingIndicator;

        public event Action<string>? PlaceActionPressed;
        public event Action? CancelPressed;

        private void Awake()
        {
            HookButton(placeFrameButton, () => PlaceActionPressed?.Invoke("frame_blue"));
            HookButton(placeDeckButton, () => PlaceActionPressed?.Invoke("deck_yellow"));
            HookButton(placeDiagonalButton, () => PlaceActionPressed?.Invoke("diagonal_red"));
            HookButton(cancelButton, () => CancelPressed?.Invoke());
            SetPendingRequestVisual(false);
        }

        public void SetPendingRequestVisual(bool pending)
        {
            if (pendingIndicator != null)
            {
                pendingIndicator.SetActive(pending);
            }
        }

        private static void HookButton(Button? button, Action onClick)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
        }
    }
}
