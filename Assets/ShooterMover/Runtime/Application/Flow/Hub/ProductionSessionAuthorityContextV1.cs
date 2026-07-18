using System;
using ShooterMover.Application.Missions.Run;
using ShooterMover.Contracts.Flow.Session;

namespace ShooterMover.Application.Flow.Hub
{
    /// <summary>
    /// Process-local projection of the single Bootstrap-owned production authority set.
    /// It carries immutable route state and prepares one exact Stage 1 dependency handoff.
    /// It never creates or replaces durable authorities.
    /// </summary>
    public static class ProductionSessionAuthorityContextV1
    {
        private static readonly object Gate = new object();
        private static object ownerToken;
        private static PlayerRouteProfilePayloadV1 routePayload;
        private static Stage1ProductionAuthorityBundleV1 stage1Bundle;

        public static void CaptureOwner(
            object token,
            PlayerRouteProfilePayloadV1 payload,
            Stage1ProductionAuthorityBundleV1 bundle)
        {
            if (token == null) throw new ArgumentNullException(nameof(token));
            if (payload == null || !payload.HasValidFingerprint())
            {
                throw new ArgumentException(
                    "A valid immutable production route payload is required.",
                    nameof(payload));
            }
            if (bundle == null) throw new ArgumentNullException(nameof(bundle));

            lock (Gate)
            {
                if (ownerToken != null && !ReferenceEquals(ownerToken, token))
                {
                    throw new InvalidOperationException(
                        "A different production authority owner is already active.");
                }

                ownerToken = token;
                routePayload = payload;
                stage1Bundle = bundle;
            }
        }

        public static bool TryReadRoutePayload(out PlayerRouteProfilePayloadV1 payload)
        {
            lock (Gate)
            {
                payload = routePayload;
                return ownerToken != null
                    && payload != null
                    && payload.HasValidFingerprint();
            }
        }

        public static bool TryUpdateRoutePayload(PlayerRouteProfilePayloadV1 payload)
        {
            if (payload == null || !payload.HasValidFingerprint())
            {
                return false;
            }

            lock (Gate)
            {
                if (ownerToken == null || stage1Bundle == null)
                {
                    return false;
                }

                routePayload = payload;
                return true;
            }
        }

        public static bool TryPrepareStage1(
            PlayerRouteProfilePayloadV1 payload,
            out string rejectionCode)
        {
            rejectionCode = string.Empty;
            if (payload == null || !payload.HasValidFingerprint())
            {
                rejectionCode = "production-session-route-invalid";
                return false;
            }

            Stage1ProductionAuthorityBundleV1 bundle;
            lock (Gate)
            {
                if (ownerToken == null || stage1Bundle == null)
                {
                    rejectionCode = "production-session-owner-missing";
                    return false;
                }

                routePayload = payload;
                bundle = stage1Bundle;
            }

            try
            {
                Stage1ProductionAuthorityContextV1.Capture(bundle);
                return true;
            }
            catch (InvalidOperationException)
            {
                rejectionCode = "stage1-authority-bundle-conflict";
                return false;
            }
        }

        public static void ReleaseOwner(object token)
        {
            if (token == null) return;

            lock (Gate)
            {
                if (!ReferenceEquals(ownerToken, token))
                {
                    return;
                }

                ownerToken = null;
                routePayload = null;
                stage1Bundle = null;
                Stage1ProductionAuthorityContextV1.ClearForTests();
            }
        }

        public static void ClearForTests()
        {
            lock (Gate)
            {
                ownerToken = null;
                routePayload = null;
                stage1Bundle = null;
                Stage1ProductionAuthorityContextV1.ClearForTests();
            }
        }
    }
}
