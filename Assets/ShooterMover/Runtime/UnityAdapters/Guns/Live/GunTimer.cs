using System;
using ShooterMover.Domain.Guns;

namespace ShooterMover.UnityAdapters.Guns.Live
{
    /// <summary>
    /// Small live-only shot timer. It keeps no replay history, receipts, hashes, or saved commands.
    /// At most one shot is returned per game tick so a slow frame cannot create a catch-up storm.
    /// </summary>
    public sealed class GunTimer
    {
        private const double Epsilon = 0.000000001d;

        public double NextShot { get; private set; }
        public int BurstLeft { get; private set; }
        public double NextBurst { get; private set; }

        public void Reset(double now)
        {
            RequireTime(now);
            NextShot = now;
            BurstLeft = 0;
            NextBurst = now;
        }

        public int Step(
            FireSettings fire,
            bool held,
            bool pressed,
            double now)
        {
            if (fire == null)
            {
                throw new ArgumentNullException(nameof(fire));
            }
            RequireTime(now);

            switch (fire.Mode)
            {
                case GunFireMode.Automatic:
                    return StepAuto(fire, held, pressed, now);
                case GunFireMode.SemiAutomatic:
                    return StepSemi(fire, pressed, now);
                case GunFireMode.Burst:
                    return StepBurst(fire, pressed, now);
                default:
                    return 0;
            }
        }

        private int StepAuto(
            FireSettings fire,
            bool held,
            bool pressed,
            double now)
        {
            if (pressed && NextShot < now)
            {
                NextShot = now;
            }
            if (!held || now + Epsilon < NextShot)
            {
                return 0;
            }

            NextShot = now + ShotGap(fire);
            return 1;
        }

        private int StepSemi(
            FireSettings fire,
            bool pressed,
            double now)
        {
            if (!pressed || now + Epsilon < NextShot)
            {
                return 0;
            }

            NextShot = now + ShotGap(fire);
            return 1;
        }

        private int StepBurst(
            FireSettings fire,
            bool pressed,
            double now)
        {
            if (pressed
                && BurstLeft == 0
                && now + Epsilon >= NextShot)
            {
                BurstLeft = fire.ShotsPerBurst;
                NextBurst = now;
            }

            if (BurstLeft <= 0 || now + Epsilon < NextBurst)
            {
                return 0;
            }

            BurstLeft--;
            if (BurstLeft > 0)
            {
                NextBurst = now + fire.IntervalBetweenBurstShotsSeconds;
            }
            else
            {
                NextShot = now + fire.IntervalAfterBurstSeconds;
            }
            return 1;
        }

        private static double ShotGap(FireSettings fire)
        {
            if (fire.ShotsPerSecond <= 0d
                || double.IsNaN(fire.ShotsPerSecond)
                || double.IsInfinity(fire.ShotsPerSecond))
            {
                throw new InvalidOperationException(
                    "gun-timer-fire-rate-invalid");
            }
            return 1d / fire.ShotsPerSecond;
        }

        private static void RequireTime(double now)
        {
            if (now < 0d || double.IsNaN(now) || double.IsInfinity(now))
            {
                throw new ArgumentOutOfRangeException(nameof(now));
            }
        }
    }
}
