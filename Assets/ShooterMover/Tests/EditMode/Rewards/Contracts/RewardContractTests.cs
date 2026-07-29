using System;
using NUnit.Framework;
using ShooterMover.Contracts.Economy;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.Contracts
{
    public sealed class RewardContractTests
    {
        private const string ContentFingerprint =
            "sha256:1111111111111111111111111111111111111111111111111111111111111111";

        [Test]
        public void RewardProfile_MoneyOnly_IsRepresentable()
        {
            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.money-only"),
                new[]
                {
                    FixedGrant("grant.money", RewardGrantKind.Money, "currency.money", 125L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            Assert.That(profile.Disposition, Is.EqualTo(RewardProfileDisposition.Configured));
            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(1));
            Assert.That(profile.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKind.Money));
        }

        [Test]
        public void RewardProfile_StrongboxOnly_IsRepresentable()
        {
            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.strongbox-only"),
                new[]
                {
                    FixedGrant(
                        "grant.strongbox",
                        RewardGrantKind.Strongbox,
                        "strongbox-definition.tier-three",
                        1L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            Assert.That(profile.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKind.Strongbox));
            Assert.That(
                profile.GuaranteedEntries[0].ContentStableId,
                Is.EqualTo(Id("strongbox-definition.tier-three")));
        }

        [Test]
        public void RewardProfile_MiscAndPremiumAmmoOnly_IsRepresentable()
        {
            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.misc-ammo-only"),
                new[]
                {
                    FixedGrant(
                        "grant.misc",
                        RewardGrantKind.Miscellaneous,
                        "item.repair-token",
                        2L),
                    FixedGrant(
                        "grant.premium-ammo",
                        RewardGrantKind.PremiumAmmo,
                        "item.premium-ammo",
                        8L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(2));
            Assert.That(
                profile.GuaranteedEntries[0].Kind,
                Is.EqualTo(RewardGrantKind.Miscellaneous));
            Assert.That(
                profile.GuaranteedEntries[1].Kind,
                Is.EqualTo(RewardGrantKind.PremiumAmmo));
        }

        [Test]
        public void RewardProfile_MixedKinds_AreRepresentableWithoutProductSpecificType()
        {
            RewardGrantKind[] kinds =
            {
                RewardGrantKind.Money,
                RewardGrantKind.Scrap,
                RewardGrantKind.Strongbox,
                RewardGrantKind.EquipmentReference,
                RewardGrantKind.PremiumAmmo,
                RewardGrantKind.Miscellaneous,
            };
            RewardGrantSpecification[] grants = new RewardGrantSpecification[kinds.Length];
            for (int index = 0; index < kinds.Length; index++)
            {
                grants[index] = FixedGrant(
                    "grant.mixed-" + index,
                    kinds[index],
                    "content.mixed-" + index,
                    1L);
            }

            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.mixed"),
                grants,
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(kinds.Length));
        }

        [Test]
        public void RewardProfile_GuaranteedIndependentAndExclusive_Coexist()
        {
            RewardGrantSpecification guaranteed = FixedGrant(
                "grant.guaranteed-money",
                RewardGrantKind.Money,
                "currency.money",
                10L);
            IndependentRewardRoll independent = IndependentRewardRoll.Create(
                Id("reward-roll.scrap"),
                250000,
                FixedGrant("grant.scrap", RewardGrantKind.Scrap, "currency.scrap", 3L));
            ExclusiveRewardGroup exclusive = ExclusiveRewardGroup.Create(
                Id("reward-group.side-reward"),
                new[]
                {
                    WeightedRewardOutcome.CreateGrant(
                        Id("reward-outcome.equipment"),
                        2L,
                        FixedGrant(
                            "grant.equipment",
                            RewardGrantKind.EquipmentReference,
                            "equipment-definition.blaster",
                            1L)),
                    WeightedRewardOutcome.CreateExplicitNoDrop(
                        Id("reward-outcome.no-drop"),
                        3L),
                });

            RewardProfile profile = RewardProfile.Create(
                Id("reward-profile.combined"),
                new[] { guaranteed },
                new[] { independent },
                new[] { exclusive });

            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(1));
            Assert.That(profile.IndependentRolls.Count, Is.EqualTo(1));
            Assert.That(profile.ExclusiveGroups.Count, Is.EqualTo(1));
            Assert.That(
                profile.ExclusiveGroups[0].Outcomes[1].Kind,
                Is.EqualTo(WeightedRewardOutcomeKind.ExplicitNoDrop));
        }

        [Test]
        public void RewardProfile_ExplicitNoDrop_IsDistinctFromAccidentalEmptyConfiguration()
        {
            RewardProfile noDrop = RewardProfile.CreateExplicitNoDrop(
                Id("reward-profile.no-drop"));

            Assert.That(noDrop.Disposition, Is.EqualTo(RewardProfileDisposition.ExplicitNoDrop));
            Assert.That(noDrop.GuaranteedEntries, Is.Empty);
            Assert.Throws<ArgumentException>(
                () => RewardProfile.Create(
                    Id("reward-profile.accidental-empty"),
                    Array.Empty<RewardGrantSpecification>(),
                    Array.Empty<IndependentRewardRoll>(),
                    Array.Empty<ExclusiveRewardGroup>()));
        }

        [Test]
        public void RewardProfile_CanonicalFingerprint_IsStableAcrossInputOrder()
        {
            RewardGrantSpecification money = FixedGrant(
                "grant.money",
                RewardGrantKind.Money,
                "currency.money",
                10L);
            RewardGrantSpecification scrap = FixedGrant(
                "grant.scrap",
                RewardGrantKind.Scrap,
                "currency.scrap",
                4L);

            RewardProfile first = RewardProfile.Create(
                Id("reward-profile.order-stable"),
                new[] { money, scrap },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            RewardProfile second = RewardProfile.Create(
                Id("reward-profile.order-stable"),
                new[] { scrap, money },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.Fingerprint, Does.StartWith("sha256:"));
        }

        [Test]
        public void RewardGrantFingerprint_ChangesWithIdentityContentQuantityOrScalingInput()
        {
            RewardGrantSpecification baseline = RewardGrantSpecification.Create(
                Id("grant.scaled"),
                RewardGrantKind.Money,
                Id("currency.money"),
                RewardQuantityRange.Create(5L, 10L),
                new[]
                {
                    RewardScalingInputDescriptor.Create(
                        Id("scaling-input.character-level"),
                        RewardScalingInputKind.CharacterLevel),
                });
            RewardGrantSpecification changedIdentity = RewardGrantSpecification.Create(
                Id("grant.changed"),
                baseline.Kind,
                baseline.ContentStableId,
                baseline.Quantity,
                baseline.ScalingInputs);
            RewardGrantSpecification changedContent = RewardGrantSpecification.Create(
                baseline.GrantStableId,
                baseline.Kind,
                Id("currency.scrap"),
                baseline.Quantity,
                baseline.ScalingInputs);
            RewardGrantSpecification changedQuantity = RewardGrantSpecification.Create(
                baseline.GrantStableId,
                baseline.Kind,
                baseline.ContentStableId,
                RewardQuantityRange.Create(5L, 11L),
                baseline.ScalingInputs);
            RewardGrantSpecification changedScaling = RewardGrantSpecification.Create(
                baseline.GrantStableId,
                baseline.Kind,
                baseline.ContentStableId,
                baseline.Quantity,
                new[]
                {
                    RewardScalingInputDescriptor.Create(
                        Id("scaling-input.region-level"),
                        RewardScalingInputKind.RegionLevel),
                });

            Assert.That(changedIdentity.Fingerprint, Is.Not.EqualTo(baseline.Fingerprint));
            Assert.That(changedContent.Fingerprint, Is.Not.EqualTo(baseline.Fingerprint));
            Assert.That(changedQuantity.Fingerprint, Is.Not.EqualTo(baseline.Fingerprint));
            Assert.That(changedScaling.Fingerprint, Is.Not.EqualTo(baseline.Fingerprint));
        }

        [Test]
        public void RewardProfile_MalformedQuantitiesProbabilitiesAndWeights_AreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RewardQuantityRange.Create(0L, 1L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RewardQuantityRange.Create(2L, 1L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => IndependentRewardRoll.Create(
                    Id("reward-roll.invalid"),
                    0,
                    FixedGrant("grant.roll", RewardGrantKind.Money, "currency.money", 1L)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => IndependentRewardRoll.Create(
                    Id("reward-roll.invalid-high"),
                    IndependentRewardRoll.ProbabilityScale + 1,
                    FixedGrant("grant.roll-high", RewardGrantKind.Money, "currency.money", 1L)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WeightedRewardOutcome.CreateExplicitNoDrop(
                    Id("reward-outcome.invalid"),
                    0L));
        }

        [Test]
        public void RewardProfile_DuplicateGrantIdentitiesAcrossSections_AreRejected()
        {
            RewardGrantSpecification guaranteed = FixedGrant(
                "grant.duplicate",
                RewardGrantKind.Money,
                "currency.money",
                1L);
            IndependentRewardRoll independent = IndependentRewardRoll.Create(
                Id("reward-roll.duplicate"),
                500000,
                FixedGrant(
                    "grant.duplicate",
                    RewardGrantKind.Scrap,
                    "currency.scrap",
                    1L));

            Assert.Throws<ArgumentException>(
                () => RewardProfile.Create(
                    Id("reward-profile.duplicate"),
                    new[] { guaranteed },
                    new[] { independent },
                    Array.Empty<ExclusiveRewardGroup>()));
        }

        [Test]
        public void LootSourceOverride_AllModesResolveDeterministically()
        {
            RewardProfile inherited = RewardProfile.Create(
                Id("reward-profile.default"),
                new[]
                {
                    FixedGrant("grant.default", RewardGrantKind.Money, "currency.money", 5L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            RewardProfile replacement = RewardProfile.Create(
                Id("reward-profile.replacement"),
                new[]
                {
                    FixedGrant(
                        "grant.replacement",
                        RewardGrantKind.Strongbox,
                        "strongbox-definition.tier-one",
                        1L),
                },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            RewardGrantSpecification appended = FixedGrant(
                "grant.appended",
                RewardGrantKind.Scrap,
                "currency.scrap",
                2L);

            LootSourceOverride inherit = LootSourceOverride.Inherit(
                Id("reward-override.inherit"),
                Id("source.crate-a"));
            LootSourceOverride noReward = LootSourceOverride.NoReward(
                Id("reward-override.none"),
                Id("source.crate-a"),
                Id("reward-profile.resolved-none"));
            LootSourceOverride replace = LootSourceOverride.ReplaceEntirely(
                Id("reward-override.replace"),
                Id("source.crate-a"),
                replacement);
            LootSourceOverride append = LootSourceOverride.AppendGuaranteedEntries(
                Id("reward-override.append"),
                Id("source.crate-a"),
                Id("reward-profile.resolved-append"),
                new[] { appended });

            Assert.That(inherit.Resolve(inherited), Is.SameAs(inherited));
            Assert.That(
                noReward.Resolve(inherited).Disposition,
                Is.EqualTo(RewardProfileDisposition.ExplicitNoDrop));
            Assert.That(replace.Resolve(inherited), Is.SameAs(replacement));
            RewardProfile appendedFirst = append.Resolve(inherited);
            RewardProfile appendedSecond = append.Resolve(inherited);
            Assert.That(appendedFirst.GuaranteedEntries.Count, Is.EqualTo(2));
            Assert.That(appendedFirst.Fingerprint, Is.EqualTo(appendedSecond.Fingerprint));
        }

        [Test]
        public void LootSourceOverride_AppendDuplicateGrantIdentity_IsRejectedDuringResolution()
        {
            RewardGrantSpecification grant = FixedGrant(
                "grant.same",
                RewardGrantKind.Money,
                "currency.money",
                1L);
            RewardProfile inherited = RewardProfile.Create(
                Id("reward-profile.default-duplicate"),
                new[] { grant },
                Array.Empty<IndependentRewardRoll>(),
                Array.Empty<ExclusiveRewardGroup>());
            LootSourceOverride append = LootSourceOverride.AppendGuaranteedEntries(
                Id("reward-override.append-duplicate"),
                Id("source.crate-b"),
                Id("reward-profile.append-duplicate"),
                new[] { grant });

            Assert.Throws<ArgumentException>(() => append.Resolve(inherited));
        }

        [Test]
        public void RewardOperationIdentity_SamePayloadIsExactDuplicate_ChangedPayloadIsConflict()
        {
            RewardOperationRequest baseline = OperationRequest(
                "source-operation.drop-a",
                "reward-profile.default");
            RewardOperationRequest exact = OperationRequest(
                "source-operation.drop-a",
                "reward-profile.default");
            RewardOperationRequest conflict = OperationRequest(
                "source-operation.drop-a",
                "reward-profile.changed");
            RewardOperationRequest distinct = OperationRequest(
                "source-operation.drop-b",
                "reward-profile.default");

            Assert.That(
                RewardOperationIdentity.Classify(baseline, exact),
                Is.EqualTo(RewardOperationIdentityComparison.ExactDuplicateNoChange));
            Assert.That(
                RewardOperationIdentity.Classify(baseline, conflict),
                Is.EqualTo(RewardOperationIdentityComparison.ConflictingDuplicate));
            Assert.That(
                RewardOperationIdentity.Classify(baseline, distinct),
                Is.EqualTo(RewardOperationIdentityComparison.DistinctOperation));
        }

        [Test]
        public void RewardResult_CanonicalOrderingAndExplicitNoDrop_AreStable()
        {
            RewardGrant money = RewardGrant.Create(
                Id("grant.money-result"),
                RewardGrantKind.Money,
                Id("currency.money"),
                4L);
            RewardGrant scrap = RewardGrant.Create(
                Id("grant.scrap-result"),
                RewardGrantKind.Scrap,
                Id("currency.scrap"),
                2L);
            RewardResult first = RewardResult.CreateGrants(
                Id("commitment.result"),
                Id("source-operation.result"),
                new[] { scrap, money });
            RewardResult second = RewardResult.CreateGrants(
                Id("commitment.result"),
                Id("source-operation.result"),
                new[] { money, scrap });
            RewardResult noDrop = RewardResult.CreateExplicitNoDrop(
                Id("commitment.no-drop"),
                Id("source-operation.no-drop"));

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(noDrop.Disposition, Is.EqualTo(RewardResultDisposition.ExplicitNoDrop));
            Assert.Throws<ArgumentException>(
                () => RewardResult.CreateGrants(
                    Id("commitment.empty"),
                    Id("source-operation.empty"),
                    Array.Empty<RewardGrant>()));
            Assert.Throws<ArgumentException>(
                () => RewardResult.CreateGrants(
                    Id("commitment.duplicate"),
                    Id("source-operation.duplicate-result"),
                    new[] { money, money }));
        }

        [Test]
        public void RewardTrace_CanonicalOrderingIsStable_AndDuplicateOrdinalsAreRejected()
        {
            RewardTraceEntry firstEntry = RewardTraceEntry.Create(
                Id("trace-entry.first"),
                0,
                Id("trace-step.guaranteed"),
                Id("grant.money"),
                RewardTraceDecisionKind.Guaranteed,
                1L,
                1L);
            RewardTraceEntry secondEntry = RewardTraceEntry.Create(
                Id("trace-entry.second"),
                1,
                Id("trace-step.quantity"),
                Id("grant.money"),
                RewardTraceDecisionKind.Quantity,
                1L,
                10L);
            RewardTrace first = RewardTrace.Create(
                Id("source-operation.trace"),
                new[] { secondEntry, firstEntry });
            RewardTrace second = RewardTrace.Create(
                Id("source-operation.trace"),
                new[] { firstEntry, secondEntry });
            RewardTraceEntry duplicateOrdinal = RewardTraceEntry.Create(
                Id("trace-entry.duplicate-ordinal"),
                1,
                Id("trace-step.other"),
                Id("grant.scrap"),
                RewardTraceDecisionKind.GrantProduced,
                0L,
                1L);

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.Throws<ArgumentException>(
                () => RewardTrace.Create(
                    Id("source-operation.trace-invalid"),
                    new[] { secondEntry, duplicateOrdinal }));
        }

        [Test]
        public void EconomyTransactionIdentity_ExactAndConflictingDuplicatesAreUnambiguous()
        {
            EconomyTransactionCommand baseline = CurrencyCommand(
                "transaction.money-a",
                "operation.reward-a",
                10L);
            EconomyTransactionCommand exact = CurrencyCommand(
                "transaction.money-a",
                "operation.reward-a",
                10L);
            EconomyTransactionCommand conflict = CurrencyCommand(
                "transaction.money-a",
                "operation.reward-a",
                11L);
            EconomyTransactionCommand distinct = CurrencyCommand(
                "transaction.money-b",
                "operation.reward-a",
                10L);

            Assert.That(
                EconomyTransactionIdentity.Classify(baseline, exact),
                Is.EqualTo(EconomyTransactionIdentityComparison.ExactDuplicateNoChange));
            Assert.That(
                EconomyTransactionIdentity.Classify(baseline, conflict),
                Is.EqualTo(EconomyTransactionIdentityComparison.ConflictingDuplicate));
            Assert.That(
                EconomyTransactionIdentity.Classify(baseline, distinct),
                Is.EqualTo(EconomyTransactionIdentityComparison.DistinctTransaction));
            Assert.That(
                EconomyTransactionStatus.ExactDuplicateNoChange,
                Is.Not.EqualTo(EconomyTransactionStatus.ConflictingDuplicate));
        }

        [Test]
        public void EconomyTransactionCommand_RejectsMalformedQuantityAndResourceShape()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CurrencyCommand("transaction.zero", "operation.zero", 0L));
            Assert.Throws<ArgumentNullException>(
                () => EconomyTransactionCommand.Create(
                    Id("transaction.unique-missing-instance"),
                    Id("operation.unique-missing-instance"),
                    Id("authority.holdings"),
                    EconomyTransactionOperation.AddUnique,
                    EconomyResourceKind.Strongbox,
                    Id("strongbox-definition.tier-one"),
                    null,
                    1L,
                    null));
            Assert.Throws<ArgumentException>(
                () => EconomyTransactionCommand.Create(
                    Id("transaction.wrong-resource-shape"),
                    Id("operation.wrong-resource-shape"),
                    Id("authority.money"),
                    EconomyTransactionOperation.Credit,
                    EconomyResourceKind.Item,
                    Id("item.token"),
                    null,
                    1L,
                    null));
        }

        [Test]
        public void EconomyTransactionResult_EncodesAppliedDuplicateConflictAndAdmissionFailures()
        {
            EconomyTransactionCommand command = CurrencyCommand(
                "transaction.status",
                "operation.status",
                5L);
            EconomyTransactionStatus[] unchangedStatuses =
            {
                EconomyTransactionStatus.ExactDuplicateNoChange,
                EconomyTransactionStatus.ConflictingDuplicate,
                EconomyTransactionStatus.InvalidRequest,
                EconomyTransactionStatus.InsufficientValue,
                EconomyTransactionStatus.InsufficientCapacity,
                EconomyTransactionStatus.ExpectedSequenceConflict,
            };

            EconomyTransactionResult applied = EconomyTransactionResult.Create(
                command.TransactionStableId,
                EconomyTransactionStatus.Applied,
                command.PayloadFingerprint,
                4L,
                5L,
                25L);
            Assert.That(applied.CurrentSequence, Is.EqualTo(5L));

            for (int index = 0; index < unchangedStatuses.Length; index++)
            {
                EconomyTransactionResult result = EconomyTransactionResult.Create(
                    command.TransactionStableId,
                    unchangedStatuses[index],
                    command.PayloadFingerprint,
                    4L,
                    4L,
                    20L);
                Assert.That(result.Status, Is.EqualTo(unchangedStatuses[index]));
            }
        }

        [Test]
        public void StrongboxOpeningEnvelope_CarriesStableIdentityWithoutOpeningBehavior()
        {
            StrongboxOpeningRequest request = StrongboxOpeningRequest.Create(
                Id("run.alpha"),
                Id("opening-operation.box-a"),
                Id("transaction.box-a"),
                Id("strongbox-instance.box-a"),
                Id("strongbox-definition.tier-two"),
                Id("commitment.box-a"),
                Id("reward-profile.box-tier-two"),
                ContentFingerprint,
                7L);
            RewardResult reward = RewardResult.CreateGrants(
                Id("commitment.box-a"),
                Id("opening-operation.box-a"),
                new[]
                {
                    RewardGrant.Create(
                        Id("grant.box-scrap"),
                        RewardGrantKind.Scrap,
                        Id("currency.scrap"),
                        4L),
                });
            RewardTrace trace = RewardTrace.Create(
                Id("opening-operation.box-a"),
                new[]
                {
                    RewardTraceEntry.Create(
                        Id("trace-entry.box-a"),
                        0,
                        Id("trace-step.box-side-reward"),
                        Id("strongbox-instance.box-a"),
                        RewardTraceDecisionKind.GrantProduced,
                        0L,
                        1L),
                });
            StrongboxOpeningResult opened = StrongboxOpeningResult.Create(
                request.OpeningOperationStableId,
                StrongboxOpeningStatus.Opened,
                request.Fingerprint,
                reward,
                trace,
                7L,
                8L);
            StrongboxOpeningResult rejected = StrongboxOpeningResult.Create(
                request.OpeningOperationStableId,
                StrongboxOpeningStatus.StrongboxNotOwned,
                request.Fingerprint,
                null,
                null,
                7L,
                7L);

            Assert.That(opened.RewardResult, Is.SameAs(reward));
            Assert.That(opened.Trace, Is.SameAs(trace));
            Assert.That(rejected.RewardResult, Is.Null);
            Assert.That(request.Fingerprint, Does.StartWith("sha256:"));
        }

        [Test]
        public void StrongboxOpeningEnvelope_RejectsMismatchedOperationIdentity()
        {
            RewardResult reward = RewardResult.CreateExplicitNoDrop(
                Id("commitment.mismatch"),
                Id("opening-operation.other"));
            RewardTrace trace = RewardTrace.Create(
                Id("opening-operation.other"),
                Array.Empty<RewardTraceEntry>());

            Assert.Throws<ArgumentException>(
                () => StrongboxOpeningResult.Create(
                    Id("opening-operation.expected"),
                    StrongboxOpeningStatus.Opened,
                    ContentFingerprint,
                    reward,
                    trace,
                    0L,
                    1L));
        }

        private static StableId Id(string canonical)
        {
            return StableId.Parse(canonical);
        }

        private static RewardGrantSpecification FixedGrant(
            string grantId,
            RewardGrantKind kind,
            string contentId,
            long quantity)
        {
            return RewardGrantSpecification.CreateFixed(
                Id(grantId),
                kind,
                Id(contentId),
                quantity);
        }

        private static RewardOperationRequest OperationRequest(
            string operationId,
            string profileId)
        {
            return RewardOperationRequest.Create(
                Id("run.alpha"),
                Id("source.crate-a"),
                Id(operationId),
                Id("commitment.crate-a"),
                Id(profileId),
                ContentFingerprint);
        }

        private static EconomyTransactionCommand CurrencyCommand(
            string transactionId,
            string operationId,
            long quantity)
        {
            return EconomyTransactionCommand.Create(
                Id(transactionId),
                Id(operationId),
                Id("authority.money"),
                EconomyTransactionOperation.Credit,
                EconomyResourceKind.Currency,
                Id("currency.money"),
                null,
                quantity,
                null);
        }
    }
}
