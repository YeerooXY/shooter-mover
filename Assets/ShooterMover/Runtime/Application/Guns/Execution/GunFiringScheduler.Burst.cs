using System;
using ShooterMover.Domain.Guns;

namespace ShooterMover.Application.Guns.Execution
{
    public sealed partial class GunFiringScheduler
    {
        private bool TryAddBurstCycle(
            SchedulePlan plan,
            EffectiveGun gun,
            long cadenceOrdinal,
            out GunFiringScheduleStatus status,
            out string code)
        {
            status = GunFiringScheduleStatus.UnsupportedConfiguration;
            code = string.Empty;

            int pulseCount = gun.ShotPattern.Kind == GunShotPatternKind.PulseSpread
                ? gun.ShotPattern.PulsesPerShot
                : 1;
            long groupsBeforeCycle;
            long minimumIntervalsPerGroup;
            if (!TryMultiply(
                    cadenceOrdinal,
                    gun.FireSettings.ShotsPerTrigger,
                    out groupsBeforeCycle)
                || !TryAdd(
                    gun.FireSettings.ShotsPerBurst,
                    pulseCount - 1L,
                    out minimumIntervalsPerGroup))
            {
                status = GunFiringScheduleStatus.NumericalFailure;
                code = "gun-firing-burst-group-ordinal-overflow";
                return false;
            }

            for (int group = 0;
                group < gun.FireSettings.ShotsPerTrigger;
                group++)
            {
                long absoluteGroup;
                long burstIntervalsBeforeGroup;
                long pulseTailIntervalsBeforeGroup;
                long minimumIntervalsBeforeGroup;
                if (!TryAdd(groupsBeforeCycle, group, out absoluteGroup)
                    || !TryMultiply(
                        absoluteGroup,
                        gun.FireSettings.ShotsPerBurst - 1L,
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
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-burst-group-ordinal-overflow";
                    return false;
                }

                long groupStartTick;
                if (!TryComputeBurstPhaseTick(
                    gun,
                    plan.CadenceOriginTick,
                    absoluteGroup,
                    burstIntervalsBeforeGroup,
                    pulseTailIntervalsBeforeGroup,
                    minimumIntervalsBeforeGroup,
                    out groupStartTick))
                {
                    status = GunFiringScheduleStatus.NumericalFailure;
                    code = "gun-firing-burst-group-tick-invalid";
                    return false;
                }
                long groupStartOffset = groupStartTick - plan.CadenceOriginTick;

                for (int burstShot = 0;
                    burstShot < gun.FireSettings.ShotsPerBurst;
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
                        status = GunFiringScheduleStatus.NumericalFailure;
                        code = "gun-firing-burst-shot-ordinal-overflow";
                        return false;
                    }

                    long shotTick;
                    if (!TryComputeBurstPhaseTick(
                        gun,
                        plan.CadenceOriginTick,
                        absoluteGroup,
                        burstOrdinal,
                        pulseTailIntervalsBeforeGroup,
                        shotMinimumOffset,
                        out shotTick))
                    {
                        status = GunFiringScheduleStatus.NumericalFailure;
                        code = "gun-firing-burst-shot-tick-invalid";
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
                            status = GunFiringScheduleStatus.NumericalFailure;
                            code = "gun-firing-burst-pulse-ordinal-overflow";
                            return false;
                        }

                        long pulseTick;
                        if (!TryComputeBurstPhaseTick(
                            gun,
                            plan.CadenceOriginTick,
                            absoluteGroup,
                            burstOrdinal,
                            pulseOrdinal,
                            minimumOffset,
                            out pulseTick))
                        {
                            status = GunFiringScheduleStatus.NumericalFailure;
                            code = "gun-firing-burst-pulse-tick-invalid";
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
            EffectiveGun gun,
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

            int pulseCount = gun.ShotPattern.Kind == GunShotPatternKind.PulseSpread
                ? gun.ShotPattern.PulsesPerShot
                : 1;
            long completedGroups;
            long burstIntervals;
            long pulseTailIntervals;
            long minimumIntervalsPerGroup;
            long minimumIntervals;
            if (!TryMultiply(
                    cadenceOrdinal,
                    gun.FireSettings.ShotsPerTrigger,
                    out completedGroups)
                || !TryMultiply(
                    completedGroups,
                    gun.FireSettings.ShotsPerBurst - 1L,
                    out burstIntervals)
                || !TryMultiply(
                    completedGroups,
                    pulseCount - 1L,
                    out pulseTailIntervals)
                || !TryAdd(
                    gun.FireSettings.ShotsPerBurst,
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
                gun,
                cadenceOriginTick,
                completedGroups,
                burstIntervals,
                pulseTailIntervals,
                minimumIntervals,
                out dueTick);
        }

        private bool TryComputeBurstPhaseTick(
            EffectiveGun gun,
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
                    gun.FireSettings.ShotsPerSecond,
                    burstIntervalOrdinal,
                    gun.FireSettings.IntervalBetweenBurstShotsSeconds,
                    pulseIntervalOrdinal,
                    gun.ShotPattern.IntervalBetweenPulsesSeconds,
                    minimumTickOffset,
                    out rateRecoveryTick)
                || !clock.TryDurationSumDueTick(
                    cadenceOriginTick,
                    burstIntervalOrdinal,
                    gun.FireSettings.IntervalBetweenBurstShotsSeconds,
                    pulseIntervalOrdinal,
                    gun.ShotPattern.IntervalBetweenPulsesSeconds,
                    completedRecoveryOrdinals,
                    gun.FireSettings.IntervalAfterBurstSeconds,
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
