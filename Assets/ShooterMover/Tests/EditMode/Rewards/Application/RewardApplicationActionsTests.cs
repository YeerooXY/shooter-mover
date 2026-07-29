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
    public sealed class RewardApplicationActionsTests
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
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 25L));
            fixture.Service.Commit(commit);

            RewardApplicationResult first = fixture.Service.Claim(Claim(commit));
            RewardApplicationResult duplicate = fixture.Service.Claim(Claim(commit));

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Value, Is.EqualTo(25L));
            Assert.That(fixture.Money.AppliedTransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void ScrapOnlyRewardAppliesOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 13L));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(13L));
        }

        [Test]
        public void StrongboxGrantAppliesToHoldingsOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                StrongboxGrant(
                    "grant.box",
                    "strongbox-definition.tier-one",
                    "strongbox-instance.one"));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(1L));
            Assert.That(fixture.Holdings.AppliedTransactionCount, Is.EqualTo(1));
        }

        [Test]
        public void EquipmentGrantAppliesToHoldingsOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                EquipmentGrant(
                    "grant.equipment",
                    Equipment(
                        "equipment-instance.one",
                        "equipment-definition.blaster")));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(1L));
        }

        [Test]
        public void MiscellaneousStackAppliesOnce()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant(
                    "grant.misc",
                    RewardGrantKind.Miscellaneous,
                    Id("misc.future-widget"),
                    7L));
            fixture.Service.Commit(commit);

            Assert.That(fixture.Service.Claim(Claim(commit)).Status,
                Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(7L));
        }

        [Test]
        public void MixedRewardAppliesCompletely()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 20L),
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 4L),
                StrongboxGrant("grant.box", "strongbox-definition.tier-one", "strongbox-instance.mixed"),
                EquipmentGrant("grant.equipment", Equipment("equipment-instance.mixed", "equipment-definition.blaster")));
            fixture.Service.Commit(commit);

            RewardApplicationResult result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(20L));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(4L));
            Assert.That(fixture.Holdings.Value, Is.EqualTo(2L));
            Assert.That(result.CommitmentSnapshot.Children, Has.Count.EqualTo(4));
            Assert.That(result.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentState.Applied));
        }

        [Test]
        public void ExactDuplicateSourceCallbackReturnsOriginalCommitment()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 3L));
            RewardApplicationResult first = fixture.Service.Commit(commit);
            long sequence = fixture.Service.Sequence;
            RewardApplicationResult duplicate = fixture.Service.Commit(commit);

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatus.Generated));
            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatus.ExactDuplicateNoChange));
            Assert.That(duplicate.CommitmentStableId, Is.EqualTo(commit.CommitmentStableId));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void ConflictingDuplicateSourceCallbackIsRejected()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand original = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 3L));
            RewardCommitCommand conflict = CommitWith(
                original.Operation,
                original.GeneratedReward,
                Hash('x'),
                original.GrantPayloads);
            fixture.Service.Commit(original);

            RewardApplicationResult result = fixture.Service.Commit(conflict);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.ConflictingDuplicate));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(1L));
        }

        [Test]
        public void ExactDuplicateProjectionIsNoChangeReplay()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardProjectCommand projection = Projection(commit, "projection.pickup");
            fixture.Service.Project(projection);
            long sequence = fixture.Service.Sequence;

            RewardApplicationResult duplicate = fixture.Service.Project(projection);

            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatus.ExactDuplicateNoChange));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void ExactDuplicateClaimCallbackIsNoChangeReplay()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);
            Assert.That(fixture.Service.Claim(claim).Status,
                Is.EqualTo(RewardApplicationResultStatus.ClaimedPendingApplication));
            long sequence = fixture.Service.Sequence;

            RewardApplicationResult duplicate = fixture.Service.Claim(claim);

            Assert.That(duplicate.Status, Is.EqualTo(RewardApplicationResultStatus.ExactDuplicateNoChange));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
            Assert.That(fixture.Scrap.ApplyCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ConflictingDuplicateClaimIdentityIsRejected()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardClaimCommand original = Claim(commit);
            fixture.Service.Claim(original);
            RewardClaimCommand conflict = RewardClaimCommand.Create(
                original.ClaimStableId,
                commit.CommitmentStableId,
                Id("player.someone-else"),
                MoneyAuthority,
                ScrapAuthority,
                HoldingsAuthority);

            RewardApplicationResult result = fixture.Service.Claim(conflict);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.ConflictingDuplicate));
        }

        [Test]
        public void FailedPreflightLeavesAllAuthoritiesUnchanged()
        {
            Fixture fixture = CreateFixture();
            fixture.Holdings.PreflightRejection = RewardStateAdmissionStatus.CapacityRejected;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 10L),
                ValueGrant("grant.misc", RewardGrantKind.Miscellaneous, Id("misc.full"), 1L));
            fixture.Service.Commit(commit);

            RewardApplicationResult result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.CapacityRejected));
            Assert.That(fixture.Money.Value, Is.Zero);
            Assert.That(fixture.Holdings.Value, Is.Zero);
            Assert.That(fixture.Money.ApplyCallCount, Is.Zero);
            Assert.That(fixture.Holdings.ApplyCallCount, Is.Zero);
        }

        [Test]
        public void CapacityRejectedInputCausesNoPartialGrant()
        {
            Fixture fixture = CreateFixture(holdingsMaximum: 5L);
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 10L),
                ValueGrant("grant.misc", RewardGrantKind.Miscellaneous, Id("misc.too-large"), 6L));
            fixture.Service.Commit(commit);

            RewardApplicationResult result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.CapacityRejected));
            Assert.That(fixture.Money.Value, Is.Zero);
            Assert.That(fixture.Holdings.Value, Is.Zero);
        }

        [Test]
        public void InterruptionDuringApplicationRemainsRetrySafe()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 10L),
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 5L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);

            RewardApplicationResult first = fixture.Service.Claim(claim);
            RewardApplicationResult retry = fixture.Service.Retry(
                RewardRetryClaimCommand.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatus.ClaimedPendingApplication));
            Assert.That(first.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentState.Claimed));
            Assert.That(retry.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(10L));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(5L));
        }

        [Test]
        public void RetryUsesSameChildTransactionIdentities()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 5L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);
            RewardApplicationResult first = fixture.Service.Claim(claim);
            StableId childId = first.CommitmentSnapshot.Children[0].Command.TransactionStableId;

            RewardApplicationResult retry = fixture.Service.Retry(
                RewardRetryClaimCommand.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(retry.CommitmentSnapshot.Children[0].Command.TransactionStableId,
                Is.EqualTo(childId));
            Assert.That(fixture.Scrap.LastAppliedTransactionId, Is.EqualTo(childId));
        }

        [Test]
        public void AlreadySuccessfulChildrenAreNotAppliedTwiceDuringRetry()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 10L),
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 5L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);
            fixture.Service.Claim(claim);
            int moneyCalls = fixture.Money.ApplyCallCount;

            fixture.Service.Retry(
                RewardRetryClaimCommand.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(fixture.Money.ApplyCallCount, Is.EqualTo(moneyCalls));
            Assert.That(fixture.Money.AppliedTransactionCount, Is.EqualTo(1));
            Assert.That(fixture.Money.Value, Is.EqualTo(10L));
        }

        [Test]
        public void AppliedIsReportedOnlyAfterEveryChildConfirmsSuccess()
        {
            Fixture fixture = CreateFixture();
            fixture.Holdings.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 10L),
                ValueGrant("grant.misc", RewardGrantKind.Miscellaneous, Id("misc.pending"), 1L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);

            RewardApplicationResult first = fixture.Service.Claim(claim);
            RewardApplicationResult second = fixture.Service.Retry(
                RewardRetryClaimCommand.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(first.Status, Is.EqualTo(RewardApplicationResultStatus.ClaimedPendingApplication));
            Assert.That(first.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentState.Claimed));
            Assert.That(second.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(second.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentState.Applied));
        }

        [Test]
        public void RestartReprojectionDoesNotDuplicateReward()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 2L));
            fixture.Service.Commit(commit);
            fixture.Service.Project(Projection(commit, "projection.first"));
            RewardApplicationSnapshot snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationActions restarted = fixture.NewService();
            Assert.That(restarted.ImportSnapshot(snapshot).Succeeded, Is.True);

            RewardApplicationResult reprojection = restarted.Project(
                Projection(commit, "projection.after-restart"));
            RewardApplicationResult applied = restarted.Claim(Claim(commit));

            Assert.That(reprojection.Status, Is.EqualTo(RewardApplicationResultStatus.Projected));
            Assert.That(applied.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(2L));
        }

        [Test]
        public void SourceCallbackAfterSnapshotImportReturnsOriginalCommitment()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 2L));
            fixture.Service.Commit(commit);
            RewardApplicationSnapshot snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationActions restored = fixture.NewService();
            Assert.That(restored.ImportSnapshot(snapshot).Succeeded, Is.True);
            long sequence = restored.Sequence;

            RewardApplicationResult replay = restored.Commit(commit);

            Assert.That(replay.Status,
                Is.EqualTo(RewardApplicationResultStatus.ExactDuplicateNoChange));
            Assert.That(replay.CommitmentStableId, Is.EqualTo(commit.CommitmentStableId));
            Assert.That(restored.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void AppliedRewardRemainsAppliedAfterSnapshotImport()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 2L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);
            fixture.Service.Claim(claim);
            RewardApplicationSnapshot snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationActions restored = fixture.NewService();

            Assert.That(restored.ImportSnapshot(snapshot).Succeeded, Is.True);
            RewardApplicationResult replay = restored.Claim(claim);

            Assert.That(replay.Status, Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Value, Is.EqualTo(2L));
        }

        [Test]
        public void ClaimedPendingRewardRemainsRetryableAfterSnapshotImport()
        {
            Fixture fixture = CreateFixture();
            fixture.Scrap.FailNextApply = true;
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 2L),
                ValueGrant("grant.scrap", RewardGrantKind.Scrap, ScrapCurrency, 3L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);
            fixture.Service.Claim(claim);
            RewardApplicationSnapshot snapshot = fixture.Service.ExportSnapshot();
            RewardApplicationActions restored = fixture.NewService();
            Assert.That(restored.ImportSnapshot(snapshot).Succeeded, Is.True);

            RewardApplicationResult result = restored.Retry(
                RewardRetryClaimCommand.Create(commit.CommitmentStableId, claim.ClaimStableId));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(fixture.Money.Value, Is.EqualTo(2L));
            Assert.That(fixture.Scrap.Value, Is.EqualTo(3L));
        }

        [Test]
        public void CorruptSnapshotImportIsRejectedWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand existing = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(existing);
            RewardApplicationSnapshot before = fixture.Service.ExportSnapshot();
            RewardApplicationSnapshot corrupt = new RewardApplicationSnapshot(
                before.SchemaVersion,
                before.AuthorityStableId,
                before.Sequence,
                before.Commitments,
                Hash('f'));

            RewardApplicationImportResult result = fixture.Service.ImportSnapshot(corrupt);
            RewardApplicationSnapshot after = fixture.Service.ExportSnapshot();

            Assert.That(result.Status, Is.EqualTo(RewardApplicationImportStatus.FingerprintMismatch));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void SemanticallyCorruptSnapshotWithValidFingerprintIsRejectedWithoutMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand existing = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(existing);
            RewardApplicationSnapshot before = fixture.Service.ExportSnapshot();
            RewardApplicationSnapshot provisional = new RewardApplicationSnapshot(
                before.SchemaVersion,
                before.AuthorityStableId,
                0L,
                before.Commitments,
                string.Empty);
            RewardApplicationSnapshot corrupt = new RewardApplicationSnapshot(
                provisional.SchemaVersion,
                provisional.AuthorityStableId,
                provisional.Sequence,
                provisional.Commitments,
                RewardApplicationSnapshot.ComputeFingerprint(provisional));

            RewardApplicationImportResult result = fixture.Service.ImportSnapshot(corrupt);
            RewardApplicationSnapshot after = fixture.Service.ExportSnapshot();

            Assert.That(result.Status,
                Is.EqualTo(RewardApplicationImportStatus.SnapshotRejected));
            Assert.That(result.RejectionCode, Is.EqualTo("snapshot-sequence-inconsistent"));
            Assert.That(after.Fingerprint, Is.EqualTo(before.Fingerprint));
        }

        [Test]
        public void UnsupportedSnapshotVersionIsRejected()
        {
            Fixture fixture = CreateFixture();
            RewardApplicationSnapshot current = fixture.Service.ExportSnapshot();
            RewardApplicationSnapshot provisional = new RewardApplicationSnapshot(
                99,
                current.AuthorityStableId,
                current.Sequence,
                current.Commitments,
                string.Empty);
            RewardApplicationSnapshot unsupported = new RewardApplicationSnapshot(
                99,
                current.AuthorityStableId,
                current.Sequence,
                current.Commitments,
                RewardApplicationSnapshot.ComputeFingerprint(provisional));

            RewardApplicationImportResult result = fixture.Service.ImportSnapshot(unsupported);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationImportStatus.UnsupportedSchemaVersion));
        }

        [Test]
        public void DuplicateGrantPayloadIdentitiesAreRejected()
        {
            RewardGrantApplicationPayload payload = ValueGrant(
                "grant.duplicate",
                RewardGrantKind.Money,
                MoneyCurrency,
                1L);
            RewardOperationRequest operation = Operation();
            RewardResult result = RewardResult.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { payload.Grant });

            Assert.Throws<ArgumentException>(() => RewardCommitCommand.Create(
                operation,
                result,
                Hash('g'),
                new[] { payload, payload }));
        }

        [Test]
        public void CanonicalOrderingProducesStableFingerprints()
        {
            RewardGrantApplicationPayload money = ValueGrant(
                "grant.a-money",
                RewardGrantKind.Money,
                MoneyCurrency,
                1L);
            RewardGrantApplicationPayload misc = ValueGrant(
                "grant.z-misc",
                RewardGrantKind.Miscellaneous,
                Id("misc.widget"),
                2L);
            RewardOperationRequest operation = Operation();
            RewardResult firstResult = RewardResult.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { misc.Grant, money.Grant });
            RewardResult secondResult = RewardResult.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                new[] { money.Grant, misc.Grant });
            RewardCommitCommand first = CommitWith(
                operation,
                firstResult,
                Hash('g'),
                new[] { misc, money });
            RewardCommitCommand second = CommitWith(
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
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L),
                StrongboxGrant("grant.box", "strongbox-definition.tier-one", "strongbox-instance.dot"));
            fixture.Service.Commit(commit);
            RewardApplicationResult result = fixture.Service.Claim(Claim(commit));

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
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 9L));
            fixture.Service.Commit(commit);
            RewardClaimCommand claim = Claim(commit);
            fixture.Service.Claim(claim);
            long sequence = fixture.Service.Sequence;
            int owners = fixture.Money.AppliedTransactionCount;

            for (int index = 0; index < 100; index++)
            {
                Assert.That(fixture.Service.Commit(commit).Status,
                    Is.EqualTo(RewardApplicationResultStatus.ExactDuplicateNoChange));
                Assert.That(fixture.Service.Claim(claim).Status,
                    Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            }

            Assert.That(fixture.Money.Value, Is.EqualTo(9L));
            Assert.That(fixture.Money.AppliedTransactionCount, Is.EqualTo(owners));
            Assert.That(fixture.Service.Sequence, Is.EqualTo(sequence));
        }

        [Test]
        public void CancellationWorksOnlyForAllowedUnappliedState()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            fixture.Service.Project(Projection(commit, "projection.cancel"));

            RewardApplicationResult cancelled = fixture.Service.Cancel(
                Cancellation(commit));

            Assert.That(cancelled.Status, Is.EqualTo(RewardApplicationResultStatus.Cancelled));
            Assert.That(cancelled.CommitmentSnapshot.State, Is.EqualTo(RewardCommitmentState.Cancelled));
            Assert.That(fixture.Money.Value, Is.Zero);
        }

        [Test]
        public void AppliedCommitmentsCannotBeCancelledOrReclaimed()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            fixture.Service.Claim(Claim(commit));

            RewardApplicationResult cancelled = fixture.Service.Cancel(Cancellation(commit));
            RewardApplicationResult reclaimed = fixture.Service.Claim(
                Claim(commit, "claim.second"));

            Assert.That(cancelled.Status, Is.EqualTo(RewardApplicationResultStatus.InvalidStateTransition));
            Assert.That(reclaimed.Status, Is.EqualTo(RewardApplicationResultStatus.AlreadyAppliedNoChange));
            Assert.That(fixture.Money.Value, Is.EqualTo(1L));
        }

        [Test]
        public void CancelledCommitmentCannotBeClaimed()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            fixture.Service.Cancel(Cancellation(commit));

            RewardApplicationResult result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.InvalidStateTransition));
            Assert.That(fixture.Money.Value, Is.Zero);
        }

        [Test]
        public void InvalidDestinationAuthorityIsRejectedBeforeMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            RewardClaimCommand invalid = RewardClaimCommand.Create(
                Id("claim.invalid-authority"),
                commit.CommitmentStableId,
                Id("player.one"),
                Id("authority.wrong-money"),
                ScrapAuthority,
                HoldingsAuthority);

            RewardApplicationResult result = fixture.Service.Claim(invalid);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.AuthorityMismatch));
            Assert.That(fixture.Money.ApplyCallCount, Is.Zero);
        }

        [Test]
        public void ExpectedSequenceConflictIsRejectedBeforeMutation()
        {
            Fixture fixture = CreateFixture();
            RewardCommitCommand commit = Commit(
                ValueGrant("grant.money", RewardGrantKind.Money, MoneyCurrency, 1L));
            fixture.Service.Commit(commit);
            RewardClaimCommand stale = Claim(commit, expectedMoney: 7L);

            RewardApplicationResult result = fixture.Service.Claim(stale);

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.ExpectedSequenceConflict));
            Assert.That(fixture.Money.Value, Is.Zero);
        }

        [Test]
        public void ExplicitNoDropCommitmentClaimsToAppliedWithoutChildMutation()
        {
            Fixture fixture = CreateFixture();
            RewardOperationRequest operation = Operation();
            RewardCommitCommand commit = RewardCommitCommand.Create(
                operation,
                RewardResult.CreateExplicitNoDrop(
                    operation.CommitmentStableId,
                    operation.SourceOperationStableId),
                Hash('n'),
                Array.Empty<RewardGrantApplicationPayload>());
            fixture.Service.Commit(commit);

            RewardApplicationResult result = fixture.Service.Claim(Claim(commit));

            Assert.That(result.Status, Is.EqualTo(RewardApplicationResultStatus.Applied));
            Assert.That(result.CommitmentSnapshot.Children, Is.Empty);
            Assert.That(fixture.Money.ApplyCallCount + fixture.Scrap.ApplyCallCount + fixture.Holdings.ApplyCallCount, Is.Zero);
        }

        private static Fixture CreateFixture(long holdingsMaximum = long.MaxValue)
        {
            var money = new DeterministicState(
                MoneyAuthority,
                new[] { RewardGrantKind.Money },
                long.MaxValue);
            var scrap = new DeterministicState(
                ScrapAuthority,
                new[] { RewardGrantKind.Scrap },
                long.MaxValue);
            var holdings = new DeterministicState(
                HoldingsAuthority,
                new[]
                {
                    RewardGrantKind.Strongbox,
                    RewardGrantKind.EquipmentReference,
                    RewardGrantKind.PremiumAmmo,
                    RewardGrantKind.Miscellaneous,
                },
                holdingsMaximum);
            return new Fixture(money, scrap, holdings);
        }

        private static RewardCommitCommand Commit(
            params RewardGrantApplicationPayload[] payloads)
        {
            RewardOperationRequest operation = Operation();
            RewardGrant[] grants = new RewardGrant[payloads.Length];
            for (int index = 0; index < payloads.Length; index++)
            {
                grants[index] = payloads[index].Grant;
            }

            RewardResult result = RewardResult.CreateGrants(
                operation.CommitmentStableId,
                operation.SourceOperationStableId,
                grants);
            return CommitWith(operation, result, Hash('g'), payloads);
        }

        private static RewardCommitCommand CommitWith(
            RewardOperationRequest operation,
            RewardResult result,
            string generationFingerprint,
            IEnumerable<RewardGrantApplicationPayload> payloads)
        {
            return RewardCommitCommand.Create(
                operation,
                result,
                generationFingerprint,
                payloads);
        }

        private static RewardOperationRequest Operation()
        {
            return RewardOperationRequest.Create(
                Id("run.test"),
                Id("source-instance.enemy-one"),
                Id("source-operation.reward-one"),
                Id("commitment.reward-one"),
                Id("reward-profile.test"),
                Hash('c'));
        }

        private static RewardGrantApplicationPayload ValueGrant(
            string grantId,
            RewardGrantKind kind,
            StableId content,
            long quantity)
        {
            return RewardGrantApplicationPayload.ForValue(
                RewardGrant.Create(Id(grantId), kind, content, quantity));
        }

        private static RewardGrantApplicationPayload StrongboxGrant(
            string grantId,
            string definitionId,
            params string[] instanceIds)
        {
            var ids = new StableId[instanceIds.Length];
            for (int index = 0; index < instanceIds.Length; index++)
            {
                ids[index] = Id(instanceIds[index]);
            }

            return RewardGrantApplicationPayload.ForStrongboxes(
                RewardGrant.Create(
                    Id(grantId),
                    RewardGrantKind.Strongbox,
                    Id(definitionId),
                    ids.Length),
                ids);
        }

        private static RewardGrantApplicationPayload EquipmentGrant(
            string grantId,
            params EquipmentInstance[] equipment)
        {
            return RewardGrantApplicationPayload.ForEquipment(
                RewardGrant.Create(
                    Id(grantId),
                    RewardGrantKind.EquipmentReference,
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

        private static RewardClaimCommand Claim(
            RewardCommitCommand commit,
            string claimId = "claim.reward-one",
            long? expectedMoney = null,
            long? expectedScrap = null,
            long? expectedHoldings = null)
        {
            return RewardClaimCommand.Create(
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

        private static RewardProjectCommand Projection(
            RewardCommitCommand commit,
            string projectionId)
        {
            return RewardProjectCommand.Create(
                Id(projectionId),
                commit.CommitmentStableId,
                Id("presentation.pickup"));
        }

        private static RewardCancelCommand Cancellation(
            RewardCommitCommand commit)
        {
            return RewardCancelCommand.Create(
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
            return RewardApplication.Fingerprint(value.ToString());
        }

        private sealed class Fixture
        {
            public Fixture(
                DeterministicState money,
                DeterministicState scrap,
                DeterministicState holdings)
            {
                Money = money;
                Scrap = scrap;
                Holdings = holdings;
                Service = NewService();
            }

            public DeterministicState Money { get; }
            public DeterministicState Scrap { get; }
            public DeterministicState Holdings { get; }
            public RewardApplicationActions Service { get; }

            public RewardApplicationActions NewService()
            {
                return new RewardApplicationActions(
                    RapAuthority,
                    Money,
                    Scrap,
                    Holdings);
            }
        }

        private sealed class DeterministicState : IRewardChildState
        {
            private readonly HashSet<RewardGrantKind> supportedKinds;
            private readonly Dictionary<StableId, StoredTransaction> transactions;
            private readonly long maximum;
            private long sequence;
            private long value;

            public DeterministicState(
                StableId authorityStableId,
                IEnumerable<RewardGrantKind> supportedKinds,
                long maximum)
            {
                AuthorityStableId = authorityStableId;
                this.supportedKinds = new HashSet<RewardGrantKind>(supportedKinds);
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
            public RewardStateAdmissionStatus? PreflightRejection { get; set; }

            public RewardStatePreflightResult Preflight(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                List<RewardChildGrantCommand> ordered = Copy(commands);
                long simulatedSequence = sequence;
                long simulatedValue = value;
                var facts = new List<RewardStatePreflightFact>(ordered.Count);
                for (int index = 0; index < ordered.Count; index++)
                {
                    RewardChildGrantCommand command = ordered[index];
                    StoredTransaction existing;
                    if (transactions.TryGetValue(command.TransactionStableId, out existing))
                    {
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                                ? RewardStateAdmissionStatus.AlreadyApplied
                                : RewardStateAdmissionStatus.ConflictingDuplicate,
                            string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                                ? null
                                : "transaction-conflict"));
                        continue;
                    }

                    if (command.DestinationAuthorityStableId != AuthorityStableId)
                    {
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            RewardStateAdmissionStatus.AuthorityMismatch,
                            "authority-mismatch"));
                        continue;
                    }

                    if (!supportedKinds.Contains(command.GrantKind))
                    {
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            RewardStateAdmissionStatus.InvalidCommand,
                            "kind-invalid"));
                        continue;
                    }

                    if (PreflightRejection.HasValue)
                    {
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            PreflightRejection.Value,
                            "configured-preflight-rejection"));
                        continue;
                    }

                    if (command.ExpectedSequence.HasValue
                        && command.ExpectedSequence.Value != simulatedSequence)
                    {
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            RewardStateAdmissionStatus.ExpectedSequenceConflict,
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
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            RewardStateAdmissionStatus.CapacityRejected,
                            "value-overflow"));
                        continue;
                    }

                    if (proposed > maximum)
                    {
                        facts.Add(new RewardStatePreflightFact(
                            command.TransactionStableId,
                            RewardStateAdmissionStatus.CapacityRejected,
                            "capacity-rejected"));
                        continue;
                    }

                    simulatedValue = proposed;
                    simulatedSequence++;
                    facts.Add(new RewardStatePreflightFact(
                        command.TransactionStableId,
                        RewardStateAdmissionStatus.Accepted,
                        null));
                }

                return new RewardStatePreflightResult(facts);
            }

            public RewardChildApplyResult Apply(RewardChildGrantCommand command)
            {
                ApplyCallCount++;
                StoredTransaction existing;
                if (transactions.TryGetValue(command.TransactionStableId, out existing))
                {
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                            ? RewardChildApplyStatus.ExactDuplicateNoChange
                            : RewardChildApplyStatus.ConflictingDuplicate,
                        string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal),
                        string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                            ? null
                            : "transaction-conflict");
                }

                if (FailNextApply)
                {
                    FailNextApply = false;
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "configured-interruption");
                }

                if (command.ExpectedSequence.HasValue
                    && command.ExpectedSequence.Value != sequence)
                {
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.ExpectedSequenceConflict,
                        false,
                        "expected-sequence-conflict");
                }

                long proposed = checked(value + command.Quantity);
                if (proposed > maximum)
                {
                    return new RewardChildApplyResult(
                        command.TransactionStableId,
                        RewardChildApplyStatus.CapacityRejected,
                        false,
                        "capacity-rejected");
                }

                value = proposed;
                sequence++;
                transactions.Add(
                    command.TransactionStableId,
                    new StoredTransaction(command.Fingerprint));
                LastAppliedTransactionId = command.TransactionStableId;
                return new RewardChildApplyResult(
                    command.TransactionStableId,
                    RewardChildApplyStatus.Applied,
                    true,
                    null);
            }

            private static List<RewardChildGrantCommand> Copy(
                IReadOnlyList<RewardChildGrantCommand> commands)
            {
                var copy = new List<RewardChildGrantCommand>(commands.Count);
                for (int index = 0; index < commands.Count; index++)
                {
                    copy.Add(commands[index]);
                }

                copy.Sort(delegate(RewardChildGrantCommand left, RewardChildGrantCommand right)
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
