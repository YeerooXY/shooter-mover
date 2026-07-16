using System;
using System.Collections.Generic;
using NUnit.Framework;
using ShooterMover.Application.Rewards.Application;
using ShooterMover.Contracts.Rewards;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Tests.EditMode.Rewards.Application
{
    public sealed class RewardApplicationServiceV1Tests
    {
        private static readonly StableId RapAuthority = Id("authority.reward-application");
        private static readonly StableId MoneyAuthority = Id("authority.money-test");
        private static readonly StableId ScrapAuthority = Id("authority.scrap-test");
        private static readonly StableId HoldingsAuthority = Id("authority.holdings-test");
        private static readonly StableId MoneyCurrency = Id("currency.money-test");
        private static readonly StableId ScrapCurrency = Id("currency.scrap-test");

        [Test]
        public void MoneyOnlyRewardAppliesOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 25L));
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 first = fixture.Service.Claim(Claim(commit));
            RewardApplicationResultV1 duplicate = fixture.Service.Claim(Claim(commit));

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Value, Is.EqualTo(25L));
            Assert.That(fixture.Money.AppliedTransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void ScrapOnlyRewardAppliesOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 13L));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(13L));
        }

        [Test]
        public void StrongboxGrantAppliesToHoldingsOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                StrongboxGrant(
                    "grant.box",
                    "strongbox-definition.tier-one",
                    "strongbox-instance.one"));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(1L));
            Assert.That(fixture.Holdings.AppliedTransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void EquipmentGrantAppliesToHoldingsOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                EquipmentGrant(
                    "grant.equipment",
                    Equipment(
                        "equipment-instance.one",
                        "equipment-definition.blaster")));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(1L));
        }

        [Test]
        public void MiscellaneousStackAppliesOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant(
                    "grant.misc",
                    RewardGrantKindV1.Miscellaneous,
                    Id("misc.future-widget"),
                    7L));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(7L));
        }

        [Test]
        public void MixedRewardAppliesCompletely()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 20L),
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 4L),
                StrongboxGrant("grant.box", "strongbox-definition.tier-one", "strongbox-instance.mixed"),
                EquipmentGrant("grant.equipment", Equipment("equipment-instance.mixed", "equipment-definition.blaster")));
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(20L));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(4L));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(2L));
            Assert.That(result.CommitmentSnapshot.Children, Has.Count.EqualTo(4));
            Assert.That(result.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentStateV1.Applied));
        }

        [Test]
        public void ExactDuplicateSourceCallbackReturnsOriginalCommitment()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 3L));
            RewardApplicationResultV1 first = fixture.Service.Commit(commit);
            long sequence = fixture.Service.Sequence;
            RewardApplicationResultV1 duplicate = fixture.Service.Commit(commit);

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatusV1.Generated));
            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatusV1.ExactDuplicateNoChange));
            Assert.That(duplicate.CommitmentStableId, Is.EqualTo(commit.CommitmentStableId));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void ConflictingDuplicateSourceCallbackIsRejected()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 original = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 3L));
            RewardCommitCommandV1 conflict = CommitWith(
                original.Operation,
                original.GeneratedReward,
                Hash('x'),
                original.GrantPayloads);
            fixture.Service.Commit(original);

            RewardApplicationResultV1 result = fixture.Service.Commit(conflict);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.ConflictingDuplicate));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExactDuplicateProjectionIsNoChangeReplay()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardProjectCommandV1 projection = Projection(commit, "projection.pickup");
            fixture.Service.Project(projection);
            long sequence = fixture.Service.Sequence;

            RewardApplicationResultV1 duplicate = fixture.Service.Project(projection);

            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatusV1.ExactDuplicateNoChange));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void ExactDuplicateClaimCallbackIsNoChangeReplay()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);
            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatusV1.ClaimedPendingApplication));
            long sequence = fixture.Service.Sequence;

            RewardApplicationResultV1 duplicate = fixture.Service.Claim(claim);

            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatusV1.ExactDuplicateNoChange));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
            Assert.That(fixture.Scrap.ApplyCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingDuplicateClaimIdentityIsRejected()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 original = Claim(commit);
            fixture.Service.Claim(original);
            RewardClaimCommandV1 conflict = RewardClaimCommandV1.Create(
                original.ClaimStableId,
                commit.CommitmentStableId,
                Id("player.someone-else"),
                MoneyAuthority,
                ScrapAuthority,
                HoldingsAuthority);

            RewardApplicationResultV1 result = fixture.Service.Claim(conflict);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.ConflictingDuplicate));
        }

        [Test]
        public void FailedPreflightLeavesAllAuthoritiesUnchanged()
        {
            Fixture fixture = CreateFixture();
            fixture.Holdings.PreflightRejection = RewardAuthorityAdmissionStatusV1.CapacityRejected;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 10L),
                ValueGrant("grant.misc", RewardGrantKindV1.Miscellaneous, Id("misc.full"), 1L));
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.CapacityRejected));
            Assert.That(fixture.Money.Value, Is.Zero);
            Assert.That(fixture.Holdings.Value, Is.Zero);
            Assert.That(fixture.Money.ApplyCallCount, Is.Zero);
            Assert.That(fixture.Holdings.ApplyCallCount, Is.Zero);
        }

        [Test]
        public void CapacityRejectedInputCausesNoPartialGrant()
        {
            Fixture fixture = CreateFixture(holdingsMaximum: 5L);
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 10L),
                ValueGrant("grant.misc", RewardGrantKindV1.Miscellaneous, Id("misc.too-large"), 6L));
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.CapacityRejected));
            Assert.That(fixture.Money.Value, Is.Zero);
            Assert.That(fixture.Holdings.Value, Is.Zero);
        }

        [Test]
        public void InterruptionDuringApplicationRemainsRetrySafe()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 10L),
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 5L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);

            RewardApplicationResultV1 first = fixture.Service.Claim(claim);
            RewardApplicationResultV1 retry = fixture.Service.Retry(
                RewardRetryClaimCommandV1.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatusV1.ClaimedPendingApplication));
            Assert.That(first.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentStateV1.Claimed));
            Assert.That(retry.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(10L));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(5L));
        }

        [Test]
        public void RetryUsesSameChildTransactionIdentities()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 5L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);
            RewardApplicationResultV1 first = fixture.Service.Claim(claim);
            StableId childId = first.CommitmentSnapshot.Children[0].Command.TransactionStableId;

            RewardApplicationResultV1 retry = fixture.Service.Retry(
                RewardRetryClaimCommandV1.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(retry.CommitmentSnapshot.Children[0].Command.TransactionStableId,
                Is.EqualTo(childId));
            Assert.That(fixture.Scrap.LastAppliedTransactionId, Is.EqualTo(childId));
        }

        [Test]
        public void AlreadySuccessfulChildrenAreNotAppliedTwiceDuringRetry()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 10L),
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 5L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);
            fixture.Service.Claim(claim);
            int moneyCalls = fixture.Money.ApplyCallCount;

            fixture.Service.Retry(
                RewardRetryClaimCommandV1.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(fixture.Money.ApplyCallCount, Is.EqualTo(moneyCalls));
            Assert.That(fixture.Money.AppliedTransactionCount, Is.EqualTo(1));
            Assert.That(fixture.Money.Value, Is.EqualTo(10L));
        }

        [Test]
        public void AppliedIsReportedOnlyAfterEveryChildConfirmsSuccess()
        {
            Fixture fixture = CreateFixture();
            fixture.Holdings.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 10L),
                ValueGrant("grant.misc", RewardGrantKindV1.Miscellaneous, Id("misc.pending"), 1L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);

            RewardApplicationResultV1 first = fixture.Service.Claim(claim);
            RewardApplicationResultV1 second = fixture.Service.Retry(
                RewardRetryClaimCommandV1.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatusV1.ClaimedPendingApplication));
            Assert.That(first.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentStateV1.Claimed));
            Assert.That(second.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(second.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentStateV1.Applied));
        }

        [Test]
        public void RestartReprojectionDoesNotDuplicateReward()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 2L));
            fixture.Service.Commit(commit);
            fixture.Service.Project(Projection(commit, "projection.first"));
            RewardApplicationSnapshotV1 snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationServiceV1 restarted = fixture.NewService();
            Assert.That(restarted.ImportSnapshot(snapshot).Succeeded, Is.True);

            RewardApplicationResultV1 reprojection = restarted.Project(
                Projection(commit, "projection.after-restart"));
            RewardApplicationResultV1 applied = restarted.Claim(Claim(commit));

            Assert.That(reprojection.Status, Is.EqualTo(RewardApplicationResultStatusV1.Projected));
            Assert.That(applied.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(2L));
        }

        [Test]
        public void SourceCallbackAfterSnapshotImportReturnsOriginalCommitment()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 2L));
            fixture.Service.Commit(commit);
            RewardApplicationSnapshotV1 snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationServiceV1 restored = fixture.NewService();
            Assert.That(restored.ImportSnapshot(snapshot).Succeeded, Is.True);
            long sequence = restored.Sequence;

            RewardApplicationResultV1 replay = restored.Commit(commit);

            Assert.That(replay.Status,
                Is.EqualTo(RewardApplicationResultStatusV1.ExactDuplicateNoChange));
            Assert.That(replay.CommitmentStableId, Is.EqualTo(commit.CommitmentStableId));
            Assert.That(restored.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void AppliedRewardRemainsAppliedAfterSnapshotImport()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 2L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);
            fixture.Service.Claim(claim);
            RewardApplicationSnapshotV1 snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationServiceV1 restored = fixture.NewService();

            Assert.That(restored.ImportSnapshot(snapshot).Succeeded, Is.True);
            RewardApplicationResultV1 replay = restored.Claim(claim);

            Assert.That(replay.Status, Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Value, Is.EqualTo(2L));
        }

        [Test]
        public void ClaimedPendingRewardRemainsRetryableAfterSnapshotImport()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 2L),
                ValueGrant("grant.scrap", RewardGrantKindV1.Scrap, ScrapCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);
            fixture.Service.Claim(claim);
            RewardApplicationSnapshotV1 snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationServiceV1 restored = fixture.NewService();
            Assert.That(restored.ImportSnapshot(snapshot).Succeeded, Is.True);

            RewardApplicationResultV1 result = restored.Retry(
                RewardRetryClaimCommandV1.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(2L));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(3L));
        }

        [Test]
        public void CorruptSnapshotImportIsRejectedWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 existing = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(existing);
            RewardApplicationSnapshotV1 before = fixture.Service.ExportSnapshot();
            RewardApplicationSnapshotV1 corrupt = new RewardApplicationSnapshotV1(
                before.SchemaVersion,
                before.AuthorityStableId,
                before.Sequence,
                before.Commitments,
                Hash('f'));

            RewardApplicationImportResultV1 result = fixture.Service.ImportSnapshot(corrupt);
            RewardApplicationSnapshotV1 after = fixture.Service.ExportSnapshot();

            Assert.That(result.Status, Is.EqualTo(RewardApplicationImportStatusV1.FingerprintMismatch));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void SemanticallyCorruptSnapshotWithValidFingerprintIsRejectedWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 existing = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(existing);
            RewardApplicationSnapshotV1 before = fixture.Service.ExportSnapshot();
            RewardApplicationSnapshotV1 provisional = new RewardApplicationSnapshotV1(
                before.SchemaVersion,
                before.AuthorityStableId,
                0L,
                before.Commitments,
                string.Empty);
            RewardApplicationSnapshotV1 corrupt = new RewardApplicationSnapshotV1(
                provisional.SchemaVersion,
                provisional.AuthorityStableId,
                provisional.Sequence,
                provisional.Commitments,
                RewardApplicationSnapshotV1.ComputeFingerprint(provisional));

            RewardApplicationImportResultV1 result = fixture.Service.ImportSnapshot(corrupt);
            RewardApplicationSnapshotV1 after = fixture.Service.ExportSnapshot();

            Assert.That(result.Status,
                Is.EqualTo(RewardApplicationImportStatusV1.SnapshotRejected));
            Assert.That(result.RejectionCode, Is.EqualTo("snapshot-sequence-inconsistent"));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void UnsupportedSnapshotVersionIsRejected()
        {
            Fixture fixture = CreateFixture();
            RewardApplicationSnapshotV1 current = fixture.Service.ExportSnapshot();
            RewardApplicationSnapshotV1 provisional = new RewardApplicationSnapshotV1(
                99,
                current.AuthorityStableId,
                current.Sequence,
                current.Commitments,
                string.Empty);
            RewardApplicationSnapshotV1 unsupported = new RewardApplicationSnapshotV1(
                99,
                current.AuthorityStableId,
                current.Sequence,
                current.Commitments,
                RewardApplicationSnapshotV1.ComputeFingerprint(provisional));

            RewardApplicationImportResultV1 result = fixture.Service.ImportSnapshot(unsupported);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationImportStatusV1.UnsupportedSchemaVersion));
        }

        [Test]
        public void DuplicateGrantPayloadIdentitiesAreRejected()
        {
            RewardGrantApplicationPayloadV1 payload = ValueGrant(
                "grant.duplicate",
                RewardGrantKindV1.Money,
                MoneyCurrency,
                1L);
            RewardOperationRequestV1 operation = Operation();
            RewardResultV1 result = RewardResultV1.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { payload.Grant });

            Assert.Throws<ArgumentException>(() => RewardCommitCommandV1.Create(
                operation,
                result,
                Hash('g'),
                new[] { payload, payload }));
        }

        [Test]
        public void CanonicalOrderingProducesStableFingerprints()
        {
            RewardGrantApplicationPayloadV1 money = ValueGrant(
                "grant.a-money",
                RewardGrantKindV1.Money,
                MoneyCurrency,
                1L);
            RewardGrantApplicationPayloadV1 misc = ValueGrant(
                "grant.z-misc",
                RewardGrantKindV1.Miscellaneous,
                Id("misc.widget"),
                2L);
            RewardOperationRequestV1 operation = Operation();
            RewardResultV1 firstResult = RewardResultV1.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { misc.Grant, money.Grant });
            RewardResultV1 secondResult = RewardResultV1.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { money.Grant, misc.Grant });
            RewardCommitCommandV1 first = CommitWith(
                operation,
                firstResult,
                Hash('g'),
                new[] { misc, money });
            RewardCommitCommandV1 second = CommitWith(
                operation,
                secondResult,
                Hash('g'),
                new[] { money, misc });

            Assert.That(first.Fingerprint, Is.EqualTo(second.Fingerprint));
            Assert.That(first.ToCanonicalString(), Is.EqualTo(second.ToCanonicalString()));
        }

        [Test]
        public void DerivedStableIdsObeyOneDotRule()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L),
                StrongboxGrant("grant.box", "strongbox-definition.tier-one", "strongbox-instance.dot"));
            fixture.Service.Commit(commit);
            RewardApplicationResultV1 result = fixture.Service.Claim(Claim(commit));

            for (int index = 0; index < result.CommitmentSnapshot.Children.Count; index++)
            {
                string transaction = result.CommitmentSnapshot.Children[index].Command.TransactionStableId.ToString();
                string operation = result.CommitmentSnapshot.Children[index].Command.OperationStableId.ToString();
                Assert.That(transaction.IndexOf('.'), Is.EqualTo(transaction.LastIndexOf('.')));
                Assert.That(operation.IndexOf('.'), Is.EqualTo(operation.LastIndexOf('.')));
                Assert.That(StableId.Parse(transaction), Is.Not.Null);
                Assert.That(StableId.Parse(operation), Is.Not.Null);
            }
        }

        [Test]
        public void HundredDuplicateCallbacksLeaveStateStable()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 9L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 claim = Claim(commit);
            fixture.Service.Claim(claim);
            long sequence = fixture.Service.Sequence;
            int owners = fixture.Money.AppliedTransactionCount;

            for (int index = 0; index < 100; index++)
            {
                Assert.That(fixture.Service.Commit(commit).Status,
                    Is.EqualTo(RewardApplicationResultStatusV1.ExactDuplicateNoChange));
                Assert.That(fixture.Service.Claim(claim).Status,
                    Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            }

            Assert.That(fixture.Money.Value, Is.EqualTo(9L));
            Assert.That(fixture.Money.AppliedTransactionCount, Is.EqualTo(owners));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void CancellationWorksOnlyForAllowedUnappliedState()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            fixture.Service.Project(Projection(commit, "projection.cancel"));

            RewardApplicationResultV1 cancelled = fixture.Service.Cancel(
                Cancellation(commit));

            Assert.That(cancelled.Status, Is.EqualTo(RewardApplicationResultStatusV1.Cancelled));
            Assert.That(cancelled.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentStateV1.Cancelled));
            Assert.That(fixture.Money.Value, Is.Zero);
        }

        [Test]
        public void AppliedCommitmentsCannotBeCancelledOrReclaimed()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            fixture.Service.Claim(Claim(commit));

            RewardApplicationResultV1 cancelled = fixture.Service.Cancel(Cancellation(commit));
            RewardApplicationResultV1 reclaimed = fixture.Service.Claim(
                Claim(commit, "claim.second"));

            Assert.That(cancelled.Status, Is.EqualTo(RewardApplicationResultStatusV1.InvalidStateTransition));
            Assert.That(reclaimed.Status, Is.EqualTo(RewardApplicationResultStatusV1.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Value, Is.EqualTo(1L));
        }

        [Test]
        public void CancelledCommitmentCannotBeClaimed()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            fixture.Service.Cancel(Cancellation(commit));

            RewardApplicationResultV1 result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.InvalidStateTransition));
            Assert.That(fixture.Money.Value, Is.Zero);
        }

        [Test]
        public void InvalidDestinationAuthorityIsRejectedBeforeMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 invalid = RewardClaimCommandV1.Create(
                Id("claim.invalid-authority"),
                commit.CommitmentStableId,
                Id("player.one"),
                Id("authority.wrong-money"),
                ScrapAuthority,
                HoldingsAuthority);

            RewardApplicationResultV1 result = fixture.Service.Claim(invalid);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.AuthorityMismatch));
            Assert.That(fixture.Money.ApplyCallCount, Is.Zero);
        }

        [Test]
        public void ExpectedSequenceConflictIsRejectedBeforeMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommandV1 commit = Commit(
                ValueGrant("grant.money", RewardGrantKindV1.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            RewardClaimCommandV1 stale = Claim(commit, expectedMoney: 7L);

            RewardApplicationResultV1 result = fixture.Service.Claim(stale);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.ExpectedSequenceConflict));
            Assert.That(fixture.Money.Value, Is.Zero);
        }

        [Test]
        public void ExplicitNoDropCommitmentClaimsToAppliedWithoutChildMutation()
        {
            Fixture fixture = CreateFixture();
            RewardOperationRequestV1 operation = Operation();
            RewardCommitCommandV1 commit = RewardCommitCommandV1.Create(
                operation,
                RewardResultV1.CreateExplicitNoDrop(
                    operation.CommitmentStableId,
                    operation.SourceOperationStableId),
                Hash('n'),
                Array.Empty<RewardGrantApplicationPayloadV1>());
            fixture.Service.Commit(commit);

            RewardApplicationResultV1 result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatusV1.Applied));
            Assert.That(result.CommitmentSnapshot.Children, Is.Empty);
            Assert.That(fixture.Money.ApplyCallCount + fixture.Scrap.ApplyCallCount + fixture.Holdings.ApplyCallCount, Is.Zero);
        }

        private static Fixture CreateFixture(long holdingsMaximum = long.MaxValue)
        {
            var money = new DeterministicAuthority(
                MoneyAuthority,
                new[] { RewardGrantKindV1.Money },
                long.MaxValue);
            var scrap = new DeterministicAuthority(
                ScrapAuthority,
                new[] { RewardGrantKindV1.Scrap },
                long.MaxValue);
            var holdings = new DeterministicAuthority(
                HoldingsAuthority,
                new[]
                {
                    RewardGrantKindV1.Strongbox,
                    RewardGrantKindV1.EquipmentReference,
                    RewardGrantKindV1.PremiumAmmo,
                    RewardGrantKindV1.Miscellaneous,
                },
                holdingsMaximum);
            return new Fixture(money, scrap, holdings);
        }

        private static RewardCommitCommandV1 Commit(
            params RewardGrantApplicationPayloadV1[] payloads)
        {
            RewardOperationRequestV1 operation = Operation();
            RewardGrantV1[] grants = new RewardGrantV1[payloads.Length];
            for (int index = 0; index < payloads.Length; index++)
            {
                grants[index] = payloads[index].Grant;
            }

            RewardResultV1 result = RewardResultV1.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                grants);
            return CommitWith(operation, result, Hash('g'), payloads);
        }

        private static RewardCommitCommandV1 CommitWith(
            RewardOperationRequestV1 operation,
            RewardResultV1 result,
            string generationFingerprint,
            IEnumerable<RewardGrantApplicationPayloadV1> payloads)
        {
            return RewardCommitCommandV1.Create(
                operation,
                result,
                generationFingerprint,
                payloads);
        }

        private static RewardOperationRequestV1 Operation()
        {
            return RewardOperationRequestV1.Create(
                Id("run.test"),
                Id("source-instance.enemy-one"),
                Id("source-operation.reward-one"),
                Id("commitment.reward-one"),
                Id("reward-profile.test"),
                Hash('c'));
        }

        private static RewardGrantApplicationPayloadV1 ValueGrant(
            string grantId,
            RewardGrantKindV1 kind,
            StableId content,
            long quantity)
        {
            return RewardGrantApplicationPayloadV1.ForValue(
                RewardGrantV1.Create(Id(grantId), kind, content, quantity));
        }

        private static RewardGrantApplicationPayloadV1 StrongboxGrant(
            string grantId,
            string definitionId,
            params string[] instanceIds)
        {
            var ids = new StableId[instanceIds.Length];
            for (int index = 0; index < instanceIds.Length; index++)
            {
                ids[index] = Id(instanceIds[index]);
            }

            return RewardGrantApplicationPayloadV1.ForStrongboxes(
                RewardGrantV1.Create(
                    Id(grantId),
                    RewardGrantKindV1.Strongbox,
                    Id(definitionId),
                    ids.Length),
                ids);
        }

        private static RewardGrantApplicationPayloadV1 EquipmentGrant(
            string grantId,
            params EquipmentInstance[] equipment)
        {
            return RewardGrantApplicationPayloadV1.ForEquipment(
                RewardGrantV1.Create(
                    Id(grantId),
                    RewardGrantKindV1.EquipmentReference,
                    equipment[0].DefinitionId,
                    equipment.Length),
                equipment);
        }

        private static EquipmentInstance Equipment(string instanceId, string definitionId)
        {
            return EquipmentInstance.Create(
                Id(instanceId),
                Id(definitionId),
                1,
                Id("quality.common"),
                Array.Empty<AugmentInstance>());
        }

        private static RewardClaimCommandV1 Claim(
            RewardCommitCommandV1 commit,
            string claimId = "claim.reward-one",
            long? expectedMoney = null,
            long? expectedScrap = null,
            long? expectedHoldings = null)
        {
            return RewardClaimCommandV1.Create(
                Id(claimId),
                commit.CommitmentStableId,
                Id("player.one"),
                MoneyAuthority,
                ScrapAuthority,
                HoldingsAuthority,
                expectedMoney,
                expectedScrap,
                expectedHoldings);
        }

        private static RewardProjectCommandV1 Projection(
            RewardCommitCommandV1 commit,
            string projectionId)
        {
            return RewardProjectCommandV1.Create(
                Id(projectionId),
                commit.CommitmentStableId,
                Id("presentation.pickup"));
        }

        private static RewardCancelCommandV1 Cancellation(
            RewardCommitCommandV1 commit)
        {
            return RewardCancelCommandV1.Create(
                Id("cancellation.reward-one"),
                commit.CommitmentStableId,
                Id("cancel-reason.source-invalidated"));
        }

        private static StableId Id(string value)
        {
            return StableId.Parse(value);
        }

        private static string Hash(char value)
        {
            return "sha256:" + new string(value, 64);
        }

        private sealed class Fixture
        {
            public Fixture(
                DeterministicAuthority money,
                DeterministicAuthority scrap,
                DeterministicAuthority holdings)
            {
                Money = money;
                Scrap = scrap;
                Holdings = holdings;
                Service = NewService();
            }

            public DeterministicAuthority Money { get; }
            public DeterministicAuthority Scrap { get; }
            public DeterministicAuthority Holdings { get; }
            public RewardApplicationServiceV1 Service { get; }

            public RewardApplicationServiceV1 NewService()
            {
                return new RewardApplicationServiceV1(
                    RapAuthority,
                    Money,
                    Scrap,
                    Holdings);
            }
        }

        private sealed class DeterministicAuthority : IRewardChildAuthorityV1
        {
            private readonly HashSet<RewardGrantKindV1> supportedKinds;
            private readonly Dictionary<StableId, StoredTransaction> transactions;
            private readonly long maximum;
            private long sequence;
            private long value;

            public DeterministicAuthority(
                StableId authorityStableId,
                IEnumerable<RewardGrantKindV1> supportedKinds,
                long maximum)
            {
                AuthorityStableId = authorityStableId;
                this.supportedKinds = new HashSet<RewardGrantKindV1>(supportedKinds);
                this.maximum = maximum;
                transactions = new Dictionary<StableId, StoredTransaction>();
            }

            public StableId AuthorityStableId { get; }
            public long Sequence { get { return sequence; } }
            public long Value { get { return value; } }
            public int ApplyCallCount { get; private set; }
            public int AppliedTransactionCount { get { return transactions.Count; } }
            public StableId LastAppliedTransactionId { get; private set; }
            public bool FailNextApply { get; set; }
            public RewardAuthorityAdmissionStatusV1? PreflightRejection { get; set; }

            public RewardAuthorityPreflightResultV1 Preflight(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                List<RewardChildGrantCommandV1> ordered = Copy(commands);
                long simulatedSequence = sequence;
                long simulatedValue = value;
                var facts = new List<RewardAuthorityPreflightFactV1>(ordered.Count);
                for (int index = 0; index < ordered.Count; index++)
                {
                    RewardChildGrantCommandV1 command = ordered[index];
                    StoredTransaction existing;
                    if (transactions.TryGetValue(command.TransactionStableId, out existing))
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                                ? RewardAuthorityAdmissionStatusV1.AlreadyApplied
                                : RewardAuthorityAdmissionStatusV1.ConflictingDuplicate,
                            string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                                ? null
                                : "transaction-conflict"));
                        continue;
                    }

                    if (command.DestinationAuthorityStableId != AuthorityStableId)
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            RewardAuthorityAdmissionStatusV1.AuthorityMismatch,
                            "authority-mismatch"));
                        continue;
                    }

                    if (!supportedKinds.Contains(command.GrantKind))
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            RewardAuthorityAdmissionStatusV1.InvalidCommand,
                            "kind-invalid"));
                        continue;
                    }

                    if (PreflightRejection.HasValue)
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            PreflightRejection.Value,
                            "configured-preflight-rejection"));
                        continue;
                    }

                    if (command.ExpectedSequence.HasValue
                        && command.ExpectedSequence.Value != simulatedSequence)
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            RewardAuthorityAdmissionStatusV1.ExpectedSequenceConflict,
                            "expected-sequence-conflict"));
                        continue;
                    }

                    long proposed;
                    try
                    {
                        proposed = checked(simulatedValue + command.Quantity);
                    }
                    catch (OverflowException)
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            RewardAuthorityAdmissionStatusV1.CapacityRejected,
                            "value-overflow"));
                        continue;
                    }

                    if (proposed > maximum)
                    {
                        facts.Add(new RewardAuthorityPreflightFactV1(
                            command.TransactionStableId,
                            RewardAuthorityAdmissionStatusV1.CapacityRejected,
                            "capacity-rejected"));
                        continue;
                    }

                    simulatedValue = proposed;
                    simulatedSequence++;
                    facts.Add(new RewardAuthorityPreflightFactV1(
                        command.TransactionStableId,
                        RewardAuthorityAdmissionStatusV1.Accepted,
                        null));
                }

                return new RewardAuthorityPreflightResultV1(facts);
            }

            public RewardChildApplyResultV1 Apply(RewardChildGrantCommandV1 command)
            {
                ApplyCallCount++;
                StoredTransaction existing;
                if (transactions.TryGetValue(command.TransactionStableId, out existing))
                {
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                            ? RewardChildApplyStatusV1.ExactDuplicateNoChange
                            : RewardChildApplyStatusV1.ConflictingDuplicate,
                        string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal),
                        string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                            ? null
                            : "transaction-conflict");
                }

                if (FailNextApply)
                {
                    FailNextApply = false;
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "configured-interruption");
                }

                if (command.ExpectedSequence.HasValue
                    && command.ExpectedSequence.Value != sequence)
                {
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.ExpectedSequenceConflict,
                        false,
                        "expected-sequence-conflict");
                }

                long proposed = checked(value + command.Quantity);
                if (proposed > maximum)
                {
                    return new RewardChildApplyResultV1(
                        command.TransactionStableId,
                        RewardChildApplyStatusV1.CapacityRejected,
                        false,
                        "capacity-rejected");
                }

                value = proposed;
                sequence++;
                transactions.Add(
                    command.TransactionStableId,
                    new StoredTransaction(command.Fingerprint));
                LastAppliedTransactionId = command.TransactionStableId;
                return new RewardChildApplyResultV1(
                    command.TransactionStableId,
                    RewardChildApplyStatusV1.Applied,
                    true,
                    null);
            }

            private static List<RewardChildGrantCommandV1> Copy(
                IReadOnlyList<RewardChildGrantCommandV1> commands)
            {
                var copy = new List<RewardChildGrantCommandV1>(commands.Count);
                for (int index = 0; index < commands.Count; index++)
                {
                    copy.Add(commands[index]);
                }

                copy.Sort(delegate(RewardChildGrantCommandV1 left, RewardChildGrantCommandV1 right)
                {
                    if (left.ExpectedSequence.HasValue && right.ExpectedSequence.HasValue)
                    {
                        int comparison = left.ExpectedSequence.Value.CompareTo(right.ExpectedSequence.Value);
                        if (comparison != 0)
                        {
                            return comparison;
                        }
                    }

                    return left.TransactionStableId.CompareTo(right.TransactionStableId);
                });
                return copy;
            }

            private sealed class StoredTransaction
            {
                public StoredTransaction(string fingerprint)
                {
                    Fingerprint = fingerprint;
                }

                public string Fingerprint { get; }
            }
        }
    }
}
