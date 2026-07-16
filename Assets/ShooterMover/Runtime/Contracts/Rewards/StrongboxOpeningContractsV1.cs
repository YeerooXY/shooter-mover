using System;
using System.Globalization;
using ShooterMover.Contracts;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Rewards
{
    /// <summary>
    /// Immutable opening intent. It does not check ownership, consume a box, sample
    /// rewards, or mutate holdings.
    /// </summary>
    public sealed class StrongboxOpeningRequestV1 : IEquatable<StrongboxOpeningRequestV1>
    {
        private readonly string canonicalText;
        private readonly string fingerprint;

        private StrongboxOpeningRequestV1(
            StableId runStableId,
            StableId openingOperationStableId,
            StableId transactionStableId,
            StableId strongboxInstanceStableId,
            StableId strongboxDefinitionStableId,
            StableId commitmentStableId,
            StableId rewardProfileStableId,
            string contentFingerprint,
            long? expectedSequence)
        {
            this.RunStableId = RewardContractFormatV1.RequireStableId(runStableId, nameof(runStableId));
            this.OpeningOperationStableId = RewardContractFormatV1.RequireStableId(
                openingOperationStableId,
                nameof(openingOperationStableId));
            this.TransactionStableId = RewardContractFormatV1.RequireStableId(
                transactionStableId,
                nameof(transactionStableId));
            this.StrongboxInstanceStableId = RewardContractFormatV1.RequireStableId(
                strongboxInstanceStableId,
                nameof(strongboxInstanceStableId));
            this.StrongboxDefinitionStableId = RewardContractFormatV1.RequireStableId(
                strongboxDefinitionStableId,
                nameof(strongboxDefinitionStableId));
            this.CommitmentStableId = RewardContractFormatV1.RequireStableId(
                commitmentStableId,
                nameof(commitmentStableId));
            this.RewardProfileStableId = RewardContractFormatV1.RequireStableId(
                rewardProfileStableId,
                nameof(rewardProfileStableId));
            this.ContentFingerprint = RewardContractFormatV1.RequireFingerprint(
                contentFingerprint,
                nameof(contentFingerprint));
            if (expectedSequence.HasValue && expectedSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedSequence),
                    expectedSequence,
                    "Expected sequence must be non-negative when supplied.");
            }

            this.ExpectedSequence = expectedSequence;
            this.canonicalText = "run_stable_id="
                + this.RunStableId
                + "\nopening_operation_stable_id="
                + this.OpeningOperationStableId
                + "\ntransaction_stable_id="
                + this.TransactionStableId
                + "\nstrongbox_instance_stable_id="
                + this.StrongboxInstanceStableId
                + "\nstrongbox_definition_stable_id="
                + this.StrongboxDefinitionStableId
                + "\ncommitment_stable_id="
                + this.CommitmentStableId
                + "\nreward_profile_stable_id="
                + this.RewardProfileStableId
                + "\ncontent_fingerprint="
                + this.ContentFingerprint
                + "\nexpected_sequence="
                + (this.ExpectedSequence.HasValue
                    ? this.ExpectedSequence.Value.ToString(CultureInfo.InvariantCulture)
                    : "none");
            this.fingerprint = RewardContractFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId RunStableId { get; }

        public StableId OpeningOperationStableId { get; }

        public StableId TransactionStableId { get; }

        public StableId StrongboxInstanceStableId { get; }

        public StableId StrongboxDefinitionStableId { get; }

        public StableId CommitmentStableId { get; }

        public StableId RewardProfileStableId { get; }

        public string ContentFingerprint { get; }

        public long? ExpectedSequence { get; }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static StrongboxOpeningRequestV1 Create(
            StableId runStableId,
            StableId openingOperationStableId,
            StableId transactionStableId,
            StableId strongboxInstanceStableId,
            StableId strongboxDefinitionStableId,
            StableId commitmentStableId,
            StableId rewardProfileStableId,
            string contentFingerprint,
            long? expectedSequence)
        {
            return new StrongboxOpeningRequestV1(
                runStableId,
                openingOperationStableId,
                transactionStableId,
                strongboxInstanceStableId,
                strongboxDefinitionStableId,
                commitmentStableId,
                rewardProfileStableId,
                contentFingerprint,
                expectedSequence);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(StrongboxOpeningRequestV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as StrongboxOpeningRequestV1);
        }

        public override int GetHashCode()
        {
            return RewardContractFormatV1.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum StrongboxOpeningStatusV1
    {
        Opened = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        StrongboxNotOwned = 5,
        InsufficientCapacity = 6,
        ExpectedSequenceConflict = 7,
    }

    /// <summary>
    /// Immutable opening result envelope. Statuses describe future authority outcomes;
    /// this contract performs no opening itself.
    /// </summary>
    public sealed class StrongboxOpeningResultV1 : IEquatable<StrongboxOpeningResultV1>
    {
        private readonly string canonicalText;
        private readonly string fingerprint;

        private StrongboxOpeningResultV1(
            StableId openingOperationStableId,
            StrongboxOpeningStatusV1 status,
            string requestFingerprint,
            RewardResultV1 rewardResult,
            RewardTraceV1 trace,
            long previousSequence,
            long currentSequence)
        {
            this.OpeningOperationStableId = RewardContractFormatV1.RequireStableId(
                openingOperationStableId,
                nameof(openingOperationStableId));
            RewardContractFormatV1.RequireDefinedEnum(status, nameof(status));
            this.Status = status;
            this.RequestFingerprint = RewardContractFormatV1.RequireFingerprint(
                requestFingerprint,
                nameof(requestFingerprint));
            if (previousSequence < 0L)
            {
                throw new ArgumentOutOfRangeException(nameof(previousSequence));
            }

            if (currentSequence < previousSequence)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentSequence),
                    currentSequence,
                    "Current sequence must not precede previous sequence.");
            }

            bool changed = status == StrongboxOpeningStatusV1.Opened;
            if (changed && currentSequence != previousSequence + 1L)
            {
                throw new ArgumentException(
                    "Opened strongbox results must advance sequence by exactly one.");
            }

            if (!changed && currentSequence != previousSequence)
            {
                throw new ArgumentException(
                    "Rejected and duplicate strongbox results must not advance sequence.");
            }

            bool hasReward = rewardResult != null;
            bool hasTrace = trace != null;
            bool statusMayCarryOutcome = status == StrongboxOpeningStatusV1.Opened
                || status == StrongboxOpeningStatusV1.ExactDuplicateNoChange;
            if (statusMayCarryOutcome != hasReward || statusMayCarryOutcome != hasTrace)
            {
                throw new ArgumentException(
                    "Opened and exact-duplicate results require reward and trace; rejected results require neither.");
            }

            if (hasReward
                && rewardResult.SourceOperationStableId != this.OpeningOperationStableId)
            {
                throw new ArgumentException(
                    "Reward result operation identity must match the strongbox opening operation.",
                    nameof(rewardResult));
            }

            if (hasTrace && trace.SourceOperationStableId != this.OpeningOperationStableId)
            {
                throw new ArgumentException(
                    "Reward trace operation identity must match the strongbox opening operation.",
                    nameof(trace));
            }

            this.RewardResult = rewardResult;
            this.Trace = trace;
            this.PreviousSequence = previousSequence;
            this.CurrentSequence = currentSequence;
            this.canonicalText = "opening_operation_stable_id="
                + this.OpeningOperationStableId
                + "\nstatus="
                + ((int)this.Status).ToString(CultureInfo.InvariantCulture)
                + "\nrequest_fingerprint="
                + this.RequestFingerprint
                + "\nprevious_sequence="
                + this.PreviousSequence.ToString(CultureInfo.InvariantCulture)
                + "\ncurrent_sequence="
                + this.CurrentSequence.ToString(CultureInfo.InvariantCulture)
                + "\nreward_result:\n"
                + (this.RewardResult == null ? "null" : this.RewardResult.ToCanonicalString())
                + "\ntrace:\n"
                + (this.Trace == null ? "null" : this.Trace.ToCanonicalString());
            this.fingerprint = RewardContractFormatV1.Fingerprint(this.canonicalText);
        }

        public StableId OpeningOperationStableId { get; }

        public StrongboxOpeningStatusV1 Status { get; }

        public string RequestFingerprint { get; }

        public RewardResultV1 RewardResult { get; }

        public RewardTraceV1 Trace { get; }

        public long PreviousSequence { get; }

        public long CurrentSequence { get; }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static StrongboxOpeningResultV1 Create(
            StableId openingOperationStableId,
            StrongboxOpeningStatusV1 status,
            string requestFingerprint,
            RewardResultV1 rewardResult,
            RewardTraceV1 trace,
            long previousSequence,
            long currentSequence)
        {
            return new StrongboxOpeningResultV1(
                openingOperationStableId,
                status,
                requestFingerprint,
                rewardResult,
                trace,
                previousSequence,
                currentSequence);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(StrongboxOpeningResultV1 other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as StrongboxOpeningResultV1);
        }

        public override int GetHashCode()
        {
            return RewardContractFormatV1.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }
}
