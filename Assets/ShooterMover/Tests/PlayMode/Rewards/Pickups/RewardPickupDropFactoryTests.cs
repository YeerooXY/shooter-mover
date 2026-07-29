using System.Collections;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.Pickups
{
    public sealed class RewardPickupDropFactoryTests : RewardPickupPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator ProfileBasedSrcSubmissionSpawnsOneMoneyPickupAcrossDuplicateCallbacks()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplaySceneScope2D scope = CreateScope("run.profile-drop");
            RewardPickupDropFactory2D factory = CreateFactory(authorities, scope);
            RewardSourceResolvedPreview preview = CreatePreview(
                "profile-drop",
                RewardGrantKind.Money,
                "currency.money",
                7L);

            RewardSourceSubmissionResult first = factory.Submit(preview);
            RewardSourceSubmissionResult duplicate = factory.Submit(preview);
            RewardPickup2D pickup = factory.LastSpawnResult.Pickup;

            Assert.That(first.Status, Is.EqualTo(RewardSourceSubmissionStatus.Accepted));
            Assert.That(duplicate.Status, Is.EqualTo(RewardSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(1));
            Assert.That(pickup.Payload.PickupStableId, Is.EqualTo(RewardPickupPayload.Create(pickup.Payload.CommitCommand).PickupStableId));
            Assert.That(pickup.TryCollect(StableId.Parse("claimant.profile-player")).Status, Is.EqualTo(RewardPickupCollectStatus.Collected));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfileBasedEquipmentReferenceUsesResolvedImmutableInstance()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplaySceneScope2D scope = CreateScope("run.equipment-drop");
            EquipmentInstance equipment = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.profile-drop"),
                StableId.Parse("equipment-definition.profile-drop"),
                12,
                StableId.Parse("quality.common"),
                new AugmentInstance[0]);
            RewardPickupDropFactory2D factory = CreateFactory(
                authorities,
                scope,
                new FixedEquipmentPayloadResolver(equipment));
            RewardSourceResolvedPreview preview = CreatePreview(
                "equipment-drop",
                RewardGrantKind.EquipmentReference,
                "equipment-definition.profile-drop");

            Assert.That(factory.Submit(preview).Status, Is.EqualTo(RewardSourceSubmissionStatus.Accepted));
            RewardPickup2D pickup = factory.LastSpawnResult.Pickup;
            Assert.That(pickup.Payload.Category, Is.EqualTo(RewardPickupCategory.Equipment));
            Assert.That(pickup.TryCollect(StableId.Parse("claimant.equipment-player")).Status, Is.EqualTo(RewardPickupCollectStatus.Collected));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Holdings.LastCommand.EquipmentInstance, Is.EqualTo(equipment));
            Assert.That(authorities.Holdings.LastCommand.InstanceStableId, Is.EqualTo(equipment.InstanceId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForcedScrapDropRoutesOnlyThroughScrapAuthority()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplaySceneScope2D scope = CreateScope("run.pickup-tests");
            RewardPickupDropFactory2D factory = CreateFactory(authorities, scope);
            var command = CreateValueCommit("scrap-drop", RewardGrantKind.Scrap, "currency.scrap", 11L);

            RewardPickupSpawnResult spawn = factory.SpawnForced(command);
            RewardPickupCollectResult collect = spawn.Pickup.TryCollect(StableId.Parse("claimant.scrap-player"));

            Assert.That(spawn.Status, Is.EqualTo(RewardPickupSpawnStatus.Spawned));
            Assert.That(spawn.Pickup.Payload.Category, Is.EqualTo(RewardPickupCategory.Scrap));
            Assert.That(collect.Status, Is.EqualTo(RewardPickupCollectStatus.Collected));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitNoDropCommitsSourceTruthWithoutPhysicalProjection()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplaySceneScope2D scope = CreateScope("run.no-drop");
            RewardPickupDropFactory2D factory = CreateFactory(authorities, scope);
            RewardSourceResolvedPreview preview = CreatePreview("no-drop", null, null);

            RewardSourceSubmissionResult first = factory.Submit(preview);
            RewardSourceSubmissionResult duplicate = factory.Submit(preview);

            Assert.That(first.Status, Is.EqualTo(RewardSourceSubmissionStatus.Accepted));
            Assert.That(duplicate.Status, Is.EqualTo(RewardSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(0));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForcedStrongboxDropReusesPreparedInstanceAndRoutesThroughHoldings()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplaySceneScope2D scope = CreateScope("run.pickup-tests");
            RewardPickupDropFactory2D factory = CreateFactory(authorities, scope);
            StableId instanceId = StableId.Parse("strongbox-instance.forced-a");
            var command = CreateStrongboxCommit("forced-box", "strongbox.tier-a", instanceId);

            RewardPickupSpawnResult first = factory.SpawnForced(command);
            RewardPickupSpawnResult duplicate = factory.SpawnForced(command);

            Assert.That(first.Status, Is.EqualTo(RewardPickupSpawnStatus.Spawned));
            Assert.That(duplicate.Status, Is.EqualTo(RewardPickupSpawnStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(1));
            Assert.That(first.Pickup.Payload.Category, Is.EqualTo(RewardPickupCategory.Strongbox));
            Assert.That(first.Pickup.TryCollect(StableId.Parse("claimant.forced-box-player")).Status, Is.EqualTo(RewardPickupCollectStatus.Collected));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Holdings.LastCommand.GrantKind, Is.EqualTo(RewardGrantKind.Strongbox));
            Assert.That(authorities.Holdings.LastCommand.InstanceStableId, Is.EqualTo(instanceId));
            yield return null;
        }
    }
}
