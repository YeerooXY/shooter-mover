using NUnit.Framework;
using ShooterMover.Application.Flow.Production;

namespace ShooterMover.Tests.EditMode.Equipment.Upgrades
{
    public sealed class LegacyFirstWeaponHoldingsAdapterRetirementTests
    {
        [Test]
        public void LegacyFirstCanonicalizingAuthorityTypeIsAbsent()
        {
            System.Type retiredType =
                typeof(ProductionWeaponHoldingsAuthorityV2).Assembly.GetType(
                    "ShooterMover.Application.Flow.Production."
                    + "CanonicalizingPlayerHoldingsAuthorityV2",
                    false);

            Assert.That(retiredType, Is.Null);
        }
    }
}
