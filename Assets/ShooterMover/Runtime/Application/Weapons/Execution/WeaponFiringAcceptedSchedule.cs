using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ShooterMover.Domain.Weapons;
using ShooterMover.Domain.Weapons.Execution;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponFiringScheduler
    {
        private static readonly object AcceptanceAuthority = new object();

        public sealed class AcceptedEmission
        {
            internal AcceptedEmission(
                object acceptanceAuthority,
                WeaponFireCommand command,
                RunParticipantId participantId,
                WeaponDefinitionId weaponDefinitionId,
                EquipmentInstanceId equipmentInstanceId,
                FireOperationId sourceFireOperationId,
                string effectiveWeaponFingerprint,
                WeaponFiringEmissionKind kind,
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
                        "Accepted weapon emissions can only be created by WeaponFiringScheduler.");
                }

                Command = command ?? throw new ArgumentNullException(nameof(command));
                ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
                WeaponDefinitionId = weaponDefinitionId
                    ?? throw new ArgumentNullException(nameof(weaponDefinitionId));
                EquipmentInstanceId = equipmentInstanceId
                    ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
                SourceFireOperationId = sourceFireOperationId
                    ?? throw new ArgumentNullException(nameof(sourceFireOperationId));
                EffectiveWeaponFingerprint = effectiveWeaponFingerprint
                    ?? throw new ArgumentNullException(nameof(effectiveWeaponFingerprint));
                if (!Enum.IsDefined(typeof(WeaponFiringEmissionKind), kind))
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
                Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
            }

            public WeaponFireCommand Command { get; }
            public RunParticipantId ParticipantId { get; }
            public WeaponDefinitionId WeaponDefinitionId { get; }
            public EquipmentInstanceId EquipmentInstanceId { get; }
            public FireOperationId SourceFireOperationId { get; }
            public FireOperationId EmissionFireOperationId { get { return Command.FireOperationId; } }
            public string EffectiveWeaponFingerprint { get; }
            public WeaponFiringEmissionKind Kind { get; }
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
                    || WeaponDefinitionId == null
                    || EquipmentInstanceId == null
                    || SourceFireOperationId == null
                    || string.IsNullOrWhiteSpace(EffectiveWeaponFingerprint)
                    || string.IsNullOrWhiteSpace(Fingerprint)
                    || CadenceOrdinal < 0L
                    || ShotSequence < 0L
                    || TicksUntilNextEmission < 0L
                    || EmissionOrdinal < 0
                    || TriggerGroupOrdinal < 0
                    || BurstShotOrdinal < 0
                    || PulseOrdinal < 0
                    || !Enum.IsDefined(typeof(WeaponFiringEmissionKind), Kind)
                    || !EquipmentInstanceId.Equals(Command.EquipmentInstanceId)
                    || !DeriveEmissionOperationId(
                        SourceFireOperationId,
                        EffectiveWeaponFingerprint,
                        ShotSequence,
                        EmissionOrdinal).Equals(Command.FireOperationId)
                    || !string.Equals(
                        Command.Fingerprint,
                        WeaponExecutionFingerprint.Compute(Command.CanonicalText),
                        StringComparison.Ordinal))
                {
                    return false;
                }

                return string.Equals(
                    Fingerprint,
                    WeaponExecutionFingerprint.Compute(BuildCanonicalText()),
                    StringComparison.Ordinal);
            }

            public bool HasValidFingerprint(EffectiveWeapon weapon)
            {
                if (weapon == null || !HasValidFingerprint())
                {
                    return false;
                }

                WeaponFiringEmissionKind expectedKind = weapon.FireSettings.IsContinuous
                    ? WeaponFiringEmissionKind.ContinuousDamageTick
                    : WeaponFiringEmissionKind.ProjectileShot;
                return weapon.DefinitionId.Equals(WeaponDefinitionId)
                    && weapon.EquipmentInstanceId.Equals(EquipmentInstanceId)
                    && Kind == expectedKind
                    && string.Equals(
                        EffectiveWeaponFingerprint,
                        EffectiveWeaponFiringFingerprint.Compute(weapon),
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
                        "weapon_definition_id=" + WeaponDefinitionId,
                        "equipment_instance_id=" + EquipmentInstanceId,
                        "source_fire_operation_id=" + SourceFireOperationId,
                        "emission_fire_operation_id=" + Command.FireOperationId,
                        "effective_weapon_fingerprint=" + EffectiveWeaponFingerprint,
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
                WeaponFireCommand sourceCommand,
                RunParticipantId participantId,
                WeaponDefinitionId weaponDefinitionId,
                EquipmentInstanceId equipmentInstanceId,
                string effectiveWeaponFingerprint,
                WeaponTriggerSignal triggerSignal,
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
                        "Accepted weapon schedules can only be created by WeaponFiringScheduler.");
                }
                if (acceptedEmissions == null || acceptedEmissions.Count < 1)
                {
                    throw new ArgumentException(
                        "An accepted firing schedule requires at least one emission.",
                        nameof(acceptedEmissions));
                }
                if (!Enum.IsDefined(typeof(WeaponTriggerSignal), triggerSignal))
                {
                    throw new ArgumentOutOfRangeException(nameof(triggerSignal));
                }

                SourceCommand = sourceCommand
                    ?? throw new ArgumentNullException(nameof(sourceCommand));
                ParticipantId = participantId ?? throw new ArgumentNullException(nameof(participantId));
                WeaponDefinitionId = weaponDefinitionId
                    ?? throw new ArgumentNullException(nameof(weaponDefinitionId));
                EquipmentInstanceId = equipmentInstanceId
                    ?? throw new ArgumentNullException(nameof(equipmentInstanceId));
                EffectiveWeaponFingerprint = effectiveWeaponFingerprint
                    ?? throw new ArgumentNullException(nameof(effectiveWeaponFingerprint));
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
                Fingerprint = WeaponExecutionFingerprint.Compute(CanonicalText);
            }

            public WeaponFireCommand SourceCommand { get; }
            public WeaponActorInstanceId ActorId { get { return SourceCommand.ActorId; } }
            public RunParticipantId ParticipantId { get; }
            public WeaponDefinitionId WeaponDefinitionId { get; }
            public EquipmentInstanceId EquipmentInstanceId { get; }
            public FireOperationId SourceFireOperationId
            {
                get { return SourceCommand.FireOperationId; }
            }
            public LifecycleGeneration LifecycleGeneration
            {
                get { return SourceCommand.LifecycleGeneration; }
            }
            public string EffectiveWeaponFingerprint { get; }
            public WeaponTriggerSignal TriggerSignal { get; }
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
                    || WeaponDefinitionId == null
                    || EquipmentInstanceId == null
                    || SourceFireOperationId == null
                    || LifecycleGeneration == null
                    || string.IsNullOrWhiteSpace(EffectiveWeaponFingerprint)
                    || string.IsNullOrWhiteSpace(RequestFingerprint)
                    || string.IsNullOrWhiteSpace(Fingerprint)
                    || !Enum.IsDefined(typeof(WeaponTriggerSignal), TriggerSignal)
                    || CadenceOriginTick < 0L
                    || FirstCadenceOrdinal < 0L
                    || NextCadenceOrdinal <= FirstCadenceOrdinal
                    || NextCadenceTick < CadenceOriginTick
                    || emissions.Count < 1
                    || FirstScheduledTick > SourceCommand.SimulationTick
                    || !EquipmentInstanceId.Equals(SourceCommand.EquipmentInstanceId)
                    || !string.Equals(
                        SourceCommand.Fingerprint,
                        WeaponExecutionFingerprint.Compute(SourceCommand.CanonicalText),
                        StringComparison.Ordinal)
                    || !string.Equals(
                        RequestFingerprint,
                        ComputeRequestFingerprint(
                            SourceCommand.Fingerprint,
                            ParticipantId,
                            TriggerSignal,
                            EffectiveWeaponFingerprint),
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
                        || !emission.WeaponDefinitionId.Equals(WeaponDefinitionId)
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
                        WeaponExecutionFingerprint.Compute(BuildCanonicalText()),
                        StringComparison.Ordinal);
            }

            public bool HasValidFingerprint(EffectiveWeapon weapon)
            {
                if (weapon == null
                    || !HasValidFingerprint()
                    || !weapon.DefinitionId.Equals(WeaponDefinitionId)
                    || !weapon.EquipmentInstanceId.Equals(EquipmentInstanceId)
                    || !string.Equals(
                        EffectiveWeaponFingerprint,
                        EffectiveWeaponFiringFingerprint.Compute(weapon),
                        StringComparison.Ordinal))
                {
                    return false;
                }

                for (int index = 0; index < emissions.Count; index++)
                {
                    if (!emissions[index].HasValidFingerprint(weapon))
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
                builder.Append("weapon_definition_id=").Append(WeaponDefinitionId).Append('\n');
                builder.Append("equipment_instance_id=").Append(EquipmentInstanceId).Append('\n');
                builder.Append("source_fire_operation_id=")
                    .Append(SourceFireOperationId)
                    .Append('\n');
                builder.Append("lifecycle_generation=").Append(LifecycleGeneration).Append('\n');
                builder.Append("effective_weapon_fingerprint=")
                    .Append(EffectiveWeaponFingerprint)
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
