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
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.money-only"),
                new[]
                {
                    FixedGrant("grant.money", RewardGrantKindV1.Money, "currency.money", 125L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            Assert.That(profile.Disposition, Is.EqualTo(RewardProfileDispositionV1.Configured));
            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(1));
            Assert.That(profile.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKindV1.Money));
        }

        [Test]
        public void RewardProfile_StrongboxOnly_IsRepresentable()
        {
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.strongbox-only"),
                new[]
                {
                    FixedGrant(
                        "grant.strongbox",
                        RewardGrantKindV1.Strongbox,
                        "strongbox-definition.tier-three",
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            Assert.That(profile.GuaranteedEntries[0].Kind, Is.EqualTo(RewardGrantKindV1.Strongbox));
            Assert.That(
                profile.GuaranteedEntries[0].ContentStableId,
                Is.EqualTo(Id("strongbox-definition.tier-three")));
        }

        [Test]
        public void RewardProfile_MiscAndPremiumAmmoOnly_IsRepresentable()
        {
            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.misc-ammo-only"),
                new[]
                {
                    FixedGrant(
                        "grant.misc",
                        RewardGrantKindV1.Miscellaneous,
                        "item.repair-token",
                        2L),
                    FixedGrant(
                        "grant.premium-ammo",
                        RewardGrantKindV1.PremiumAmmo,
                        "item.premium-ammo",
                        8L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(2));
            Assert.That(
                profile.GuaranteedEntries[0].Kind,
                Is.EqualTo(RewardGrantKindV1.Miscellaneous));
            Assert.That(
                profile.GuaranteedEntries[1].Kind,
                Is.EqualTo(RewardGrantKindV1.PremiumAmmo));
        }

        [Test]
        public void RewardProfile_MixedKinds_AreRepresentableWithoutProductSpecificType()
        {
            RewardGrantKindV1[] kinds =
            {
                RewardGrantKindV1.Money,
                RewardGrantKindV1.Scrap,
                RewardGrantKindV1.Strongbox,
                RewardGrantKindV1.EquipmentReference,
                RewardGrantKindV1.PremiumAmmo,
                RewardGrantKindV1.Miscellaneous,
            };
            RewardGrantSpecificationV1[] grants = new RewardGrantSpecificationV1[kinds.Length];
            for (int index = 0; index < kinds.Length; index++)
            {
                grants[index] = FixedGrant(
                    "grant.mixed-" + index,
                    kinds[index],
                    "content.mixed-" + index,
                    1L);
            }

            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.mixed"),
                grants,
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(kinds.Length));
        }

        [Test]
        public void RewardProfile_GuaranteedIndependentAndExclusive_Coexist()
        {
            RewardGrantSpecificationV1 guaranteed = FixedGrant(
                "grant.guaranteed-money",
                RewardGrantKindV1.Money,
                "currency.money",
                10L);
            IndependentRewardRollV1 independent = IndependentRewardRollV1.Create(
                Id("reward-roll.scrap"),
                250000,
                FixedGrant("grant.scrap", RewardGrantKindV1.Scrap, "currency.scrap", 3L));
            ExclusiveRewardGroupV1 exclusive = ExclusiveRewardGroupV1.Create(
                Id("reward-group.side-reward"),
                new[]
                {
                    WeightedRewardOutcomeV1.CreateGrant(
                        Id("reward-outcome.equipment"),
                        2L,
                        FixedGrant(
                            "grant.equipment",
                            RewardGrantKindV1.EquipmentReference,
                            "equipment-definition.blaster",
                            1L)),
                    WeightedRewardOutcomeV1.CreateExplicitNoDrop(
                        Id("reward-outcome.no-drop"),
                        3L),
                });

            RewardProfileV1 profile = RewardProfileV1.Create(
                Id("reward-profile.combined"),
                new[] { guaranteed },
                new[] { independent },
                new[] { exclusive });

            Assert.That(profile.GuaranteedEntries.Count, Is.EqualTo(1));
            Assert.That(profile.IndependentRolls.Count, Is.EqualTo(1));
            Assert.That(profile.ExclusiveGroups.Count, Is.EqualTo(1));
            Assert.That(
                profile.ExclusiveGroups[0].Outcomes[1].Kind,
                Is.EqualTo(WeightedRewardOutcomeKindV1.ExplicitNoDrop));
        }

        [Test]
        public void RewardProfile_ExplicitNoDrop_IsDistinctFromAccidentalEmptyConfiguration()
        {
            RewardProfileV1 noDrop = RewardProfileV1.CreateExplicitNoDrop(
                Id("reward-profile.no-drop"));

            Assert.That(noDrop.Disposition, Is.EqualTo(RewardProfileDispositionV1.ExplicitNoDrop));
            Assert.That(noDrop.GuaranteedEntries, Is.Empty);
            Assert.Throws<ArgumentException>(
                () => RewardProfileV1.Create(
                    Id("reward-profile.accidental-empty"),
                    Array.Empty<RewardGrantSpecificationV1>(),
                    Array.Empty<IndependentRewardRollV1>(),
                    Array.Empty<ExclusiveRewardGroupV1>()));
        }

        [Test]
        public void RewardProfile_CanonicalFingerprint_IsStableAcrossInputOrder()
        {
            RewardGrantSpecificationV1 money = FixedGrant(
                "grant.money",
                RewardGrantKindV1.Money,
                "currency.money",
                10L);
            RewardGrantSpecificationV1 scrap = FixedGrant(
                "grant.scrap",
                RewardGrantKindV1.Scrap,
                "currency.scrap",
                4L);

            RewardProfileV1 first = RewardProfileV1.Create(
                Id("reward-profile.order-stable"),
                new[] { money, scrap },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 second = RewardProfileV1.Create(
                Id("reward-profile.order-stable"),
                new[] { scrap, money },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());

            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.Fingerprint, Does.StartWith("sha256:"));
        }

        [Test]
        public void RewardGrantFingerprint_ChangesWithIdentityContentQuantityOrScalingInput()
        {
            RewardGrantSpecificationV1 baseline = RewardGrantSpecificationV1.Create(
                Id("grant.scaled"),
                RewardGrantKindV1.Money,
                Id("currency.money"),
                RewardQuantityRangeV1.Create(5L, 10L),
                new[]
                {
                    RewardScalingInputDescriptorV1.Create(
                        Id("scaling-input.character-level"),
                        RewardScalingInputKindV1.CharacterLevel),
                });
            RewardGrantSpecificationV1 changedIdentity = RewardGrantSpecificationV1.Create(
                Id("grant.changed"),
                baseline.Kind,
                baseline.ContentStableId,
                baseline.Quantity,
                baseline.ScalingInputs);
            RewardGrantSpecificationV1 changedContent = RewardGrantSpecificationV1.Create(
                baseline.GrantStableId,
                baseline.Kind,
                Id("currency.scrap"),
                baseline.Quantity,
                baseline.ScalingInputs);
            RewardGrantSpecificationV1 changedQuantity = RewardGrantSpecificationV1.Create(
                baseline.GrantStableId,
                baseline.Kind,
                baseline.ContentStableId,
                RewardQuantityRangeV1.Create(5L, 11L),
                baseline.ScalingInputs);
            RewardGrantSpecificationV1 changedScaling = RewardGrantSpecificationV1.Create(
                baseline.GrantStableId,
                baseline.Kind,
                baseline.ContentStableId,
                baseline.Quantity,
                new[]
                {
                    RewardScalingInputDescriptorV1.Create(
                        Id("scaling-input.region-level"),
                        RewardScalingInputKindV1.RegionLevel),
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
                () => RewardQuantityRangeV1.Create(0L, 1L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => RewardQuantityRangeV1.Create(2L, 1L));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => IndependentRewardRollV1.Create(
                    Id("reward-roll.invalid"),
                    0,
                    FixedGrant("grant.roll", RewardGrantKindV1.Money, "currency.money", 1L)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => IndependentRewardRollV1.Create(
                    Id("reward-roll.invalid-high"),
                    IndependentRewardRollV1.ProbabilityScale + 1,
                    FixedGrant("grant.roll-high", RewardGrantKindV1.Money, "currency.money", 1L)));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => WeightedRewardOutcomeV1.CreateExplicitNoDrop(
                    Id("reward-outcome.invalid"),
                    0L));
        }

        [Test]
        public void RewardProfile_DuplicateGrantIdentitiesAcrossSections_AreRejected()
        {
            RewardGrantSpecificationV1 guaranteed = FixedGrant(
                "grant.duplicate",
                RewardGrantKindV1.Money,
                "currency.money",
                1L);
            IndependentRewardRollV1 independent = IndependentRewardRollV1.Create(
                Id("reward-roll.duplicate"),
                500000,
                FixedGrant(
                    "grant.duplicate",
                    RewardGrantKindV1.Scrap,
                    "currency.scrap",
                    1L));

            Assert.Throws<ArgumentException>(
                () => RewardProfileV1.Create(
                    Id("reward-profile.duplicate"),
                    new[] { guaranteed },
                    new[] { independent },
                    Array.Empty<ExclusiveRewardGroupV1>()));
        }

        [Test]
        public void RewardSourceOverride_AllModesResolveDeterministically()
        {
            RewardProfileV1 inherited = RewardProfileV1.Create(
                Id("reward-profile.default"),
                new[]
                {
                    FixedGrant("grant.default", RewardGrantKindV1.Money, "currency.money", 5L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardProfileV1 replacement = RewardProfileV1.Create(
                Id("reward-profile.replacement"),
                new[]
                {
                    FixedGrant(
                        "grant.replacement",
                        RewardGrantKindV1.Strongbox,
                        "strongbox-definition.tier-one",
                        1L),
                },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardGrantSpecificationV1 appended = FixedGrant(
                "grant.appended",
                RewardGrantKindV1.Scrap,
                "currency.scrap",
                2L);

            RewardSourceOverrideV1 inherit = RewardSourceOverrideV1.Inherit(
                Id("reward-override.inherit"),
                Id("source.crate-a"));
            RewardSourceOverrideV1 noReward = RewardSourceOverrideV1.NoReward(
                Id("reward-override.none"),
                Id("source.crate-a"),
                Id("reward-profile.resolved-none"));
            RewardSourceOverrideV1 replace = RewardSourceOverrideV1.ReplaceEntirely(
                Id("reward-override.replace"),
                Id("source.crate-a"),
                replacement);
            RewardSourceOverrideV1 append = RewardSourceOverrideV1.AppendGuaranteedEntries(
                Id("reward-override.append"),
                Id("source.crate-a"),
                Id("reward-profile.resolved-append"),
                new[] { appended });

            Assert.That(inherit.Resolve(inherited), Is.SameAs(inherited));
            Assert.That(
                noReward.Resolve(inherited).Disposition,
                Is.EqualTo(RewardProfileDispositionV1.ExplicitNoDrop));
            Assert.That(replace.Resolve(inherited), Is.SameAs(replacement));
            RewardProfileV1 appendedFirst = append.Resolve(inherited);
            RewardProfileV1 appendedSecond = append.Resolve(inherited);
            Assert.That(appendedFirst.GuaranteedEntries.Count, Is.EqualTo(2));
            Assert.That(appendedFirst.Fingerprint, Is.EqualTo(appendedSecond.Fingerprint));
        }

        [Test]
        public void RewardSourceOverride_AppendDuplicateGrantIdentity_IsRejectedDuringResolution()
        {
            RewardGrantSpecificationV1 grant = FixedGrant(
                "grant.same",
                RewardGrantKindV1.Money,
                "currency.money",
                1L);
            RewardProfileV1 inherited = RewardProfileV1.Create(
                Id("reward-profile.default-duplicate"),
                new[] { grant },
                Array.Empty<IndependentRewardRollV1>(),
                Array.Empty<ExclusiveRewardGroupV1>());
            RewardSourceOverrideV1 append = RewardSourceOverrideV1.AppendGuaranteedEntries(
                Id("reward-override.append-duplicate"),
                Id("source.crate-b"),
                Id("reward-profile.append-duplicate"),
                new[] { grant });

            Assert.Throws<ArgumentException>(() => append.Resolve(inherited));
        }

        [Test]
        public void RewardOperationIdentity_SamePayloadIsExactDuplicate_ChangedPayloadIsConflict()
        {
            RewardOperationRequestV1 baseline = OperationRequest(
                "source-operation.drop-a",
                "reward-profile.default");
            RewardOperationRequestV1 exact = OperationRequest(
                "source-operation.drop-a",
                "reward-profile.default");
            RewardOperationRequestV1 conflict = OperationRequest(
                "source-operation.drop-a",
                "reward-profile.changed");
            RewardOperationRequestV1 distinct = OperationRequest(
                "source-operation.drop-b",
                "reward-profile.default");

            Assert.That(
                RewardOperationIdentityV1.Classify(baseline, exact),
                Is.EqualTo(RewardOperationIdentityComparisonV1.ExactDuplicateNoChange));
            Assert.That(
                RewardOperationIdentityV1.Classify(baseline, conflict),
                Is.EqualTo(RewardOperationIdentityComparisonV1.ConflictingDuplicate));
            Assert.That(
                RewardOperationIdentityV1.Classify(baseline, distinct),
                Is.EqualTo(RewardOperationIdentityComparisonV1.DistinctOperation));
        }

        [Test]
        public void RewardResult_CanonicalOrderingAndExplicitNoDrop_AreStable()
        {
            RewardGrantV1 money = RewardGrantV1.Create(
                Id("grant.money-result"),
                RewardGrantKindV1.Money,
                Id("currency.money"),
                4L);
            RewardGrantV1 scrap = RewardGrantV1.Create(
                Id("grant.scrap-result"),
                RewardGrantKindV1.Scrap,
                Id("currency.scrap"),
                2L);
            RewardResultV1 first = RewardResultV1.CreateGrants(
                Id("commitment.result"),
                Id("source-operation.result"),
                new[] { scrap, money });
            RewardResultV1 second = RewardResultV1.CreateGrants(
                Id("commitment.result"),
                Id("source-operation.result"),
                new[] { money, scrap });
            RewardResultV1 noDrop = RewardResultV1.CreateExplicitNoDrop(
                Id("commitment.no-drop"),
                Id("source-operation.no-drop"));

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(noDrop.Disposition, Is.EqualTo(RewardResultDispositionV1.ExplicitNoDrop));
            Assert.Throws<ArgumentException>(
                () => RewardResultV1.CreateGrants(
                    Id("commitment.empty"),
                    Id("source-operation.empty"),
                    Array.Empty<RewardGrantV1>()));
            Assert.Throws<ArgumentException>(
                () => RewardResultV1.CreateGrants(
                    Id("commitment.duplicate"),
                    Id("source-operation.duplicate-result"),
                    new[] { money, money }));
        }

        [Test]
        public void RewardTrace_CanonicalOrderingIsStable_AndDuplicateOrdinalsAreRejected()
        {
            RewardTraceEntryV1 firstEntry = RewardTraceEntryV1.Create(
                Id("trace-entry.first"),
                0,
                Id("trace-step.guaranteed"),
                Id("grant.money"),
                RewardTraceDecisionKindV1.Guaranteed,
                1L,
                1L);
            RewardTraceEntryV1 secondEntry = RewardTraceEntryV1.Create(
                Id("trace-entry.second"),
                1,
                Id("trace-step.quantity"),
                Id("grant.money"),
                RewardTraceDecisionKindV1.Quantity,
                1L,
                10L);
            RewardTraceV1 first = RewardTraceV1.Create(
                Id("source-operation.trace"),
                new[] { secondEntry, firstEntry });
            RewardTraceV1 second = RewardTraceV1.Create(
                Id("source-operation.trace"),
                new[] { firstEntry, secondEntry });
            RewardTraceEntryV1 duplicateOrdinal = RewardTraceEntryV1.Create(
                Id("trace-entry.duplicate-ordinal"),
                1,
                Id("trace-step.other"),
                Id("grant.scrap"),
                RewardTraceDecisionKindV1.GrantProduced,
                0L,
                1L);

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.Throws<ArgumentException>(
                () => RewardTraceV1.Create(
                    Id("source-operation.trace-invalid"),
                    new[] { secondEntry, duplicateOrdinal }));
        }

        [Test]
        public void EconomyTransactionIdentity_ExactAndConflictingDuplicatesAreUnambiguous()
        {
            EconomyTransactionCommandV1 baseline = CurrencyCommand(
                "transaction.money-a",
                "operation.reward-a",
                10L);
            EconomyTransactionCommandV1 exact = CurrencyCommand(
                "transaction.money-a",
                "operation.reward-a",
                10L);
            EconomyTransactionCommandV1 conflict = CurrencyCommand(
                "transaction.money-a",
                "operation.reward-a",
                11L);
            EconomyTransactionCommandV1 distinct = CurrencyCommand(
                "transaction.money-b",
                "operation.reward-a",
                10L);

            Assert.That(
                EconomyTransactionIdentityV1.Classify(baseline, exact),
                Is.EqualTo(EconomyTransactionIdentityComparisonV1.ExactDuplicateNoChange));
            Assert.That(
                EconomyTransactionIdentityV1.Classify(baseline, conflict),
                Is.EqualTo(EconomyTransactionIdentityComparisonV1.ConflictingDuplicate));
            Assert.That(
                EconomyTransactionIdentityV1.Classify(baseline, distinct),
                Is.EqualTo(EconomyTransactionIdentityComparisonV1.DistinctTransaction));
            Assert.That(
                EconomyTransactionStatusV1.ExactDuplicateNoChange,
                Is.Not.EqualTo(EconomyTransactionStatusV1.ConflictingDuplicate));
        }

        [Test]
        public void EconomyTransactionCommand_RejectsMalformedQuantityAndResourceShape()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => CurrencyCommand("transaction.zero", "operation.zero", 0L));
            Assert.Throws<ArgumentNullException>(
                () => EconomyTransactionCommandV1.Create(
                    Id("transaction.unique-missing-instance"),
                    Id("operation.unique-missing-instance"),
                    Id("authority.holdings"),
                    EconomyTransactionOperationV1.AddUnique,
                    EconomyResourceKindV1.Strongbox,
                    Id("strongbox-definition.tier-one"),
                    null,
                    1L,
                    null));
            Assert.Throws<ArgumentException>(
                () => EconomyTransactionCommandV1.Create(
                    Id("transaction.wrong-resource-shape"),
                    Id("operation.wrong-resource-shape"),
                    Id("authority.money"),
                    EconomyTransactionOperationV1.Credit,
                    EconomyResourceKindV1.Item,
                    Id("item.token"),
                    null,
                    1L,
                    null));
        }

        [Test]
        public void EconomyTransactionResult_EncodesAppliedDuplicateConflictAndAdmissionFailures()
        {
            EconomyTransactionCommandV1 command = CurrencyCommand(
                "transaction.status",
                "operation.status",
                5L);
            EconomyTransactionStatusV1[] unchangedStatuses =
            {
                EconomyTransactionStatusV1.ExactDuplicateNoChange,
                EconomyTransactionStatusV1.ConflictingDuplicate,
                EconomyTransactionStatusV1.InvalidRequest,
                EconomyTransactionStatusV1.InsufficientValue,
                EconomyTransactionStatusV1.InsufficientCapacity,
                EconomyTransactionStatusV1.ExpectedSequenceConflict,
            };

            EconomyTransactionResultV1 applied = EconomyTransactionResultV1.Create(
                command.TransactionStableId,
                EconomyTransactionStatusV1.Applied,
                command.PayloadFingerprint,
                4L,
                5L,
                25L);
            Assert.That(applied.CurrentSequence, Is.EqualTo(5L));

            for (int index = 0; index < unchangedStatuses.Length; index++)
            {
                EconomyTransactionResultV1 result = EconomyTransactionResultV1.Create(
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
            StrongboxOpeningRequestV1 request = StrongboxOpeningRequestV1.Create(
                Id("run.alpha"),
                Id("opening-operation.box-a"),
                Id("transaction.box-a"),
                Id("strongbox-instance.box-a"),
                Id("strongbox-definition.tier-two"),
                Id("commitment.box-a"),
                Id("reward-profile.box-tier-two"),
                ContentFingerprint,
                7L);
            RewardResultV1 reward = RewardResultV1.CreateGrants(
                Id("commitment.box-a"),
                Id("opening-operation.box-a"),
                new[]
                {
                    RewardGrantV1.Create(
                        Id("grant.box-scrap"),
                        RewardGrantKindV1.Scrap,
                        Id("currency.scrap"),
                        4L),
                });
            RewardTraceV1 trace = RewardTraceV1.Create(
                Id("opening-operation.box-a"),
                new[]
                {
                    RewardTraceEntryV1.Create(
                        Id("trace-entry.box-a"),
                        0,
                        Id("trace-step.box-side-reward"),
                        Id("strongbox-instance.box-a"),
                        RewardTraceDecisionKindV1.GrantProduced,
                        0L,
                        1L),
                });
            StrongboxOpeningResultV1 opened = StrongboxOpeningResultV1.Create(
                request.OpeningOperationStableId,
                StrongboxOpeningStatusV1.Opened,
                request.Fingerprint,
                reward,
                trace,
                7L,
                8L);
            StrongboxOpeningResultV1 rejected = StrongboxOpeningResultV1.Create(
                request.OpeningOperationStableId,
                StrongboxOpeningStatusV1.StrongboxNotOwned,
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
            RewardResultV1 reward = RewardResultV1.CreateExplicitNoDrop(
                Id("commitment.mismatch"),
                Id("opening-operation.other"));
            RewardTraceV1 trace = RewardTraceV1.Create(
                Id("opening-operation.other"),
                Array.Empty<RewardTraceEntryV1>());

            Assert.Throws<ArgumentException>(
                () => StrongboxOpeningResultV1.Create(
                    Id("opening-operation.expected"),
                    StrongboxOpeningStatusV1.Opened,
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

        private static RewardGrantSpecificationV1 FixedGrant(
            string grantId,
            RewardGrantKindV1 kind,
            string contentId,
            long quantity)
        {
            return RewardGrantSpecificationV1.CreateFixed(
                Id(grantId),
                kind,
                Id(contentId),
                quantity);
        }

        private static RewardOperationRequestV1 OperationRequest(
            string operationId,
            string profileId)
        {
            return RewardOperationRequestV1.Create(
                Id("run.alpha"),
                Id("source.crate-a"),
                Id(operationId),
                Id("commitment.crate-a"),
                Id(profileId),
                ContentFingerprint);
        }

        private static EconomyTransactionCommandV1 CurrencyCommand(
            string transactionId,
            string operationId,
            long quantity)
        {
            return EconomyTransactionCommandV1.Create(
                Id(transactionId),
                Id(operationId),
                Id("authority.money"),
                EconomyTransactionOperationV1.Credit,
                EconomyResourceKindV1.Currency,
                Id("currency.money"),
                null,
                quantity,
                null);
        }
    }
}
