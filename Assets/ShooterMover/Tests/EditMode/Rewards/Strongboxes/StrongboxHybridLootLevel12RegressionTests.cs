using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Rewards.Strongboxes
{
    public sealed class StrongboxHybridLootLevel12RegressionTests
    {
        [Test]
        public void TierElevenCatalogAcceptsItsAuthoredLevelTwelveOutcome()
        {
            Assert.DoesNotThrow(delegate
            {
                StrongboxHybridLootCatalog.GetByTierNumber(11);
            });
        }
    }
}
