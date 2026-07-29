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
    public sealed class LootPickupDropFactoryTests : LootPickupPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator ProfileBasedSrcSubmissionSpawnsOneMoneyPickupAcrossDuplicateCallbacks()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.profile-drop");
            LootSpawner factory = CreateFactory(authorities, scope);
            LootSourceResolvedPreview preview = CreatePreview(
                "profile-drop",
                RewardGrantKind.Money,
                "currency.money",
                7L);

            LootSourceSubmissionResult first = factory.Submit(preview);
            LootSourceSubmissionResult duplicate = factory.Submit(preview);
            LootPickup pickup = factory.LastSpawnResult.Pickup;

            Assert.That(first.Status, Is.EqualTo(LootSourceSubmissionStatus.Accepted));
            Assert.That(duplicate.Status, Is.EqualTo(LootSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(1));
            Assert.That(pickup.Payload.PickupStableId, Is.EqualTo(LootPickupPayload.Create(pickup.Payload.CommitCommand).PickupStableId));
            Assert.That(pickup.TryCollect(StableId.Parse("claimant.profile-player")).Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ProfileBasedEquipmentReferenceUsesResolvedImmutableInstance()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.equipment-drop");
            EquipmentInstance equipment = EquipmentInstance.Create(
                StableId.Parse("equipment-instance.profile-drop"),
                StableId.Parse("equipment-definition.profile-drop"),
                12,
                StableId.Parse("quality.common"),
                new AugmentInstance[0]);
            LootSpawner factory = CreateFactory(
                authorities,
                scope,
                new FixedEquipmentPayloadResolver(equipment));
            LootSourceResolvedPreview preview = CreatePreview(
                "equipment-drop",
                RewardGrantKind.EquipmentReference,
                "equipment-definition.profile-drop");

            Assert.That(factory.Submit(preview).Status, Is.EqualTo(LootSourceSubmissionStatus.Accepted));
            LootPickup pickup = factory.LastSpawnResult.Pickup;
            Assert.That(pickup.Payload.Category, Is.EqualTo(LootPickupCategory.Equipment));
            Assert.That(pickup.TryCollect(StableId.Parse("claimant.equipment-player")).Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Holdings.LastCommand.EquipmentInstance, Is.EqualTo(equipment));
            Assert.That(authorities.Holdings.LastCommand.InstanceStableId, Is.EqualTo(equipment.InstanceId));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForcedScrapDropRoutesOnlyThroughScrapAuthority()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.pickup-tests");
            LootSpawner factory = CreateFactory(authorities, scope);
            var command = CreateValueCommit("scrap-drop", RewardGrantKind.Scrap, "currency.scrap", 11L);

            LootPickupSpawnResult spawn = factory.SpawnForced(command);
            LootPickupCollectResult collect = spawn.Pickup.TryCollect(StableId.Parse("claimant.scrap-player"));

            Assert.That(spawn.Status, Is.EqualTo(LootPickupSpawnStatus.Spawned));
            Assert.That(spawn.Pickup.Payload.Category, Is.EqualTo(LootPickupCategory.Scrap));
            Assert.That(collect.Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(0));
            yield return null;
        }

        [UnityTest]
        public IEnumerator ExplicitNoDropCommitsSourceTruthWithoutPhysicalProjection()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.no-drop");
            LootSpawner factory = CreateFactory(authorities, scope);
            LootSourceResolvedPreview preview = CreatePreview("no-drop", null, null);

            LootSourceSubmissionResult first = factory.Submit(preview);
            LootSourceSubmissionResult duplicate = factory.Submit(preview);

            Assert.That(first.Status, Is.EqualTo(LootSourceSubmissionStatus.Accepted));
            Assert.That(duplicate.Status, Is.EqualTo(LootSourceSubmissionStatus.ExactDuplicateNoChange));
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
            GameplayScene scope = CreateScope("run.pickup-tests");
            LootSpawner factory = CreateFactory(authorities, scope);
            StableId instanceId = StableId.Parse("strongbox-instance.forced-a");
            var command = CreateStrongboxCommit("forced-box", "strongbox.tier-a", instanceId);

            LootPickupSpawnResult first = factory.SpawnForced(command);
            LootPickupSpawnResult duplicate = factory.SpawnForced(command);

            Assert.That(first.Status, Is.EqualTo(LootPickupSpawnStatus.Spawned));
            Assert.That(duplicate.Status, Is.EqualTo(LootPickupSpawnStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(1));
            Assert.That(first.Pickup.Payload.Category, Is.EqualTo(LootPickupCategory.Strongbox));
            Assert.That(first.Pickup.TryCollect(StableId.Parse("claimant.forced-box-player")).Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Holdings.LastCommand.GrantKind, Is.EqualTo(RewardGrantKind.Strongbox));
            Assert.That(authorities.Holdings.LastCommand.InstanceStableId, Is.EqualTo(instanceId));
            yield return null;
        }
    }
}
