using System;
using ShooterMover.Domain.Weapons;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponFiringScheduler
    {
        private bool TryAddBurstCycle(
            SchedulePlan plan,
            EffectiveWeapon weapon,
            long cadenceOrdinal,
            out WeaponFiringScheduleStatus status,
            out string code)
        {
            status = WeaponFiringScheduleStatus.UnsupportedConfiguration;
            code = string.Empty;

            int pulseCount = weapon.ShotPattern.Kind == WeaponShotPatternKind.PulseSpread
                ? weapon.ShotPattern.PulsesPerShot
                : 1;
            long groupsBeforeCycle;
            long minimumIntervalsPerGroup;
            if (!TryMultiply(
                    cadenceOrdinal,
                    weapon.FireSettings.ShotsPerTrigger,
                    out groupsBeforeCycle)
                || !TryAdd(
                    weapon.FireSettings.ShotsPerBurst,
                    pulseCount - 1L,
                    out minimumIntervalsPerGroup))
            {
                status = WeaponFiringScheduleStatus.NumericalFailure;
                code = "weapon-firing-burst-group-ordinal-overflow";
                return false;
            }

            for (int group = 0;
                group < weapon.FireSettings.ShotsPerTrigger;
                group++)
            {
                long absoluteGroup;
                long burstIntervalsBeforeGroup;
                long pulseTailIntervalsBeforeGroup;
                long minimumIntervalsBeforeGroup;
                if (!TryAdd(groupsBeforeCycle, group, out absoluteGroup)
                    || !TryMultiply(
                        absoluteGroup,
                        weapon.FireSettings.ShotsPerBurst - 1L,
                        out burstIntervalsBeforeGroup)
                    || !TryMultiply(
                        absoluteGroup,
                        pulseCount - 1L,
                        out pulseTailIntervalsBeforeGroup)
                    || !TryMultiply(
                        absoluteGroup,
                        minimumIntervalsPerGroup,
                        out minimumIntervalsBeforeGroup))
                {
                    status = WeaponFiringScheduleStatus.NumericalFailure;
                    code = "weapon-firing-burst-group-ordinal-overflow";
                    return false;
                }

                long groupStartTick;
                if (!TryComputeBurstPhaseTick(
                    weapon,
                    plan.CadenceOriginTick,
                    absoluteGroup,
                    burstIntervalsBeforeGroup,
                    pulseTailIntervalsBeforeGroup,
                    minimumIntervalsBeforeGroup,
                    out groupStartTick))
                {
                    status = WeaponFiringScheduleStatus.NumericalFailure;
                    code = "weapon-firing-burst-group-tick-invalid";
                    return false;
                }
                long groupStartOffset = groupStartTick - plan.CadenceOriginTick;

                for (int burstShot = 0;
                    burstShot < weapon.FireSettings.ShotsPerBurst;
                    burstShot++)
                {
                    long burstOrdinal;
                    long shotMinimumOffset;
                    if (!TryAdd(
                        burstIntervalsBeforeGroup,
                        burstShot,
                        out burstOrdinal)
                        || !TryAdd(
                            groupStartOffset,
                            burstShot,
                            out shotMinimumOffset))
                    {
                        status = WeaponFiringScheduleStatus.NumericalFailure;
                        code = "weapon-firing-burst-shot-ordinal-overflow";
                        return false;
                    }

                    long shotTick;
                    if (!TryComputeBurstPhaseTick(
                        weapon,
                        plan.CadenceOriginTick,
                        absoluteGroup,
                        burstOrdinal,
                        pulseTailIntervalsBeforeGroup,
                        shotMinimumOffset,
                        out shotTick))
                    {
                        status = WeaponFiringScheduleStatus.NumericalFailure;
                        code = "weapon-firing-burst-shot-tick-invalid";
                        return false;
                    }
                    long shotOffset = shotTick - plan.CadenceOriginTick;

                    for (int pulse = 0; pulse < pulseCount; pulse++)
                    {
                        long pulseOrdinal;
                        long minimumOffset;
                        if (!TryAdd(
                            pulseTailIntervalsBeforeGroup,
                            pulse,
                            out pulseOrdinal)
                            || !TryAdd(shotOffset, pulse, out minimumOffset))
                        {
                            status = WeaponFiringScheduleStatus.NumericalFailure;
                            code = "weapon-firing-burst-pulse-ordinal-overflow";
                            return false;
                        }

                        long pulseTick;
                        if (!TryComputeBurstPhaseTick(
                            weapon,
                            plan.CadenceOriginTick,
                            absoluteGroup,
                            burstOrdinal,
                            pulseOrdinal,
                            minimumOffset,
                            out pulseTick))
                        {
                            status = WeaponFiringScheduleStatus.NumericalFailure;
                            code = "weapon-firing-burst-pulse-tick-invalid";
                            return false;
                        }

                        if (!TryAddPlan(
                            plan,
                            new EmissionPlan(
                                pulseTick,
                                cadenceOrdinal,
                                group,
                                burstShot,
                                pulse),
                            out status,
                            out code))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        private bool TryComputeBurstCadenceTick(
            EffectiveWeapon weapon,
            long cadenceOriginTick,
            long cadenceOrdinal,
            out long dueTick)
        {
            dueTick = 0L;
            if (cadenceOrdinal < 0L || cadenceOriginTick < 0L)
            {
                return false;
            }
            if (cadenceOrdinal == 0L)
            {
                dueTick = cadenceOriginTick;
                return true;
            }

            int pulseCount = weapon.ShotPattern.Kind == WeaponShotPatternKind.PulseSpread
                ? weapon.ShotPattern.PulsesPerShot
                : 1;
            long completedGroups;
            long burstIntervals;
            long pulseTailIntervals;
            long minimumIntervalsPerGroup;
            long minimumIntervals;
            if (!TryMultiply(
                    cadenceOrdinal,
                    weapon.FireSettings.ShotsPerTrigger,
                    out completedGroups)
                || !TryMultiply(
                    completedGroups,
                    weapon.FireSettings.ShotsPerBurst - 1L,
                    out burstIntervals)
                || !TryMultiply(
                    completedGroups,
                    pulseCount - 1L,
                    out pulseTailIntervals)
                || !TryAdd(
                    weapon.FireSettings.ShotsPerBurst,
                    pulseCount - 1L,
                    out minimumIntervalsPerGroup)
                || !TryMultiply(
                    completedGroups,
                    minimumIntervalsPerGroup,
                    out minimumIntervals))
            {
                return false;
            }

            return TryComputeBurstPhaseTick(
                weapon,
                cadenceOriginTick,
                completedGroups,
                burstIntervals,
                pulseTailIntervals,
                minimumIntervals,
                out dueTick);
        }

        private bool TryComputeBurstPhaseTick(
            EffectiveWeapon weapon,
            long cadenceOriginTick,
            long completedRecoveryOrdinals,
            long burstIntervalOrdinal,
            long pulseIntervalOrdinal,
            long minimumTickOffset,
            out long dueTick)
        {
            dueTick = 0L;
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

            dueTick = Math.Max(rateRecoveryTick, authoredRecoveryTick);
            return true;
        }
    }
}
