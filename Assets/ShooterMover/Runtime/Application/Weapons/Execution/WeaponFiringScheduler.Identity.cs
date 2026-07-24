using System;
using System.Globalization;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponFiringScheduler
    {
        private const double DirectionEpsilon = 0.000000000001d;
        private const string EmissionOperationNamespace = "fire-emission";

        private static bool TryValidateRequest(
            WeaponFiringRequest request,
            out string code)
        {
            if (request == null
                || request.Weapon == null
                || request.Command == null
                || request.ParticipantId == null
                || !Enum.IsDefined(
                    typeof(WeaponTriggerSignal),
                    request.TriggerSignal))
            {
                code = "weapon-firing-request-missing-or-invalid";
                return false;
            }

            WeaponFireCommand command = request.Command;
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
                    WeaponExecutionFingerprint.Compute(command.CanonicalText),
                    StringComparison.Ordinal))
            {
                code = "weapon-firing-command-invalid";
                return false;
            }

            if (!request.Weapon.EquipmentInstanceId.Equals(
                    command.EquipmentInstanceId))
            {
                code = "weapon-firing-equipment-instance-mismatch";
                return false;
            }

            code = string.Empty;
            return true;
        }

        private static string RequestFingerprint(
            WeaponFiringRequest request,
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
            WeaponTriggerSignal triggerSignal,
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
            if (!Enum.IsDefined(typeof(WeaponTriggerSignal), triggerSignal))
            {
                throw new ArgumentOutOfRangeException(nameof(triggerSignal));
            }
            if (string.IsNullOrWhiteSpace(effectiveFingerprint))
            {
                throw new ArgumentException(
                    "An effective weapon fingerprint is required.",
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
                    "effective_weapon_fingerprint=" + effectiveFingerprint,
                });
            return WeaponExecutionFingerprint.Compute(canonical);
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
                    "An effective weapon fingerprint is required.",
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
                    "effective_weapon_fingerprint=" + effectiveFingerprint,
                    "shot_sequence=" + shotSequence.ToString(CultureInfo.InvariantCulture),
                    "emission_ordinal="
                        + emissionOrdinal.ToString(CultureInfo.InvariantCulture),
                });
            string fingerprint = WeaponExecutionFingerprint.Compute(canonical);
            string digest = fingerprint.Substring(WeaponExecutionFingerprint.Prefix.Length);
            return new FireOperationId(
                StableId.Create(EmissionOperationNamespace, digest));
        }

        private static bool IsExactReplay(
            WeaponFiringReplayRecord replay,
            string requestFingerprint,
            string effectiveFingerprint)
        {
            return replay != null
                && string.Equals(
                    replay.RequestFingerprint,
                    requestFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    replay.EffectiveWeaponFingerprint,
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
