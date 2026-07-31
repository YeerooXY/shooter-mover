using NUnit.Framework;
using ShooterMover.Domain.Guns;
using ShooterMover.UnityAdapters.Guns.Live;

namespace ShooterMover.Tests.EditMode.Guns.Live
{
    public sealed class GunTimerTests
    {
        [Test]
        public void Automatic_HeldForThreeSeconds_FiresTwelveShots()
        {
            var timer = new GunTimer();
            timer.Reset(0d);
            FireSettings fire = FireSettings.Automatic(4d);
            int shots = 0;

            for (int tick = 0; tick < 150; tick++)
            {
                double now = tick / 50d;
                shots += timer.Step(
                    fire,
                    true,
                    tick == 0,
                    now);
            }

            Assert.That(shots, Is.EqualTo(12));
        }

        [Test]
        public void Automatic_SlowFrame_DoesNotCreateCatchUpStorm()
        {
            var timer = new GunTimer();
            timer.Reset(0d);
            FireSettings fire = FireSettings.Automatic(4d);

            Assert.That(timer.Step(fire, true, true, 0d), Is.EqualTo(1));
            Assert.That(timer.Step(fire, true, false, 2d), Is.EqualTo(1));
            Assert.That(timer.Step(fire, true, false, 2d), Is.EqualTo(0));
        }

        [Test]
        public void SemiAutomatic_HeldInput_DoesNotRepeat()
        {
            var timer = new GunTimer();
            timer.Reset(0d);
            FireSettings fire = FireSettings.SemiAutomatic(4d);

            Assert.That(timer.Step(fire, true, true, 0d), Is.EqualTo(1));
            Assert.That(timer.Step(fire, true, false, 1d), Is.EqualTo(0));
            Assert.That(timer.Step(fire, true, true, 1d), Is.EqualTo(1));
        }

        [Test]
        public void Burst_FiresOnlyItsAuthoredShots()
        {
            var timer = new GunTimer();
            timer.Reset(0d);
            FireSettings fire = FireSettings.Burst(
                1d,
                new GunBurstSettings(3, 0.1d));

            Assert.That(timer.Step(fire, true, true, 0d), Is.EqualTo(1));
            Assert.That(timer.Step(fire, true, false, 0.05d), Is.EqualTo(0));
            Assert.That(timer.Step(fire, true, false, 0.1d), Is.EqualTo(1));
            Assert.That(timer.Step(fire, true, false, 0.2d), Is.EqualTo(1));
            Assert.That(timer.Step(fire, true, false, 0.9d), Is.EqualTo(0));
        }
    }
}
