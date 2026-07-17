using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.GameplayDrops;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.GameplayDrops
{
    public sealed class GameplayDropOperationV1Tests
    {
        [Test]
        public void SameRunAndSourceProduceExactOperationIdentity()
        {
            RewardProfileV1 profile = MoneyProfile();
            GameplayDropOverrideV1 dropOverride =
                GameplayDropOverrideV1.Default(
                    StableId.Parse("gameplay-drop-override.default"));

            GameplayDropOperationV1 first = GameplayDropOperationFactoryV1.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);
            GameplayDropOperationV1 second = GameplayDropOperationFactoryV1.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);

            Assert.That(
                second.OperationRequest.SourceOperationStableId,
                Is.EqualTo(first.OperationRequest.SourceOperationStableId));
            Assert.That(
                second.OperationRequest.CommitmentStableId,
                Is.EqualTo(first.OperationRequest.CommitmentStableId));
            Assert.That(second.Fingerprint, Is.EqualTo(first.Fingerprint));
            Assert.That(
                RewardOperationIdentityV1.Classify(
                    first.OperationRequest,
                    second.OperationRequest),
                Is.EqualTo(RewardOperationIdentityComparisonV1.ExactDuplicateNoChange));
        }

        [Test]
        public void DistinctSourceInstancesProduceDistinctOperations()
        {
            RewardProfileV1 profile = MoneyProfile();
            GameplayDropOverrideV1 dropOverride =
                GameplayDropOverrideV1.Default(
                    StableId.Parse("gameplay-drop-override.default"));

            GameplayDropOperationV1 first = GameplayDropOperationFactoryV1.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);
            GameplayDropOperationV1 second = GameplayDropOperationFactoryV1.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-b"),
                profile,
                dropOverride);

            Assert.That(
                second.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(first.OperationRequest.SourceOperationStableId));
            Assert.That(
                RewardOperationIdentityV1.Classify(
                    first.OperationRequest,
                    second.OperationRequest),
                Is.EqualTo(RewardOperationIdentityComparisonV1.DistinctOperation));
        }

        [Test]
        public void ManualOverrideModesResolveWithoutChangingAuthorityOwnership()
        {
            RewardProfileV1 inherited = MoneyProfile();
            StableId source = StableId.Parse("placed.enemy-a");
            RewardGrantSpecificationV1 scrap = RewardGrantSpecificationV1.CreateFixed(
                StableId.Parse("gameplay-drop-grant.scrap"),
                RewardGrantKindV1.Scrap,
                StableId.Parse("currency.scrap"),
                2L);

            RewardProfileV1 defaultProfile = GameplayDropOverrideV1.Default(
                StableId.Parse("gameplay-drop-override.default"))
                .Resolve(source, inherited);
            RewardProfileV1 none = GameplayDropOverrideV1.ForcedNone(
                StableId.Parse("gameplay-drop-override.none"),
                StableId.Parse("gameplay-drop-profile.none"))
                .Resolve(source, inherited);
            RewardProfileV1 forced = GameplayDropOverrideV1.ForcedSpecificReward(
                StableId.Parse("gameplay-drop-override.forced"),
                StableId.Parse("gameplay-drop-profile.forced"),
                scrap)
                .Resolve(source, inherited);
            RewardProfileV1 appended = GameplayDropOverrideV1.AppendGuaranteedReward(
                StableId.Parse("gameplay-drop-override.append"),
                StableId.Parse("gameplay-drop-profile.appended"),
                scrap)
                .Resolve(source, inherited);

            Assert.That(defaultProfile, Is.SameAs(inherited));
            Assert.That(none.Disposition, Is.EqualTo(RewardProfileDispositionV1.ExplicitNoDrop));
            Assert.That(forced.GuaranteedEntries.Count, Is.EqualTo(1));
            Assert.That(forced.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKindV1.Scrap));
            Assert.That(appended.GuaranteedEntries.Count, Is.EqualTo(2));
        }

        [Test]
        public void ChangedProfileForSameRunAndSourceIsConflictingDuplicate()
        {
            GameplayDropOperationV1 first = GameplayDropOperationFactoryV1.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                MoneyProfile(),
                GameplayDropOverrideV1.Default(
                    StableId.Parse("gameplay-drop-override.default")));
            RewardProfileV1 changed = RewardProfileV1.Create(
                StableId.Parse("gameplay-drop-profile.changed"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("gameplay-drop-grant.scrap"),
                        RewardGrantKindV1.Scrap,
                        StableId.Parse("currency.scrap"),
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            GameplayDropOperationV1 second = GameplayDropOperationFactoryV1.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                changed,
                GameplayDropOverrideV1.Default(
                    StableId.Parse("gameplay-drop-override.default")));

            Assert.That(
                RewardOperationIdentityV1.Classify(
                    first.OperationRequest,
                    second.OperationRequest),
                Is.EqualTo(RewardOperationIdentityComparisonV1.ConflictingDuplicate));
        }

        private static RewardProfileV1 MoneyProfile()
        {
            return RewardProfileV1.Create(
                StableId.Parse("gameplay-drop-profile.money"),
                new[]
                {
                    RewardGrantSpecificationV1.CreateFixed(
                        StableId.Parse("gameplay-drop-grant.money"),
                        RewardGrantKindV1.Money,
                        StableId.Parse("currency.money"),
                        5L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
        }
    }
}
