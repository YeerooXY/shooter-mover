using NUnit.Framework;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Enemies;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.LootDropBinding;
using ShooterMover.UI.Game;

namespace ShooterMover.Tests.EditMode.Enemies
{
    public sealed class CompactEnemyLootBridgeTests
    {
        [Test]
        public void GunnerDroidNormalProfileProjectsToCanonicalEnemyDropFact()
        {
            StableId definitionId = StableId.Parse("enemy.gunner-droid");
            Assert.That(
                CompactEnemyDropProfiles.Resolve("normal", definitionId),
                Is.EqualTo(LootSourceCatalog.NormalEnemyId));

            var fact = new CompactEnemyTerminalRewardFact(
                StableId.Parse("enemy-death.test-gunner"),
                StableId.Parse("hit.test-gunner"),
                StableId.Parse("run.test-gunner"),
                1L,
                1,
                StableId.Parse("enemy-entity.test-gunner"),
                StableId.Parse("enemy-placement.test-gunner"),
                1L,
                StableId.Parse("room.test-gunner"),
                definitionId,
                1,
                StableId.Parse("character.test-player"),
                StableId.Parse("run-participant.test-player"),
                LootSourceCatalog.NormalEnemyId,
                EnemyActorDeathCause.IncomingDamage);

            LootDropAdaptationResult result =
                new CompactEnemyLootDropFactBridge().Adapt(fact);

            Assert.That(result, Is.Not.Null);
            Assert.That(result.Succeeded, Is.True, result.Diagnostic);
            Assert.That(result.SourceFact, Is.Not.Null);
            Assert.That(
                result.SourceFact.FactKindStableId,
                Is.EqualTo(LootDropFactKindIds.EnemyDeath));
            Assert.That(
                result.SourceFact.SourceDefinitionStableId,
                Is.EqualTo(definitionId));
            Assert.That(
                result.SourceFact.DeclaredDropProfileStableId,
                Is.EqualTo(LootSourceCatalog.NormalEnemyId));
            Assert.That(
                result.SourceFact.AttributedParticipantStableId,
                Is.EqualTo(StableId.Parse("run-participant.test-player")));
            Assert.That(
                result.SourceFact.DamageChannelStableId,
                Is.EqualTo(StableId.Parse("damage.kinetic")));
            Assert.That(
                fact.RewardRoomStableId,
                Is.EqualTo(StableId.Parse("room.test-gunner")));
            Assert.That(
                fact.RewardPlacementStableId,
                Is.EqualTo(StableId.Parse("enemy-placement.test-gunner")));
            Assert.That(fact.RewardPlacementFingerprint, Is.Not.Empty);
        }

        [Test]
        public void UnsupportedCompactEnemyDropProfileFailsClosed()
        {
            Assert.Throws<System.InvalidOperationException>(delegate
            {
                CompactEnemyDropProfiles.Resolve(
                    "mystery",
                    StableId.Parse("enemy.gunner-droid"));
            });
        }
    }
}
