using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed partial class GunFiringScheduler
    {
        private static readonly object AcceptanceAuthority = new object();

        public sealed class AcceptedEmission
        {
            internal AcceptedEmission(
                object acceptanceAuthority,
                GunFireCommand command,
                RunParticipantId participantId,
                GunDefinitionId gunDefinitionId,
                EquipmentInstanceId equipmentInstanceId,
                FireOperationId sourceFireOperationId,
                string effectiveGunFingerprint,
                GunFiringEmissionKind kind,
                long cadenceOrdinal,
                long shotSequence,
                long ticksUntilNextEmission,
                int emissionOrdinal,
                int triggerGroupOrdinal,
                int burstShotOrdinal,
                int pulseOrdinal)
            {
                if (!ReferenceEquals(acceptanceAuthority, AcceptanceAuthority))
                {
                    throw new InvalidOperationException(
                        "Accepted gun emissions can only be created by GunFiringScheduler.");
                }

                Command = command ?? throw new ArgumentNullException(nameof(command));
                ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
                GunDefinitionId = gunDefinitionId
                    ?? throw new ArgumentNullException(nameof(gunDefinitionId));
                EquipmentInstanceId = equipmentInstanceId
                    ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
                SourceFireOperationId = sourceFireOperationId
                    ?? throw new ArgumentNullException(nameof(sourceFireOperationId));
                EffectiveGunFingerprint = effectiveGunFingerprint
                    ?? throw new ArgumentNullException(nameof(effectiveGunFingerprint));
                if (!Enum.IsDefined(typeof(GunFiringEmissionKind), kind))
                {
                    throw new ArgumentOutOfRangeException(nameof(kind));
                }
                if (cadenceOrdinal < 0L
                    || shotSequence < 0L
                    || ticksUntilNextEmission < 0L
                    || emissionOrdinal < 0
                    || triggerGroupOrdinal < 0
                    || burstShotOrdinal < 0
                    || pulseOrdinal < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(emissionOrdinal),
                        "Firing timing and ordinals must be non-negative.");
                }

                Kind = kind;
                CadenceOrdinal = cadenceOrdinal;
                ShotSequence = shotSequence;
                TicksUntilNextEmission = ticksUntilNextEmission;
                EmissionOrdinal = emissionOrdinal;
                TriggerGroupOrdinal = triggerGroupOrdinal;
                BurstShotOrdinal = burstShotOrdinal;
                PulseOrdinal = pulseOrdinal;
                CanonicalText = BuildCanonicalText();
                Fingerprint = GunExecutionFingerprint.Compute(CanonicalText);
            }

            public GunFireCommand Command { get; }
            public RunParticipantId ParticipantId { get; }
            public GunDefinitionId GunDefinitionId { get; }
            public EquipmentInstanceId EquipmentInstanceId { get; }
            public FireOperationId SourceFireOperationId { get; }
            public FireOperationId EmissionFireOperationId { get { return Command.FireOperationId; } }
            public string EffectiveGunFingerprint { get; }
            public GunFiringEmissionKind Kind { get; }
            public long ScheduledTick { get { return Command.SimulationTick; } }
            public long CadenceOrdinal { get; }
            public long ShotSequence { get; }
            public long TicksUntilNextEmission { get; }
            public int EmissionOrdinal { get; }
            public int TriggerGroupOrdinal { get; }
            public int BurstShotOrdinal { get; }
            public int PulseOrdinal { get; }
            public string CanonicalText { get; }
            public string Fingerprint { get; }

            public bool HasValidFingerprint()
            {
                if (Command == null
                    || ParticipantId == null
                    || GunDefinitionId == null
                    || EquipmentInstanceId == null
                    || SourceFireOperationId == null
                    || string.IsNullOrWhiteSpace(EffectiveGunFingerprint)
                    || string.IsNullOrWhiteSpace(Fingerprint)
                    || CadenceOrdinal < 0L
                    || ShotSequence < 0L
                    || TicksUntilNextEmission < 0L
                    || EmissionOrdinal < 0
                    || TriggerGroupOrdinal < 0
                    || BurstShotOrdinal < 0
                    || PulseOrdinal < 0
                    || !Enum.IsDefined(typeof(GunFiringEmissionKind), Kind)
                    || !EquipmentInstanceId.Equals(Command.EquipmentInstanceId)
                    || !DeriveEmissionOperationId(
                        SourceFireOperationId,
                        EffectiveGunFingerprint,
                        ShotSequence,
                        EmissionOrdinal).Equals(Command.FireOperationId)
                    || !string.Equals(
                        Command.Fingerprint,
                        GunExecutionFingerprint.Compute(Command.CanonicalText),
                        StringComparison.Ordinal))
                {
                    return false;
                }

                return string.Equals(
                    Fingerprint,
                    GunExecutionFingerprint.Compute(BuildCanonicalText()),
                    StringComparison.Ordinal);
            }

            public bool HasValidFingerprint(EffectiveGun gun)
            {
                if (gun == null || !HasValidFingerprint())
                {
                    return false;
                }

                GunFiringEmissionKind expectedKind = gun.FireSettings.IsContinuous
                    ? GunFiringEmissionKind.ContinuousDamageTick
                    : GunFiringEmissionKind.ProjectileShot;
                return gun.DefinitionId.Equals(GunDefinitionId)
                    && gun.EquipmentInstanceId.Equals(EquipmentInstanceId)
                    && Kind == expectedKind
                    && string.Equals(
                        EffectiveGunFingerprint,
                        EffectiveGunFiringFingerprint.Compute(gun),
                        StringComparison.Ordinal);
            }

            private string BuildCanonicalText()
            {
                return string.Join(
                    "\n",
                    new[]
                    {
                        "command_fingerprint=" + Command.Fingerprint,
                        "participant_id=" + ParticipantId,
                        "gun_definition_id=" + GunDefinitionId,
                        "equipment_instance_id=" + EquipmentInstanceId,
                        "source_fire_operation_id=" + SourceFireOperationId,
                        "emission_fire_operation_id=" + Command.FireOperationId,
                        "effective_gun_fingerprint=" + EffectiveGunFingerprint,
                        "kind=" + ((int)Kind).ToString(CultureInfo.InvariantCulture),
                        "scheduled_tick="
                            + ScheduledTick.ToString(CultureInfo.InvariantCulture),
                        "cadence_ordinal="
                            + CadenceOrdinal.ToString(CultureInfo.InvariantCulture),
                        "shot_sequence=" + ShotSequence.ToString(CultureInfo.InvariantCulture),
                        "ticks_until_next_emission="
                            + TicksUntilNextEmission.ToString(CultureInfo.InvariantCulture),
                        "emission_ordinal=" + EmissionOrdinal.ToString(CultureInfo.InvariantCulture),
                        "trigger_group_ordinal="
                            + TriggerGroupOrdinal.ToString(CultureInfo.InvariantCulture),
                        "burst_shot_ordinal="
                            + BurstShotOrdinal.ToString(CultureInfo.InvariantCulture),
                        "pulse_ordinal=" + PulseOrdinal.ToString(CultureInfo.InvariantCulture),
                    });
            }
        }

        public sealed class AcceptedSchedule
        {
            private readonly ReadOnlyCollection<AcceptedEmission> emissions;

            internal AcceptedSchedule(
                object acceptanceAuthority,
                GunFireCommand sourceCommand,
                RunParticipantId participantId,
                GunDefinitionId gunDefinitionId,
                EquipmentInstanceId equipmentInstanceId,
                string effectiveGunFingerprint,
                GunTriggerSignal triggerSignal,
                string requestFingerprint,
                long cadenceOriginTick,
                long firstCadenceOrdinal,
                long nextCadenceOrdinal,
                long nextCadenceTick,
                IList<AcceptedEmission> acceptedEmissions)
            {
                if (!ReferenceEquals(acceptanceAuthority, AcceptanceAuthority))
                {
                    throw new InvalidOperationException(
                        "Accepted gun schedules can only be created by GunFiringScheduler.");
                }
                if (acceptedEmissions == null || acceptedEmissions.Count < 1)
                {
                    throw new ArgumentException(
                        "An accepted firing schedule requires at least one emission.",
                        nameof(acceptedEmissions));
                }
                if (!Enum.IsDefined(typeof(GunTriggerSignal), triggerSignal))
                {
                    throw new ArgumentOutOfRangeException(nameof(triggerSignal));
                }

                SourceCommand = sourceCommand
                    ?? throw new ArgumentNullException(nameof(sourceCommand));
                ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
                GunDefinitionId = gunDefinitionId
                    ?? throw new ArgumentNullException(nameof(gunDefinitionId));
                EquipmentInstanceId = equipmentInstanceId
                    ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
                EffectiveGunFingerprint = effectiveGunFingerprint
                    ?? throw new ArgumentNullException(nameof(effectiveGunFingerprint));
                TriggerSignal = triggerSignal;
                RequestFingerprint = requestFingerprint
                    ?? throw new ArgumentNullException(nameof(requestFingerprint));
                CadenceOriginTick = cadenceOriginTick;
                FirstCadenceOrdinal = firstCadenceOrdinal;
                NextCadenceOrdinal = nextCadenceOrdinal;
                NextCadenceTick = nextCadenceTick;
                emissions = new ReadOnlyCollection<AcceptedEmission>(
                    new List<AcceptedEmission>(acceptedEmissions));
                CanonicalText = BuildCanonicalText();
                Fingerprint = GunExecutionFingerprint.Compute(CanonicalText);
            }

            public GunFireCommand SourceCommand { get; }
            public GunActorInstanceId ActorId { get { return SourceCommand.ActorId; } }
            public RunParticipantId ParticipantId { get; }
            public GunDefinitionId GunDefinitionId { get; }
            public EquipmentInstanceId EquipmentInstanceId { get; }
            public FireOperationId SourceFireOperationId
            {
                get { return SourceCommand.FireOperationId; }
            }
            public LifecycleGeneration LifecycleGeneration
            {
                get { return SourceCommand.LifecycleGeneration; }
            }
            public string EffectiveGunFingerprint { get; }
            public GunTriggerSignal TriggerSignal { get; }
            public string RequestFingerprint { get; }
            public long CadenceOriginTick { get; }
            public long FirstCadenceOrdinal { get; }
            public long NextCadenceOrdinal { get; }
            public long NextCadenceTick { get; }
            public IReadOnlyList<AcceptedEmission> Emissions { get { return emissions; } }
            public int EmissionCount { get { return emissions.Count; } }
            public long FirstScheduledTick { get { return emissions[0].ScheduledTick; } }
            public long LastScheduledTick { get { return emissions[emissions.Count - 1].ScheduledTick; } }
            public long FirstShotSequence { get { return emissions[0].ShotSequence; } }
            public long LastShotSequence { get { return emissions[emissions.Count - 1].ShotSequence; } }
            public string CanonicalText { get; }
            public string Fingerprint { get; }

            public bool HasValidFingerprint()
            {
                if (SourceCommand == null
                    || ActorId == null
                    || ParticipantId == null
                    || GunDefinitionId == null
                    || EquipmentInstanceId == null
                    || SourceFireOperationId == null
                    || LifecycleGeneration == null
                    || string.IsNullOrWhiteSpace(EffectiveGunFingerprint)
                    || string.IsNullOrWhiteSpace(RequestFingerprint)
                    || string.IsNullOrWhiteSpace(Fingerprint)
                    || !Enum.IsDefined(typeof(GunTriggerSignal), TriggerSignal)
                    || CadenceOriginTick < 0L
                    || FirstCadenceOrdinal < 0L
                    || NextCadenceOrdinal <= FirstCadenceOrdinal
                    || NextCadenceTick < CadenceOriginTick
                    || emissions.Count < 1
                    || FirstScheduledTick > SourceCommand.SimulationTick
                    || !EquipmentInstanceId.Equals(SourceCommand.EquipmentInstanceId)
                    || !string.Equals(
                        SourceCommand.Fingerprint,
                        GunExecutionFingerprint.Compute(SourceCommand.CanonicalText),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        RequestFingerprint,
                        ComputeRequestFingerprint(
                            SourceCommand.Fingerprint,
                            ParticipantId,
                            TriggerSignal,
                            EffectiveGunFingerprint),
                        StringComparison.Ordinal))
                {
                    return false;
                }

                HashSet<string> operationIds = new HashSet<string>(StringComparer.Ordinal);
                HashSet<long> cadenceOrdinals = new HashSet<long>();
                long expectedSequence = emissions[0].ShotSequence;
                for (int index = 0; index < emissions.Count; index++)
                {
                    AcceptedEmission emission = emissions[index];
                    long expectedNextTick = index + 1 < emissions.Count
                        ? emissions[index + 1].ScheduledTick
                        : NextCadenceTick;
                    long expectedDelay = emission == null
                        ? -1L
                        : Math.Max(0L, expectedNextTick - emission.ScheduledTick);
                    if (emission == null
                        || !emission.HasValidFingerprint()
                        || emission.EmissionOrdinal != index
                        || emission.ShotSequence != expectedSequence
                        || emission.CadenceOrdinal < FirstCadenceOrdinal
                        || emission.CadenceOrdinal >= NextCadenceOrdinal
                        || emission.TicksUntilNextEmission != expectedDelay
                        || !emission.Command.ActorId.Equals(ActorId)
                        || !emission.ParticipantId.Equals(ParticipantId)
                        || !emission.GunDefinitionId.Equals(GunDefinitionId)
                        || !emission.EquipmentInstanceId.Equals(EquipmentInstanceId)
                        || !emission.SourceFireOperationId.Equals(SourceFireOperationId)
                        || !emission.Command.LifecycleGeneration.Equals(LifecycleGeneration)
                        || emission.Command.DeterministicSeed != SourceCommand.DeterministicSeed
                        || !emission.Command.Origin.Equals(SourceCommand.Origin)
                        || !emission.Command.AimDirection.Equals(SourceCommand.AimDirection)
                        || !operationIds.Add(emission.EmissionFireOperationId.ToString())
                        || (index > 0 && CompareEmissions(emissions[index - 1], emission) > 0))
                    {
                        return false;
                    }

                    cadenceOrdinals.Add(emission.CadenceOrdinal);
                    if (expectedSequence == long.MaxValue && index + 1 < emissions.Count)
                    {
                        return false;
                    }
                    expectedSequence++;
                }

                long cadenceCount;
                try
                {
                    cadenceCount = checked(NextCadenceOrdinal - FirstCadenceOrdinal);
                }
                catch (OverflowException)
                {
                    return false;
                }

                return cadenceCount == cadenceOrdinals.Count
                    && string.Equals(
                        Fingerprint,
                        GunExecutionFingerprint.Compute(BuildCanonicalText()),
                        StringComparison.Ordinal);
            }

            public bool HasValidFingerprint(EffectiveGun gun)
            {
                if (gun == null
                    || !HasValidFingerprint()
                    || !gun.DefinitionId.Equals(GunDefinitionId)
                    || !gun.EquipmentInstanceId.Equals(EquipmentInstanceId)
                    || !string.Equals(
                        EffectiveGunFingerprint,
                        EffectiveGunFiringFingerprint.Compute(gun),
                        StringComparison.Ordinal))
                {
                    return false;
                }

                for (int index = 0; index < emissions.Count; index++)
                {
                    if (!emissions[index].HasValidFingerprint(gun))
                    {
                        return false;
                    }
                }
                return true;
            }

            private string BuildCanonicalText()
            {
                StringBuilder builder = new StringBuilder();
                builder.Append("source_command_fingerprint=")
                    .Append(SourceCommand.Fingerprint)
                    .Append('\n');
                builder.Append("actor_id=").Append(ActorId).Append('\n');
                builder.Append("participant_id=").Append(ParticipantId).Append('\n');
                builder.Append("gun_definition_id=").Append(GunDefinitionId).Append('\n');
                builder.Append("equipment_instance_id=").Append(EquipmentInstanceId).Append('\n');
                builder.Append("source_fire_operation_id=")
                    .Append(SourceFireOperationId)
                    .Append('\n');
                builder.Append("lifecycle_generation=").Append(LifecycleGeneration).Append('\n');
                builder.Append("effective_gun_fingerprint=")
                    .Append(EffectiveGunFingerprint)
                    .Append('\n');
                builder.Append("trigger_signal=")
                    .Append(((int)TriggerSignal).ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                builder.Append("request_fingerprint=").Append(RequestFingerprint).Append('\n');
                builder.Append("cadence_origin_tick=")
                    .Append(CadenceOriginTick.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                builder.Append("first_cadence_ordinal=")
                    .Append(FirstCadenceOrdinal.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                builder.Append("next_cadence_ordinal=")
                    .Append(NextCadenceOrdinal.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                builder.Append("next_cadence_tick=")
                    .Append(NextCadenceTick.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                builder.Append("emission_count=")
                    .Append(emissions.Count.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
                for (int index = 0; index < emissions.Count; index++)
                {
                    builder.Append("emission[")
                        .Append(index.ToString(CultureInfo.InvariantCulture))
                        .Append("]=")
                        .Append(emissions[index] == null ? "null" : emissions[index].Fingerprint)
                        .Append('\n');
                }
                return builder.ToString();
            }

            private static int CompareEmissions(
                AcceptedEmission left,
                AcceptedEmission right)
            {
                int tick = left.ScheduledTick.CompareTo(right.ScheduledTick);
                if (tick != 0) { return tick; }
                int cadence = left.CadenceOrdinal.CompareTo(right.CadenceOrdinal);
                if (cadence != 0) { return cadence; }
                int group = left.TriggerGroupOrdinal.CompareTo(right.TriggerGroupOrdinal);
                if (group != 0) { return group; }
                int burst = left.BurstShotOrdinal.CompareTo(right.BurstShotOrdinal);
                return burst != 0
                    ? burst
                    : left.PulseOrdinal.CompareTo(right.PulseOrdinal);
            }
        }
    }
}
