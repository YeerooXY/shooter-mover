using System.Linq;
using NUnit.Framework;
using ShooterMover.Application.Flow.Game;
using ShooterMover.Domain.Common;

namespace ShooterMover.Tests.EditMode.Flow.Hub
{
    public sealed class GunMountSpacingTests
    {
        [Test]
        public void StrikerActiveGunsStayCloseToPlayerCenter()
        {
            GunSlots layout = GunMountPolicy.ResolveLayout(
                StableId.Parse(GunMountPolicy.AggressiveLoadoutProfileId));

            double[] offsets = layout.ConfigurablePositions
                .Select(position => position.LateralOffset)
                .ToArray();

            Assert.That(offsets, Is.EqualTo(new[] { -0.28d, 0.28d }));
            Assert.That(offsets[1] - offsets[0], Is.EqualTo(0.56d));
        }

        [Test]
        public void FourGunLayoutUsesOneCompactOrderedBank()
        {
            GunSlots layout = GunMountPolicy.ResolveLayout(
                StableId.Parse(GunMountPolicy.DefensiveLoadoutProfileId));

            double[] offsets = layout.ConfigurablePositions
                .Select(position => position.LateralOffset)
                .ToArray();

            Assert.That(
                offsets,
                Is.EqualTo(new[] { -0.28d, -0.09d, 0.09d, 0.28d }));
            Assert.That(offsets.All(value => System.Math.Abs(value) <= 0.28d), Is.True);
        }
    }
}
