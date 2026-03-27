using System;
using UnityEngine;

namespace SbossClient.Client.Input
{
    /// <summary>
    /// Input and preview-only drag shell.
    /// This class never decides placement legality or scoring.
    /// </summary>
    public sealed class PlacementDragShell : MonoBehaviour
    {
        [SerializeField] private Camera worldCamera;
        [SerializeField] private LayerMask placementSurfaceMask;
        [SerializeField] private Transform ghostVisual;
        [SerializeField] private float dragPlaneHeight = 0f;

        private bool _isDragging;
        private bool _ignoreReleaseUntilNextPress;
        private string _activeComponentId = string.Empty;
        private Vector3 _currentPreviewWorld;

        public event Action<PlacementRequestPayload>? PlacementRequested;
        public event Action<Vector3>? PreviewUpdated;

        public bool IsDragging => _isDragging;
        public string ActiveComponentId => _activeComponentId;

        public void BeginDrag(string componentId)
        {
            if (string.IsNullOrWhiteSpace(componentId))
            {
                return;
            }

            _activeComponentId = componentId;
            _isDragging = true;
            _ignoreReleaseUntilNextPress = true;
            SetGhostVisible(true);
        }

        public void CancelDrag()
        {
            _isDragging = false;
            _ignoreReleaseUntilNextPress = false;
            _activeComponentId = string.Empty;
            SetGhostVisible(false);
        }

        private void Update()
        {
            if (!_isDragging || worldCamera == null)
            {
                return;
            }

            if (TryGetPointerWorldPosition(out var worldPosition))
            {
                _currentPreviewWorld = worldPosition;
                if (ghostVisual != null)
                {
                    ghostVisual.position = worldPosition;
                }

                PreviewUpdated?.Invoke(worldPosition);
            }

            if (_ignoreReleaseUntilNextPress)
            {
                if (UnityEngine.Input.GetMouseButtonDown(0))
                {
                    _ignoreReleaseUntilNextPress = false;
                }
                else if (UnityEngine.Input.GetMouseButtonUp(0))
                {
                    return;
                }
            }

            if (!_ignoreReleaseUntilNextPress && UnityEngine.Input.GetMouseButtonUp(0))
            {
                PlacementRequested?.Invoke(new PlacementRequestPayload(_activeComponentId, _currentPreviewWorld));
                CancelDrag();
            }
        }

        private bool TryGetPointerWorldPosition(out Vector3 worldPosition)
        {
            var screenPoint = UnityEngine.Input.mousePosition;
            var ray = worldCamera.ScreenPointToRay(screenPoint);

            if (Physics.Raycast(ray, out var hit, Mathf.Infinity, placementSurfaceMask))
            {
                worldPosition = hit.point;
                return true;
            }

            var dragPlane = new Plane(Vector3.up, new Vector3(0f, dragPlaneHeight, 0f));
            if (dragPlane.Raycast(ray, out var enter))
            {
                worldPosition = ray.GetPoint(enter);
                return true;
            }

            worldPosition = default;
            return false;
        }

        private void SetGhostVisible(bool visible)
        {
            if (ghostVisual != null)
            {
                ghostVisual.gameObject.SetActive(visible);
            }
        }
    }

    public readonly struct PlacementRequestPayload
    {
        public PlacementRequestPayload(string componentId, Vector3 worldPosition)
        {
            ComponentId = componentId;
            WorldPosition = worldPosition;
        }

        public string ComponentId { get; }
        public Vector3 WorldPosition { get; }
    }
}
