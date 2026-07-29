using System;
using NUnit.Framework;
using ShooterMover.Application.Rewards.LootDrops;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.LootDrops
{
    public sealed class LootDropOperationTests
    {
        [Test]
        public void SameRunAndSourceProduceExactOperationIdentity()
        {
            RewardProfile profile = MoneyProfile();
            LootDropOverride dropOverride =
                LootDropOverride.Default(
                    StableId.Parse("gameplay-drop-override.default"));

            LootDropOperation first = LootDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);
            LootDropOperation second = LootDropOperationFactory.Create(
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
            LootDropOverride dropOverride =
                LootDropOverride.Default(
                    StableId.Parse("gameplay-drop-override.default"));

            LootDropOperation first = LootDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                profile,
                dropOverride);
            LootDropOperation second = LootDropOperationFactory.Create(
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

            RewardProfile defaultProfile = LootDropOverride.Default(
                StableId.Parse("gameplay-drop-override.default"))
                .Resolve(source, inherited);
            RewardProfile none = LootDropOverride.ForcedNone(
                StableId.Parse("gameplay-drop-override.none"),
                StableId.Parse("gameplay-drop-profile.none"))
                .Resolve(source, inherited);
            RewardProfile forced = LootDropOverride.ForcedSpecificReward(
                StableId.Parse("gameplay-drop-override.forced"),
                StableId.Parse("gameplay-drop-profile.forced"),
                scrap)
                .Resolve(source, inherited);
            RewardProfile appended = LootDropOverride.AppendGuaranteedReward(
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
            LootDropOperation first = LootDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                MoneyProfile(),
                LootDropOverride.Default(
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
            LootDropOperation second = LootDropOperationFactory.Create(
                StableId.Parse("run.test"),
                StableId.Parse("placed.enemy-a"),
                changed,
                LootDropOverride.Default(
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
