using System;
using System.Collections;
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
        [SerializeField] private GameObject rejectionIndicator;
        [SerializeField] private float rejectionVisibleSeconds = 1.25f;

        public event Action<string>? PlaceActionPressed;
        public event Action? CancelPressed;
        private Coroutine? _rejectionCoroutine;

        private void Awake()
        {
            HookButton(placeFrameButton, () => PlaceActionPressed?.Invoke("frame_blue"));
            HookButton(placeDeckButton, () => PlaceActionPressed?.Invoke("deck_yellow"));
            HookButton(placeDiagonalButton, () => PlaceActionPressed?.Invoke("diagonal_red"));
            HookButton(cancelButton, () => CancelPressed?.Invoke());
            SetPendingRequestVisual(false);
            SetRejectionVisual(false);
        }

        public void SetPendingRequestVisual(bool pending)
        {
            if (pendingIndicator != null)
            {
                pendingIndicator.SetActive(pending);
            }
        }

        public void ShowRejectionVisual(string message)
        {
            _ = message;
            if (_rejectionCoroutine != null)
            {
                StopCoroutine(_rejectionCoroutine);
            }

            _rejectionCoroutine = StartCoroutine(ShowRejectionVisualCoroutine());
        }

        private IEnumerator ShowRejectionVisualCoroutine()
        {
            SetRejectionVisual(true);
            yield return new WaitForSeconds(rejectionVisibleSeconds);
            SetRejectionVisual(false);
            _rejectionCoroutine = null;
        }

        private void SetRejectionVisual(bool visible)
        {
            if (rejectionIndicator != null)
            {
                rejectionIndicator.SetActive(visible);
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
