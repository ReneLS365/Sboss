using System;
using System.Collections;
using SbossClient.Client.Input;
using UnityEngine;

namespace SbossClient.Client.Networking
{
    /// <summary>
    /// Transport shell that emits placement requests toward authoritative backend.
    /// It does not evaluate acceptance/rejection locally.
    /// </summary>
    public sealed class PlacementRequestDispatcher : MonoBehaviour
    {
        [SerializeField] private float simulatedTransportDelaySeconds = 0.2f;

        public event Action? RequestBegan;
        public event Action? RequestCompleted;

        public void DispatchPlacementRequest(PlacementRequestPayload payload)
        {
            StartCoroutine(SimulateSend(payload));
        }

        private IEnumerator SimulateSend(PlacementRequestPayload payload)
        {
            _ = payload;
            RequestBegan?.Invoke();
            yield return new WaitForSeconds(simulatedTransportDelaySeconds);
            RequestCompleted?.Invoke();
        }
    }
}
