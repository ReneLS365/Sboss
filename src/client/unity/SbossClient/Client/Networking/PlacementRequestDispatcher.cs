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
        [SerializeField] private bool simulateRejections;
        [SerializeField] [Range(0f, 1f)] private float simulatedRejectionRate = 0.15f;

        public event Action<PlacementRequestPayload>? RequestBegan;
        public event Action<PlacementAuthoritativeResult>? RequestCompleted;

        public void DispatchPlacementRequest(PlacementRequestPayload payload)
        {
            StartCoroutine(SimulateSend(payload));
        }

        private IEnumerator SimulateSend(PlacementRequestPayload payload)
        {
            RequestBegan?.Invoke(payload);
            yield return new WaitForSeconds(simulatedTransportDelaySeconds);

            var accepted = !simulateRejections || UnityEngine.Random.value >= simulatedRejectionRate;
            var result = accepted
                ? PlacementAuthoritativeResult.Accepted(payload.ClientRequestId)
                : PlacementAuthoritativeResult.Rejected(payload.ClientRequestId, "server_rejected", "Placement rejected by authoritative validation.");

            RequestCompleted?.Invoke(result);
        }
    }

    public readonly struct PlacementAuthoritativeResult
    {
        public PlacementAuthoritativeResult(string clientRequestId, bool accepted, string code, string message)
        {
            ClientRequestId = clientRequestId;
            Accepted = accepted;
            Code = code;
            Message = message;
        }

        public string ClientRequestId { get; }
        public bool Accepted { get; }
        public string Code { get; }
        public string Message { get; }

        public static PlacementAuthoritativeResult Accepted(string clientRequestId)
        {
            return new PlacementAuthoritativeResult(clientRequestId, true, "accepted", "Placement accepted.");
        }

        public static PlacementAuthoritativeResult Rejected(string clientRequestId, string code, string message)
        {
            return new PlacementAuthoritativeResult(clientRequestId, false, code, message);
        }
    }
}
