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
    public sealed partial class RewardApplicationActions
    {
        private readonly object sync = new object();
        private readonly IRewardChildState moneyAuthority;
        private readonly IRewardChildState scrapAuthority;
        private readonly IRewardChildState holdingsAuthority;
        private Dictionary<StableId, CommitmentRecord> commitments;
        private Dictionary<StableId, IdentityRecord> sourceOperations;
        private Dictionary<StableId, IdentityRecord> projections;
        private Dictionary<StableId, IdentityRecord> claims;
        private Dictionary<StableId, IdentityRecord> cancellations;
        private long sequence;

        public RewardApplicationActions(
            StableId authorityStableId,
            IRewardChildState moneyAuthority,
            IRewardChildState scrapAuthority,
            IRewardChildState holdingsAuthority)
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

        public RewardApplicationResult Commit(RewardCommitCommand command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidCommand,
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
                            RewardApplicationResultStatus.ExactDuplicateNoChange,
                            original,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatus.ConflictingDuplicate,
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
                            RewardApplicationResultStatus.ExactDuplicateNoChange,
                            existing,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatus.ConflictingDuplicate,
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
                    RewardApplicationResultStatus.Generated,
                    record,
                    before,
                    command.Fingerprint,
                    null);
            }
        }

        public RewardApplicationResult Project(RewardProjectCommand command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidCommand,
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
                            RewardApplicationResultStatus.ExactDuplicateNoChange,
                            priorRecord,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatus.ConflictingDuplicate,
                        priorRecord,
                        before,
                        command.Fingerprint,
                        "projection-conflicting-duplicate");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatus.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        command.Fingerprint,
                        "commitment-unknown");
                }

                if (record.State != RewardCommitmentState.Generated
                    && record.State != RewardCommitmentState.Projected)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidStateTransition,
                        record,
                        before,
                        command.Fingerprint,
                        "projection-state-invalid");
                }

                record.Projections.Add(command);
                record.Projections.Sort();
                if (record.State == RewardCommitmentState.Generated)
                {
                    record.State = RewardCommitmentState.Projected;
                }

                projections.Add(
                    command.ProjectionStableId,
                    new IdentityRecord(command.CommitmentStableId, command.Fingerprint));
                sequence++;
                return Result(
                    RewardApplicationResultStatus.Projected,
                    record,
                    before,
                    command.Fingerprint,
                    null);
            }
        }

        public RewardApplicationResult Claim(RewardClaimCommand command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidCommand,
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
                        RewardApplicationResultStatus replayStatus =
                            priorRecord != null
                                && priorRecord.State == RewardCommitmentState.Applied
                                ? RewardApplicationResultStatus.AlreadyAppliedNoChange
                                : RewardApplicationResultStatus.ExactDuplicateNoChange;
                        return Result(
                            replayStatus,
                            priorRecord,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatus.ConflictingDuplicate,
                        priorRecord,
                        before,
                        command.Fingerprint,
                        "claim-conflicting-duplicate");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatus.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        command.Fingerprint,
                        "commitment-unknown");
                }

                if (record.State == RewardCommitmentState.Applied)
                {
                    return Result(
                        RewardApplicationResultStatus.AlreadyAppliedNoChange,
                        record,
                        before,
                        command.Fingerprint,
                        null);
                }

                if (record.State == RewardCommitmentState.Cancelled
                    || record.State == RewardCommitmentState.Claimed)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidStateTransition,
                        record,
                        before,
                        command.Fingerprint,
                        record.State == RewardCommitmentState.Cancelled
                            ? "claim-cancelled"
                            : "commitment-already-claimed");
                }

                RewardApplicationResultStatus authorityValidation;
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

                List<RewardChildGrantCommand> childCommands =
                    BuildChildPlan(record.CommitCommand, command);
                Dictionary<StableId, RewardStatePreflightFact> preflightFacts;
                RewardApplicationResultStatus preflightStatus;
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
                    RewardChildGrantCommand child = childCommands[index];
                    RewardStatePreflightFact fact = preflightFacts[child.TransactionStableId];
                    record.Children.Add(new RewardChildApplicationSnapshot(
                        child,
                        fact.Status == RewardStateAdmissionStatus.AlreadyApplied
                            ? RewardChildResolutionState.Applied
                            : RewardChildResolutionState.Pending,
                        fact.Status == RewardStateAdmissionStatus.AlreadyApplied
                            ? (RewardChildApplyStatus?)RewardChildApplyStatus.ExactDuplicateNoChange
                            : null,
                        fact.RejectionCode));
                }

                OrderChildrenForExecution(record.Children);
                record.State = RewardCommitmentState.Claimed;
                claims.Add(
                    command.ClaimStableId,
                    new IdentityRecord(command.CommitmentStableId, command.Fingerprint));
                sequence++;
                return ApplyPending(record, before, command.Fingerprint);
            }
        }

        public RewardApplicationResult Retry(RewardRetryClaimCommand command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidCommand,
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
                        RewardApplicationResultStatus.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        null,
                        "commitment-unknown");
                }

                if (record.State == RewardCommitmentState.Applied)
                {
                    return Result(
                        RewardApplicationResultStatus.AlreadyAppliedNoChange,
                        record,
                        before,
                        record.ClaimCommand == null ? null : record.ClaimCommand.Fingerprint,
                        null);
                }

                if (record.State != RewardCommitmentState.Claimed
                    || record.ClaimCommand == null
                    || record.ClaimCommand.ClaimStableId != command.ClaimStableId)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidStateTransition,
                        record,
                        before,
                        record.ClaimCommand == null ? null : record.ClaimCommand.Fingerprint,
                        "retry-claim-state-invalid");
                }

                List<RewardChildGrantCommand> childCommands = new List<RewardChildGrantCommand>();
                for (int index = 0; index < record.Children.Count; index++)
                {
                    childCommands.Add(record.Children[index].Command);
                }

                Dictionary<StableId, RewardStatePreflightFact> preflightFacts;
                RewardApplicationResultStatus preflightStatus;
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
                    RewardChildApplicationSnapshot child = record.Children[index];
                    RewardStatePreflightFact fact =
                        preflightFacts[child.Command.TransactionStableId];
                    if (child.ResolutionState == RewardChildResolutionState.Pending
                        && fact.Status == RewardStateAdmissionStatus.AlreadyApplied)
                    {
                        record.Children[index] = new RewardChildApplicationSnapshot(
                            child.Command,
                            RewardChildResolutionState.Applied,
                            RewardChildApplyStatus.ExactDuplicateNoChange,
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

        public RewardApplicationResult Cancel(RewardCancelCommand command)
        {
            lock (sync)
            {
                long before = sequence;
                if (command == null)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidCommand,
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
                            RewardApplicationResultStatus.ExactDuplicateNoChange,
                            priorRecord,
                            before,
                            command.Fingerprint,
                            null);
                    }

                    return Result(
                        RewardApplicationResultStatus.ConflictingDuplicate,
                        priorRecord,
                        before,
                        command.Fingerprint,
                        "cancellation-conflicting-duplicate");
                }

                CommitmentRecord record;
                if (!commitments.TryGetValue(command.CommitmentStableId, out record))
                {
                    return Result(
                        RewardApplicationResultStatus.UnknownCommitment,
                        command.CommitmentStableId,
                        null,
                        before,
                        command.Fingerprint,
                        "commitment-unknown");
                }

                if (record.State != RewardCommitmentState.Generated
                    && record.State != RewardCommitmentState.Projected)
                {
                    return Result(
                        RewardApplicationResultStatus.InvalidStateTransition,
                        record,
                        before,
                        command.Fingerprint,
                        "cancellation-state-invalid");
                }

                record.CancelCommand = command;
                record.State = RewardCommitmentState.Cancelled;
                cancellations.Add(
                    command.CancellationStableId,
                    new IdentityRecord(command.CommitmentStableId, command.Fingerprint));
                sequence++;
                return Result(
                    RewardApplicationResultStatus.Cancelled,
                    record,
                    before,
                    command.Fingerprint,
                    null);
            }
        }

        public bool TryGetCommitment(
            StableId commitmentStableId,
            out RewardCommitmentSnapshot snapshot)
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

        private RewardApplicationResult ApplyPending(
            CommitmentRecord record,
            long operationSequenceBefore,
            string commandFingerprint)
        {
            string firstRejection = null;
            for (int index = 0; index < record.Children.Count; index++)
            {
                RewardChildApplicationSnapshot child = record.Children[index];
                if (child.ResolutionState == RewardChildResolutionState.Applied)
                {
                    continue;
                }

                RewardChildApplyResult applied;
                try
                {
                    applied = AuthorityFor(child.Command.GrantKind).Apply(child.Command);
                }
                catch (Exception exception)
                {
                    applied = new RewardChildApplyResult(
                        child.Command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "child-authority-exception-"
                        + exception.GetType().Name.ToLowerInvariant());
                }

                if (applied == null
                    || applied.TransactionStableId != child.Command.TransactionStableId)
                {
                    applied = new RewardChildApplyResult(
                        child.Command.TransactionStableId,
                        RewardChildApplyStatus.Rejected,
                        false,
                        "child-authority-result-invalid");
                }

                if (applied.IsConfirmedApplied)
                {
                    record.Children[index] = new RewardChildApplicationSnapshot(
                        child.Command,
                        RewardChildResolutionState.Applied,
                        applied.Status,
                        applied.RejectionCode);
                    sequence++;
                }
                else
                {
                    RewardChildApplicationSnapshot replacement =
                        new RewardChildApplicationSnapshot(
                            child.Command,
                            RewardChildResolutionState.Pending,
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
                if (record.State != RewardCommitmentState.Applied)
                {
                    record.State = RewardCommitmentState.Applied;
                    sequence++;
                }

                return Result(
                    RewardApplicationResultStatus.Applied,
                    record,
                    operationSequenceBefore,
                    commandFingerprint,
                    null);
            }

            return Result(
                RewardApplicationResultStatus.ClaimedPendingApplication,
                record,
                operationSequenceBefore,
                commandFingerprint,
                firstRejection ?? "child-application-pending");
        }

        private bool TryPreflight(
            IReadOnlyList<RewardChildGrantCommand> childCommands,
            out Dictionary<StableId, RewardStatePreflightFact> facts,
            out RewardApplicationResultStatus failureStatus,
            out string rejectionCode)
        {
            facts = new Dictionary<StableId, RewardStatePreflightFact>();
            var money = new List<RewardChildGrantCommand>();
            var scrap = new List<RewardChildGrantCommand>();
            var holdings = new List<RewardChildGrantCommand>();
            for (int index = 0; index < childCommands.Count; index++)
            {
                RewardChildGrantCommand child = childCommands[index];
                switch (child.GrantKind)
                {
                    case RewardGrantKind.Money:
                        money.Add(child);
                        break;
                    case RewardGrantKind.Scrap:
                        scrap.Add(child);
                        break;
                    case RewardGrantKind.Strongbox:
                    case RewardGrantKind.EquipmentReference:
                    case RewardGrantKind.PremiumAmmo:
                    case RewardGrantKind.Miscellaneous:
                        holdings.Add(child);
                        break;
                    default:
                        failureStatus = RewardApplicationResultStatus.InvalidCommand;
                        rejectionCode = "grant-kind-unsupported";
                        return false;
                }
            }

            RewardStatePreflightResult[] results;
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
                failureStatus = RewardApplicationResultStatus.ChildAuthorityRejected;
                rejectionCode = "preflight-exception-"
                    + exception.GetType().Name.ToLowerInvariant();
                return false;
            }

            for (int resultIndex = 0; resultIndex < results.Length; resultIndex++)
            {
                RewardStatePreflightResult result = results[resultIndex];
                if (result == null)
                {
                    failureStatus = RewardApplicationResultStatus.ChildAuthorityRejected;
                    rejectionCode = "preflight-result-null";
                    return false;
                }

                for (int factIndex = 0; factIndex < result.Facts.Count; factIndex++)
                {
                    RewardStatePreflightFact fact = result.Facts[factIndex];
                    if (facts.ContainsKey(fact.TransactionStableId))
                    {
                        failureStatus = RewardApplicationResultStatus.ChildAuthorityRejected;
                        rejectionCode = "preflight-duplicate-transaction-fact";
                        return false;
                    }

                    facts.Add(fact.TransactionStableId, fact);
                }
            }

            if (facts.Count != childCommands.Count)
            {
                failureStatus = RewardApplicationResultStatus.ChildAuthorityRejected;
                rejectionCode = "preflight-fact-count-mismatch";
                return false;
            }

            for (int index = 0; index < childCommands.Count; index++)
            {
                RewardStatePreflightFact fact;
                if (!facts.TryGetValue(childCommands[index].TransactionStableId, out fact))
                {
                    failureStatus = RewardApplicationResultStatus.ChildAuthorityRejected;
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

            failureStatus = RewardApplicationResultStatus.Applied;
            rejectionCode = null;
            return true;
        }

        private bool TryValidateClaimAuthorities(
            RewardClaimCommand command,
            out RewardApplicationResultStatus status,
            out string rejectionCode)
        {
            if (command.MoneyAuthorityStableId != moneyAuthority.AuthorityStableId
                || command.ScrapAuthorityStableId != scrapAuthority.AuthorityStableId
                || command.HoldingsAuthorityStableId != holdingsAuthority.AuthorityStableId)
            {
                status = RewardApplicationResultStatus.AuthorityMismatch;
                rejectionCode = "claim-authority-mismatch";
                return false;
            }

            status = RewardApplicationResultStatus.Applied;
            rejectionCode = null;
            return true;
        }

        private List<RewardChildGrantCommand> BuildChildPlan(
            RewardCommitCommand commit,
            RewardClaimCommand claim)
        {
            var result = new List<RewardChildGrantCommand>();
            int moneyOrdinal = 0;
            int scrapOrdinal = 0;
            int holdingsOrdinal = 0;
            for (int payloadIndex = 0; payloadIndex < commit.GrantPayloads.Count; payloadIndex++)
            {
                RewardGrantApplicationPayload payload = commit.GrantPayloads[payloadIndex];
                RewardGrantKind kind = payload.Grant.Kind;
                if (kind == RewardGrantKind.Strongbox
                    || kind == RewardGrantKind.EquipmentReference)
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
                            kind == RewardGrantKind.EquipmentReference
                                ? payload.EquipmentInstances[unit]
                                : null,
                            expected));
                    }
                }
                else
                {
                    StableId destination;
                    long? expected;
                    if (kind == RewardGrantKind.Money)
                    {
                        destination = moneyAuthority.AuthorityStableId;
                        expected = IncrementExpected(
                            claim.ExpectedMoneySequence,
                            moneyOrdinal++);
                    }
                    else if (kind == RewardGrantKind.Scrap)
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

        private static RewardChildGrantCommand CreateChild(
            RewardCommitCommand commit,
            RewardClaimCommand claim,
            RewardGrantApplicationPayload payload,
            int unitOrdinal,
            StableId destinationAuthorityStableId,
            long quantity,
            StableId instanceStableId,
            EquipmentInstance equipmentInstance,
            long? expectedSequence)
        {
            string ordinal = unitOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
            StableId transactionId = RewardApplication.DeriveStableId(
                "raptx",
                commit.CommitmentStableId.ToString(),
                claim.ClaimStableId.ToString(),
                payload.Grant.GrantStableId.ToString(),
                ordinal,
                destinationAuthorityStableId.ToString());
            StableId operationId = RewardApplication.DeriveStableId(
                "rapop",
                commit.SourceOperationStableId.ToString(),
                claim.ClaimStableId.ToString(),
                payload.Grant.GrantStableId.ToString(),
                ordinal,
                destinationAuthorityStableId.ToString());
            return RewardChildGrantCommand.Create(
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

        private IRewardChildState AuthorityFor(RewardGrantKind kind)
        {
            if (kind == RewardGrantKind.Money)
            {
                return moneyAuthority;
            }

            if (kind == RewardGrantKind.Scrap)
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
            List<RewardChildApplicationSnapshot> children)
        {
            children.Sort(delegate(
                RewardChildApplicationSnapshot left,
                RewardChildApplicationSnapshot right)
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

        private static int AuthorityRank(RewardGrantKind kind)
        {
            if (kind == RewardGrantKind.Money)
            {
                return 0;
            }

            if (kind == RewardGrantKind.Scrap)
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
                    != RewardChildResolutionState.Applied)
                {
                    return false;
                }
            }

            return true;
        }

        private static RewardStatePreflightResult EmptyPreflight()
        {
            return new RewardStatePreflightResult(
                Array.Empty<RewardStatePreflightFact>());
        }

        private static RewardApplicationResultStatus MapAdmissionFailure(
            RewardStateAdmissionStatus status)
        {
            switch (status)
            {
                case RewardStateAdmissionStatus.ConflictingDuplicate:
                    return RewardApplicationResultStatus.ConflictingDuplicate;
                case RewardStateAdmissionStatus.AuthorityMismatch:
                    return RewardApplicationResultStatus.AuthorityMismatch;
                case RewardStateAdmissionStatus.ExpectedSequenceConflict:
                    return RewardApplicationResultStatus.ExpectedSequenceConflict;
                case RewardStateAdmissionStatus.InsufficientFunds:
                    return RewardApplicationResultStatus.InsufficientFunds;
                case RewardStateAdmissionStatus.CapacityRejected:
                    return RewardApplicationResultStatus.CapacityRejected;
                case RewardStateAdmissionStatus.InvalidCommand:
                    return RewardApplicationResultStatus.InvalidCommand;
                default:
                    return RewardApplicationResultStatus.ChildAuthorityRejected;
            }
        }

        private RewardApplicationResult Result(
            RewardApplicationResultStatus status,
            CommitmentRecord record,
            long previousSequence,
            string commandFingerprint,
            string rejectionCode)
        {
            return Result(
                status,
                record == null ? null : record.CommitCommand.CommitmentStableId,
                record == null ? (RewardCommitmentState?)null : record.State,
                previousSequence,
                commandFingerprint,
                rejectionCode,
                record == null ? null : record.ToSnapshot());
        }

        private RewardApplicationResult Result(
            RewardApplicationResultStatus status,
            StableId commitmentStableId,
            RewardCommitmentState? state,
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

        private RewardApplicationResult Result(
            RewardApplicationResultStatus status,
            StableId commitmentStableId,
            RewardCommitmentState? state,
            long previousSequence,
            string commandFingerprint,
            string rejectionCode,
            RewardCommitmentSnapshot snapshot)
        {
            return new RewardApplicationResult(
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
            public CommitmentRecord(RewardCommitCommand commitCommand)
            {
                CommitCommand = commitCommand;
                State = RewardCommitmentState.Generated;
                Projections = new List<RewardProjectCommand>();
                Children = new List<RewardChildApplicationSnapshot>();
            }

            public RewardCommitCommand CommitCommand { get; }
            public RewardCommitmentState State { get; set; }
            public List<RewardProjectCommand> Projections { get; }
            public RewardClaimCommand ClaimCommand { get; set; }
            public List<RewardChildApplicationSnapshot> Children { get; }
            public RewardCancelCommand CancelCommand { get; set; }

            public RewardCommitmentSnapshot ToSnapshot()
            {
                return RewardCommitmentSnapshot.CreateCanonical(
                    CommitCommand,
                    State,
                    Projections,
                    ClaimCommand,
                    Children,
                    CancelCommand);
            }

            public static CommitmentRecord FromSnapshot(
                RewardCommitmentSnapshot snapshot)
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
