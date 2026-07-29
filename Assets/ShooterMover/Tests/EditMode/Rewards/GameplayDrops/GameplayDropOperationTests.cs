using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.GameplayDrops;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.GameplayDrops
{
    public sealed class GameplayDropOperationTests
    {
        [Test]
        public void SameRunAndSourceProduceExactOperationIdentity()
        {
            RewardProfile profile = MoneyProfile();
            GameplayDropOverride dropOverride =
                GameplayDropOverride.Default(
                    StableId.Parse("gameplay-drop-override.default"));

            GameplayDropOperation first = GameplayDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);
            GameplayDropOperation second = GameplayDropOperationFactory.Create(
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
                RewardOperationIdentity.Classify(
                    first.OperationRequest,
                    second.OperationRequest),
                Is.EqualTo(RewardOperationIdentityComparison.ExactDuplicateNoChange));
        }

        [Test]
        public void DistinctSourceInstancesProduceDistinctOperations()
        {
            RewardProfile profile = MoneyProfile();
            GameplayDropOverride dropOverride =
                GameplayDropOverride.Default(
                    StableId.Parse("gameplay-drop-override.default"));

            GameplayDropOperation first = GameplayDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);
            GameplayDropOperation second = GameplayDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-b"),
                profile,
                dropOverride);

            Assert.That(
                second.OperationRequest.SourceOperationStableId,
                Is.Not.EqualTo(first.OperationRequest.SourceOperationStableId));
            Assert.That(
                RewardOperationIdentity.Classify(
                    first.OperationRequest,
                    second.OperationRequest),
                Is.EqualTo(RewardOperationIdentityComparison.DistinctOperation));
        }

        [Test]
        public void ManualOverrideModesResolveWithoutChangingAuthorityOwnership()
        {
            RewardProfile inherited = MoneyProfile();
            StableId source = StableId.Parse("placed.enemy-a");
            RewardGrantSpecification scrap = RewardGrantSpecification.CreateFixed(
                StableId.Parse("gameplay-drop-grant.scrap"),
                RewardGrantKind.Scrap,
                StableId.Parse("currency.scrap"),
                2L);

            RewardProfile defaultProfile = GameplayDropOverride.Default(
                StableId.Parse("gameplay-drop-override.default"))
                .Resolve(source, inherited);
            RewardProfile none = GameplayDropOverride.ForcedNone(
                StableId.Parse("gameplay-drop-override.none"),
                StableId.Parse("gameplay-drop-profile.none"))
                .Resolve(source, inherited);
            RewardProfile forced = GameplayDropOverride.ForcedSpecificReward(
                StableId.Parse("gameplay-drop-override.forced"),
                StableId.Parse("gameplay-drop-profile.forced"),
                scrap)
                .Resolve(source, inherited);
            RewardProfile appended = GameplayDropOverride.AppendGuaranteedReward(
                StableId.Parse("gameplay-drop-override.append"),
                StableId.Parse("gameplay-drop-profile.appended"),
                scrap)
                .Resolve(source, inherited);

            Assert.That(defaultProfile, Is.SameAs(inherited));
            Assert.That(none.Disposition, Is.EqualTo(RewardProfileDisposition.ExplicitNoDrop));
            Assert.That(forced.GuaranteedEntries.Count, Is.EqualTo(1));
            Assert.That(forced.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKind.Scrap));
            Assert.That(appended.GuaranteedEntries.Count, Is.EqualTo(2));
        }

        [Test]
        public void ChangedProfileForSameRunAndSourceIsConflictingDuplicate()
        {
            GameplayDropOperation first = GameplayDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                MoneyProfile(),
                GameplayDropOverride.Default(
                    StableId.Parse("gameplay-drop-override.default")));
            RewardProfile changed = RewardProfile.Create(
                StableId.Parse("gameplay-drop-profile.changed"),
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        StableId.Parse("gameplay-drop-grant.scrap"),
                        RewardGrantKind.Scrap,
                        StableId.Parse("currency.scrap"),
                        1L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            GameplayDropOperation second = GameplayDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                changed,
                GameplayDropOverride.Default(
                    StableId.Parse("gameplay-drop-override.default")));

            Assert.That(
                RewardOperationIdentity.Classify(
                    first.OperationRequest,
                    second.OperationRequest),
                Is.EqualTo(RewardOperationIdentityComparison.ConflictingDuplicate));
        }

        private static RewardProfile MoneyProfile()
        {
            return RewardProfile.Create(
                StableId.Parse("gameplay-drop-profile.money"),
                new[]
                {
                    RewardGrantSpecification.CreateFixed(
                        StableId.Parse("gameplay-drop-grant.money"),
                        RewardGrantKind.Money,
                        StableId.Parse("currency.money"),
                        5L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
        }
    }
}
