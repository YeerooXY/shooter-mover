using System;

namespace ShooterMover.Application.Weapons.Execution
{
    public sealed partial class WeaponFiringClock
    {
        internal bool TryRateDueTick(
            long originTick,
            long cadenceOrdinal,
            double eventsPerSecond,
            out long dueTick)
        {
            dueTick = 0L;
            decimal rate;
            if (originTick < 0L
                || cadenceOrdinal < 0L
                || !TryPositiveDecimal(eventsPerSecond, out rate))
            {
                return false;
            }

            try
            {
                decimal authoredOffsetTicks =
                    ((decimal)cadenceOrdinal * TicksPerSecond) / rate;
                return TryCeilingTick(
                    originTick,
                    authoredOffsetTicks,
                    cadenceOrdinal,
                    out dueTick);
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        internal bool TryRatePlusDurationDueTick(
            long originTick,
            long rateOrdinal,
            double eventsPerSecond,
            long durationOrdinal,
            double durationSeconds,
            long minimumTickOffset,
            out long dueTick)
        {
            return TryRateAndDurationSumDueTick(
                originTick,
                rateOrdinal,
                eventsPerSecond,
                durationOrdinal,
                durationSeconds,
                0L,
                0d,
                minimumTickOffset,
                out dueTick);
        }

        internal bool TryRateAndDurationSumDueTick(
            long originTick,
            long rateOrdinal,
            double eventsPerSecond,
            long firstDurationOrdinal,
            double firstDurationSeconds,
            long secondDurationOrdinal,
            double secondDurationSeconds,
            long minimumTickOffset,
            out long dueTick)
        {
            dueTick = 0L;
            decimal rate;
            decimal firstDuration;
            decimal secondDuration;
            if (originTick < 0L
                || rateOrdinal < 0L
                || firstDurationOrdinal < 0L
                || secondDurationOrdinal < 0L
                || minimumTickOffset < 0L
                || !TryPositiveDecimal(eventsPerSecond, out rate)
                || !TryNonNegativeDecimal(
                    firstDurationSeconds,
                    out firstDuration)
                || !TryNonNegativeDecimal(
                    secondDurationSeconds,
                    out secondDuration))
            {
                return false;
            }

            try
            {
                decimal authoredSeconds =
                    ((decimal)rateOrdinal / rate)
                    + ((decimal)firstDurationOrdinal * firstDuration)
                    + ((decimal)secondDurationOrdinal * secondDuration);
                decimal authoredOffsetTicks = authoredSeconds * TicksPerSecond;
                return TryCeilingTick(
                    originTick,
                    authoredOffsetTicks,
                    minimumTickOffset,
                    out dueTick);
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        internal bool TryDurationDueTick(
            long originTick,
            long intervalOrdinal,
            double intervalSeconds,
            long minimumTickOffset,
            out long dueTick)
        {
            return TryDurationSumDueTick(
                originTick,
                intervalOrdinal,
                intervalSeconds,
                0L,
                0d,
                0L,
                0d,
                minimumTickOffset,
                out dueTick);
        }

        internal bool TryDurationSumDueTick(
            long originTick,
            long firstOrdinal,
            double firstSeconds,
            long secondOrdinal,
            double secondSeconds,
            long thirdOrdinal,
            double thirdSeconds,
            long minimumTickOffset,
            out long dueTick)
        {
            dueTick = 0L;
            decimal first;
            decimal second;
            decimal third;
            if (originTick < 0L
                || firstOrdinal < 0L
                || secondOrdinal < 0L
                || thirdOrdinal < 0L
                || minimumTickOffset < 0L
                || !TryNonNegativeDecimal(firstSeconds, out first)
                || !TryNonNegativeDecimal(secondSeconds, out second)
                || !TryNonNegativeDecimal(thirdSeconds, out third))
            {
                return false;
            }

            try
            {
                decimal authoredSeconds =
                    ((decimal)firstOrdinal * first)
                    + ((decimal)secondOrdinal * second)
                    + ((decimal)thirdOrdinal * third);
                decimal authoredOffsetTicks = authoredSeconds * TicksPerSecond;
                return TryCeilingTick(
                    originTick,
                    authoredOffsetTicks,
                    minimumTickOffset,
                    out dueTick);
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool TryPositiveDecimal(double value, out decimal converted)
        {
            if (!TryNonNegativeDecimal(value, out converted) || converted <= 0m)
            {
                converted = 0m;
                return false;
            }

            return true;
        }

        private static bool TryNonNegativeDecimal(double value, out decimal converted)
        {
            converted = 0m;
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0d)
            {
                return false;
            }

            try
            {
                converted = Convert.ToDecimal(value);
                return converted >= 0m;
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        private static bool TryCeilingTick(
            long originTick,
            decimal authoredOffsetTicks,
            long minimumTickOffset,
            out long dueTick)
        {
            dueTick = 0L;
            if (authoredOffsetTicks < 0m || minimumTickOffset < 0L)
            {
                return false;
            }

            try
            {
                decimal ceiling = decimal.Ceiling(authoredOffsetTicks);
                decimal bounded = Math.Max(ceiling, (decimal)minimumTickOffset);
                if (bounded > long.MaxValue)
                {
                    return false;
                }

                dueTick = checked(originTick + (long)bounded);
                return true;
            }
            catch (OverflowException)
            {
                return false;
            }
        }
    }
}
