using System;
using ShooterMover.Contracts.Flow.Session;

namespace ShooterMover.Application.Flow.Hub
{
    public static class CharacterSelectionEntryRouteContextV1
    {
        private static readonly object Gate = new object();
        private static PlayerRouteProfilePayloadV1 current;

        public static void Capture(PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null || !payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable character-selection payload is required.",
                    nameof(payload));
            }

            lock (Gate)
            {
                current = payload;
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
