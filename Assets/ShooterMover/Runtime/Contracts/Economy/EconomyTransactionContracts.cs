using System;
using System.Globalization;
using ShooterMover.Contracts;
using ShooterMover.Domain.Common;

namespace ShooterMover.Contracts.Economy
{
    public enum EconomyTransactionOperation
    {
        Credit = 1,
        Debit = 2,
        AddStack = 3,
        RemoveStack = 4,
        AddUnique = 5,
        RemoveUnique = 6,
    }

    public enum EconomyResourceKind
    {
        Currency = 1,
        Item = 2,
        Strongbox = 3,
        EquipmentReference = 4,
    }

    /// <summary>
    /// Immutable generic transaction command consumed later by money, scrap, and
    /// holdings authorities. It defines identity and payload but mutates nothing.
    /// </summary>
    public sealed class EconomyTransactionCommand : IEquatable<EconomyTransactionCommand>
    {
        private readonly string canonicalText;
        private readonly string payloadFingerprint;

        private EconomyTransactionCommand(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            EconomyTransactionOperation operation,
            EconomyResourceKind resourceKind,
            StableId resourceStableId,
            StableId instanceStableId,
            long quantity,
            long? expectedSequence)
        {
            this.TransactionStableId = RewardContractFormat.RequireStableId(
                transactionStableId,
                nameof(transactionStableId));
            this.OperationStableId = RewardContractFormat.RequireStableId(
                operationStableId,
                nameof(operationStableId));
            this.AuthorityStableId = RewardContractFormat.RequireStableId(
                authorityStableId,
                nameof(authorityStableId));
            RewardContractFormat.RequireDefinedEnum(operation, nameof(operation));
            RewardContractFormat.RequireDefinedEnum(resourceKind, nameof(resourceKind));
            this.Operation = operation;
            this.ResourceKind = resourceKind;
            this.ResourceStableId = RewardContractFormat.RequireStableId(
                resourceStableId,
                nameof(resourceStableId));
            if (quantity < 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Economy transaction quantities must be positive.");
            }

            if (expectedSequence.HasValue && expectedSequence.Value < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expectedSequence),
                    expectedSequence,
                    "Expected sequence must be non-negative when supplied.");
            }

            bool isUniqueOperation = operation == EconomyTransactionOperation.AddUnique
                || operation == EconomyTransactionOperation.RemoveUnique;
            if (isUniqueOperation && instanceStableId == null)
            {
                throw new ArgumentNullException(
                    nameof(instanceStableId),
                    "Unique holdings transactions require an instance StableId.");
            }

            if (!isUniqueOperation && instanceStableId != null)
            {
                throw new ArgumentException(
                    "Non-unique economy transactions must not carry an instance StableId.",
                    nameof(instanceStableId));
            }

            if (isUniqueOperation && quantity != 1L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(quantity),
                    quantity,
                    "Unique holdings transactions must use quantity one.");
            }

            bool isCurrencyOperation = operation == EconomyTransactionOperation.Credit
                || operation == EconomyTransactionOperation.Debit;
            if ((resourceKind == EconomyResourceKind.Currency) != isCurrencyOperation)
            {
                throw new ArgumentException(
                    "Currency resources require credit/debit operations and non-currency resources require holdings operations.");
            }

            bool isStackOperation = operation == EconomyTransactionOperation.AddStack
                || operation == EconomyTransactionOperation.RemoveStack;
            if (resourceKind == EconomyResourceKind.Item && !isStackOperation)
            {
                throw new ArgumentException("Item resources require stack operations.");
            }

            if ((resourceKind == EconomyResourceKind.Strongbox
                || resourceKind == EconomyResourceKind.EquipmentReference)
                && !isUniqueOperation)
            {
                throw new ArgumentException(
                    "Strongbox and equipment-reference resources require unique operations.");
            }

            this.InstanceStableId = instanceStableId;
            this.Quantity = quantity;
            this.ExpectedSequence = expectedSequence;
            this.canonicalText = "transaction_stable_id="
                + this.TransactionStableId
                + "\noperation_stable_id="
                + this.OperationStableId
                + "\nauthority_stable_id="
                + this.AuthorityStableId
                + "\noperation="
                + ((int)this.Operation).ToString(CultureInfo.InvariantCulture)
                + "\nresource_kind="
                + ((int)this.ResourceKind).ToString(CultureInfo.InvariantCulture)
                + "\nresource_stable_id="
                + this.ResourceStableId
                + "\ninstance_stable_id="
                + (this.InstanceStableId == null ? "null" : this.InstanceStableId.ToString())
                + "\nquantity="
                + this.Quantity.ToString(CultureInfo.InvariantCulture)
                + "\nexpected_sequence="
                + (this.ExpectedSequence.HasValue
                    ? this.ExpectedSequence.Value.ToString(CultureInfo.InvariantCulture)
                    : "none");
            this.payloadFingerprint = RewardContractFormat.Fingerprint(this.canonicalText);
        }

        public StableId TransactionStableId { get; }

        public StableId OperationStableId { get; }

        public StableId AuthorityStableId { get; }

        public EconomyTransactionOperation Operation { get; }

        public EconomyResourceKind ResourceKind { get; }

        /// <summary>
        /// Currency, item, strongbox definition, or equipment definition identifier.
        /// </summary>
        public StableId ResourceStableId { get; }

        public StableId InstanceStableId { get; }

        public long Quantity { get; }

        public long? ExpectedSequence { get; }

        public string PayloadFingerprint
        {
            get { return this.payloadFingerprint; }
        }

        public static EconomyTransactionCommand Create(
            StableId transactionStableId,
            StableId operationStableId,
            StableId authorityStableId,
            EconomyTransactionOperation operation,
            EconomyResourceKind resourceKind,
            StableId resourceStableId,
            StableId instanceStableId,
            long quantity,
            long? expectedSequence)
        {
            return new EconomyTransactionCommand(
                transactionStableId,
                operationStableId,
                authorityStableId,
                operation,
                resourceKind,
                resourceStableId,
                instanceStableId,
                quantity,
                expectedSequence);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(EconomyTransactionCommand other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as EconomyTransactionCommand);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }

    public enum EconomyTransactionStatus
    {
        Applied = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
        InvalidRequest = 4,
        InsufficientValue = 5,
        InsufficientCapacity = 6,
        ExpectedSequenceConflict = 7,
    }

    public enum EconomyTransactionIdentityComparison
    {
        DistinctTransaction = 1,
        ExactDuplicateNoChange = 2,
        ConflictingDuplicate = 3,
    }

    /// <summary>
    /// Pure duplicate classification. Authorities later persist prior fingerprints;
    /// this helper only defines the comparison semantics.
    /// </summary>
    public static class EconomyTransactionIdentity
    {
        public static EconomyTransactionIdentityComparison Classify(
            EconomyTransactionCommand existingCommand,
            EconomyTransactionCommand incomingCommand)
        {
            if (existingCommand == null)
            {
                throw new ArgumentNullException(nameof(existingCommand));
            }

            if (incomingCommand == null)
            {
                throw new ArgumentNullException(nameof(incomingCommand));
            }

            if (existingCommand.TransactionStableId != incomingCommand.TransactionStableId)
            {
                return EconomyTransactionIdentityComparison.DistinctTransaction;
            }

            if (string.Equals(
                existingCommand.PayloadFingerprint,
                incomingCommand.PayloadFingerprint,
                StringComparison.Ordinal))
            {
                return EconomyTransactionIdentityComparison.ExactDuplicateNoChange;
            }

            return EconomyTransactionIdentityComparison.ConflictingDuplicate;
        }
    }

    /// <summary>
    /// Immutable duplicate-safe transaction result vocabulary. No ledger or authority
    /// behavior is implemented here.
    /// </summary>
    public sealed class EconomyTransactionResult : IEquatable<EconomyTransactionResult>
    {
        private readonly string canonicalText;
        private readonly string fingerprint;

        private EconomyTransactionResult(
            StableId transactionStableId,
            EconomyTransactionStatus status,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long resultingValue)
        {
            this.TransactionStableId = RewardContractFormat.RequireStableId(
                transactionStableId,
                nameof(transactionStableId));
            RewardContractFormat.RequireDefinedEnum(status, nameof(status));
            this.Status = status;
            this.CommandFingerprint = RewardContractFormat.RequireFingerprint(
                commandFingerprint,
                nameof(commandFingerprint));
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

            if (resultingValue < 0L)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(resultingValue),
                    resultingValue,
                    "Resulting value or quantity must be non-negative.");
            }

            bool changed = status == EconomyTransactionStatus.Applied;
            if (changed && currentSequence != previousSequence + 1L)
            {
                throw new ArgumentException(
                    "Applied transactions must advance sequence by exactly one.");
            }

            if (!changed && currentSequence != previousSequence)
            {
                throw new ArgumentException(
                    "Rejected and duplicate transactions must not advance sequence.");
            }

            this.PreviousSequence = previousSequence;
            this.CurrentSequence = currentSequence;
            this.ResultingValue = resultingValue;
            this.canonicalText = "transaction_stable_id="
                + this.TransactionStableId
                + "\nstatus="
                + ((int)this.Status).ToString(CultureInfo.InvariantCulture)
                + "\ncommand_fingerprint="
                + this.CommandFingerprint
                + "\nprevious_sequence="
                + this.PreviousSequence.ToString(CultureInfo.InvariantCulture)
                + "\ncurrent_sequence="
                + this.CurrentSequence.ToString(CultureInfo.InvariantCulture)
                + "\nresulting_value="
                + this.ResultingValue.ToString(CultureInfo.InvariantCulture);
            this.fingerprint = RewardContractFormat.Fingerprint(this.canonicalText);
        }

        public StableId TransactionStableId { get; }

        public EconomyTransactionStatus Status { get; }

        public string CommandFingerprint { get; }

        public long PreviousSequence { get; }

        public long CurrentSequence { get; }

        public long ResultingValue { get; }

        public string Fingerprint
        {
            get { return this.fingerprint; }
        }

        public static EconomyTransactionResult Create(
            StableId transactionStableId,
            EconomyTransactionStatus status,
            string commandFingerprint,
            long previousSequence,
            long currentSequence,
            long resultingValue)
        {
            return new EconomyTransactionResult(
                transactionStableId,
                status,
                commandFingerprint,
                previousSequence,
                currentSequence,
                resultingValue);
        }

        public string ToCanonicalString()
        {
            return this.canonicalText;
        }

        public bool Equals(EconomyTransactionResult other)
        {
            return !ReferenceEquals(other, null)
                && string.Equals(this.canonicalText, other.canonicalText, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as EconomyTransactionResult);
        }

        public override int GetHashCode()
        {
            return RewardContractFormat.DeterministicHash(this.canonicalText);
        }

        public override string ToString()
        {
            return this.canonicalText;
        }
    }
}
