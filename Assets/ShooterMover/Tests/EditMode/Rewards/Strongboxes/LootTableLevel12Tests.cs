using NUnit.Framework;
using ShooterMover.Application.Rewards.Strongboxes;

namespace ShooterMover.Tests.EditMode.Rewards.Strongboxes
{
    public sealed class LootTableLevel12Tests
    {
        [Test]
        public void TierElevenAcceptsItsAuthoredLevelTwelveOutcome()
        {
            Assert.DoesNotThrow(delegate
            {
                LootTable.GetTier(11);
            });
        }
    }
}
