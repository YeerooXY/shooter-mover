using ShooterMover.Application.Flow.Hub;
using ShooterMover.Contracts.Flow.Session;
using UnityEngine;

namespace ShooterMover.UI.Hub
{
    /// <summary>
    /// Consumes the one-shot Results-to-Hub handoff only after the Hub controller has
    /// captured the exact immutable payload and reached the Hub route.
    /// </summary>
    [DefaultExecutionOrder(11000)]
    [DisallowMultipleComponent]
    public sealed class HubReturnRouteInstallerV1 : MonoBehaviour
    {
        public bool Applied { get; private set; }
        public string RejectionCode { get; private set; }

        private void Awake()
        {
            PlayerRouteProfilePayloadV1 payload;
            if (!HubReturnRouteContextV1.TryPeek(out payload))
            {
                return;
            }

            HubFlowControllerV1 controller =
                GetComponent<HubFlowControllerV1>();
            if (controller == null)
            {
                RejectionCode = "hub-return-controller-missing";
                return;
            }

            controller.ConfigureForTests(
                payload,
                new HubRoutePlaceholderAdapterV1());
            if (!controller.OpenCharacterSelect()
                || !controller.ContinueToHub()
                || controller.Payload.Fingerprint != payload.Fingerprint)
            {
                RejectionCode = "hub-return-route-rejected";
                return;
            }

            PlayerRouteProfilePayloadV1 consumed;
            if (!HubReturnRouteContextV1.TryConsume(out consumed)
                || !ReferenceEquals(consumed, payload))
            {
                RejectionCode = "hub-return-context-consume-failed";
                return;
            }

            Applied = true;
            RejectionCode = string.Empty;
        }
    }
}
