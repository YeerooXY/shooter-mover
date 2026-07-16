using System;
using System.Collections.Generic;
using ShooterMover.Contracts.Rewards.Application;
using ShooterMover.Domain.Common;
using ShooterMover.Domain.Equipment;
using ShooterMover.Domain.Rewards.Application;
using ShooterMover.Domain.Rewards.Model;

namespace ShooterMover.Application.Rewards.Application
{
    /// <summary>
    /// Sole durable reward commitment/claim/application authority. Presentation owns
    /// no reward truth; all child authority mutations are prepared and preflighted as
    /// a complete batch before the first apply call.
    /// </summary>
    public sealed partial class RewardApplicationServiceV1
    {
        private readonly object sync = new object();
        private readonly IRewardChildAuthorityV1 moneyAuthority;
        private readonly IRewardChildAuthorityV1 scrapAuthority;
        private readonly IRewardChildAuthorityV1 holdingsAuthority;
        private Dictionary<StableId, CommitmentRecord> commitments;
        private Dictionary<StableId, IdentityRecord> sourceOperations;
        private Dictionary<StableId, IdentityRecord> projections;
        private Dictionary<StableId, IdentityRecord> claims;
        private Dictionary<StableId, IdentityRecord> cancellations;
        private long sequence;

        public RewardApplicationServiceV1(
            StableId authorityStableId,
            IRewardChildAuthorityV1 moneyAuthority,
            IRewardChildAuthorityV1 scrapAuthority,
            IRewardChildAuthorityV1 holdingsAuthority)
        {
            AuthorityStableId = authorityStableId
                ?? throw new ArgumentNullException(nameof(authorityStableId));
            this.moneyAuthority = moneyAuthority
                ?? throw new ArgumentNullException(nameof(moneyAuthority));
            this.scrapAuthority = scrapAuthority
                ?? throw new ArgumentNullException(nameof(scrapAuthority));
            this.holdingsAuthority = holdingsAuthority
                ?? throw new ArgumentNullException(nameof(holdingsAuthority));
            if (moneyAuthority.AuthorityStableId == scrapAuthority.AuthorityStableId
                || moneyAuthority.AuthorityStableId == holdingsAuthority.AuthorityStableId
                || scrapAuthority.AuthorityStableId == holdingsAuthority.AuthorityStableId)
            {
                throw new ArgumentException(
                    "Money, scrap, and holdings authority identities must be distinct.");
            }

            commitments = new Dictionary<StableId, CommitmentRecord>();
            sourceOperations = new Dictionary<StableId, IdentityRecord>();
            projections = new Dictionary<StableId, IdentityRecord>();
            claims = new Dictionary<StableId, IdentityRecord>();
            cancellations = new Dictionary<StableId, IdentityRecord>();
        }

        public StableId AuthorityStableId { get; }

        public long Sequence
        {
            get
            {
                lock (sync)
                {
                    return sequence;
                }
            }
        }

        public RewardApplicationResultV1 Commit(RewardCommitCommandV1 command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidCommand,
                        null,
                        null,
                        before,
                        null,
                        "commit-command-null");
                }

                IdentityRecord sourceRecord;
                if (sourceOperations.TryGetValue(
                    command.SourceOperationStableId,
                    out sourceRecord))
                {
                    CommitmentRecord original;
                    commitments.TryGetValue(sourceRecord.CommitmentStableId, out original);
                    if (string.Equals(
                        sourceRecord.Fingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return Result(
                            RewardApplicationResultStatusV1.ExactDuplicateNoChange,
                            original,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatusV1.ConflictingDuplicate,
                        original,
                        before,
                        command.Fingerprint,
                        "source-operation-conflicting-duplicate");
                }

                CommitmentRecord existing;
                if (commitments.TryGetValue(command.CommitmentStableId, out existing))
                {
                    if (string.Equals(
                        existing.CommitCommand.Fingerprint,
                        command.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        return Result(
                            RewardApplicationResultStatusV1.ExactDuplicateNoChange,
                            existing,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatusV1.ConflictingDuplicate,
                        existing,
                        before,
                        command.Fingerprint,
                        "commitment-conflicting-duplicate");
                }

                var record = new CommitmentRecord(command);
                commitments.Add(command.CommitmentStableId, record);
                sourceOperations.Add(
                    command.SourceOperationStableId,
                    new IdentityRecord(
                        command.CommitmentStableId,
                        command.Fingerprint));
                sequence++;
                return Result(
                    RewardApplicationResultStatusV1.Generated,
                    record,
                    before,
                    command.Fingerprint,
                    null);
            }
        }

        public RewardApplicationResultV1 Project(RewardProjectCommandV1 command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidCommand,
                        null,
                        null,
                        before,
                        null,
                        "project-command-null");
                }

                IdentityRecord priorProjection;
                if (projections.TryGetValue(command.ProjectionStableId, out priorProjection))
                {
                    CommitmentRecord priorRecord;
                    commitments.TryGetValue(priorProjection.CommitmentStableId, out priorRecord);
                    if (priorProjection.CommitmentStableId == command.CommitmentStableId
                        && string.Equals(
                            priorProjection.Fingerprint,
                            command.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return Result(
                            RewardApplicationResultStatusV1.ExactDuplicateNoChange,
                            priorRecord,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatusV1.ConflictingDuplicate,
                        priorRecord,
                        before,
                        command.Fingerprint,
                        "projection-conflicting-duplicate");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatusV1.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        command.Fingerprint,
                        "commitment-unknown");
                }

                if (record.State != RewardCommitmentStateV1.Generated
                    && record.State != RewardCommitmentStateV1.Projected)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidStateTransition,
                        record,
                        before,
                        command.Fingerprint,
                        "projection-state-invalid");
                }

                record.Projections.Add(command);
                record.Projections.Sort();
                if (record.State == RewardCommitmentStateV1.Generated)
                {
                    record.State = RewardCommitmentStateV1.Projected;
                }

                projections.Add(
                    command.ProjectionStableId,
                    new IdentityRecord(command.CommitmentStableId, command.Fingerprint));
                sequence++;
                return Result(
                    RewardApplicationResultStatusV1.Projected,
                    record,
                    before,
                    command.Fingerprint,
                    null);
            }
        }

        public RewardApplicationResultV1 Claim(RewardClaimCommandV1 command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidCommand,
                        null,
                        null,
                        before,
                        null,
                        "claim-command-null");
                }

                IdentityRecord priorClaim;
                if (claims.TryGetValue(command.ClaimStableId, out priorClaim))
                {
                    CommitmentRecord priorRecord;
                    commitments.TryGetValue(priorClaim.CommitmentStableId, out priorRecord);
                    if (priorClaim.CommitmentStableId == command.CommitmentStableId
                        && string.Equals(
                            priorClaim.Fingerprint,
                            command.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        RewardApplicationResultStatusV1 replayStatus =
                            priorRecord != null
                                && priorRecord.State == RewardCommitmentStateV1.Applied
                                ? RewardApplicationResultStatusV1.AlreadyAppliedNoChange
                                : RewardApplicationResultStatusV1.ExactDuplicateNoChange;
                        return Result(
                            replayStatus,
                            priorRecord,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatusV1.ConflictingDuplicate,
                        priorRecord,
                        before,
                        command.Fingerprint,
                        "claim-conflicting-duplicate");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatusV1.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        command.Fingerprint,
                        "commitment-unknown");
                }

                if (record.State == RewardCommitmentStateV1.Applied)
                {
                    return Result(
                        RewardApplicationResultStatusV1.AlreadyAppliedNoChange,
                        record,
                        before,
                        command.Fingerprint,
                        null);
                }

                if (record.State == RewardCommitmentStateV1.Cancelled
                    || record.State == RewardCommitmentStateV1.Claimed)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidStateTransition,
                        record,
                        before,
                        command.Fingerprint,
                        record.State == RewardCommitmentStateV1.Cancelled
                            ? "claim-cancelled"
                            : "commitment-already-claimed");
                }

                RewardApplicationResultStatusV1 authorityValidation;
                string authorityCode;
                if (!TryValidateClaimAuthorities(
                    command,
                    out authorityValidation,
                    out authorityCode))
                {
                    return Result(
                        authorityValidation,
                        record,
                        before,
                        command.Fingerprint,
                        authorityCode);
                }

                List<RewardChildGrantCommandV1> childCommands =
                    BuildChildPlan(record.CommitCommand, command);
                Dictionary<StableId, RewardAuthorityPreflightFactV1> preflightFacts;
                RewardApplicationResultStatusV1 preflightStatus;
                string preflightCode;
                if (!TryPreflight(
                    childCommands,
                    out preflightFacts,
                    out preflightStatus,
                    out preflightCode))
                {
                    return Result(
                        preflightStatus,
                        record,
                        before,
                        command.Fingerprint,
                        preflightCode);
                }

                record.ClaimCommand = command;
                record.Children.Clear();
                for (int index = 0; index < childCommands.Count; index++)
                {
                    RewardChildGrantCommandV1 child = childCommands[index];
                    RewardAuthorityPreflightFactV1 fact = preflightFacts[child.TransactionStableId];
                    record.Children.Add(new RewardChildApplicationSnapshotV1(
                        child,
                        fact.Status == RewardAuthorityAdmissionStatusV1.AlreadyApplied
                            ? RewardChildResolutionStateV1.Applied
                            : RewardChildResolutionStateV1.Pending,
                        fact.Status == RewardAuthorityAdmissionStatusV1.AlreadyApplied
                            ? (RewardChildApplyStatusV1?)RewardChildApplyStatusV1.ExactDuplicateNoChange
                            : null,
                        fact.RejectionCode));
                }

                OrderChildrenForExecution(record.Children);
                record.State = RewardCommitmentStateV1.Claimed;
                claims.Add(
                    command.ClaimStableId,
                    new IdentityRecord(command.CommitmentStableId, command.Fingerprint));
                sequence++;
                return ApplyPending(record, before, command.Fingerprint);
            }
        }

        public RewardApplicationResultV1 Retry(RewardRetryClaimCommandV1 command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidCommand,
                        null,
                        null,
                        before,
                        null,
                        "retry-command-null");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatusV1.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        null,
                        "commitment-unknown");
                }

                if (record.State == RewardCommitmentStateV1.Applied)
                {
                    return Result(
                        RewardApplicationResultStatusV1.AlreadyAppliedNoChange,
                        record,
                        before,
                        record.ClaimCommand == null ? null : record.ClaimCommand.Fingerprint,
                        null);
                }

                if (record.State != RewardCommitmentStateV1.Claimed
                    || record.ClaimCommand == null
                    || record.ClaimCommand.ClaimStableId != command.ClaimStableId)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidStateTransition,
                        record,
                        before,
                        record.ClaimCommand == null ? null : record.ClaimCommand.Fingerprint,
                        "retry-claim-state-invalid");
                }

                List<RewardChildGrantCommandV1> childCommands = new List<RewardChildGrantCommandV1>();
                for (int index = 0; index < record.Children.Count; index++)
                {
                    childCommands.Add(record.Children[index].Command);
                }

                Dictionary<StableId, RewardAuthorityPreflightFactV1> preflightFacts;
                RewardApplicationResultStatusV1 preflightStatus;
                string preflightCode;
                if (!TryPreflight(
                    childCommands,
                    out preflightFacts,
                    out preflightStatus,
                    out preflightCode))
                {
                    return Result(
                        preflightStatus,
                        record,
                        before,
                        record.ClaimCommand.Fingerprint,
                        preflightCode);
                }

                for (int index = 0; index < record.Children.Count; index++)
                {
                    RewardChildApplicationSnapshotV1 child = record.Children[index];
                    RewardAuthorityPreflightFactV1 fact =
                        preflightFacts[child.Command.TransactionStableId];
                    if (child.ResolutionState == RewardChildResolutionStateV1.Pending
                        && fact.Status == RewardAuthorityAdmissionStatusV1.AlreadyApplied)
                    {
                        record.Children[index] = new RewardChildApplicationSnapshotV1(
                            child.Command,
                            RewardChildResolutionStateV1.Applied,
                            RewardChildApplyStatusV1.ExactDuplicateNoChange,
                            fact.RejectionCode);
                        sequence++;
                    }
                }

                return ApplyPending(
                    record,
                    before,
                    record.ClaimCommand.Fingerprint);
            }
        }

        public RewardApplicationResultV1 Cancel(RewardCancelCommandV1 command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidCommand,
                        null,
                        null,
                        before,
                        null,
                        "cancel-command-null");
                }

                IdentityRecord priorCancellation;
                if (cancellations.TryGetValue(
                    command.CancellationStableId,
                    out priorCancellation))
                {
                    CommitmentRecord priorRecord;
                    commitments.TryGetValue(priorCancellation.CommitmentStableId, out priorRecord);
                    if (priorCancellation.CommitmentStableId == command.CommitmentStableId
                        && string.Equals(
                            priorCancellation.Fingerprint,
                            command.Fingerprint,
                            StringComparison.Ordinal))
                    {
                        return Result(
                            RewardApplicationResultStatusV1.ExactDuplicateNoChange,
                            priorRecord,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatusV1.ConflictingDuplicate,
                        priorRecord,
                        before,
                        command.Fingerprint,
                        "cancellation-conflicting-duplicate");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatusV1.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        command.Fingerprint,
                        "commitment-unknown");
                }

                if (record.State != RewardCommitmentStateV1.Generated
                    && record.State != RewardCommitmentStateV1.Projected)
                {
                    return Result(
                        RewardApplicationResultStatusV1.InvalidStateTransition,
                        record,
                        before,
                        command.Fingerprint,
                        "cancellation-state-invalid");
                }

                record.CancelCommand = command;
                record.State = RewardCommitmentStateV1.Cancelled;
                cancellations.Add(
                    command.CancellationStableId,
                    new IdentityRecord(command.CommitmentStableId, command.Fingerprint));
                sequence++;
                return Result(
                    RewardApplicationResultStatusV1.Cancelled,
                    record,
                    before,
                    command.Fingerprint,
                    null);
            }
        }

        public bool TryGetCommitment(
            StableId commitmentStableId,
            out RewardCommitmentSnapshotV1 snapshot)
        {
            lock (sync)
            {
                CommitmentRecord record;
                if (commitmentStableId != null
                    && commitments.TryGetValue(commitmentStableId, out record))
                {
                    snapshot = record.ToSnapshot();
                    return true;
                }

                snapshot = null;
                return false;
            }
        }

        private RewardApplicationResultV1 ApplyPending(
            CommitmentRecord record,
            long operationSequenceBefore,
            string commandFingerprint)
        {
            string firstRejection = null;
            for (int index = 0; index < record.Children.Count; index++)
            {
                RewardChildApplicationSnapshotV1 child = record.Children[index];
                if (child.ResolutionState == RewardChildResolutionStateV1.Applied)
                {
                    continue;
                }

                RewardChildApplyResultV1 applied;
                try
                {
                    applied = AuthorityFor(child.Command.GrantKind).Apply(child.Command);
                }
                catch (Exception exception)
                {
                    applied = new RewardChildApplyResultV1(
                        child.Command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "child-authority-exception-"
                        + exception.GetType().Name.ToLowerInvariant());
                }

                if (applied == null
                    || applied.TransactionStableId != child.Command.TransactionStableId)
                {
                    applied = new RewardChildApplyResultV1(
                        child.Command.TransactionStableId,
                        RewardChildApplyStatusV1.Rejected,
                        false,
                        "child-authority-result-invalid");
                }

                if (applied.IsConfirmedApplied)
                {
                    record.Children[index] = new RewardChildApplicationSnapshotV1(
                        child.Command,
                        RewardChildResolutionStateV1.Applied,
                        applied.Status,
                        applied.RejectionCode);
                    sequence++;
                }
                else
                {
                    RewardChildApplicationSnapshotV1 replacement =
                        new RewardChildApplicationSnapshotV1(
                            child.Command,
                            RewardChildResolutionStateV1.Pending,
                            applied.Status,
                            applied.RejectionCode);
                    if (!string.Equals(
                        replacement.Fingerprint,
                        child.Fingerprint,
                        StringComparison.Ordinal))
                    {
                        record.Children[index] = replacement;
                        sequence++;
                    }

                    if (firstRejection == null)
                    {
                        firstRejection = applied.RejectionCode
                            ?? "child-authority-rejected";
                    }
                }
            }

            if (AllChildrenApplied(record))
            {
                if (record.State != RewardCommitmentStateV1.Applied)
                {
                    record.State = RewardCommitmentStateV1.Applied;
                    sequence++;
                }

                return Result(
                    RewardApplicationResultStatusV1.Applied,
                    record,
                    operationSequenceBefore,
                    commandFingerprint,
                    null);
            }

            return Result(
                RewardApplicationResultStatusV1.ClaimedPendingApplication,
                record,
                operationSequenceBefore,
                commandFingerprint,
                firstRejection ?? "child-application-pending");
        }

        private bool TryPreflight(
            IReadOnlyList<RewardChildGrantCommandV1> childCommands,
            out Dictionary<StableId, RewardAuthorityPreflightFactV1> facts,
            out RewardApplicationResultStatusV1 failureStatus,
            out string rejectionCode)
        {
            facts = new Dictionary<StableId, RewardAuthorityPreflightFactV1>();
            var money = new List<RewardChildGrantCommandV1>();
            var scrap = new List<RewardChildGrantCommandV1>();
            var holdings = new List<RewardChildGrantCommandV1>();
            for (int index = 0; index < childCommands.Count; index++)
            {
                RewardChildGrantCommandV1 child = childCommands[index];
                switch (child.GrantKind)
                {
                    case RewardGrantKindV1.Money:
                        money.Add(child);
                        break;
                    case RewardGrantKindV1.Scrap:
                        scrap.Add(child);
                        break;
                    case RewardGrantKindV1.Strongbox:
                    case RewardGrantKindV1.EquipmentReference:
                    case RewardGrantKindV1.PremiumAmmo:
                    case RewardGrantKindV1.Miscellaneous:
                        holdings.Add(child);
                        break;
                    default:
                        failureStatus = RewardApplicationResultStatusV1.InvalidCommand;
                        rejectionCode = "grant-kind-unsupported";
                        return false;
                }
            }

            RewardAuthorityPreflightResultV1[] results;
            try
            {
                results = new[]
                {
                    money.Count == 0 ? EmptyPreflight() : moneyAuthority.Preflight(money),
                    scrap.Count == 0 ? EmptyPreflight() : scrapAuthority.Preflight(scrap),
                    holdings.Count == 0 ? EmptyPreflight() : holdingsAuthority.Preflight(holdings),
                };
            }
            catch (Exception exception)
            {
                failureStatus = RewardApplicationResultStatusV1.ChildAuthorityRejected;
                rejectionCode = "preflight-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }

            for (int resultIndex = 0; resultIndex < results.Length; resultIndex++)
            {
                RewardAuthorityPreflightResultV1 result = results[resultIndex];
                if (result == null)
                {
                    failureStatus = RewardApplicationResultStatusV1.ChildAuthorityRejected;
                    rejectionCode = "preflight-result-null";
                    return false;
                }

                for (int factIndex = 0; factIndex < result.Facts.Count; factIndex++)
                {
                    RewardAuthorityPreflightFactV1 fact = result.Facts[factIndex];
                    if (facts.ContainsKey(fact.TransactionStableId))
                    {
                        failureStatus = RewardApplicationResultStatusV1.ChildAuthorityRejected;
                        rejectionCode = "preflight-duplicate-transaction-fact";
                        return false;
                    }

                    facts.Add(fact.TransactionStableId, fact);
                }
            }

            if (facts.Count != childCommands.Count)
            {
                failureStatus = RewardApplicationResultStatusV1.ChildAuthorityRejected;
                rejectionCode = "preflight-fact-count-mismatch";
                return false;
            }

            for (int index = 0; index < childCommands.Count; index++)
            {
                RewardAuthorityPreflightFactV1 fact;
                if (!facts.TryGetValue(childCommands[index].TransactionStableId, out fact))
                {
                    failureStatus = RewardApplicationResultStatusV1.ChildAuthorityRejected;
                    rejectionCode = "preflight-fact-missing";
                    return false;
                }

                if (!fact.CanProceed)
                {
                    failureStatus = MapAdmissionFailure(fact.Status);
                    rejectionCode = fact.RejectionCode ?? "preflight-rejected";
                    return false;
                }
            }

            failureStatus = RewardApplicationResultStatusV1.Applied;
            rejectionCode = null;
            return true;
        }

        private bool TryValidateClaimAuthorities(
            RewardClaimCommandV1 command,
            out RewardApplicationResultStatusV1 status,
            out string rejectionCode)
        {
            if (command.MoneyAuthorityStableId != moneyAuthority.AuthorityStableId
                || command.ScrapAuthorityStableId != scrapAuthority.AuthorityStableId
                || command.HoldingsAuthorityStableId != holdingsAuthority.AuthorityStableId)
            {
                status = RewardApplicationResultStatusV1.AuthorityMismatch;
                rejectionCode = "claim-authority-mismatch";
                return false;
            }

            status = RewardApplicationResultStatusV1.Applied;
            rejectionCode = null;
            return true;
        }

        private List<RewardChildGrantCommandV1> BuildChildPlan(
            RewardCommitCommandV1 commit,
            RewardClaimCommandV1 claim)
        {
            var result = new List<RewardChildGrantCommandV1>();
            int moneyOrdinal = 0;
            int scrapOrdinal = 0;
            int holdingsOrdinal = 0;
            for (int payloadIndex = 0; payloadIndex < commit.GrantPayloads.Count; payloadIndex++)
            {
                RewardGrantApplicationPayloadV1 payload = commit.GrantPayloads[payloadIndex];
                RewardGrantKindV1 kind = payload.Grant.Kind;
                if (kind == RewardGrantKindV1.Strongbox
                    || kind == RewardGrantKindV1.EquipmentReference)
                {
                    for (int unit = 0; unit < payload.InstanceStableIds.Count; unit++)
                    {
                        long? expected = IncrementExpected(
                            claim.ExpectedHoldingsSequence,
                            holdingsOrdinal++);
                        result.Add(CreateChild(
                            commit,
                            claim,
                            payload,
                            unit,
                            holdingsAuthority.AuthorityStableId,
                            1L,
                            payload.InstanceStableIds[unit],
                            kind == RewardGrantKindV1.EquipmentReference
                                ? payload.EquipmentInstances[unit]
                                : null,
                            expected));
                    }
                }
                else
                {
                    StableId destination;
                    long? expected;
                    if (kind == RewardGrantKindV1.Money)
                    {
                        destination = moneyAuthority.AuthorityStableId;
                        expected = IncrementExpected(
                            claim.ExpectedMoneySequence,
                            moneyOrdinal++);
                    }
                    else if (kind == RewardGrantKindV1.Scrap)
                    {
                        destination = scrapAuthority.AuthorityStableId;
                        expected = IncrementExpected(
                            claim.ExpectedScrapSequence,
                            scrapOrdinal++);
                    }
                    else
                    {
                        destination = holdingsAuthority.AuthorityStableId;
                        expected = IncrementExpected(
                            claim.ExpectedHoldingsSequence,
                            holdingsOrdinal++);
                    }

                    result.Add(CreateChild(
                        commit,
                        claim,
                        payload,
                        0,
                        destination,
                        payload.Grant.Quantity,
                        null,
                        null,
                        expected));
                }
            }

            return result;
        }

        private static RewardChildGrantCommandV1 CreateChild(
            RewardCommitCommandV1 commit,
            RewardClaimCommandV1 claim,
            RewardGrantApplicationPayloadV1 payload,
            int unitOrdinal,
            StableId destinationAuthorityStableId,
            long quantity,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            long? expectedSequence)
        {
            string ordinal = unitOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            StableId transactionId = RewardApplicationCanonicalV1.DeriveStableId(
                "raptx",
                commit.CommitmentStableId.ToString(),
                claim.ClaimStableId.ToString(),
                payload.Grant.GrantStableId.ToString(),
                ordinal,
                destinationAuthorityStableId.ToString());
            StableId operationId = RewardApplicationCanonicalV1.DeriveStableId(
                "rapop",
                commit.SourceOperationStableId.ToString(),
                claim.ClaimStableId.ToString(),
                payload.Grant.GrantStableId.ToString(),
                ordinal,
                destinationAuthorityStableId.ToString());
            return RewardChildGrantCommandV1.Create(
                transactionId,
                operationId,
                destinationAuthorityStableId,
                commit.SourceOperationStableId,
                claim.ClaimantStableId,
                payload.Grant.GrantStableId,
                payload.Grant.Kind,
                payload.Grant.ContentStableId,
                quantity,
                instanceStableId,
                equipmentInstance,
                expectedSequence);
        }

        private IRewardChildAuthorityV1 AuthorityFor(RewardGrantKindV1 kind)
        {
            if (kind == RewardGrantKindV1.Money)
            {
                return moneyAuthority;
            }

            if (kind == RewardGrantKindV1.Scrap)
            {
                return scrapAuthority;
            }

            return holdingsAuthority;
        }

        private static long? IncrementExpected(long? baseSequence, int ordinal)
        {
            return baseSequence.HasValue
                ? checked(baseSequence.Value + ordinal)
                : (long?)null;
        }

        private void OrderChildrenForExecution(
            List<RewardChildApplicationSnapshotV1> children)
        {
            children.Sort(delegate(
                RewardChildApplicationSnapshotV1 left,
                RewardChildApplicationSnapshotV1 right)
            {
                int authorityComparison = AuthorityRank(left.Command.GrantKind)
                    .CompareTo(AuthorityRank(right.Command.GrantKind));
                if (authorityComparison != 0)
                {
                    return authorityComparison;
                }

                long? leftSequence = left.Command.ExpectedSequence;
                long? rightSequence = right.Command.ExpectedSequence;
                if (leftSequence.HasValue && rightSequence.HasValue)
                {
                    int sequenceComparison = leftSequence.Value.CompareTo(
                        rightSequence.Value);
                    if (sequenceComparison != 0)
                    {
                        return sequenceComparison;
                    }
                }
                else if (leftSequence.HasValue)
                {
                    return -1;
                }
                else if (rightSequence.HasValue)
                {
                    return 1;
                }

                int grantComparison = left.Command.GrantStableId.CompareTo(
                    right.Command.GrantStableId);
                return grantComparison != 0
                    ? grantComparison
                    : left.Command.TransactionStableId.CompareTo(
                        right.Command.TransactionStableId);
            });
        }

        private static int AuthorityRank(RewardGrantKindV1 kind)
        {
            if (kind == RewardGrantKindV1.Money)
            {
                return 0;
            }

            if (kind == RewardGrantKindV1.Scrap)
            {
                return 1;
            }

            return 2;
        }

        private static bool AllChildrenApplied(CommitmentRecord record)
        {
            for (int index = 0; index < record.Children.Count; index++)
            {
                if (record.Children[index].ResolutionState
                    != RewardChildResolutionStateV1.Applied)
                {
                    return false;
                }
            }

            return true;
        }

        private static RewardAuthorityPreflightResultV1 EmptyPreflight()
        {
            return new RewardAuthorityPreflightResultV1(
                Array.Empty<RewardAuthorityPreflightFactV1>());
        }

        private static RewardApplicationResultStatusV1 MapAdmissionFailure(
            RewardAuthorityAdmissionStatusV1 status)
        {
            switch (status)
            {
                case RewardAuthorityAdmissionStatusV1.ConflictingDuplicate:
                    return RewardApplicationResultStatusV1.ConflictingDuplicate;
                case RewardAuthorityAdmissionStatusV1.AuthorityMismatch:
                    return RewardApplicationResultStatusV1.AuthorityMismatch;
                case RewardAuthorityAdmissionStatusV1.ExpectedSequenceConflict:
                    return RewardApplicationResultStatusV1.ExpectedSequenceConflict;
                case RewardAuthorityAdmissionStatusV1.InsufficientFunds:
                    return RewardApplicationResultStatusV1.InsufficientFunds;
                case RewardAuthorityAdmissionStatusV1.CapacityRejected:
                    return RewardApplicationResultStatusV1.CapacityRejected;
                case RewardAuthorityAdmissionStatusV1.InvalidCommand:
                    return RewardApplicationResultStatusV1.InvalidCommand;
                default:
                    return RewardApplicationResultStatusV1.ChildAuthorityRejected;
            }
        }

        private RewardApplicationResultV1 Result(
            RewardApplicationResultStatusV1 status,
            CommitmentRecord record,
            long previousSequence,
            string commandFingerprint,
            string rejectionCode)
        {
            return Result(
                status,
                record == null ? null : record.CommitCommand.CommitmentStableId,
                record == null ? (RewardCommitmentStateV1?)null : record.State,
                previousSequence,
                commandFingerprint,
                rejectionCode,
                record == null ? null : record.ToSnapshot());
        }

        private RewardApplicationResultV1 Result(
            RewardApplicationResultStatusV1 status,
            StableId commitmentStableId,
            RewardCommitmentStateV1? state,
            long previousSequence,
            string commandFingerprint,
            string rejectionCode)
        {
            return Result(
                status,
                commitmentStableId,
                state,
                previousSequence,
                commandFingerprint,
                rejectionCode,
                null);
        }

        private RewardApplicationResultV1 Result(
            RewardApplicationResultStatusV1 status,
            StableId commitmentStableId,
            RewardCommitmentStateV1? state,
            long previousSequence,
            string commandFingerprint,
            string rejectionCode,
            RewardCommitmentSnapshotV1 snapshot)
        {
            return new RewardApplicationResultV1(
                status,
                commitmentStableId,
                state,
                previousSequence,
                sequence,
                commandFingerprint,
                rejectionCode,
                snapshot);
        }

        private sealed class IdentityRecord
        {
            public IdentityRecord(StableId commitmentStableId, string fingerprint)
            {
                CommitmentStableId = commitmentStableId;
                Fingerprint = fingerprint;
            }

            public StableId CommitmentStableId { get; }
            public string Fingerprint { get; }
        }

        private sealed class CommitmentRecord
        {
            public CommitmentRecord(RewardCommitCommandV1 commitCommand)
            {
                CommitCommand = commitCommand;
                State = RewardCommitmentStateV1.Generated;
                Projections = new List<RewardProjectCommandV1>();
                Children = new List<RewardChildApplicationSnapshotV1>();
            }

            public RewardCommitCommandV1 CommitCommand { get; }
            public RewardCommitmentStateV1 State { get; set; }
            public List<RewardProjectCommandV1> Projections { get; }
            public RewardClaimCommandV1 ClaimCommand { get; set; }
            public List<RewardChildApplicationSnapshotV1> Children { get; }
            public RewardCancelCommandV1 CancelCommand { get; set; }

            public RewardCommitmentSnapshotV1 ToSnapshot()
            {
                return RewardCommitmentSnapshotV1.CreateCanonical(
                    CommitCommand,
                    State,
                    Projections,
                    ClaimCommand,
                    Children,
                    CancelCommand);
            }

            public static CommitmentRecord FromSnapshot(
                RewardCommitmentSnapshotV1 snapshot)
            {
                var record = new CommitmentRecord(snapshot.CommitCommand);
                record.State = snapshot.State;
                record.Projections.AddRange(snapshot.Projections);
                record.ClaimCommand = snapshot.ClaimCommand;
                record.Children.AddRange(snapshot.Children);
                record.CancelCommand = snapshot.CancelCommand;
                return record;
            }
        }
    }
}
