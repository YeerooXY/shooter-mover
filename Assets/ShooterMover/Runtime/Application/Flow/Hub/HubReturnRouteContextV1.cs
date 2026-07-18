using System;
using ShooterMover.Contracts.Flow.Session;

namespace ShooterMover.Application.Flow.Hub
{
    /// <summary>
    /// Narrow one-shot handoff used when Results returns to the exact selected Hub profile.
    /// It stores only the immutable route payload and owns no inventory or progression state.
    /// </summary>
    public static class HubReturnRouteContextV1
    {
        private static readonly object Gate = new object();
        private static PlayerRouteProfilePayloadV1 current;

        public static bool HasValue
        {
            get
            {
                lock (Gate)
                {
                    return current != null;
                }
            }
        }

        public static void Capture(PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null || !payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable Hub route payload is required.",
                    nameof(payload));
            }

            lock (Gate)
            {
                current = payload;
            }
        }

        public static bool TryPeek(out PlayerRouteProfilePayloadV1 payload)
        {
            lock (Gate)
            {
                payload = current;
                return payload != null && payload.HasValidFingerprint();
            }
        }

        public static bool TryConsume(out PlayerRouteProfilePayloadV1 payload)
        {
            lock (Gate)
            {
                payload = current;
                if (payload == null || !payload.HasValidFingerprint())
                {
                    payload = null;
                    return false;
                }

                current = null;
                return true;
            }
        }

        public static void Clear()
        {
            lock (Gate)
            {
                current = null;
            }
        }
    }
}
