using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Content.Definitions.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;
using ShooterMover.UnityAdapters.Rewards.Sources;
using UnityEngine;

namespace ShooterMover.Tests.EditMode.Rewards.Authoring
{
    public sealed class RewardProfileAuthoringTests
    {
        private readonly List<UnityEngine.Object> _created =
            new List<UnityEngine.Object>();

        [TearDown]
        public void TearDown()
        {
            for (int index = _created.Count - 1; index >= 0; index--)
            {
                if (_created[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_created[index]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ProfileAssetBuildsAllSectionsAndIgnoresSerializedListOrder()
        {
            RewardGrantAuthoring money = Grant(
                "reward-grant.money",
                RewardGrantKindV1.Money,
                "currency.money",
                2L,
                5L);
            RewardGrantAuthoring scrap = Grant(
                "reward-grant.scrap",
                RewardGrantKindV1.Scrap,
                "currency.scrap",
                1L,
                3L);
            IndependentRewardRollAuthoring independent =
                new IndependentRewardRollAuthoring(
                    "reward-roll.ammo",
                    250000,
                    Grant(
                        "reward-grant.ammo",
                        RewardGrantKindV1.PremiumAmmo,
                        "ammo.premium",
                        1L,
                        2L));
            ExclusiveRewardGroupAuthoring exclusive =
                new ExclusiveRewardGroupAuthoring(
                    "reward-group.box-or-none",
                    WeightedRewardOutcomeAuthoring.Grant(
                        "reward-outcome.box",
                        3L,
                        Grant(
                            "reward-grant.box",
                            RewardGrantKindV1.Strongbox,
                            "strongbox-tier.tier-2",
                            1L,
                            1L)),
                    WeightedRewardOutcomeAuthoring.NoDrop(
                        "reward-outcome.none",
                        1L));

            RewardProfileDefinitionAsset first = Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    "reward-profile.mixed",
                    false,
                    new[] { money, scrap },
                    new[] { independent },
                    new[] { exclusive }));
            RewardProfileDefinitionAsset second = Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    "reward-profile.mixed",
                    false,
                    new[] { scrap, money },
                    new[] { independent },
                    new[] { exclusive }));

            RewardProfileV1 firstProfile = first.BuildProfile();
            RewardProfileV1 secondProfile = second.BuildProfile();

            Assert.That(firstProfile.GuaranteedEntries.Count, Is.EqualTo(2));
            Assert.That(firstProfile.IndependentRolls.Count, Is.EqualTo(1));
            Assert.That(firstProfile.ExclusiveGroups.Count, Is.EqualTo(1));
            Assert.That(firstProfile, Is.EqualTo(secondProfile));
            Assert.That(firstProfile.Fingerprint, Is.EqualTo(secondProfile.Fingerprint));
            Assert.That(
                first.BuildDefinition().Fingerprint,
                Is.EqualTo(second.BuildDefinition().Fingerprint));
        }

        [Test]
        public void EveryRequiredOverrideModeResolvesIntoRewVocabulary()
        {
            RewardProfileDefinitionAsset inheritedAsset = CreateMoneyProfile(
                "reward-profile.inherited",
                "reward-grant.inherited-money");
            RewardProfileDefinitionAsset replacementAsset = Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    "reward-profile.replacement",
                    false,
                    new[]
                    {
                        Grant(
                            "reward-grant.replacement-scrap",
                            RewardGrantKindV1.Scrap,
                            "currency.scrap",
                            4L,
                            4L)
                    },
                    Array.Empty<IndependentRewardRollAuthoring>(),
                    Array.Empty<ExclusiveRewardGroupAuthoring>()));
            RewardProfileV1 inherited = inheritedAsset.BuildProfile();
            StableId sourceId = StableId.Parse("placed.reward-source-a");

            RewardProfileV1 inherit = RewardSourceOverrideAuthoring.Inherit(
                "reward-override.inherit").Resolve(sourceId, inherited);
            RewardProfileV1 none = RewardSourceOverrideAuthoring.None(
                "reward-override.none",
                "reward-profile.none").Resolve(sourceId, inherited);
            RewardProfileV1 replace = RewardSourceOverrideAuthoring.Replace(
                "reward-override.replace",
                replacementAsset).Resolve(sourceId, inherited);
            RewardProfileV1 append = RewardSourceOverrideAuthoring.AppendGuaranteed(
                "reward-override.append",
                "reward-profile.appended",
                new RewardGrantOverrideAuthoring(
                    "reward-grant.appended-misc",
                    RewardGrantKindV1.Miscellaneous,
                    "misc.token",
                    1L,
                    1L)).Resolve(sourceId, inherited);
            RewardProfileV1 money = RewardSourceOverrideAuthoring.MoneyOnly(
                "reward-override.money",
                "reward-profile.money-only",
                "reward-grant.money-only",
                "currency.money",
                10L,
                20L).Resolve(sourceId, inherited);
            RewardProfileV1 exactBox =
                RewardSourceOverrideAuthoring.StrongboxExactTier(
                    "reward-override.exact-box",
                    "reward-profile.exact-box",
                    "reward-grant.exact-box",
                    "strongbox-tier.tier-4").Resolve(sourceId, inherited);
            RewardProfileV1 boxRange =
                RewardSourceOverrideAuthoring.StrongboxTierRange(
                    "reward-override.box-range",
                    "reward-profile.box-range",
                    "reward-group.box-range",
                    "reward-grant.box-range",
                    2,
                    4,
                    Tier(4),
                    Tier(2),
                    Tier(3)).Resolve(sourceId, inherited);
            RewardProfileV1 misc = RewardSourceOverrideAuthoring.Miscellaneous(
                "reward-override.misc",
                "reward-profile.misc",
                new RewardGrantOverrideAuthoring(
                    "reward-grant.misc",
                    RewardGrantKindV1.Miscellaneous,
                    "misc.key-fragment",
                    1L,
                    3L),
                new RewardGrantOverrideAuthoring(
                    "reward-grant.premium-ammo",
                    RewardGrantKindV1.PremiumAmmo,
                    "ammo.premium",
                    2L,
                    2L)).Resolve(sourceId, inherited);

            Assert.That(inherit, Is.SameAs(inherited));
            Assert.That(none.Disposition, Is.EqualTo(RewardProfileDispositionV1.ExplicitNoDrop));
            Assert.That(replace.ProfileStableId, Is.EqualTo(StableId.Parse("reward-profile.replacement")));
            Assert.That(append.GuaranteedEntries.Count, Is.EqualTo(2));
            Assert.That(money.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKindV1.Money));
            Assert.That(exactBox.GuaranteedEntries[0].ContentStableId, Is.EqualTo(StableId.Parse("strongbox-tier.tier-4")));
            Assert.That(boxRange.ExclusiveGroups.Count, Is.EqualTo(1));
            Assert.That(boxRange.ExclusiveGroups[0].Outcomes.Count, Is.EqualTo(3));
            Assert.That(misc.GuaranteedEntries.Count, Is.EqualTo(2));
        }

        [Test]
        public void InvalidStrongboxTierRangesFailClosed()
        {
            RewardProfileV1 inherited = CreateMoneyProfile(
                "reward-profile.inherited",
                "reward-grant.money").BuildProfile();
            StableId sourceId = StableId.Parse("placed.reward-source-a");

            RewardSourceOverrideAuthoring reversed =
                RewardSourceOverrideAuthoring.StrongboxTierRange(
                    "reward-override.reversed",
                    "reward-profile.reversed",
                    "reward-group.reversed",
                    "reward-grant.reversed",
                    4,
                    2,
                    Tier(2),
                    Tier(3),
                    Tier(4));
            RewardSourceOverrideAuthoring missingMiddle =
                RewardSourceOverrideAuthoring.StrongboxTierRange(
                    "reward-override.missing",
                    "reward-profile.missing",
                    "reward-group.missing",
                    "reward-grant.missing",
                    2,
                    4,
                    Tier(2),
                    Tier(4));

            Assert.Throws<InvalidOperationException>(
                delegate { reversed.Resolve(sourceId, inherited); });
            Assert.Throws<InvalidOperationException>(
                delegate { missingMiddle.Resolve(sourceId, inherited); });
        }

        [Test]
        public void ClearingOverrideBackToInheritRestoresInheritedProfile()
        {
            RewardProfileV1 inherited = CreateMoneyProfile(
                "reward-profile.inherited",
                "reward-grant.inherited").BuildProfile();
            StableId sourceId = StableId.Parse("placed.reward-source-a");
            RewardProfileV1 overridden = RewardSourceOverrideAuthoring.MoneyOnly(
                "reward-override.money",
                "reward-profile.money-only",
                "reward-grant.money-only",
                "currency.money",
                99L,
                99L).Resolve(sourceId, inherited);
            RewardProfileV1 cleared = RewardSourceOverrideAuthoring.Inherit(
                "reward-override.cleared").Resolve(sourceId, inherited);

            Assert.That(overridden, Is.Not.EqualTo(inherited));
            Assert.That(cleared, Is.SameAs(inherited));
        }

        private RewardProfileDefinitionAsset CreateMoneyProfile(
            string profileId,
            string grantId)
        {
            return Track(
                RewardProfileDefinitionAsset.CreateRuntime(
                    profileId,
                    false,
                    new[]
                    {
                        Grant(
                            grantId,
                            RewardGrantKindV1.Money,
                            "currency.money",
                            1L,
                            2L)
                    },
                    Array.Empty<IndependentRewardRollAuthoring>(),
                    Array.Empty<ExclusiveRewardGroupAuthoring>()));
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

        private static StrongboxTierOptionAuthoring Tier(int order)
        {
            return new StrongboxTierOptionAuthoring(
                order,
                "reward-outcome.tier-" + order,
                "strongbox-tier.tier-" + order,
                order);
        }

        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _created.Add(value);
            return value;
        }
    }
}
