using SbossClient.Client.Input;
using SbossClient.Client.Networking;
using SbossClient.Client.Presentation;
using UnityEngine;

namespace SbossClient.Client.App
{
    /// <summary>
    /// Phase 2B composition root for Unity shell wiring.
    /// Keeps client responsibilities to rendering/input/request dispatch only.
    /// </summary>
    public sealed class Phase2BShellBootstrap : MonoBehaviour
    {
        [SerializeField] private IsometricCameraController cameraController;
        [SerializeField] private PlacementDragShell placementDragShell;
        [SerializeField] private MobileBottomActionBarController bottomActionBar;
        [SerializeField] private PlacementRequestDispatcher placementRequestDispatcher;

        [Header("Camera Gesture Tuning")]
        [SerializeField] private float panGestureScale = 0.0025f;
        [SerializeField] private float zoomThreshold = 0.01f;

        private Vector2 _lastPointer;
        private bool _isPanning;

        private void Awake()
        {
            bottomActionBar.PlaceActionPressed += HandlePlaceActionPressed;
            bottomActionBar.CancelPressed += placementDragShell.CancelDrag;
            placementDragShell.PlacementRequested += placementRequestDispatcher.DispatchPlacementRequest;

            placementRequestDispatcher.RequestBegan += OnRequestBegan;
            placementRequestDispatcher.RequestCompleted += OnRequestCompleted;
        }

        private void Update()
        {
            HandleCameraPan();
            HandleCameraZoom();
        }

        private void OnDestroy()
        {
            if (bottomActionBar != null)
            {
                bottomActionBar.PlaceActionPressed -= HandlePlaceActionPressed;
                bottomActionBar.CancelPressed -= placementDragShell.CancelDrag;
            }

            if (placementDragShell != null && placementRequestDispatcher != null)
            {
                placementDragShell.PlacementRequested -= placementRequestDispatcher.DispatchPlacementRequest;
            }

            if (placementRequestDispatcher != null)
            {
                placementRequestDispatcher.RequestBegan -= OnRequestBegan;
                placementRequestDispatcher.RequestCompleted -= OnRequestCompleted;
            }
        }

        private void HandlePlaceActionPressed(string componentId)
        {
            placementDragShell.BeginDrag(componentId);
        }

        private void OnRequestBegan()
        {
            bottomActionBar.SetPendingRequestVisual(true);
        }

        private void OnRequestCompleted()
        {
            bottomActionBar.SetPendingRequestVisual(false);
        }

        private void HandleCameraPan()
        {
            if (cameraController == null)
            {
                return;
            }

            if (UnityEngine.Input.GetMouseButtonDown(1))
            {
                _isPanning = true;
                _lastPointer = UnityEngine.Input.mousePosition;
            }

            if (UnityEngine.Input.GetMouseButtonUp(1))
            {
                _isPanning = false;
            }

            if (!_isPanning)
            {
                return;
            }

            var pointer = (Vector2)UnityEngine.Input.mousePosition;
            var delta = pointer - _lastPointer;
            _lastPointer = pointer;
            cameraController.PanByViewportDelta(delta * panGestureScale);
        }

        private void HandleCameraZoom()
        {
            if (cameraController == null)
            {
                return;
            }

            var scroll = UnityEngine.Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) < zoomThreshold)
            {
                return;
            }

            cameraController.ZoomByStep(Mathf.Sign(scroll));
        }
    }
}
