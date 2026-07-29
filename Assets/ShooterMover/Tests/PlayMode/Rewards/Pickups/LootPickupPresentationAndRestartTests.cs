using System.Collections;
using NUnit.Framework;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.Pickups
{
    public sealed class LootPickupPresentationAndRestartTests : LootPickupPlayModeTestBase
    {
        [Test]
        public void CategoryMappingCoversEveryPickupGrantFamily()
        {
            Assert.That(LootPickupCategoryMap.FromGrantKind(RewardGrantKind.Money), Is.EqualTo(LootPickupCategory.Money));
            Assert.That(LootPickupCategoryMap.FromGrantKind(RewardGrantKind.Scrap), Is.EqualTo(LootPickupCategory.Scrap));
            Assert.That(LootPickupCategoryMap.FromGrantKind(RewardGrantKind.Strongbox), Is.EqualTo(LootPickupCategory.Strongbox));
            Assert.That(LootPickupCategoryMap.FromGrantKind(RewardGrantKind.EquipmentReference), Is.EqualTo(LootPickupCategory.Equipment));
            Assert.That(LootPickupCategoryMap.FromGrantKind(RewardGrantKind.PremiumAmmo), Is.EqualTo(LootPickupCategory.Miscellaneous));
            Assert.That(LootPickupCategoryMap.FromGrantKind(RewardGrantKind.Miscellaneous), Is.EqualTo(LootPickupCategory.Miscellaneous));
        }

        [UnityTest]
        public IEnumerator RepeatedCollisionCallbacksApplyOneAtomicHoldingsGrant()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.pickup-tests");
            var command = CreateValueCommit(
                "misc-a",
                RewardGrantKind.Miscellaneous,
                "item.misc-a",
                2L);
            Assert.That(authorities.Adapter.Commit(command).Status, Is.EqualTo(RewardApplicationResultStatus.Generated));

            GameObject pickupObject = Track(new GameObject("Pickup"));
            LootPickup pickup = pickupObject.AddComponent<LootPickup>();
            LootPickupPresentationStyle style = new LootPickupPresentationStyle(
                LootPickupCategory.Miscellaneous,
                null,
                new Color(0.25f, 0.5f, 0.75f, 1f),
                new Vector3(1.5f, 1.5f, 1f));
            pickup.ConfigureForTests(
                LootPickupPayload.Create(command),
                authorities.Adapter,
                scope,
                1.25f,
                new[] { style });

            GameObject claimantObject = Track(new GameObject("Claimant"));
            LootCollector claimant = claimantObject.AddComponent<LootCollector>();
            claimant.ConfigureForTests("claimant.player-one");
            pickup.HandleTriggerForTests(claimant);
            LootPickupCollectResult first = pickup.LastCollectResult;
            pickup.HandleTriggerForTests(claimant);
            LootPickupCollectResult duplicate = pickup.LastCollectResult;

            CircleCollider2D trigger = pickup.GetComponent<CircleCollider2D>();
            SpriteRenderer renderer = pickup.GetComponent<SpriteRenderer>();
            Assert.That(first.Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            Assert.That(duplicate.Status, Is.EqualTo(LootPickupCollectStatus.AlreadyCollectedNoChange));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(0));
            Assert.That(trigger.radius, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(trigger.enabled, Is.False);
            Assert.That(renderer.enabled, Is.False);
            Assert.That(renderer.color, Is.EqualTo(style.Tint));
            Assert.That(pickup.transform.localScale, Is.EqualTo(style.LocalScale));
            yield return null;
        }

        [UnityTest]
        public IEnumerator QuickRestartKeepsAppliedPickupRetiredAndUnclaimedPickupAvailable()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.pickup-tests");
            LootPickup collected = CreateConfiguredPickup(
                authorities,
                scope,
                CreateValueCommit("restart-collected", RewardGrantKind.Miscellaneous, "item.restart-collected", 1L));
            LootPickup available = CreateConfiguredPickup(
                authorities,
                scope,
                CreateValueCommit("restart-available", RewardGrantKind.Miscellaneous, "item.restart-available", 1L));

            Assert.That(collected.TryCollect(StableId.Parse("claimant.restart-player")).IsCollected, Is.True);
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(2));
            scope.RunRestart(1L);

            Assert.That(collected.IsCollected, Is.True);
            Assert.That(collected.GetComponent<CircleCollider2D>().enabled, Is.False);
            Assert.That(collected.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(available.IsCollected, Is.False);
            Assert.That(available.GetComponent<CircleCollider2D>().enabled, Is.True);
            Assert.That(available.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(scope.RegisteredRestartParticipantCount, Is.EqualTo(2));
            yield return null;
        }

        [UnityTest]
        public IEnumerator RecreatedProjectionCannotDoubleRewardAfterRapAlreadyApplied()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.pickup-tests");
            var command = CreateValueCommit("recreated", RewardGrantKind.Miscellaneous, "item.recreated", 1L);
            authorities.Adapter.Commit(command);

            LootPickup first = CreatePickupProjection(authorities, scope, command);
            Assert.That(first.TryCollect(StableId.Parse("claimant.recreated-player")).Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            LootPickup recreated = CreatePickupProjection(authorities, scope, command, false);
            LootPickupCollectResult replay = recreated.TryCollect(StableId.Parse("claimant.recreated-player"));

            Assert.That(replay.Status, Is.EqualTo(LootPickupCollectStatus.AlreadyCollectedNoChange));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(1));
            yield return null;
        }
    }
}
