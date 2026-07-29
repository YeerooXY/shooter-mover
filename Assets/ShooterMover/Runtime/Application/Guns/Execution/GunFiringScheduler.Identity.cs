using System;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed partial class GunFiringScheduler
    {
        private const double DirectionEpsilon = 0.000000000001d;
        private const string EmissionOperationNamespace = "fire-emission";

        private static bool TryValidateRequest(
            GunFiringRequest request,
            out string code)
        {
            if (request == null
                || request.Gun == null
                || request.Command == null
                || request.ParticipantId == null
                || !Enum.IsDefined(
                    typeof(GunTriggerSignal),
                    request.TriggerSignal))
            {
                code = "gun-firing-request-missing-or-invalid";
                return false;
            }

            GunFireCommand command = request.Command;
            if (command.ActorId == null
                || command.EquipmentInstanceId == null
                || command.FireOperationId == null
                || command.LifecycleGeneration == null
                || command.SimulationTick < 0L
                || command.Origin == null
                || !command.Origin.IsFinite
                || command.AimDirection == null
                || !command.AimDirection.IsFinite
                || command.AimDirection.LengthSquared <= DirectionEpsilon
                || string.IsNullOrWhiteSpace(command.CanonicalText)
                || string.IsNullOrWhiteSpace(command.Fingerprint)
                || !string.Equals(
                    command.Fingerprint,
                    GunExecutionFingerprint.Compute(command.CanonicalText),
                    StringComparison.Ordinal))
            {
                code = "gun-firing-command-invalid";
                return false;
            }

            if (!request.Gun.EquipmentInstanceId.Equals(
                    command.EquipmentInstanceId))
            {
                code = "gun-firing-equipment-instance-mismatch";
                return false;
            }

            code = string.Empty;
            return true;
        }

        private static string RequestFingerprint(
            GunFiringRequest request,
            string effectiveFingerprint)
        {
            return ComputeRequestFingerprint(
                request.Command.Fingerprint,
                request.ParticipantId,
                request.TriggerSignal,
                effectiveFingerprint);
        }

        internal static string ComputeRequestFingerprint(
            string sourceCommandFingerprint,
            RunParticipantId participantId,
            GunTriggerSignal triggerSignal,
            string effectiveFingerprint)
        {
            if (string.IsNullOrWhiteSpace(sourceCommandFingerprint))
            {
                throw new ArgumentException(
                    "A source command fingerprint is required.",
                    nameof(sourceCommandFingerprint));
            }
            if (participantId == null)
            {
                throw new ArgumentNullException(nameof(participantId));
            }
            if (!Enum.IsDefined(typeof(GunTriggerSignal), triggerSignal))
            {
                throw new ArgumentOutOfRangeException(nameof(triggerSignal));
            }
            if (string.IsNullOrWhiteSpace(effectiveFingerprint))
            {
                throw new ArgumentException(
                    "An effective gun fingerprint is required.",
                    nameof(effectiveFingerprint));
            }

            string canonical = string.Join(
                "\n",
                new[]
                {
                    "command_fingerprint=" + sourceCommandFingerprint,
                    "participant_id=" + participantId,
                    "trigger_signal="
                        + ((int)triggerSignal).ToString(CultureInfo.InvariantCulture),
                    "effective_gun_fingerprint=" + effectiveFingerprint,
                });
            return GunExecutionFingerprint.Compute(canonical);
        }

        internal static FireOperationId DeriveEmissionOperationId(
            FireOperationId sourceFireOperationId,
            string effectiveFingerprint,
            long shotSequence,
            int emissionOrdinal)
        {
            if (sourceFireOperationId == null)
            {
                throw new ArgumentNullException(nameof(sourceFireOperationId));
            }
            if (string.IsNullOrWhiteSpace(effectiveFingerprint))
            {
                throw new ArgumentException(
                    "An effective gun fingerprint is required.",
                    nameof(effectiveFingerprint));
            }
            if (shotSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(shotSequence));
            }
            if (emissionOrdinal < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(emissionOrdinal));
            }

            string canonical = string.Join(
                "\n",
                new[]
                {
                    "source_fire_operation_id=" + sourceFireOperationId,
                    "effective_gun_fingerprint=" + effectiveFingerprint,
                    "shot_sequence=" + shotSequence.ToString(CultureInfo.InvariantCulture),
                    "emission_ordinal="
                        + emissionOrdinal.ToString(CultureInfo.InvariantCulture),
                });
            string fingerprint = GunExecutionFingerprint.Compute(canonical);
            string digest = fingerprint.Substring(GunExecutionFingerprint.Prefix.Length);
            return new FireOperationId(
                StableId.Create(EmissionOperationNamespace, digest));
        }

        private static bool IsExactReplay(
            GunFiringReplayRecord replay,
            string requestFingerprint,
            string effectiveFingerprint)
        {
            return replay != null
                && string.Equals(
                    replay.RequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    replay.EffectiveGunFingerprint,
                    effectiveFingerprint,
                    StringComparison.Ordinal);
        }

        private static bool TryAdd(long left, long right, out long result)
        {
            try
            {
                result = checked(left + right);
                return true;
            }
            catch (OverflowException)
            {
                result = 0L;
                return false;
            }
        }

        private static bool TryMultiply(long left, long right, out long result)
        {
            try
            {
                result = checked(left * right);
                return true;
            }
            catch (OverflowException)
            {
                result = 0L;
                return false;
            }
        }
    }
}
