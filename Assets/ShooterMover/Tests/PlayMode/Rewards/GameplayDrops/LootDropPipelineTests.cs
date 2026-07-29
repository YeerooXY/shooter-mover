using System.Collections;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.LootDrops;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Tests.PlayMode.Rewards.Pickups;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.LootDrops;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.LootDrops
{
    public sealed class LootDropPipelineTests : LootPickupPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator DuplicateDeathAndCollectionApplyMoneyExactlyOnceThroughRap()
        {
            TestStateSet authorities = CreateAuthoritySet();
            GameplayScene scope = CreateScope("run.gameplay-drop-pipeline");
            LootSpawner factory = CreateFactory(authorities, scope);
            ObjectFamilyDefinitionAsset family = CreateFamily();
            LootDropProfileDefinitionAsset profile = Track(
                LootDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.pipeline-money",
                    false,
                    new[]
                    {
                        new RewardGrantAuthoring(
                            "gameplay-drop-grant.pipeline-money",
                            RewardGrantKind.Money,
                            "currency.money",
                            7L,
                            7L),
                    },
                    new IndependentRewardRollAuthoring[0],
                    new ExclusiveRewardGroupAuthoring[0]));
            GameObject host = Track(new GameObject("AnyGameplayHost"));
            host.transform.SetParent(scope.transform);
            PlacedObject placed =
                host.AddComponent<PlacedObject>();
            placed.ConfigureForTests(
                "placed.gameplay-drop-pipeline",
                family,
                "variant.standard",
                scope,
                "scope.gameplay",
                new CapabilityOverrideAuthoring[0]);
            LootEmitter source =
                host.AddComponent<LootEmitter>();
            source.ConfigureForTests(
                placed,
                profile,
                LootDropOverrideAuthoring.Default(
                    "gameplay-drop-override.pipeline-default"),
                factory);

            LootSourceSubmissionResult firstDeath = source.SubmitLootDrop();
            LootSourceSubmissionResult repeatedDeath = source.SubmitLootDrop();
            LootPickup pickup = factory.LastSpawnResult.Pickup;
            LootPickupCollectResult firstCollect =
                pickup.TryCollect(StableId.Parse("claimant.gameplay-drop-player"));
            LootPickupCollectResult repeatedCollect =
                pickup.TryCollect(StableId.Parse("claimant.gameplay-drop-player"));

            Assert.That(firstDeath.Status, Is.EqualTo(LootSourceSubmissionStatus.Accepted));
            Assert.That(
                repeatedDeath.Status,
                Is.EqualTo(LootSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(1));
            Assert.That(firstCollect.Status, Is.EqualTo(LootPickupCollectStatus.Collected));
            Assert.That(
                repeatedCollect.Status,
                Is.EqualTo(LootPickupCollectStatus.AlreadyCollectedNoChange));
            Assert.That(authorities.Money.ApplyCount, Is.EqualTo(1));
            Assert.That(authorities.Scrap.ApplyCount, Is.EqualTo(0));
            Assert.That(authorities.Holdings.ApplyCount, Is.EqualTo(0));
            yield return null;
        }

        private ObjectFamilyDefinitionAsset CreateFamily()
        {
            ObjectCapabilityDefinitionAsset presentation = Track(
                ObjectCapabilityDefinitionAsset.CreateRuntime(
                    "capability.presentation",
                    new CapabilityFieldAuthoring(
                        "field.sprite",
                        CapabilityFieldValue.FromStableId(
                            StableId.Parse("sprite.gameplay-drop-host")))));
            return Track(
                ObjectFamilyDefinitionAsset.CreateRuntime(
                    "family.gameplay-drop-pipeline",
                    "Gameplay drop pipeline host",
                    "variant.standard",
                    new[] { presentation },
                    new ObjectVariantAuthoring(
                        "variant.standard",
                        null,
                        ObjectCapabilitySelectionAuthoring.Inherit(
                            "capability.presentation"))));
        }
    }
}
