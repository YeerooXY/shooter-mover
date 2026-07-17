using System.Collections;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Objects;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.GameplayDrops;
using ShooterMover.Contracts.Authoring;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Authoring;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.Tests.PlayMode.Rewards.Pickups;
using ShooterMover.UnityAdapters.Authoring;
using ShooterMover.UnityAdapters.Rewards.GameplayDrops;
using ShooterMover.UnityAdapters.Rewards.Pickups;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;
using UnityEngine.TestTools;

namespace ShooterMover.Tests.PlayMode.Rewards.GameplayDrops
{
    public sealed class GameplayDropPipelineTests : RewardPickupPlayModeTestBase
    {
        [UnityTest]
        public IEnumerator DuplicateDeathAndCollectionApplyMoneyExactlyOnceThroughRap()
        {
            TestAuthoritySet authorities = CreateAuthoritySet();
            GameplaySceneScope2D scope = CreateScope("run.gameplay-drop-pipeline");
            RewardPickupDropFactory2D factory = CreateFactory(authorities, scope);
            ObjectFamilyDefinitionAsset family = CreateFamily();
            GameplayDropProfileDefinitionAsset profile = Track(
                GameplayDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.pipeline-money",
                    false,
                    new[]
                    {
                        new RewardGrantAuthoring(
                            "gameplay-drop-grant.pipeline-money",
                            RewardGrantKindV1.Money,
                            "currency.money",
                            7L,
                            7L),
                    },
                    new IndependentRewardRollAuthoring[0],
                    new ExclusiveRewardGroupAuthoring[0]));
            GameObject host = Track(new GameObject("AnyGameplayHost"));
            host.transform.SetParent(scope.transform);
            PlacedObjectAuthoring2D placed =
                host.AddComponent<PlacedObjectAuthoring2D>();
            placed.ConfigureForTests(
                "placed.gameplay-drop-pipeline",
                family,
                "variant.standard",
                scope,
                "scope.gameplay",
                new CapabilityOverrideAuthoring[0]);
            GameplayDropSource2D source =
                host.AddComponent<GameplayDropSource2D>();
            source.ConfigureForTests(
                placed,
                profile,
                GameplayDropOverrideAuthoring.Default(
                    "gameplay-drop-override.pipeline-default"),
                factory);

            RewardSourceSubmissionResult firstDeath = source.SubmitGameplayDrop();
            RewardSourceSubmissionResult repeatedDeath = source.SubmitGameplayDrop();
            RewardPickup2D pickup = factory.LastSpawnResult.Pickup;
            RewardPickupCollectResultV1 firstCollect =
                pickup.TryCollect(StableId.Parse("claimant.gameplay-drop-player"));
            RewardPickupCollectResultV1 repeatedCollect =
                pickup.TryCollect(StableId.Parse("claimant.gameplay-drop-player"));

            Assert.That(firstDeath.Status, Is.EqualTo(RewardSourceSubmissionStatus.Accepted));
            Assert.That(
                repeatedDeath.Status,
                Is.EqualTo(RewardSourceSubmissionStatus.ExactDuplicateNoChange));
            Assert.That(factory.SpawnedPickupCount, Is.EqualTo(1));
            Assert.That(firstCollect.Status, Is.EqualTo(RewardPickupCollectStatusV1.Collected));
            Assert.That(
                repeatedCollect.Status,
                Is.EqualTo(RewardPickupCollectStatusV1.AlreadyCollectedNoChange));
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
