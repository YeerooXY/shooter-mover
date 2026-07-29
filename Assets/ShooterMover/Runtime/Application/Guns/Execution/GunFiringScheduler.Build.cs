using System;
using System.Collections.Generic;
using ShooterMover.Domain.Guns;
using ShooterMover.Domain.Guns.Execution;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed partial class GunFiringScheduler
    {
        private bool TryBuildPressedSchedule(
            GunFiringRequest request,
            string effectiveFingerprint,
            string requestFingerprint,
            long firstShotSequence,
            out AcceptedSchedule schedule,
            out long nextCadenceOrdinal,
            out long nextCadenceTick,
            out long nextShotSequence,
            out GunFiringScheduleStatus status,
            out string code)
        {
            schedule = null;
            nextCadenceOrdinal = 0L;
            nextCadenceTick = -1L;
            nextShotSequence = firstShotSequence;
            status = GunFiringScheduleStatus.UnsupportedConfiguration;
            code = string.Empty;

            if (!TryValidateSchedulingConfiguration(request.Gun, out code))
            {
                return false;
            }

            SchedulePlan plan = new SchedulePlan(
                request.Command.SimulationTick,
                0L,
                firstShotSequence);
            if (!TryAddCadenceCycle(plan, request.Gun, 0L, out status, out code)
                || !TryAdd(0L, 1L, out nextCadenceOrdinal)
                || !TryComputeCadenceTick(
                    request.Gun,
                    plan.CadenceOriginTick,
                    nextCadenceOrdinal,
                    out nextCadenceTick))
            {
                if (string.IsNullOrWhiteSpace(code))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-next-cadence-tick-invalid";
                }
                return false;
            }

            plan.Complete(nextCadenceOrdinal, nextCadenceTick);
            return TryMaterializeSchedule(
                request,
                effectiveFingerprint,
                requestFingerprint,
                plan,
                out schedule,
                out nextShotSequence,
                out status,
                out code);
        }

        private bool TryBuildHeldCatchUpSchedule(
            GunFiringRequest request,
            GunFiringTrackState track,
            string effectiveFingerprint,
            string requestFingerprint,
            out AcceptedSchedule schedule,
            out long nextCadenceOrdinal,
            out long nextCadenceTick,
            out long nextShotSequence,
            out bool noEmissionDue,
            out GunFiringScheduleStatus status,
            out string code)
        {
            schedule = null;
            nextCadenceOrdinal = track.NextCadenceOrdinal;
            nextCadenceTick = track.NextCadenceTick;
            nextShotSequence = track.NextGlobalShotSequence;
            noEmissionDue = false;
            status = GunFiringScheduleStatus.UnsupportedConfiguration;
            code = string.Empty;

            if (!TryValidateSchedulingConfiguration(request.Gun, out code))
            {
                return false;
            }

            SchedulePlan plan = new SchedulePlan(
                track.CadenceOriginTick,
                track.NextCadenceOrdinal,
                track.NextGlobalShotSequence);
            long cadenceOrdinal = track.NextCadenceOrdinal;
            while (true)
            {
                long dueTick;
                if (!TryComputeCadenceTick(
                    request.Gun,
                    track.CadenceOriginTick,
                    cadenceOrdinal,
                    out dueTick))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-catch-up-due-tick-invalid";
                    return false;
                }
                if (dueTick > request.Command.SimulationTick)
                {
                    nextCadenceTick = dueTick;
                    break;
                }

                if (!TryAddCadenceCycle(
                    plan,
                    request.Gun,
                    cadenceOrdinal,
                    out status,
                    out code))
                {
                    return false;
                }
                if (!TryAdd(cadenceOrdinal, 1L, out cadenceOrdinal))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-cadence-ordinal-overflow";
                    return false;
                }
            }

            if (plan.Emissions.Count == 0)
            {
                noEmissionDue = true;
                nextCadenceOrdinal = track.NextCadenceOrdinal;
                nextShotSequence = track.NextGlobalShotSequence;
                return true;
            }

            nextCadenceOrdinal = cadenceOrdinal;
            plan.Complete(nextCadenceOrdinal, nextCadenceTick);
            return TryMaterializeSchedule(
                request,
                effectiveFingerprint,
                requestFingerprint,
                plan,
                out schedule,
                out nextShotSequence,
                out status,
                out code);
        }

        private bool TryAddCadenceCycle(
            SchedulePlan plan,
            EffectiveGun gun,
            long cadenceOrdinal,
            out GunFiringScheduleStatus status,
            out string code)
        {
            status = GunFiringScheduleStatus.UnsupportedConfiguration;
            code = string.Empty;
            if (gun.FireSettings.IsContinuous)
            {
                long dueTick;
                if (!clock.TryRateDueTick(
                    plan.CadenceOriginTick,
                    cadenceOrdinal,
                    gun.FireSettings.DamageTicksPerSecond,
                    out dueTick))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-continuous-due-tick-invalid";
                    return false;
                }

                return TryAddPlan(
                    plan,
                    new EmissionPlan(dueTick, cadenceOrdinal, 0, 0, 0),
                    out status,
                    out code);
            }

            if (gun.FireSettings.Mode == GunFireMode.Burst)
            {
                return TryAddBurstCycle(
                    plan,
                    gun,
                    cadenceOrdinal,
                    out status,
                    out code);
            }

            return TryAddProjectileCycle(
                plan,
                gun,
                cadenceOrdinal,
                out status,
                out code);
        }

        private bool TryAddProjectileCycle(
            SchedulePlan plan,
            EffectiveGun gun,
            long cadenceOrdinal,
            out GunFiringScheduleStatus status,
            out string code)
        {
            status = GunFiringScheduleStatus.UnsupportedConfiguration;
            code = string.Empty;

            long cycleBaseOrdinal;
            if (!TryMultiply(
                cadenceOrdinal,
                gun.FireSettings.ShotsPerTrigger,
                out cycleBaseOrdinal))
            {
                status = GunFiringScheduleStatus.NumericalFailure;
                code = "gun-firing-trigger-group-ordinal-overflow";
                return false;
            }

            int pulseCount = gun.ShotPattern.Kind == GunShotPatternKind.PulseSpread
                ? gun.ShotPattern.PulsesPerShot
                : 1;
            for (int group = 0;
                group < gun.FireSettings.ShotsPerTrigger;
                group++)
            {
                long rateOrdinal;
                if (!TryAdd(cycleBaseOrdinal, group, out rateOrdinal))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-trigger-group-ordinal-overflow";
                    return false;
                }

                long groupTick;
                if (!clock.TryRateDueTick(
                    plan.CadenceOriginTick,
                    rateOrdinal,
                    gun.FireSettings.ShotsPerSecond,
                    out groupTick))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-projectile-due-tick-invalid";
                    return false;
                }

                long groupOffset = groupTick - plan.CadenceOriginTick;
                for (int pulse = 0; pulse < pulseCount; pulse++)
                {
                    long minimumOffset;
                    if (!TryAdd(groupOffset, pulse, out minimumOffset))
                    {
                        status = GunFiringScheduleStatus.NumericalFailure;
                        code = "gun-firing-pulse-minimum-offset-overflow";
                        return false;
                    }

                    long pulseTick;
                    if (!clock.TryRatePlusDurationDueTick(
                        plan.CadenceOriginTick,
                        rateOrdinal,
                        gun.FireSettings.ShotsPerSecond,
                        pulse,
                        gun.ShotPattern.IntervalBetweenPulsesSeconds,
                        minimumOffset,
                        out pulseTick))
                    {
                        status = GunFiringScheduleStatus.NumericalFailure;
                        code = "gun-firing-pulse-due-tick-invalid";
                        return false;
                    }

                    if (!TryAddPlan(
                        plan,
                        new EmissionPlan(
                            pulseTick,
                            cadenceOrdinal,
                            group,
                            0,
                            pulse),
                        out status,
                        out code))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private bool TryComputeCadenceTick(
            EffectiveGun gun,
            long cadenceOriginTick,
            long cadenceOrdinal,
            out long dueTick)
        {
            dueTick = 0L;
            if (cadenceOrdinal < 0L)
            {
                return false;
            }

            if (gun.FireSettings.IsContinuous)
            {
                return clock.TryRateDueTick(
                    cadenceOriginTick,
                    cadenceOrdinal,
                    gun.FireSettings.DamageTicksPerSecond,
                    out dueTick);
            }

            if (gun.FireSettings.Mode == GunFireMode.Burst)
            {
                return TryComputeBurstCadenceTick(
                    gun,
                    cadenceOriginTick,
                    cadenceOrdinal,
                    out dueTick);
            }

            long rateOrdinal;
            return TryMultiply(
                    cadenceOrdinal,
                    gun.FireSettings.ShotsPerTrigger,
                    out rateOrdinal)
                && clock.TryRateDueTick(
                    cadenceOriginTick,
                    rateOrdinal,
                    gun.FireSettings.ShotsPerSecond,
                    out dueTick);
        }

        private bool TryMaterializeSchedule(
            GunFiringRequest request,
            string effectiveFingerprint,
            string requestFingerprint,
            SchedulePlan plan,
            out AcceptedSchedule schedule,
            out long nextShotSequence,
            out GunFiringScheduleStatus status,
            out string code)
        {
            schedule = null;
            nextShotSequence = plan.FirstShotSequence;
            status = GunFiringScheduleStatus.NumericalFailure;
            code = string.Empty;

            plan.Emissions.Sort(ComparePlans);
            if (plan.Emissions.Count < 1
                || plan.Emissions.Count > MaximumEmissionsPerSchedule
                || !TryAdd(
                    plan.FirstShotSequence,
                    plan.Emissions.Count,
                    out nextShotSequence))
            {
                code = plan.Emissions.Count > MaximumEmissionsPerSchedule
                    ? "gun-firing-schedule-capacity-exceeded"
                    : "gun-firing-shot-sequence-overflow";
                status = plan.Emissions.Count > MaximumEmissionsPerSchedule
                    ? GunFiringScheduleStatus.ScheduleCapacityExceeded
                    : GunFiringScheduleStatus.NumericalFailure;
                return false;
            }

            List<AcceptedEmission> emissions =
                new List<AcceptedEmission>(plan.Emissions.Count);
            GunFiringEmissionKind kind = request.Gun.FireSettings.IsContinuous
                ? GunFiringEmissionKind.ContinuousDamageTick
                : GunFiringEmissionKind.ProjectileShot;
            for (int index = 0; index < plan.Emissions.Count; index++)
            {
                EmissionPlan planned = plan.Emissions[index];
                long sequence;
                if (!TryAdd(plan.FirstShotSequence, index, out sequence))
                {
                    code = "gun-firing-shot-sequence-overflow";
                    return false;
                }

                long nextTick = index + 1 < plan.Emissions.Count
                    ? plan.Emissions[index + 1].ScheduledTick
                    : plan.NextCadenceTick;
                long delay = Math.Max(0L, nextTick - planned.ScheduledTick);
                FireOperationId emissionOperationId = DeriveEmissionOperationId(
                    request.Command.FireOperationId,
                    effectiveFingerprint,
                    sequence,
                    index);
                GunFireCommand emissionCommand = new GunFireCommand(
                    request.Command.ActorId,
                    request.Command.EquipmentInstanceId,
                    emissionOperationId,
                    request.Command.LifecycleGeneration,
                    planned.ScheduledTick,
                    request.Command.DeterministicSeed,
                    request.Command.Origin,
                    request.Command.AimDirection);
                emissions.Add(new AcceptedEmission(
                    AcceptanceAuthority,
                    emissionCommand,
                    request.ParticipantId,
                    request.Gun.DefinitionId,
                    request.Gun.EquipmentInstanceId,
                    request.Command.FireOperationId,
                    effectiveFingerprint,
                    kind,
                    planned.CadenceOrdinal,
                    sequence,
                    delay,
                    index,
                    planned.TriggerGroupOrdinal,
                    planned.BurstShotOrdinal,
                    planned.PulseOrdinal));
            }

            schedule = new AcceptedSchedule(
                AcceptanceAuthority,
                request.Command,
                request.ParticipantId,
                request.Gun.DefinitionId,
                request.Gun.EquipmentInstanceId,
                effectiveFingerprint,
                request.TriggerSignal,
                requestFingerprint,
                plan.CadenceOriginTick,
                plan.FirstCadenceOrdinal,
                plan.NextCadenceOrdinal,
                plan.NextCadenceTick,
                emissions);
            if (!ScheduleMatchesPlan(schedule, plan)
                || !schedule.HasValidFingerprint(request.Gun))
            {
                schedule = null;
                code = "gun-firing-schedule-plan-validation-failed";
                return false;
            }

            status = GunFiringScheduleStatus.Accepted;
            return true;
        }

        private static bool ScheduleMatchesPlan(
            AcceptedSchedule schedule,
            SchedulePlan plan)
        {
            if (schedule == null
                || schedule.CadenceOriginTick != plan.CadenceOriginTick
                || schedule.FirstCadenceOrdinal != plan.FirstCadenceOrdinal
                || schedule.NextCadenceOrdinal != plan.NextCadenceOrdinal
                || schedule.NextCadenceTick != plan.NextCadenceTick
                || schedule.EmissionCount != plan.Emissions.Count)
            {
                return false;
            }

            for (int index = 0; index < plan.Emissions.Count; index++)
            {
                EmissionPlan expected = plan.Emissions[index];
                AcceptedEmission actual = schedule.Emissions[index];
                if (actual.ScheduledTick != expected.ScheduledTick
                    || actual.CadenceOrdinal != expected.CadenceOrdinal
                    || actual.EmissionOrdinal != index
                    || actual.TriggerGroupOrdinal != expected.TriggerGroupOrdinal
                    || actual.BurstShotOrdinal != expected.BurstShotOrdinal
                    || actual.PulseOrdinal != expected.PulseOrdinal)
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryValidateSchedulingConfiguration(
            EffectiveGun gun,
            out string code)
        {
            if (gun == null
                || gun.FireSettings == null
                || gun.ShotPattern == null
                || !Enum.IsDefined(
                    typeof(GunFireMode),
                    gun.FireSettings.Mode)
                || !Enum.IsDefined(
                    typeof(GunShotPatternKind),
                    gun.ShotPattern.Kind))
            {
                code = "gun-firing-configuration-invalid";
                return false;
            }

            bool pulsePattern =
                gun.ShotPattern.Kind == GunShotPatternKind.PulseSpread;
            if (pulsePattern)
            {
                if (gun.ShotPattern.PulsesPerShot < 2
                    || gun.ShotPattern.IntervalBetweenPulsesSeconds <= 0d)
                {
                    code = "gun-firing-pulse-pattern-invalid";
                    return false;
                }
            }
            else if (gun.ShotPattern.PulsesPerShot != 1
                || gun.ShotPattern.IntervalBetweenPulsesSeconds != 0d)
            {
                code = "gun-firing-unexpected-pulse-timing";
                return false;
            }

            if (gun.FireSettings.IsContinuous)
            {
                if (gun.FireSettings.DamageTicksPerSecond <= 0d
                    || (gun.ShotPattern.Kind != GunShotPatternKind.Beam
                        && gun.ShotPattern.Kind != GunShotPatternKind.Spray))
                {
                    code = "gun-firing-continuous-configuration-invalid";
                    return false;
                }
            }
            else if (gun.FireSettings.ShotsPerSecond <= 0d
                || gun.FireSettings.ShotsPerTrigger < 1
                || gun.ShotPattern.Kind == GunShotPatternKind.Beam
                || gun.ShotPattern.Kind == GunShotPatternKind.Spray)
            {
                code = "gun-firing-projectile-configuration-invalid";
                return false;
            }

            code = string.Empty;
            return true;
        }

        private static bool TryAddPlan(
            SchedulePlan plan,
            EmissionPlan emission,
            out GunFiringScheduleStatus status,
            out string code)
        {
            if (plan.Emissions.Count >= MaximumEmissionsPerSchedule)
            {
                status = GunFiringScheduleStatus.ScheduleCapacityExceeded;
                code = "gun-firing-schedule-capacity-exceeded";
                return false;
            }

            plan.Emissions.Add(emission);
            status = GunFiringScheduleStatus.Accepted;
            code = string.Empty;
            return true;
        }

        private static int ComparePlans(EmissionPlan left, EmissionPlan right)
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

        private sealed class SchedulePlan
        {
            public SchedulePlan(
                long cadenceOriginTick,
                long firstCadenceOrdinal,
                long firstShotSequence)
            {
                CadenceOriginTick = cadenceOriginTick;
                FirstCadenceOrdinal = firstCadenceOrdinal;
                FirstShotSequence = firstShotSequence;
                Emissions = new List<EmissionPlan>();
                NextCadenceOrdinal = firstCadenceOrdinal;
                NextCadenceTick = cadenceOriginTick;
            }

            public long CadenceOriginTick { get; }
            public long FirstCadenceOrdinal { get; }
            public long FirstShotSequence { get; }
            public List<EmissionPlan> Emissions { get; }
            public long NextCadenceOrdinal { get; private set; }
            public long NextCadenceTick { get; private set; }

            public void Complete(long nextCadenceOrdinal, long nextCadenceTick)
            {
                NextCadenceOrdinal = nextCadenceOrdinal;
                NextCadenceTick = nextCadenceTick;
            }
        }

        private struct EmissionPlan
        {
            public EmissionPlan(
                long scheduledTick,
                long cadenceOrdinal,
                int triggerGroupOrdinal,
                int burstShotOrdinal,
                int pulseOrdinal)
            {
                ScheduledTick = scheduledTick;
                CadenceOrdinal = cadenceOrdinal;
                TriggerGroupOrdinal = triggerGroupOrdinal;
                BurstShotOrdinal = burstShotOrdinal;
                PulseOrdinal = pulseOrdinal;
            }

            public long ScheduledTick { get; }
            public long CadenceOrdinal { get; }
            public int TriggerGroupOrdinal { get; }
            public int BurstShotOrdinal { get; }
            public int PulseOrdinal { get; }
        }
    }
}
