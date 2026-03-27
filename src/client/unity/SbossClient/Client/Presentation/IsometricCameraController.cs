using UnityEngine;

namespace SbossClient.Client.Presentation
{
    /// <summary>
    /// Visual-only isometric camera shell for Phase 2B.
    /// No gameplay validation or simulation authority is allowed here.
    /// </summary>
    public sealed class IsometricCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform focusTarget;
        [SerializeField] private Vector3 focusOffset = Vector3.zero;

        [Header("Isometric Framing")]
        [SerializeField] private float fixedPitchDegrees = 35f;
        [SerializeField] private float fixedYawDegrees = 45f;
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 30f;
        [SerializeField] private float initialDistance = 14f;

        [Header("Mobile-Friendly Controls")]
        [SerializeField] private float panUnitsPerScreen = 30f;
        [SerializeField] private float zoomStep = 2.5f;
        [SerializeField] private float positionSmoothing = 18f;

        private Vector3 _panOffset;
        private float _distance;

        private void Awake()
        {
            _distance = Mathf.Clamp(initialDistance, minDistance, maxDistance);
        }

        public void PanByViewportDelta(Vector2 viewportDelta)
        {
            var yawRotation = Quaternion.Euler(0f, fixedYawDegrees, 0f);
            var right = yawRotation * Vector3.right;
            var forward = yawRotation * Vector3.forward;
            _panOffset += ((-right * viewportDelta.x) + (-forward * viewportDelta.y)) * panUnitsPerScreen;
        }

        public void ZoomByStep(float direction)
        {
            _distance = Mathf.Clamp(_distance - (direction * zoomStep), minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (focusTarget == null)
            {
                return;
            }

            var focus = focusTarget.position + focusOffset + _panOffset;
            var rotation = Quaternion.Euler(fixedPitchDegrees, fixedYawDegrees, 0f);
            var desiredPosition = focus - (rotation * Vector3.forward * _distance);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, desiredPosition, Time.deltaTime * positionSmoothing),
                rotation);
        }
    }
}
