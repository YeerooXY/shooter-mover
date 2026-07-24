using System;
using System.Collections.Generic;
using System.Globalization;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponFiringScheduler
    {
        private bool ScheduleMatchesAuthoredTiming(
            EffectiveWeapon weapon,
            AcceptedSchedule schedule)
        {
            if (weapon == null
                || schedule == null
                || schedule.NextCadenceOrdinal <= schedule.FirstCadenceOrdinal)
            {
                return false;
            }

            long cadenceCount;
            long emissionsPerCadence;
            long expectedEmissionCount;
            int pulseCount = weapon.ShotPattern.Kind == WeaponShotPatternKind.PulseSpread
                ? weapon.ShotPattern.PulsesPerShot
                : 1;
            if (!TryAdd(
                    schedule.NextCadenceOrdinal,
                    -schedule.FirstCadenceOrdinal,
                    out cadenceCount)
                || !TryComputeEmissionsPerCadence(
                    weapon,
                    pulseCount,
                    out emissionsPerCadence)
                || !TryMultiply(
                    cadenceCount,
                    emissionsPerCadence,
                    out expectedEmissionCount)
                || expectedEmissionCount != schedule.EmissionCount)
            {
                return false;
            }

            long expectedNextCadenceTick;
            if (!TryComputeCadenceTick(
                    weapon,
                    schedule.CadenceOriginTick,
                    schedule.NextCadenceOrdinal,
                    out expectedNextCadenceTick)
                || expectedNextCadenceTick != schedule.NextCadenceTick)
            {
                return false;
            }

            HashSet<string> ordinalCoordinates =
                new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < schedule.EmissionCount; index++)
            {
                AcceptedEmission emission = schedule.Emissions[index];
                long expectedSequence;
                long expectedTick;
                if (emission == null
                    || !ordinalCoordinates.Add(CoordinateKey(emission))
                    || !TryAdd(
                        schedule.FirstShotSequence,
                        index,
                        out expectedSequence)
                    || emission.ShotSequence != expectedSequence
                    || !TryComputeExpectedEmissionTick(
                        weapon,
                        schedule.CadenceOriginTick,
                        emission,
                        pulseCount,
                        out expectedTick)
                    || emission.ScheduledTick != expectedTick)
                {
                    return false;
                }
            }

            return true;
        }

        private static string CoordinateKey(AcceptedEmission emission)
        {
            return emission.CadenceOrdinal.ToString(CultureInfo.InvariantCulture)
                + "|" + emission.TriggerGroupOrdinal.ToString(CultureInfo.InvariantCulture)
                + "|" + emission.BurstShotOrdinal.ToString(CultureInfo.InvariantCulture)
                + "|" + emission.PulseOrdinal.ToString(CultureInfo.InvariantCulture);
        }

        private static bool TryComputeEmissionsPerCadence(
            EffectiveWeapon weapon,
            int pulseCount,
            out long emissionsPerCadence)
        {
            emissionsPerCadence = 0L;
            if (weapon.FireSettings.IsContinuous)
            {
                emissionsPerCadence = 1L;
                return true;
            }

            long shotGroups = weapon.FireSettings.ShotsPerTrigger;
            if (weapon.FireSettings.Mode == WeaponFireMode.Burst)
            {
                long burstShotGroups;
                if (!TryMultiply(
                    shotGroups,
                    weapon.FireSettings.ShotsPerBurst,
                    out burstShotGroups))
                {
                    return false;
                }
                shotGroups = burstShotGroups;
            }

            return TryMultiply(shotGroups, pulseCount, out emissionsPerCadence);
        }

        private bool TryComputeExpectedEmissionTick(
            EffectiveWeapon weapon,
            long cadenceOriginTick,
            AcceptedEmission emission,
            int pulseCount,
            out long expectedTick)
        {
            expectedTick = 0L;
            if (weapon.FireSettings.IsContinuous)
            {
                return emission.TriggerGroupOrdinal == 0
                    && emission.BurstShotOrdinal == 0
                    && emission.PulseOrdinal == 0
                    && clock.TryRateDueTick(
                        cadenceOriginTick,
                        emission.CadenceOrdinal,
                        weapon.FireSettings.DamageTicksPerSecond,
                        out expectedTick);
            }

            if (emission.TriggerGroupOrdinal < 0
                || emission.TriggerGroupOrdinal >= weapon.FireSettings.ShotsPerTrigger
                || emission.PulseOrdinal < 0
                || emission.PulseOrdinal >= pulseCount)
            {
                return false;
            }

            if (weapon.FireSettings.Mode == WeaponFireMode.Burst)
            {
                return TryComputeExpectedBurstEmissionTick(
                    weapon,
                    cadenceOriginTick,
                    emission,
                    pulseCount,
                    out expectedTick);
            }

            if (emission.BurstShotOrdinal != 0)
            {
                return false;
            }

            long cycleBaseOrdinal;
            long rateOrdinal;
            long groupTick;
            long minimumOffset;
            if (!TryMultiply(
                    emission.CadenceOrdinal,
                    weapon.FireSettings.ShotsPerTrigger,
                    out cycleBaseOrdinal)
                || !TryAdd(
                    cycleBaseOrdinal,
                    emission.TriggerGroupOrdinal,
                    out rateOrdinal)
                || !clock.TryRateDueTick(
                    cadenceOriginTick,
                    rateOrdinal,
                    weapon.FireSettings.ShotsPerSecond,
                    out groupTick)
                || !TryAdd(
                    groupTick - cadenceOriginTick,
                    emission.PulseOrdinal,
                    out minimumOffset))
            {
                return false;
            }

            return clock.TryRatePlusDurationDueTick(
                cadenceOriginTick,
                rateOrdinal,
                weapon.FireSettings.ShotsPerSecond,
                emission.PulseOrdinal,
                weapon.ShotPattern.IntervalBetweenPulsesSeconds,
                minimumOffset,
                out expectedTick);
        }

        private bool TryComputeExpectedBurstEmissionTick(
            EffectiveWeapon weapon,
            long cadenceOriginTick,
            AcceptedEmission emission,
            int pulseCount,
            out long expectedTick)
        {
            expectedTick = 0L;
            if (emission.BurstShotOrdinal < 0
                || emission.BurstShotOrdinal >= weapon.FireSettings.ShotsPerBurst)
            {
                return false;
            }

            long groupsBeforeCycle;
            long absoluteGroup;
            long burstIntervalsBeforeGroup;
            long pulseTailIntervalsBeforeGroup;
            long burstOrdinal;
            long pulseOrdinal;
            long minimumIntervalsPerGroup;
            long minimumIntervalsBeforeGroup;
            long minimumOffset;
            if (!TryMultiply(
                    emission.CadenceOrdinal,
                    weapon.FireSettings.ShotsPerTrigger,
                    out groupsBeforeCycle)
                || !TryAdd(
                    groupsBeforeCycle,
                    emission.TriggerGroupOrdinal,
                    out absoluteGroup)
                || !TryMultiply(
                    absoluteGroup,
                    weapon.FireSettings.ShotsPerBurst - 1L,
                    out burstIntervalsBeforeGroup)
                || !TryMultiply(
                    absoluteGroup,
                    pulseCount - 1L,
                    out pulseTailIntervalsBeforeGroup)
                || !TryAdd(
                    burstIntervalsBeforeGroup,
                    emission.BurstShotOrdinal,
                    out burstOrdinal)
                || !TryAdd(
                    pulseTailIntervalsBeforeGroup,
                    emission.PulseOrdinal,
                    out pulseOrdinal)
                || !TryAdd(
                    weapon.FireSettings.ShotsPerBurst,
                    pulseCount - 1L,
                    out minimumIntervalsPerGroup)
                || !TryMultiply(
                    absoluteGroup,
                    minimumIntervalsPerGroup,
                    out minimumIntervalsBeforeGroup)
                || !TryAdd(
                    minimumIntervalsBeforeGroup,
                    emission.BurstShotOrdinal,
                    out minimumOffset))
            {
                return false;
            }

            long pulseMinimumOffset;
            if (!TryAdd(
                minimumOffset,
                emission.PulseOrdinal,
                out pulseMinimumOffset))
            {
                return false;
            }

            return TryComputeExpectedBurstPhaseTick(
                weapon,
                cadenceOriginTick,
                absoluteGroup,
                burstOrdinal,
                pulseOrdinal,
                pulseMinimumOffset,
                out expectedTick);
        }

        private bool TryComputeExpectedBurstPhaseTick(
            EffectiveWeapon weapon,
            long cadenceOriginTick,
            long completedRecoveryOrdinals,
            long burstIntervalOrdinal,
            long pulseIntervalOrdinal,
            long minimumTickOffset,
            out long expectedTick)
        {
            expectedTick = 0L;
            long rateRecoveryTick;
            long authoredRecoveryTick;
            if (!clock.TryRateAndDurationSumDueTick(
                    cadenceOriginTick,
                    completedRecoveryOrdinals,
                    weapon.FireSettings.ShotsPerSecond,
                    burstIntervalOrdinal,
                    weapon.FireSettings.IntervalBetweenBurstShotsSeconds,
                    pulseIntervalOrdinal,
                    weapon.ShotPattern.IntervalBetweenPulsesSeconds,
                    minimumTickOffset,
                    out rateRecoveryTick)
                || !clock.TryDurationSumDueTick(
                    cadenceOriginTick,
                    burstIntervalOrdinal,
                    weapon.FireSettings.IntervalBetweenBurstShotsSeconds,
                    pulseIntervalOrdinal,
                    weapon.ShotPattern.IntervalBetweenPulsesSeconds,
                    completedRecoveryOrdinals,
                    weapon.FireSettings.IntervalAfterBurstSeconds,
                    minimumTickOffset,
                    out authoredRecoveryTick))
            {
                return false;
            }

            expectedTick = Math.Max(rateRecoveryTick, authoredRecoveryTick);
            return true;
        }
    }
}
