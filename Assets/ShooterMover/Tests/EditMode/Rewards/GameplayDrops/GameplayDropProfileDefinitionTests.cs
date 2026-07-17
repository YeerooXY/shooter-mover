using System;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Content.Definitions.Rewards.GameplayDrops;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Rewards.Model;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Rewards.GameplayDrops
{
    public sealed class GameplayDropProfileDefinitionTests
    {
        [Test]
        public void MixedProfileBuildsEverySupportedGameplayRewardFamily()
        {
            GameplayDropProfileDefinitionAsset asset =
                GameplayDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.mixed",
                    false,
                    new[]
                    {
                        Grant(
                            "gameplay-drop-grant.money",
                            RewardGrantKindV1.Money,
                            "currency.money",
                            5L,
                            10L),
                        Grant(
                            "gameplay-drop-grant.scrap",
                            RewardGrantKindV1.Scrap,
                            "currency.scrap",
                            1L,
                            3L),
                    },
                    new[]
                    {
                        new IndependentRewardRollAuthoring(
                            "gameplay-drop-roll.premium-ammo",
                            250000,
                            Grant(
                                "gameplay-drop-grant.premium-ammo",
                                RewardGrantKindV1.PremiumAmmo,
                                "ammo.premium",
                                1L,
                                2L)),
                    },
                    new[]
                    {
                        new ExclusiveRewardGroupAuthoring(
                            "gameplay-drop-group.weighted",
                            WeightedRewardOutcomeAuthoring.Grant(
                                "gameplay-drop-outcome.strongbox",
                                3L,
                                Grant(
                                    "gameplay-drop-grant.strongbox",
                                    RewardGrantKindV1.Strongbox,
                                    "strongbox-tier.standard",
                                    1L,
                                    1L)),
                            WeightedRewardOutcomeAuthoring.Grant(
                                "gameplay-drop-outcome.misc",
                                2L,
                                Grant(
                                    "gameplay-drop-grant.misc",
                                    RewardGrantKindV1.Miscellaneous,
                                    "misc.component",
                                    1L,
                                    4L)),
                            WeightedRewardOutcomeAuthoring.NoDrop(
                                "gameplay-drop-outcome.none",
                                5L)),
                    });

            try
            {
                RewardProfileV1 profile = asset.BuildProfile();

                Assert.That(profile.Disposition, Is.EqualTo(RewardProfileDispositionV1.Configured));
                Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(2));
                Assert.That(profile.IndependentRolls.Count, Is.EqualTo(1));
                Assert.That(profile.ExclusiveGroups.Count, Is.EqualTo(1));
                bool hasExplicitNoDrop = false;
                for (int index = 0;
                    index < profile.ExclusiveGroups[0].Outcomes.Count;
                    index++)
                {
                    if (profile.ExclusiveGroups[0].Outcomes[index].Kind
                        == WeightedRewardOutcomeKindV1.ExplicitNoDrop)
                    {
                        hasExplicitNoDrop = true;
                    }
                }

                Assert.That(hasExplicitNoDrop, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [TestCase(RewardGrantKindV1.Money, "currency.money")]
        [TestCase(RewardGrantKindV1.Scrap, "currency.scrap")]
        [TestCase(RewardGrantKindV1.Strongbox, "strongbox-tier.standard")]
        [TestCase(RewardGrantKindV1.PremiumAmmo, "ammo.premium")]
        [TestCase(RewardGrantKindV1.Miscellaneous, "misc.component")]
        public void SingleFamilyProfilesRemainAuthorable(
            RewardGrantKindV1 kind,
            string contentId)
        {
            GameplayDropProfileDefinitionAsset asset =
                GameplayDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.single-" + ((int)kind),
                    false,
                    new[]
                    {
                        Grant(
                            "gameplay-drop-grant.single-" + ((int)kind),
                            kind,
                            contentId,
                            1L,
                            1L),
                    },
                    Array.Empty<IndependentRewardRollAuthoring>(),
                    Array.Empty<ExclusiveRewardGroupAuthoring>());

            try
            {
                RewardProfileV1 profile = asset.BuildProfile();
                Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(1));
                Assert.That(profile.GuaranteedEntries[0].Kind, Is.EqualTo(kind));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        [Test]
        public void ExplicitNoDropIsFirstClass()
        {
            GameplayDropProfileDefinitionAsset asset =
                GameplayDropProfileDefinitionAsset.CreateRuntime(
                    "gameplay-drop-profile.none",
                    true,
                    Array.Empty<RewardGrantAuthoring>(),
                    Array.Empty<IndependentRewardRollAuthoring>(),
                    Array.Empty<ExclusiveRewardGroupAuthoring>());

            try
            {
                Assert.That(
                    asset.BuildProfile().Disposition,
                    Is.EqualTo(RewardProfileDispositionV1.ExplicitNoDrop));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }

        private static RewardGrantAuthoring Grant(
            string grantId,
            RewardGrantKindV1 kind,
            string contentId,
            long minimum,
            long maximum)
        {
            return new RewardGrantAuthoring(
                grantId,
                kind,
                contentId,
                minimum,
                maximum);
        }
    }
}
